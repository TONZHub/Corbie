import {visibleControls} from './controls.mjs';
import {readGameCalendar} from './calendar.mjs';

const garment=/\b(summer|winter)\s+(?:dress|clothing|clothes|outfit)\b/i;
const canonical=text=>{const m=text.match(garment);return m?m[1].toLowerCase()+' dress':null;};

// Only explicit inventory/equipment evidence establishes ownership. Shop stock does not.
export function readWardrobe(frame) {
  const lines=frame.detail?.ocr?.available?frame.detail.ocr.lines||[]:[];
  const labels=lines.map(l=>String(l.text||'').trim());
  const shop=labels.some(t=>/\b(shop|tailor|buy|purchase|sell|price)\b/i.test(t));
  const inventory=!shop&&labels.some(t=>/^(?:inventory|items|equipment|possessions|owned items)\s*:?$/i.test(t));
  const {month}=readGameCalendar(frame);
  const owned=[],evidence=[];let equipped=null;
  for(const text of labels) {
    const item=canonical(text);if(!item||shop)continue;
    const explicit=/^(?:owned|possessed)\s*:\s*(summer|winter)\s+(dress|clothing|clothes|outfit)\s*\.?$/i.test(text);
    const wearing=/^(?:equipped|wearing|current (?:dress|clothing|outfit))\s*:\s*(summer|winter)\s+(dress|clothing|clothes|outfit)\s*\.?$/i.test(text);
    const listed=inventory&&/^(summer|winter)\s+(dress|clothing|clothes|outfit)(?:\s+[x×]\s*[1-9]\d*)?\s*\.?$/i.test(text);
    if(explicit||wearing||listed){owned.push(item);evidence.push(text);}
    if(wearing)equipped=item;
  }
  return {month,inventory,shop,owned:[...new Set(owned)],equipped,evidence,
    weatherPrompt:labels.some(t=>/\b(hot|cold|heat|chilly|freezing)\b/i.test(t))&&labels.some(t=>garment.test(t))};
}

export function updateWardrobe(previous={},frame) {
  const read=readWardrobe(frame),seen={...(previous.seen||{})};
  for(const item of read.owned)seen[item]={observation:frame.id||null,month:read.month,evidence:read.evidence};
  const month=read.month??previous.lastMonth??null;
  const desired=[6,7,8].includes(month)?'summer dress':[12,1,2].includes(month)?'winter dress':null;
  let checkedFor=month===previous.lastMonth?previous.checkedFor:null;
  if(desired&&read.owned.includes(desired))checkedFor=month;
  // Any shop visit may change possessions; require another inspection afterwards.
  if(read.shop)checkedFor=null;
  return {seen,current:read,lastMonth:month,checkedFor};
}

export function clothingContext(frame,wardrobe={}) {
  const current=readWardrobe(frame);
  const desired=[6,7,8].includes(current.month)?'summer dress':[12,1,2].includes(current.month)?'winter dress':null;
  return {current,desired,previouslySeen:wardrobe.seen||{},
    needsCheck:!!desired&&!current.owned.includes(desired)&&wardrobe.checkedFor!==current.month};
}

export function wardrobePlan(frame,wardrobe={}) {
  const context=clothingContext(frame,wardrobe),controls=visibleControls(frame);
  // Do not interrupt an already open schedule. Check the wardrobe at home.
  if(!context.needsCheck||!controls.some(c=>c.id==='schedule')||context.current.weatherPrompt)return null;
  const entries=controls.filter(c=>/^(inventory|items|equipment|possessions)$/i.test(c.label.trim()));
  const chosen=entries.length===1?entries[0]:null;
  return {screen:'home',confidence:.99,summary:'Check seasonal clothing before scheduling.',date:null,age:null,gold:null,stress:null,health:null,
    action:chosen?'click':'pause',target:chosen?.id||'',x:chosen?.x??null,y:chosen?.y??null,key:null,
    reason:chosen?'Inspect owned clothing for the '+context.desired+'. Previous sightings do not confirm the current wardrobe.':
      'Seasonal clothing needs checking, but I cannot locate a unique inventory control. Open her inventory and use watch.',
    risk:'normal',outcome:'none',endingEvidence:''};
}
