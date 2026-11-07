import {visibleControls} from './controls.mjs';
import {readGameCalendar} from './calendar.mjs';

const talk=/^(talk|talk to (?:your |her |my )?daughter|conversation|speak to (?:your )?daughter)$/i;
const castle=/^(castle|visit (?:the )?castle|royal castle)$/i;
const town=/^(go out|town|visit town|go to town)$/i;
const home=frame=>visibleControls(frame).some(c=>c.id==='schedule')&&!visibleControls(frame).some(c=>c.id==='dialog_next');
export function gameMonth(frame) {
  const {year,month}=readGameCalendar(frame);
  return month&&year?year+'-'+String(month).padStart(2,'0'):null;
}
export function socialContext(frame,state={}) {
  const month=gameMonth(frame);
  return {month,active:state.active||null,records:month?state.months?.[month]||{}:{},
    lastConversation:state.lastConversation||null,atHome:home(frame)};
}
export function observeSocial(previous={},frame) {
  const state=structuredClone(previous);state.months||={};
  const active=state.active;if(!active)return state;
  const controls=visibleControls(frame);
  const marker=controls.find(c=>c.id==='dialog_next');
  const dialogue=controls.filter(c=>c.source==='ocr'&&c.x<710&&c.y>300&&c.y<(marker?.y??1000)).map(c=>c.label).join(' ').slice(0,1500);
  // Store a quote, not an invented relationship or reputation change.
  if(dialogue&&controls.some(c=>c.id==='dialog_next')) {
    active.evidence=dialogue;active.sawDialogue=true;
    active.quotes=[...(active.quotes||[]).filter(t=>t!==dialogue),dialogue].slice(-6);
    if(active.kind==='daughter')state.lastConversation={month:active.month,text:dialogue,
      quotes:active.quotes,needsRest:active.quotes.some(t=>/\b(?:I am|I'm|I feel) (?:very |so )?(?:tired|exhausted)\b/i.test(t))};
  }
  if(home(frame)&&active.leftHome) {
    state.months[active.month]||={};
    state.months[active.month][active.kind]={status:active.sawDialogue?'dialogue observed':'returned without confirmed dialogue',evidence:active.evidence||'',inputs:active.inputs};
    state.active=null;
  } else if(!home(frame))active.leftHome=true;
  return state;
}
export function recordSocialInput(previous={},frame,plan) {
  const state=structuredClone(previous);state.months||={};
  if(state.active){state.active.inputs++;return state;}
  const month=gameMonth(frame);if(!month||!home(frame)||plan.action!=='click')return state;
  const label=visibleControls(frame).find(c=>c.id===plan.target)?.label||plan.target;
  const kind=talk.test(label)?'daughter':castle.test(label)||town.test(label)?'castle':null;
  if(!kind||state.months[month]?.[kind])return state;
  state.months[month]||={};state.months[month][kind]={status:'attempted; result unconfirmed'};
  state.active={kind,month,inputs:1,leftHome:false,sawDialogue:false};
  // Keep bounded records without losing the active month.
  for(const key of Object.keys(state.months).sort().slice(0,-24))delete state.months[key];
  return state;
}
function decision(chosen,reason) {
  return {screen:'home',confidence:.99,summary:reason,date:null,age:null,gold:null,stress:null,health:null,
    action:chosen?'click':'pause',target:chosen?.id||'',x:chosen?.x??null,y:chosen?.y??null,key:null,
    reason,risk:'normal',outcome:'none',endingEvidence:''};
}
export function socialPlan(frame,state={}) {
  const context=socialContext(frame,state);
  if(context.active) {
    if(context.active.inputs>=12)return decision(null,'This conversation or castle visit has taken twelve inputs without a confirmed return. Paused to avoid a menu loop.');
    return null;
  }
  if(!context.atHome)return null;
  if(!context.month)return decision(null,'Before scheduling, I need to read the game calendar for the monthly conversation and castle check. The date is unclear; paused instead of skipping them.');
  const controls=visibleControls(frame);
  const select=pattern=>{const matches=controls.filter(c=>pattern.test(c.label.trim()));return matches.length===1?matches[0]:null;};
  if(!context.records.daughter) {
    const choice=select(talk);if(choice)return decision(choice,'Check in with her before planning the month.');
    return decision(null,'The monthly daughter check-in is pending, but I cannot identify a unique Talk control. Paused for a home-menu reading check instead of scheduling.');
  }
  if(!context.records.castle) {
    const choice=select(castle)||select(town);
    if(choice)return decision(choice,'Consider a castle audience before scheduling work.');
    return decision(null,'The monthly town/castle visit is pending, but I cannot identify a unique Town, Go out or Castle control. Paused for a home-menu reading check instead of scheduling.');
  }
  return null;
}
