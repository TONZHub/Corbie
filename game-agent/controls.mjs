const name=value=>String(value||'').toLowerCase().replace(/[^a-z0-9]+/g,' ').trim();
const menuLabels=new Set(('schedule|study|work|time off|housework|babysitter|inn|farm|church|restaurant|free time|vacation|add to schedule|cancel|yes|no|give|don t give|back|return|return home|go back|done|close|start|start schedule|begin|begin schedule|execute|execute schedule|ok|proceed|talk|talk to daughter|talk to your daughter|conversation|speak to your daughter|castle|visit castle|visit the castle|royal castle|go out|town|visit town|inventory|items|equipment|possessions|king|queen|prince|princess|minister|knight|gatekeeper|guard|audience|request audience|visit|chat|ordinary chat|how are you|equip|change clothes|summer dress|winter dress|summer clothing|winter clothing|cotton dress|tailor|buy|sell|accept|decline').split('|'));
menuLabels.add('run schedule');
menuLabels.add('go to town');

export function clickableControls(frame) {
  const controls=visibleControls(frame).filter(c=>c.source!=='ocr'||
    (c.y>200&&!(c.x<500&&c.y>680)&&menuLabels.has(name(c.label))));
  // Cancelling here rebuilds the already completed month. Keep that recovery manual.
  return controls.some(c=>name(c.label)==='run schedule')?controls.filter(c=>name(c.label)!=='cancel'):controls;
}

export function visibleControls(frame) {
  const controls=(frame.detail?.controls||[]).filter(c=>typeof c.id==='string'&&typeof c.label==='string'&&[c.x,c.y].every(v=>Number.isFinite(v)&&v>=0&&v<=1000));
  const lines=frame.detail?.ocr?.available?frame.detail.ocr.lines||[]:[];
  for(const line of lines) {
    if(!line.words?.length)continue;
    const words=line.words.filter(w=>[w.x,w.y,w.w,w.h].every(Number.isFinite));
    if(!words.length)continue;
    const left=Math.min(...words.map(w=>w.x)),right=Math.max(...words.map(w=>w.x+w.w));
    const top=Math.min(...words.map(w=>w.y)),bottom=Math.max(...words.map(w=>w.y+w.h));
    if(left<0||right>1000||top<0||bottom>1000)continue;
    controls.push({id:'text_'+controls.length,label:line.text,x:(left+right)/2,y:(top+bottom)/2,source:'ocr'});
  }
  return controls.slice(0,80);
}

// Resolve a named target using this observation, never the model's guessed coordinates.
// Only recognized menu labels outside the HUD/dialogue regions can become OCR clicks.
export function groundClick(plan,frame) {
  if(plan.action!=='click')return plan;
  const target=name(plan.target),controls=clickableControls(frame);
  const matches=controls.filter(c=>name(c.id)===target||name(c.label)===target);
  const templates=matches.filter(c=>c.source!=='ocr');
  const candidates=templates.length?templates:matches;
  const control=candidates.length===1?candidates[0]:null;
  if(!control)throw new GameReadError('Could not locate “'+plan.target+'” unambiguously in this picture.');
  return {...plan,target:control.label,x:control.x,y:control.y,key:null};
}
import {GameReadError} from './read-error.mjs';
