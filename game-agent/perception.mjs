import {createHash} from 'node:crypto';
import {visibleControls} from './controls.mjs';
import {readGameCalendar} from './calendar.mjs';

const clean=value=>String(value||'').toLowerCase().replace(/\s+/g,' ').trim();
const hash=value=>createHash('sha256').update(value).digest('hex');

export function sceneSignature(frame) {
  const controls=visibleControls(frame).map(c=>[clean(c.label),Math.round(c.x/10),Math.round(c.y/10)]);
  if(!controls.length)return hash(frame.png||'');
  // A blinking sprite or animated cursor is not evidence that a menu accepted a click.
  return hash(JSON.stringify(controls.sort((a,b)=>a[2]-b[2]||a[1]-b[1]||a[0].localeCompare(b[0]))));
}

export function sceneLabel(frame) {
  const controls=visibleControls(frame),labels=controls.map(c=>clean(c.label));
  const period=labels.map(s=>s.match(/\bperiod\s*([123])\b/)).find(Boolean);
  if(period&&labels.includes('work')&&labels.includes('time off'))return 'Schedule · period '+period[1];
  if(labels.includes('add to schedule')&&labels.includes('cancel'))return 'Activity confirmation';
  if(['housework','farm','inn','babysitter'].every(s=>labels.includes(s)))return 'Job choices';
  if(labels.includes('free time')&&labels.includes('vacation'))return 'Rest choices';
  if(controls.some(c=>c.id==='dialog_next'))return 'Dialogue';
  if(controls.some(c=>c.id==='schedule'))return 'Home';
  return 'Game screen';
}

export function hasChoicePrompt(frame) {
  const controls=visibleControls(frame);
  const prompt=controls.some(c=>c.source==='ocr'&&c.x<650&&c.y>400&&/[?？]/.test(c.label));
  const choices=controls.filter(c=>c.source==='ocr'&&c.x>700&&c.y>380&&c.y<900);
  return prompt&&choices.length>=2;
}

export function readVitals(frame) {
  const lines=frame.detail?.ocr?.available?frame.detail.ocr.lines||[]:[];
  const result={gold:null,stress:null,health:null};
  for(const line of lines) {
    const text=clean(line.text);
    let match=text.match(/^gold\s*[:=]?\s*(\d{1,8})\s*g?$/);
    if(match)result.gold=Number(match[1]);
    match=text.match(/^stress\s*[:=]?\s*(\d{1,3})$/);
    if(match&&Number(match[1])<=999)result.stress=Number(match[1]);
    if(/^(?:health|condition)\s*[:=]\s*(?:ill|sick|bedridden)\.?$/.test(text)||/^(?:she|your daughter) is (?:ill|sick|bedridden)\.?$/.test(text))result.health='ill';
    if(/^(?:health|condition)\s*[:=]\s*healthy\.?$/.test(text))result.health='healthy';
  }
  return result;
}

export function confirmedHistory(history=[]) {
  // Old records only say an input was attempted. They must not teach the model that it worked.
  return history.filter(h=>h.result==='changed').slice(-5).map(h=>({screen:h.screen,action:h.action,target:h.target,result:h.result,after:h.afterScene}));
}

export function observationSummary(frame) {
  return {id:frame.id,width:frame.width,height:frame.height,scene:sceneLabel(frame),signature:sceneSignature(frame),
    controls:visibleControls(frame),text:frame.detail?.ocr?.text||'',vitals:readVitals(frame),calendar:readGameCalendar(frame)};
}
