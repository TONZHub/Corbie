import {visibleControls} from './controls.mjs';
import {JOBS} from './guide.mjs';

// Recognize the actual period prompt and its menu; old actions cannot enter this path.
// Handle recognized menu mechanics and urgent recovery, not normal job strategy.
export function schedulePlan(frame,{vitals={},needsRest=false}={}) {
  const controls=visibleControls(frame);
  const prompt=controls.find(c=>c.source==='ocr'&&c.x<500&&c.y>680&&/\bperiod\s*[123]\b/i.test(c.label));
  const period=prompt?Number(prompt.label.match(/\bperiod\s*([123])\b/i)[1]):null;
  const menu=controls.filter(c=>c.source==='ocr'&&c.x>500&&c.y>200&&c.y<900);
  const find=label=>menu.filter(c=>c.label.trim().toLowerCase()===label.toLowerCase());
  let chosen,reason;
  const confirmationText=controls.filter(c=>c.source==='ocr'&&c.x<500&&c.y>680).map(c=>c.label).join(' ');
  if(/\bthis is the schedule for this month\b/i.test(confirmationText)&&find('Run schedule').length===1&&find('Cancel').length===1) {
    const choice=find('Run schedule')[0];
    return {screen:'schedule',confidence:.99,summary:'Run the completed monthly schedule.',date:null,age:null,gold:vitals.gold??null,stress:vitals.stress??null,health:vitals.health??null,
      action:'click',target:choice.id,x:choice.x,y:choice.y,key:null,
      reason:'The game is showing the final monthly schedule confirmation. Run it instead of cancelling and rebuilding it.',risk:'normal',outcome:'none',endingEvidence:''};
  }
  const pocketPrompt=controls.some(c=>c.source==='ocr'&&c.x<550&&c.y>600&&/^give her\b.*\bpocket money\s*\?/i.test(c.label));
  const give=find('Give'),decline=menu.filter(c=>/^don[’' ]?t give$/i.test(c.label.trim()));
  if(pocketPrompt&&give.length===1&&decline.length===1) {
    const choice=decline[0];
    return {screen:'dialog',confidence:.99,summary:'Continue free time without pocket money.',date:null,age:null,gold:null,stress:null,health:null,
      action:'click',target:choice.id,x:choice.x,y:choice.y,key:null,
      reason:'Decline this optional expense; no amount is displayed. Free time can continue.',risk:'normal',outcome:'none',endingEvidence:''};
  }
  const dialogue=controls.filter(c=>c.id==='dialog_next');
  const caption=controls.filter(c=>c.source==='ocr'&&c.x<550&&c.y>680).map(c=>c.label).join(' ');
  if(dialogue.length===1&&/\b(?:from|fran) today\b.*\bwill do\b/i.test(caption)&&/\bpart[ -]time work at a farm\b/i.test(caption)) {
    return {screen:'dialog',confidence:.99,summary:'Continue the farming introduction.',date:null,age:null,gold:null,stress:null,health:null,
      action:'click',target:dialogue[0].id,x:dialogue[0].x,y:dialogue[0].y,key:null,
      reason:'The farm block is waiting at its dialogue marker, not running yet.',risk:'normal',outcome:'none',endingEvidence:''};
  }
  const recover=needsRest||vitals.health==='ill'||(Number.isFinite(vitals.stress)&&vitals.stress>=60);
  const activity=controls.find(c=>c.source==='ocr'&&c.x<500&&c.y>680&&[...JOBS,'Free time','Time off'].some(j=>j.toLowerCase()===c.label.trim().replace(/\.$/,'').toLowerCase()));
  const workActivity=activity&&JOBS.some(j=>j.toLowerCase()===activity.label.trim().replace(/\.$/,'').toLowerCase());
  if(activity&&find('Add to schedule').length===1&&find('Cancel').length===1) {
    chosen=find(recover&&workActivity?'Cancel':'Add to schedule')[0];
    reason=recover&&workActivity?'Cancel the work block while the current reading calls for recovery.':'Confirm the visible '+activity.label.replace(/\.$/,'')+' activity for this block.';
  } else if(prompt&&['Study','Work','Errantry','Time off'].every(label=>find(label).length===1)) {
    if(!recover)return null;
    chosen=find('Time off')[0];
    reason=needsRest?'She said she was tired this month. Choose a recovery block.':'The current screen reports illness or high stress. Make this a recovery block.';
  } else if(find('Free time').length===1&&find('Vacation').length===1) {
    chosen=find('Free time')[0];reason='Choose the free rest option instead of the paid vacation.';
  }
  if(!chosen)return null;
  return {screen:'schedule',confidence:.99,summary:(period?'Period '+period+': ':'Choose ')+chosen.label+'.',date:null,age:null,gold:vitals.gold??null,stress:vitals.stress??null,health:vitals.health??null,
    action:'click',target:chosen.id,x:chosen.x,y:chosen.y,key:null,reason,risk:'normal',outcome:'none',endingEvidence:''};
}
