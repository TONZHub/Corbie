import test from 'node:test';
import assert from 'node:assert/strict';
import {gameMonth,socialPlan,recordSocialInput,observeSocial} from './social.mjs';
import {groundClick} from './controls.mjs';
import {createGameModel} from './vision.mjs';
const frame=(labels=[],{home=true,dialog=false,month='Oct',year='1210'}={})=>({id:'test',png:'AA==',width:1200,height:900,detail:{
  controls:[...(home?[{id:'schedule',label:'Schedule',x:880,y:540}]:[]),...(dialog?[{id:'dialog_next',label:'Next dialogue',x:290,y:970}]:[])],
  ocr:{available:true,lines:[{text:year+' '+month,words:[{x:10,y:20,w:200,h:40}]},...labels.map((text,i)=>({text,words:[{x:dialog?50:750,y:dialog?780+i*30:450+i*40,w:200,h:24}]}))]}}});
test('monthly routine requires a complete game year/month and prioritizes daughter',()=>{
  const f=frame(['Talk','Go out']);
  assert.equal(gameMonth(f),'1210-10');
  assert.equal(groundClick(socialPlan(f),f).target,'Talk');
  assert.equal(socialPlan(frame(['Talk'],{year:''})).action,'pause');
  assert.equal(socialPlan(frame(['Talk'],{home:false})),null);
  assert.equal(socialPlan(frame(['Talk'],{dialog:true})),null);
});
test('attempts persist, while completion requires leaving home and returning',()=>{
  const f=frame(['Talk','Go out']);
  let s=recordSocialInput({},f,groundClick(socialPlan(f),f));
  assert.equal(s.months['1210-10'].daughter.status,'attempted; result unconfirmed');
  s=observeSocial(s,f);assert(s.active);
  s=observeSocial(s,frame(["I'm tired."],{home:false,dialog:true}));
  assert.equal(s.lastConversation.needsRest,true);
  s=observeSocial(s,f);
  assert.equal(s.active,null);
  assert.equal(s.months['1210-10'].daughter.status,'dialogue observed');
  assert.equal(groundClick(socialPlan(f,s),f).target,'Go out');
  const restarted=JSON.parse(JSON.stringify(s));
  assert.equal(groundClick(socialPlan(f,restarted),f).target,'Go out');
  assert.equal(groundClick(socialPlan(frame(['Talk'],{year:'1211'}),s),frame(['Talk'],{year:'1211'})).target,'Talk');
});
test('castle return without dialogue is not called a successful audience or retried',()=>{
  const f=frame(['Castle']);let s=recordSocialInput({},f,{action:'click',target:'Castle'});
  s=observeSocial(s,frame(['Unavailable'],{home:false}));
  s=observeSocial(s,f);
  assert.equal(s.months['1210-10'].castle.status,'returned without confirmed dialogue');
  assert.equal(socialPlan(f,s).action,'pause'); // The daughter check-in is still pending.
  s.months['1210-10'].daughter={status:'dialogue observed'};
  assert.equal(socialPlan(f,s),null);
});
test('active social tasks have a bounded input budget and ambiguous controls are not clicked',()=>{
  assert.equal(socialPlan(frame(['Talk','Talk'])).action,'pause');
  const p=socialPlan(frame([],{home:false}),{active:{kind:'castle',inputs:12}});
  assert.equal(p.action,'pause');
});
test('previews do not record attempts, seasonal wardrobe takes priority over social actions',async()=>{
  const model=createGameModel({fetcher:()=>{throw new Error('No inference expected');}});
  const s={};const f=frame(['Talk','Castle']);
  const p=await model.plan(f,[],{social:s});
  assert.equal(groundClick(p,f).target,'Talk');assert.deepEqual(s,{});
  const winter=frame(['Talk','Inventory'],{month:'Dec'});
  assert.equal(groundClick(await model.plan(winter,[],{social:s}),winter).target,'Inventory');
});
test('negated tiredness is not converted into a recovery instruction',()=>{
  const s=recordSocialInput({},frame(['Talk']),{action:'click',target:'Talk'});
  const read=observeSocial(s,frame(["I'm not tired."],{home:false,dialog:true}));
  assert.equal(read.lastConversation.needsRest,false);
});

test('monthly checks cannot fall through to scheduling or bypass an unchanged Talk click',async()=>{
  const model=createGameModel({fetcher:()=>{throw new Error('No inference expected');}});
  const f=frame(['Talk','Go to town']);
  const social={months:{'1210-10':{daughter:{status:'dialogue observed'}}}};
  assert.equal(groundClick(await model.plan(f,[],{social}),f).target,'Go to town');
  assert.equal((await model.plan(f,[],{blockedTargets:['Talk']})).action,'pause');
  assert.equal((await model.plan(frame([]))).action,'pause');
  const done={months:{'1210-10':{daughter:{status:'dialogue observed'},castle:{status:'returned without confirmed dialogue'}}}};
  assert.equal(socialPlan(f,done),null);
});

test('an active social visit removes Schedule from model click choices',async()=>{
  let choices;
  const model=createGameModel({fetcher:async(url,options)=>{
    const body=JSON.parse(options.body);choices=body.format.properties.move.enum;
    return {ok:true,json:async()=>({message:{content:JSON.stringify({screen:'home',confidence:.99,move:'click:schedule',
      reason:'Skip the conversation.',risk:'normal',outcome:'none',endingEvidence:''})}})};
  }});
  await assert.rejects(model.plan(frame(['Talk']),[],{social:{active:{kind:'daughter',month:'1210-10',inputs:1}}}),/current control list/);
  assert(!choices.includes('click:schedule'));
});
