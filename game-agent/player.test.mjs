import test from 'node:test';
import assert from 'node:assert/strict';
import {parsePlan,decisionIssue,isEnding,createGameModel,playGame} from './player.mjs';

const plan=(changes={})=>({screen:'home',confidence:.98,summary:'The schedule is visible.',date:'June',age:10,gold:400,stress:8,health:null,
  action:'click',target:'Schedule',x:300,y:500,key:null,reason:'Set a safe month.',risk:'normal',outcome:'none',endingEvidence:'',...changes});
const move=(changes={})=>({screen:'home',confidence:.98,move:'click:schedule',reason:'Set a safe month.',risk:'normal',outcome:'none',endingEvidence:'',...changes});
function harness(plans,{armed=true}={}) {
  let last={},frame=0;const calls=[],saved=[],pictures=[];
  return {calls,saved,pictures,
    adapter:{request:async(command,args)=>{calls.push({command,args});if(command==='observe')return {id:'f'+frame,png:Buffer.from('frame'+frame++).toString('base64'),width:640,height:480,armed,detail:{controls:[{id:'schedule',label:'Schedule',x:875,y:534}]}};if(command==='status')return {armed};return {};}},
    model:{verify:async()=>{},plan:async()=>plans.shift()||plan({action:'pause',reason:'Finished the test.'})},
    store:{load:async()=>({history:[]}),save:async s=>{last=structuredClone(s);saved.push(last);},frame:async(png,name)=>pictures.push(name)},
    delay:async()=>{},write:()=>{}
  };
}

test('invalid or dangerous model decisions cannot become input',()=>{
  assert.throws(()=>parsePlan('not json'));
  assert.throws(()=>parsePlan(JSON.stringify(plan({action:'key',key:'Alt+F4'}))));
  assert.throws(()=>parsePlan(JSON.stringify(plan({x:1100}))));
  assert.throws(()=>parsePlan(JSON.stringify({...plan(),shell:'delete files'})));
  assert.match(decisionIssue(plan({confidence:.4})),/not confident/);
  for(const screen of ['title','setup','save','load','unknown'])assert(decisionIssue(plan({screen})));
  assert(decisionIssue(plan({target:'Overwrite save'})));
  assert(decisionIssue(plan({outcome:'death'})));
});
test('one observation leads to one action, then a fresh observation; journal records observed resources',async()=>{
  const h=harness([plan(),plan({action:'pause',reason:'Need human help.'})]);
  const result=await playGame({...h});
  assert.equal(result.status,'paused');
  const inputs=h.calls.filter(c=>c.command==='act');
  assert.equal(inputs.length,1);assert.equal(inputs[0].args.observation,'f0');
  assert.equal(inputs[0].args.x,875);assert.equal(inputs[0].args.y,534);
  assert.equal(h.calls.filter(c=>c.command==='observe').length,3);
  assert.equal(h.saved.at(-1).history[0].result,'unchanged');
  assert.equal(h.saved.at(-1).history[0].gold,400);
  assert.equal(h.calls.at(-1).command,'pause');
});
test('F8 during local inference stops the action before injection',async()=>{
  const h=harness([plan()],{armed:false});
  await playGame({...h});
  assert.equal(h.calls.filter(c=>c.command==='act').length,0);
  assert.match(h.saved.at(-1).pauseReason,/F8/);
  const h2=harness([plan()]);h2.adapter.request=async(command)=>{h2.calls.push({command});return command==='observe'?{id:'one',png:'AA==',width:640,height:480,armed:true,detail:{controls:[{id:'schedule',label:'Schedule',x:875,y:534}]}}:command==='status'?{armed:false}:{};};
  await playGame({...h2});
  assert(!h2.calls.some(c=>c.command==='act'));
});
test('an invented or disappeared click target pauses before input',async()=>{
  const h=harness([plan({target:'Farm'}),plan({target:'Farm'})]);
  const result=await playGame({...h});
  assert.match(result.reason,/Could not locate/);
  assert(!h.calls.some(c=>c.command==='act'));
});
test('watch mode focuses and inspects without arming or clicking',async()=>{
  const h=harness([plan()]);
  assert.equal((await playGame({...h,watch:true})).status,'watching');
  assert(h.calls.some(c=>c.command==='focus'));
  assert(!h.calls.some(c=>['arm','act'].includes(c.command)));
});
test('a bad reading gets one fresh picture and only the repaired decision can send input',async()=>{
  const h=harness([plan({target:'Imaginary button'}),plan()]);
  await playGame({...h,singleStep:true});
  const actions=h.calls.filter(c=>c.command==='act');
  assert.equal(actions.length,1);assert.equal(actions[0].args.observation,'f1');
  assert.equal(h.saved.at(-1).totalInputs,1);
});
test('verified changes are recorded only from the screen following an input',async()=>{
  const h=harness([plan()]);let n=0;
  h.adapter.request=async(command,args)=>{h.calls.push({command,args});if(command==='status')return {armed:true};if(command==='observe')return {id:'frame'+n,png:'AA==',width:640,height:480,armed:true,detail:{controls:[n++?{id:'work',label:'Work',x:700,y:500}:{id:'schedule',label:'Schedule',x:875,y:534}]}};return {};};
  await playGame({...h,singleStep:true});
  assert.equal(h.saved.at(-1).history[0].result,'changed');
  assert.equal(h.saved.at(-1).verifiedChanges,1);
  assert(h.pictures.includes('before-frame.png'));
});
test('single-step makes exactly one move, disarms, and saves its resulting screen',async()=>{
  const h=harness([plan()]);
  const result=await playGame({...h,singleStep:true});
  assert.match(result.reason,/Single move finished/);
  assert.equal(h.calls.filter(c=>c.command==='act').length,1);
  assert.equal(h.calls.filter(c=>c.command==='observe').length,2);
  assert.equal(h.calls.at(-2).command,'observe');
  assert.equal(h.calls.at(-3).command,'pause');
  assert.equal(h.saved.at(-1).lastFrame,'after_action');
  assert.equal(h.saved.at(-1).steps,1);
});
test('unchanged progress and repeated choices pause instead of endless clicks',async()=>{
  const h=harness(Array.from({length:12},()=>plan()));
  const result=await playGame({...h,maxSteps:20});
  assert.match(result.reason,/no visible change/);
  assert.equal(h.calls.filter(c=>c.command==='act').length,1);
  assert.deepEqual(h.saved.at(-1).blockedControl.targets,['Schedule']);
});
test('Enter advances once and an unchanged dialogue blocks repeated keypresses',async()=>{
  const p=plan({screen:'dialog',action:'key',key:'Enter',target:'',x:null,y:null});
  const h=harness([p,p,p]);
  const result=await playGame({...h,maxSteps:10});
  const inputs=h.calls.filter(c=>c.command==='act');
  assert.equal(inputs.length,1);assert.equal(inputs[0].args.type,'key');assert.equal(inputs[0].args.key,'Enter');
  assert.match(result.reason,/no visible change/);
  assert.deepEqual(h.saved.at(-1).blockedControl.targets,['key:Enter']);
});
test('OCR jitter cannot keep a wait loop alive indefinitely',async()=>{
  const h=harness(Array.from({length:20},()=>plan({action:'wait',target:'',x:null,y:null})));let n=0;
  const request=h.adapter.request;
  h.adapter.request=async(...args)=>{const r=await request(...args);if(args[0]==='observe')r.detail.controls[0].label='Noisy text '+n++;return r;};
  const result=await playGame({...h,maxSteps:30});
  assert.match(result.reason,/Twelve consecutive waits/);
  assert.equal(h.saved.at(-1).steps,12);
  assert(!h.calls.some(c=>c.command==='act'));
});
test('only adulthood ending evidence on two fresh frames can claim completion',async()=>{
  const ending=plan({screen:'ending',action:'pause',outcome:'adulthood',endingEvidence:'Her final career is shown here.'});
  assert(isEnding(ending));
  for(const p of [plan({age:18}),plan({screen:'title',action:'pause',outcome:'adulthood'}),plan({screen:'ending',outcome:'death'}),plan({screen:'ending',endingEvidence:''})])assert(!isEnding(p));
  const h=harness([ending,ending]);assert.equal((await playGame({...h})).status,'completed');
  assert.equal(h.calls.filter(c=>c.command==='observe').length,2);
  assert(!h.calls.some(c=>c.command==='act'));
  assert(h.pictures.includes('ending-frame.png')&&h.pictures.includes('ending-confirmation.png'));
  const uncertain=harness([ending,plan()]);assert.equal((await playGame({...uncertain})).status,'paused');
  assert(!uncertain.saved.some(s=>s.status==='completed'));
});
test('action-limit and adapter failure never claim an ending or repeat uncertain input',async()=>{
  const h=harness([plan()]);
  assert.match((await playGame({...h,maxSteps:1})).reason,/action limit/);
  const fail=harness([plan()]);
  const request=fail.adapter.request;
  fail.adapter.request=async(...args)=>{if(args[0]==='act'){fail.calls.push({command:'act'});throw new Error('Input is uncertain.');}return request(...args);};
  assert.match((await playGame({...fail})).reason,/uncertain/);
  assert.equal(fail.calls.filter(c=>c.command==='act').length,1);
});
test('local image model uses actual frame bytes, structured decisions and no remote providers',async()=>{
  const requests=[];
  const model=createGameModel({plannerModel:'gemma3:4b',fetcher:async(url,options)=>{const body=JSON.parse(options.body);requests.push({url,body});return {ok:true,json:async()=>url.endsWith('show')?{capabilities:['vision']}: {message:{content:JSON.stringify(body.format.required.includes('age')?{age:10,gold:500}:move())}}};}});
  await model.verify();
  const controls=[{id:'schedule',label:'Schedule',x:875,y:534}];
  assert.equal((await model.plan({png:'YWJj',width:640,height:480,detail:{controls}},[],{useRules:false})).target,'schedule');
  assert(requests.every(r=>r.url.startsWith('http://127.0.0.1:11434/')));
  assert.deepEqual(requests[1].body.messages[1].images,['YWJj']);
  assert.equal(requests[1].body.format.additionalProperties,false);
  const result=await model.plan({png:'YWJj',width:1200,height:900,detail:{controls,hud:'HUD',calendar:'CAL',dialog:'DIALOG',ocr:{available:true,text:'Cube the butler'}}},[],{useRules:false});
  assert.equal(result.age,10);assert.equal(result.gold,500);assert.equal(result.date,null);
  assert.deepEqual(requests[2].body.messages[0].images,['HUD']);
  assert.deepEqual(requests[3].body.messages[1].images,['YWJj','HUD','CAL','DIALOG']);
  assert.match(requests[3].body.messages[1].content,/Cube the butler/);
  assert.match(requests[3].body.messages[0].content,/first image is the full game/);
  assert(requests[3].body.format.properties.move.enum.includes('click:schedule'));
  assert(!requests[3].body.format.properties.move.enum.includes('click:farm'));
  await assert.rejects(createGameModel({fetcher:async()=>({ok:true,json:async()=>({remote_host:'cloud',capabilities:['vision']})})}).verify(),/Cloud models/);
});
test('vision rejects an old target even when a mock model ignores the current target enum',async()=>{
  const model=createGameModel({fetcher:async()=>({ok:true,json:async()=>({message:{content:JSON.stringify(move())}})})});
  await assert.rejects(model.plan({png:'AA==',width:1200,height:900,detail:{controls:[{id:'work',label:'Work',x:700,y:500}]}},[],{useRules:false}),/current control list/);
});
test('choice prompts use images and reject wait even when the model ignores the allowed moves',async()=>{
  const requests=[];
  const model=createGameModel({fetcher:async(url,options)=>{const body=JSON.parse(options.body);requests.push(body);return {ok:true,json:async()=>url.endsWith('show')?{capabilities:['vision','completion']}:{message:{content:JSON.stringify(move({move:'wait'}))}}};}});
  await model.verify();
  const f={png:'AA==',width:1200,height:900,detail:{ocr:{available:true,lines:[
    {text:'Choose an option?',words:[{x:50,y:750,w:300,h:20}]},
    {text:'First',words:[{x:760,y:500,w:100,h:20}]},
    {text:'Second',words:[{x:760,y:550,w:100,h:20}]}
  ]}}};
  await assert.rejects(model.plan(f,[],{useRules:false}),/current control list/);
  const body=requests.at(-1);assert.equal(body.model,'gemma3:4b');
  assert.deepEqual(body.messages[1].images,['AA==']);
  assert(!body.format.properties.move.enum.includes('wait'));
});
test('local text planner uses current controls and cached independent HUD readings',async()=>{
  const requests=[];
  const model=createGameModel({fetcher:async(url,options)=>{
    const body=JSON.parse(options.body);requests.push(body);
    return {ok:true,json:async()=>url.endsWith('show')?{capabilities:['vision','completion']}:
      {message:{content:JSON.stringify(body.format.required.includes('age')?{age:10,gold:500}:move())}}};
  }});
  assert.equal((await model.verify()).planner,'qwen3:4b-instruct');
  const frame={png:'AA==',width:1200,height:900,detail:{hud:'same-panel',controls:[{id:'schedule',label:'Schedule',x:875,y:534}]}};
  for(let n=0;n<2;n++)assert.equal((await model.plan(frame,[],{useRules:false})).gold,500);
  assert.equal(requests.filter(r=>r.format?.required.includes('age')).length,1);
  const decisions=requests.filter(r=>r.format?.required.includes('move'));
  assert.equal(decisions.length,2);
  assert(decisions.every(r=>r.model==='qwen3:4b-instruct'&&!r.messages[1].images));
  await model.plan(frame,[],{confirmEnding:true});
  assert.equal(requests.at(-1).model,'gemma3:4b');
  assert.deepEqual(requests.at(-1).messages[1].images,['AA==','same-panel']);
});
test('cloud-hosted text planners are rejected too',async()=>{
  const model=createGameModel({fetcher:async(url,options)=>({ok:true,json:async()=>JSON.parse(options.body).model==='gemma3:4b'?{capabilities:['vision']}:{capabilities:['completion'],remote_host:'cloud'}})});
  await assert.rejects(model.verify(),/downloaded local model/);
});
test('losing focus after input records an uncertain result instead of waiting forever',async()=>{
  const h=harness([plan()]);const request=h.adapter.request;let observations=0;
  h.adapter.request=async(...args)=>{const result=await request(...args);if(args[0]==='observe'&&observations++>0)result.armed=false;return result;};
  await playGame({...h});
  assert.equal(h.saved.at(-1).lastReceipt.result,'uncertain');
  assert.equal(h.saved.at(-1).verifiedChanges,0);
  assert.equal(h.calls.filter(c=>c.command==='act').length,1);
});
