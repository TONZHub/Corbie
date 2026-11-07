import {randomUUID} from 'node:crypto';
import {decisionIssue,isEnding} from './decision.mjs';
import {groundClick} from './controls.mjs';
import {sceneSignature,sceneLabel,observationSummary} from './perception.mjs';
import {GameReadError} from './read-error.mjs';
import {updateWardrobe} from './inventory.mjs';
import {observeSocial,recordSocialInput} from './social.mjs';

export async function playGame({adapter,model,store,write=console.log,signal,maxSteps=1500,
  delay=ms=>new Promise(r=>setTimeout(r,ms)),watch=false,singleStep=false}={}) {
  const loaded=await store.load();
  const state={...loaded,status:watch?'watching':'playing',phase:'connecting',pauseReason:null,lastReadError:null,
    wardrobe:{...loaded.wardrobe,checkedFor:null},
    goal:'Finish alive, any adulthood ending',history:Array.isArray(loaded.history)?loaded.history.slice(-80):[],steps:0,
    totalInputs:loaded.totalInputs||0,verifiedChanges:loaded.verifiedChanges||0};
  let frame,blockedTargets=loaded.blockedControl?.targets||[],blockedScene=loaded.blockedControl?.scene||'',unchangedWaits=0,consecutiveWaits=0;
  async function save(){await store.save(state);}
  async function observe(phase='reading') {
    if(signal?.aborted)throw new Error('Paused by your human.');
    state.phase=phase;await save();
    let next=await adapter.request('observe');
    if(!watch&&!singleStep&&!next.armed)throw new Error('Paused with F8 or by switching apps. Use resume when ready.');
    if(model.inspectFrame)next=await model.inspectFrame(next,{signal});
    await store.frame(next.png,'last-frame.png');
    state.scene=sceneLabel(next);state.currentObservation=observationSummary(next);
    state.wardrobe=updateWardrobe(state.wardrobe,next);
    state.social=observeSocial(state.social,next);
    if(store.observation)await store.observation(state.currentObservation);
    return next;
  }
  async function choose(current) {
    let feedback=blockedTargets.length?'The last input caused no visible menu/text change. Do not repeat '+blockedTargets.join(', ')+'. Inspect the current screen.':'';
    for(let attempt=0;attempt<2;attempt++) {
      state.phase=attempt?'rechecking':'planning';await save();
      write(attempt?'Corbie > That reading was dodgy. One fresh look.':'Reading · '+sceneLabel(current));
      try {
        let p=await model.plan(current,state.history,{signal,feedback,blockedTargets,wardrobe:state.wardrobe,social:state.social,forceVision:consecutiveWaits>=2||attempt>0});
        if(p.action==='key')p={...p,target:'key:'+p.key};
        state.lastObservation={...p};
        if(!decisionIssue(p))p=groundClick(p,current);
        state.lastObservation={...p};
        if(['click','key'].includes(p.action)&&blockedTargets.some(t=>t.toLowerCase()===p.target.toLowerCase()))throw new GameReadError('That input already produced no visible change.');
        state.lastReadError=null;
        return {frame:current,plan:p};
      } catch(error) {
        if(error.code!=='GAME_READ')throw error;
        state.lastReadError=error.message;await save();
        if(attempt)throw new Error('Still could not read a reliable move: '+error.message+' Use watch to inspect, or resume after adjusting the game.');
        feedback=error.message+' Choose only a control visible in this NEW picture. Ignore old guesses.';
        current=await observe('rechecking');
      }
    }
  }
  try {
    await save();state.models=await model.verify();
    await adapter.request(watch?'focus':'arm');
    frame=await observe();
    if(!watch&&!frame.armed)throw new Error('Paused with F8. Use resume when ready.');
    for(let step=0;step<maxSteps;step++) {
      const signature=sceneSignature(frame);
      if(signature!==blockedScene){blockedTargets=[];blockedScene='';state.blockedControl=null;}
      const decision=await choose(frame);frame=decision.frame;const p=decision.plan;
      state.steps=step;state.lastFrame='before_action';
      write('Corbie > '+p.summary+' '+p.reason);
      if(watch){state.status='watching';state.phase='preview';await save();return {status:'watching',plan:p};}
      if(isEnding(p)) {
        await adapter.request('pause');await delay(1000);
        state.phase='confirming ending';await save();
        const second=await adapter.request('observe');
        const confirmation=await model.plan(second,state.history,{signal,confirmEnding:true});
        if(!isEnding(confirmation))throw new Error('The ending could not be confirmed from a second fresh picture. Paused for review.');
        await store.frame(frame.png,'ending-frame.png');await store.frame(second.png,'ending-confirmation.png');
        state.status='completed';state.phase='finished';state.ending={first:p.endingEvidence,confirmation:confirmation.endingEvidence};await save();
        write('Corbie > An adulthood ending, confirmed on two pictures. I shall accept a modest parade.');
        return {status:'completed',ending:state.ending};
      }
      const issue=decisionIssue(p);if(issue)throw new Error(issue);
      consecutiveWaits=p.action==='wait'?consecutiveWaits+1:0;
      if(consecutiveWaits>12)throw new Error('Twelve consecutive waits without input. Text-reading changes may be hiding a stalled dialogue. Paused for inspection; use watch.');
      if(['ending','credits'].includes(p.screen))throw new Error('This might be an ending, but the evidence is not clear enough. Paused for review.');
      if(signal?.aborted||!(await adapter.request('status')).armed)throw new Error('Paused by your human.');
      const beforeSignature=sceneSignature(frame),beforeScene=sceneLabel(frame);
      await store.frame(frame.png,'before-frame.png');
      const receipt={id:randomUUID(),at:new Date().toISOString(),screen:p.screen,action:p.action,target:p.target||p.action,
        x:p.x,y:p.y,key:p.key,reason:p.reason,date:p.date,age:p.age,gold:p.gold,stress:p.stress,health:p.health,
        beforeScene,result:p.action==='wait'?'waiting':'pending'};
      state.history.push(receipt);state.history=state.history.slice(-80);state.lastReceipt=receipt;
      state.phase=p.action==='wait'?'waiting':'acting';await save();
      if(p.action!=='wait') {
        state.social=recordSocialInput(state.social,frame,p);await save();
        write('Move · '+(p.action==='click'?'click “'+p.target+'”':'press '+p.key));
        try {await adapter.request('act',{observation:frame.id,type:p.action,x:p.x,y:p.y,key:p.key});}
        catch(error){receipt.result='uncertain';await save();throw error;}
        state.totalInputs++;
      }
      state.steps=step+1;state.phase='verifying';await save();
      await delay(p.action==='wait'?2500:1000);
      if(singleStep)await adapter.request('pause');
      frame=await observe('verifying');
      // Allow a slow menu one additional moment to settle, without repeating its input.
      if(!singleStep&&p.action!=='wait'&&sceneSignature(frame)===beforeSignature) {
        await delay(1200);frame=await observe('verifying');
      }
      const changed=sceneSignature(frame)!==beforeSignature;
      receipt.result=changed?'changed':p.action==='wait'?'waiting':'unchanged';
      receipt.afterScene=sceneLabel(frame);receipt.checkedAt=new Date().toISOString();
      state.lastFrame='after_action';state.scene=receipt.afterScene;
      if(changed){state.verifiedChanges++;blockedTargets=[];blockedScene='';state.blockedControl=null;unchangedWaits=0;}
      else if(p.action!=='wait') {blockedTargets=[p.target];blockedScene=sceneSignature(frame);state.blockedControl={targets:blockedTargets,scene:blockedScene};}
      else unchangedWaits++;
      write(changed?'Result · '+beforeScene+' → '+receipt.afterScene:'Result · no visible menu/text change'+(p.action==='wait'?' yet.':'; that click will not be repeated.'));
      await save();
      if(singleStep) {
        state.status='paused';state.phase='ready';state.pauseReason='Single move finished. '+(changed?'The screen changed.':'No visible change was confirmed.')+' Use step or resume to continue.';
        await save();write('Corbie > '+state.pauseReason);return {status:'paused',reason:state.pauseReason};
      }
      if(unchangedWaits>=12)throw new Error('The game has shown no readable progress for twelve observations. Paused for a look.');
    }
    throw new Error('Reached this run’s action limit. Progress is kept; resume when ready. No ending has been claimed.');
  } catch(error) {
    if(state.lastReceipt?.result==='pending')state.lastReceipt.result='uncertain';
    state.status='paused';state.phase='needs attention';state.pauseReason=error.message;await save();
    write('Corbie > '+error.message);return {status:'paused',reason:error.message};
  } finally {await adapter.request('pause').catch(()=>{});}
}
