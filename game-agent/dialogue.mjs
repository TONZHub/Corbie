import {visibleControls} from './controls.mjs';

// A matched continue marker confirms an ordinary dialogue box for keyboard advance.
export function dialoguePlan(frame) {
  const controls=visibleControls(frame),markers=controls.filter(c=>c.id==='dialog_next');
  if(markers.length!==1)return null;
  const marker=markers[0];
  const lines=controls.filter(c=>c.source==='ocr'&&c.x<710&&c.y>300&&c.y<marker.y);
  const text=lines.map(c=>c.label).join(' ');
  if(text.replace(/[^a-z]/gi,'').length<3)return null;
  // Choices and potentially final/save screens require full model interpretation.
  const salary=/\bhere is your salary\b/i.test(text)&&/\b\d+\s*G\s+for\s+\d+\s+days\b/i.test(text);
  if((/[?？]/.test(text)&&!salary)||/\b(save|load|overwrite|delete|quit|ending|married|marriage|died|death|career|credits|congratulations)\b/i.test(text))return null;
  if(controls.some(c=>/^(yes|no|accept|decline|cancel|buy|sell|equip|return|back|add to schedule)$/i.test(c.label.trim())))return null;
  return {screen:'dialog',confidence:.99,summary:'Continue the dialogue.',date:null,age:null,gold:null,stress:null,health:null,
    action:'key',target:'key:Enter',x:null,y:null,key:'Enter',
    reason:'Press Enter once to advance this ordinary dialogue, then check the new screen.',risk:'normal',outcome:'none',endingEvidence:''};
}
