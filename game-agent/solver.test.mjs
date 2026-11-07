import test from 'node:test';
import assert from 'node:assert/strict';
import {mkdtemp,writeFile,readFile,readdir,unlink,rmdir} from 'node:fs/promises';
import {join} from 'node:path';
import {fileURLToPath} from 'node:url';
import {runSolver,createSolverStore} from './solver.mjs';
import {createGameModel} from './vision.mjs';
import {runGameConsole} from './play.mjs';
import {groundClick} from './controls.mjs';

const keys='constitution strength intelligence refinement charisma morality faith sin sensitivity stress combat_skill combat_attack combat_defense magic_skill magic_attack magic_defense decorum art conversation cooking cleaning temperament'.split(' ');
const state=()=>({year:1210,month:11,age:10,birth_month:1,blood:'A',sign:'capricorn',diet:'normal',gold:500,
  stats:{...Object.fromEntries(keys.map(k=>[k,40])),constitution:70,stress:10,sin:0}});
const frame=()=>({png:'AA==',width:1200,height:900,detail:{calendarRead:{year:1210,month:11},ocr:{available:true,lines:
  ['Housework','Babysitter','Inn','Farm','Church','Restaurant'].map((text,i)=>({text,words:[{x:750,y:400+i*50,w:120,h:24}]}))}}});
async function temporary(work) {
  const dir=await mkdtemp(fileURLToPath(new URL('./solver-check-',import.meta.url)));
  try{await work(dir);}finally{for(const name of await readdir(dir))await unlink(join(dir,name));await rmdir(dir);}
}

test('real Python bridge supplies source-labelled job knowledge and rejects incomplete or mid-month stats',async()=>{
  const result=await runSolver({command:'knowledge',names:['Farm','Church','Babysitter','protocol','Free time']});
  assert.equal(result.knowledge.Farm.stress,3);
  assert.equal(result.knowledge.Church.stress,1);
  assert(result.knowledge.Farm.source.startsWith('https://'));
  assert(result.knowledge.Babysitter.pay.length);
  assert(result.knowledge.protocol.cost.length);
  await assert.rejects(runSolver({command:'validate',stats:{year:1210}}),/Missing current values/);
  await assert.rejects(runSolver({command:'validate',stats:{...state(),period:1}}),/start of a month/);
  const bad=state();delete bad.stats.constitution;
  await assert.rejects(runSolver({command:'validate',stats:bad}),/Missing observed stats/);
  await assert.rejects(runSolver({command:'validate',stats:{...state(),gold:null}}),/Invalid gold/);
  await assert.rejects(runSolver({command:'validate',stats:{...state(),job_success_days:{farm:-1}}}),/Invalid/);
});

test('real survival search adapts to stress, uses only offered jobs and never changes observations',async()=>{
  const input=state(),before=JSON.stringify(input);
  const low=await runSolver({command:'plan',stats:input,jobs:['church'],goal:'survival'});
  assert.equal(low.actions.length,3);assert(low.actions.every(a=>['job:church','rest:free_time'].includes(a)));
  const tired=state();tired.stats.stress=99;
  const high=await runSolver({command:'plan',stats:tired,jobs:['farm'],goal:'survival'});
  assert.equal(high.actions[0],'rest:free_time');
  assert.equal(JSON.stringify(input),before);assert(low.projected);assert.match(low.notice,/not observed/);
  const october=await runSolver({command:'plan',stats:{...state(),month:10},goal:'survival'});
  assert.equal(october.status,'festival');assert(!october.actions);
});

test('named targets use the actual upstream one-month planner without claiming completion',async()=>{
  const result=await runSolver({command:'plan',stats:state(),goal:'queen'});
  assert.equal(result.goal,'ruling_queen');assert.equal(result.actions.length,3);
  assert.match(result.note,/not a demonstrated ending route/);
});

test('loaded snapshots persist, forecasts cache, stale/unknown dates and changed resources withhold plans',async()=>{
  await temporary(async dir=>{
    const path=join(dir,'Actual Stats.JSON');await writeFile(path,JSON.stringify(state()));
    let plans=0;
    const runner=async(req,options)=>{if(req.command==='plan')plans++;return runSolver(req,options);};
    const store=createSolverStore({directory:dir,runner});
    assert.equal((await store.context(frame())).status,'needs-stats');
    await store.importFile('"'+path+'"');
    const saved=await readFile(join(dir,'solver-stats.json'),'utf8');
    const observed={gold:500,age:10,stress:10};
    assert.equal((await store.context(frame(),observed)).status,'advice');
    await store.context(frame(),observed);assert.equal(plans,1);
    assert.equal(await readFile(join(dir,'solver-stats.json'),'utf8'),saved);
    const next=frame();next.detail.calendarRead.month=12;
    assert.equal((await store.context(next,observed)).status,'stale');
    const unknown=frame();delete unknown.detail.calendarRead;
    assert.equal((await store.context(unknown,observed)).status,'stale');
    for(const key of ['gold','age','stress'])assert.equal((await store.context(frame(),{...observed,[key]:observed[key]+1})).status,'stale');
    assert.equal(plans,1);
    assert.match(await createSolverStore({directory:dir}).describe(),/1210-11/);
    await writeFile(path,'{}');await assert.rejects(store.importFile(path));
    assert.equal(await readFile(join(dir,'solver-stats.json'),'utf8'),saved);
    await store.clear();assert.equal((await store.context(frame())).status,'needs-stats');
  });
});

test('stats forms start unknown and repeat requests preserve user edits',async()=>{
  await temporary(async dir=>{
    const store=createSolverStore({directory:dir});
    await store.template();const path=join(dir,'solver-stats-template.json');
    const form=JSON.parse(await readFile(path,'utf8'));
    assert.equal(form.gold,null);assert(Object.values(form.stats).every(v=>v===null));
    await writeFile(path,'my edits');await store.template();
    assert.equal(await readFile(path,'utf8'),'my edits');
    await assert.rejects(store.preview(),/Load actual stats/);
  });
});

test('solver guidance reaches the game planner and cannot add ungrounded controls',async()=>{
  let prompt;
  const model=createGameModel({solverStore:{context:async()=>({status:'advice',advice:{actions:['job:church'],projected:{gold:1000}}})},
    fetcher:async(url,options)=>{const body=JSON.parse(options.body);prompt=body.messages;
      return {ok:true,json:async()=>({message:{content:JSON.stringify({screen:'schedule',confidence:1,move:'click:text_4',reason:'Compare the Church forecast with current health.',risk:'normal',outcome:'none',endingEvidence:''})}})};}});
  assert.equal(groundClick(await model.plan(frame()),frame()).target,'Church');
  assert.match(prompt[1].content,/job:church/);assert.match(prompt[0].content,/projected outcomes, not observations/);
  const failed=createGameModel({solverStore:{context:async()=>{throw new Error('Python unavailable');}},fetcher:async()=>({ok:true,json:async()=>({message:{content:'{"screen":"schedule","confidence":1,"move":"pause","reason":"Need help","risk":"normal","outcome":"none","endingEvidence":""}'}})})});
  assert.equal((await failed.plan(frame())).action,'pause');
});

test('solver commands preserve file paths and never start game input or require Ollama',async()=>{
  const commands=['solver','solver template','solver load "C:\\My Stats\\Daughter.JSON"','solver plan queen','solver info Farm','solver clear','back'];
  const calls=[],out=[];
  const store=Object.fromEntries(['describe','template','importFile','preview','info','clear'].map(name=>[name,async arg=>{calls.push([name,arg]);return 'okay';}]));
  await runGameConsole({reader:{question:async()=>commands.shift()},write:s=>out.push(s),solverStore:store,
    model:{verify:async()=>{throw new Error('No model expected');}},adapter:{request:async c=>{assert.equal(c,'pause');return {};},close:()=>{}}});
  assert.deepEqual(calls.map(c=>c[0]),['describe','template','importFile','preview','info','clear']);
  assert.equal(calls[2][1],'"C:\\My Stats\\Daughter.JSON"');assert.equal(calls[3][1],'queen');
});
