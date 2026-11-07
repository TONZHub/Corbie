const outcome={changed:'screen changed',unchanged:'no visible change',uncertain:'input outcome uncertain',pending:'awaiting verification',waiting:'waited'};
export const GAME_HELP='watch · preview  |  step · one move  |  play/resume · continue\nstatus · progress  |  why · last decision  |  history · recent moves\nguide · guide files  |  guide load "path.txt" · import notes  |  guide clear · remove notes\nsolver · planning adviser, stats form and commands\nesc · press Escape once in game  |  open · launch Refine\nback · return to CLI  |  F8 · pause input';
export function statusText(state={}) {
  const p=state.lastObservation||{},facts=[];
  if(p.date)facts.push('date read: '+p.date);
  if(p.age!=null)facts.push('age '+p.age);
  if(p.gold!=null)facts.push(p.gold+'G');
  if(p.stress!=null)facts.push('stress '+p.stress);
  const lines=['Corbie · '+(state.status||'not started')+(state.phase?' · '+state.phase:''),
    'Goal: finish alive, any adulthood ending.',
    (state.totalInputs||0)+' inputs recorded · '+(state.verifiedChanges||0)+' visible screen changes'];
  if(state.scene)lines.push('Last screen: '+state.scene);
  if(state.social?.active)lines.push('Social task: '+state.social.active.kind+' · '+state.social.active.inputs+' inputs');
  const socialMonth=Object.keys(state.social?.months||{}).sort().at(-1);
  if(socialMonth)lines.push('Social record '+socialMonth+': '+Object.entries(state.social.months[socialMonth]).map(([kind,r])=>kind+' — '+r.status).join('; '));
  if(state.social?.lastConversation)lines.push('Last daughter dialogue (OCR): '+state.social.lastConversation.text);
  if(state.wardrobe){
    const current=state.wardrobe.current||{},seen=Object.keys(state.wardrobe.seen||{});
    lines.push('Clothing seen previously: '+(seen.join(', ')||'not verified'));
    lines.push('Currently verified outfit: '+(current.equipped||'unknown'));
    if(current.month)lines.push('Game calendar month: '+current.month);
  }
  if(facts.length)lines.push('Last decision’s readings: '+facts.join(' · '));
  if(state.lastReceipt)lines.push('Last move: '+state.lastReceipt.target+' — '+(outcome[state.lastReceipt.result]||state.lastReceipt.result));
  if(state.pauseReason)lines.push('Paused: '+state.pauseReason);
  return lines.join('\n');
}
export function whyText(state={}) {
  const p=state.lastObservation;
  if(!p)return 'No decision recorded yet. Use watch to let me inspect the game.';
  return [p.summary,'Move: '+p.action+(p.target?' “'+p.target+'”':''),p.reason,
    state.lastReadError?'Reading issue: '+state.lastReadError:null].filter(Boolean).join('\n');
}
export function historyText(state={}) {
  const rows=(state.history||[]).filter(h=>h.id).slice(-8);
  if(!rows.length)return 'No verified move log yet. Older attempted inputs remain in the progress file.';
  return rows.map(h=>'• '+h.target+' — '+(outcome[h.result]||h.result||'unverified')+(h.afterScene?' → '+h.afterScene:'')).join('\n');
}
export function sessionReport(state) {
  return '# Corbie’s game notebook\n\n'+statusText(state)+'\n\n## Last decision\n\n'+whyText(state)+'\n\n## Recent moves\n\n'+historyText(state)+'\n';
}
