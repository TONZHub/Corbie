import {readGameCalendar} from './calendar.mjs';

export const STAT_KEYS='constitution strength intelligence refinement charisma morality faith sin sensitivity stress combat_skill combat_attack combat_defense magic_skill magic_attack magic_defense decorum art conversation cooking cleaning temperament'.split(' ');
export const META_KEYS=['year','month','age','birth_month','blood','sign','diet','gold'];
export const FIELD_KEYS=[...META_KEYS,...STAT_KEYS];
const aliases={stamina:'constitution',con:'constitution',elegance:'refinement',glamour:'charisma',karma:'sin',manners:'decorum',
  'combat skill':'combat_skill','combat attack':'combat_attack','combat defense':'combat_defense','magic skill':'magic_skill',
  'magic attack':'magic_attack','magic defense':'magic_defense','cleaning/laundry':'cleaning','birthday month':'birth_month',
  'blood type':'blood','star sign':'sign','zodiac sign':'sign'};
const labels={constitution:['stamina','constitution'],refinement:['elegance','refinement'],charisma:['glamour','charisma'],sin:['karma','sin'],decorum:['manners','decorum'],
  birth_month:['birth month','birthday month'],blood:['blood type','blood'],sign:['star sign','zodiac sign','sign'],diet:['diet'],gold:['gold'],year:['year'],month:['month'],age:['age']};
const signs=['capricorn','aquarius','pisces','taurus','libra','gemini','virgo','cancer','leo','scorpio','sagittarius'];
export const canonicalField=key=>aliases[String(key).toLowerCase()]||String(key).toLowerCase().replace(/ /g,'_');
export function validateField(field,value) {
  if(!FIELD_KEYS.includes(field))throw new Error('Unknown stat field: '+field);
  if(['blood','sign','diet'].includes(field)) {
    if(typeof value!=='string')throw new Error(field+' needs a named value.');
    value=value.trim().toLowerCase();
    if(field==='blood')value=value.toUpperCase();
    if(field==='diet')value=({'standard':'normal','standard diet':'normal','normal diet':'normal','robust diet':'robust','slim down':'slim','weight loss':'weight_loss'})[value]||value;
    const choices=field==='blood'?['A','B','O','AB']:field==='sign'?signs:['normal','robust','slim','weight_loss'];
    if(!choices.includes(value))throw new Error('Unknown '+field+': '+value);
    return value;
  }
  const range=field==='year'?[1100,1299]:['month','birth_month'].includes(field)?[1,12]:field==='age'?[10,17]:field==='gold'?[0,99999999]:[0,9999];
  if(!Number.isInteger(value)||value<range[0]||value>range[1])throw new Error('Invalid '+field+' value.');
  return value;
}
function labelled(field,evidence) {
  return (labels[field]||[field.replace(/_/g,' ')]).some(label=>new RegExp('\\b'+label.replace(/[.*+?^${}()|[\]\\]/g,'\\$&')+'\\b','i').test(evidence));
}

export const STATS_SCHEMA={type:'object',additionalProperties:false,required:['readings'],properties:{readings:{type:'array',maxItems:60,items:{type:'object',additionalProperties:false,
  required:['field','value','confidence','evidence'],properties:{field:{type:'string',enum:FIELD_KEYS},value:{type:['integer','string','null']},confidence:{type:'number',minimum:0,maximum:1},evidence:{type:'string'}}}}}};

export async function readStatsFrame(frame,{infer}={}) {
  if(!frame?.png||!Number.isInteger(frame.width)||!Number.isInteger(frame.height))throw new Error('No readable game picture.');
  const candidates={};
  const add=(field,value,confidence,evidence,source)=>{
    try{value=validateField(field,value);}catch{return;}
    const next={value,confidence,evidence:String(evidence).slice(0,200),source};
    const prior=candidates[field];
    if(prior&&prior.value!==value){candidates[field]={...prior,confidence:0,conflict:[...(prior.conflict||[prior.value]),value]};return;}
    if(!prior||(!prior.conflict&&confidence>prior.confidence))candidates[field]=next;
  };
  // OCR is accepted only for an explicit label/value pair, never bare HUD digits.
  for(const line of frame.detail?.ocr?.available?frame.detail.ocr.lines||[]:[]) {
    const text=String(line.text||'').trim();
    for(const field of FIELD_KEYS) {
      if(frame.detail?.controls?.some(c=>c.id==='dialog_next')&&STAT_KEYS.includes(field))continue;
      const names=labels[field]||[field.replace(/_/g,' ')];
      for(const label of names) {
        const escaped=label.replace(/[.*+?^${}()|[\]\\]/g,'\\$&');
        const numeric=!['blood','sign','diet'].includes(field);
        const match=text.match(new RegExp('^'+escaped+'\\s*[:=]?\\s+'+(numeric?'(\\d{1,8})\\s*G?':'([a-zA-Z_ ]+)')+'\\s*$','i'));
        if(match)add(field,numeric?Number(match[1]):match[1],.98,text,'ocr');
      }
    }
    if(/^Standard diet$/i.test(text))add('diet','normal',.98,text,'ocr');
  }
  const date=readGameCalendar(frame);
  for(const field of ['year','month'])if(date[field])add(field,date[field],.85,'Current game calendar: '+date.year+'-'+date.month,'calendar');
  const images=[frame.png];
  if(frame.detail?.hud)images.push(frame.detail.hud);
  if(frame.detail?.calendar)images.push(frame.detail.calendar);
  const raw=await infer({format:STATS_SCHEMA,images,prompt:
    'Read only explicitly VISIBLE Princess Maker 2 Refine stat labels and their current values from this game picture. Later images are details of the SAME frame. Return readings; omit unreadable/absent fields. Never complete a default daughter or use guide/simulator values. Quote the visible label and value in evidence.\n'+
    'Fields: '+FIELD_KEYS.join(', ')+'. Stamina=constitution, Elegance=refinement, Glamour=charisma, Karma=sin, Manners=decorum. The five numbers under Standard diet are BODY MEASUREMENTS, never stats. A small unlabeled bar is not a readable stat. Never infer sign/blood/birthday from an icon, age or name. birth_month requires a printed birthday. Calendar month is beside the month NAME, not the large DAY. Age is under AGE; Gold is under GOLD. combat_attack means the displayed attack, not a value you calculate. Only read combat/magic stats when their full group/label is unambiguous. Standard diet maps to normal.\nFallible OCR: '+
    String(frame.detail?.ocr?.text||'').slice(0,4000)});
  if(!raw||!Array.isArray(raw.readings))throw new Error('Stat reader returned an unreadable proposal. Previous stats were kept.');
  for(const reading of raw.readings.slice(0,60)) {
    const {field,value,evidence,confidence}=reading||{};
    if(!FIELD_KEYS.includes(field)||value==null||typeof evidence!=='string'||!Number.isFinite(confidence)||confidence<0||confidence>1)continue;
    if(!labelled(field,evidence))continue;
    if(typeof value==='number'&&!new RegExp('(^|[^0-9])'+value+'([^0-9]|$)').test(evidence))continue;
    if(typeof value==='string') {
      const valueText=value.toLowerCase().replace(/_/g,' ');
      if(!evidence.toLowerCase().includes(valueText)&&!(field==='diet'&&value==='normal'&&/standard diet/i.test(evidence)))continue;
    }
    add(field,value,Math.min(confidence,.85),evidence,'vision');
  }
  return {fields:candidates,missing:FIELD_KEYS.filter(k=>!candidates[k]),observation:frame.id||null};
}

export function parseCorrections(text) {
  const changes={};let remainder=text;
  for(const match of text.matchAll(/([a-zA-Z_]+)=("[^"]*"|'[^']*'|\S+)/g)) {
    const field=canonicalField(match[1]),raw=match[2].replace(/^["']|["']$/g,'');
    if(Object.hasOwn(changes,field))throw new Error('Duplicate correction: '+field);
    const named=['blood','sign','diet'].includes(field);
    if(!named&&!/^\d+$/.test(raw))throw new Error('Use whole-number values for '+field+'.');
    changes[field]=validateField(field,named?raw:Number(raw));
    remainder=remainder.replace(match[0],'');
  }
  if(!Object.keys(changes).length||remainder.trim())throw new Error('Use solver set stamina=120 stress=10 diet=normal (no commas).');
  return changes;
}
