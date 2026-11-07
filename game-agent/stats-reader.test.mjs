import test from 'node:test';
import assert from 'node:assert/strict';
import {mkdtemp,readFile,rm} from 'node:fs/promises';
import {dirname,join} from 'node:path';
import {fileURLToPath} from 'node:url';
import {FIELD_KEYS,STAT_KEYS,readStatsFrame,parseCorrections} from './stats-reader.mjs';
import {createStatsNotebook,mergeStatsReading} from './stats-notebook.mjs';
import {createGameModel} from './vision.mjs';
import {createSolverStore} from './solver.mjs';
import {runGameConsole} from './play.mjs';

const value=v=>({value:v,confidence:.85,source:'vision',evidence:'visible label/value'});
const reading=(fields={})=>({fields:{year:value(1210),month:value(11),...fields},observation:'current'});
const picture=()=>({id:'current',png:'AA==',width:1200,height:900,detail:{calendarRead:{year:1210,month:11},
  ocr:{available:true,text:'Standard diet 146 58 75/55/76',lines:[{text:'Standard diet'},{text:'146 58 75/55/76'}]}}});
const actual=()=>({year:1210,month:11,age:10,birth_month:1,sign:'capricorn',blood:'A',diet:'normal',gold:500,
  stats:{...Object.fromEntries(STAT_KEYS.map(k=>[k,40])),constitution:70,stress:10,sin:0}});
async function temporary(work) {
  const base=dirname(fileURLToPath(import.meta.url)),dir=await mkdtemp(join(base,'stats-check-'));
  try{await work(dir);}finally{assert.equal(dirname(dir),base);assert(dir.startsWith(join(base,'stats-check-')));await rm(dir,{recursive:true,force:true});}
}

test('labelled OCR and image proposals are independent; bare measurements and invented evidence are excluded',async()=>{
  const frame=picture();frame.detail.ocr.lines.push({text:'Stamina: 70'},{text:'Stress 10'},{text:'Gold: 500G'});
  const result=await readStatsFrame(frame,{infer:async()=>({readings:[
    {field:'constitution',value:70,confidence:1,evidence:'Stamina 70'},
    {field:'strength',value:146,confidence:1,evidence:'146 58 75/55/76'},
    {field:'intelligence',value:99,confidence:1,evidence:'Intelligence 12'},
    {field:'blood',value:'AB',confidence:1,evidence:'Blood O'},
    {field:'combat_attack',value:5,confidence:.95,evidence:'Combat attack 5'}]})});
  assert.equal(result.fields.constitution.value,70);assert.equal(result.fields.constitution.source,'ocr');
  assert.equal(result.fields.gold.value,500);assert.equal(result.fields.diet.value,'normal');
  assert.equal(result.fields.combat_attack.confidence,.85);
  assert(!result.fields.strength&&!result.fields.intelligence&&!result.fields.blood);
  assert(result.missing.includes('strength'));
});

test('OCR/vision disagreements become conflicts rather than replacing a labelled value',async()=>{
  const frame=picture();frame.detail.ocr.lines.push({text:'Stress: 10'});
  const result=await readStatsFrame(frame,{infer:async()=>({readings:[{field:'stress',value:70,confidence:1,evidence:'Stress 70'}]})});
  assert.deepEqual(result.fields.stress.conflict,[10,70]);assert.equal(result.fields.stress.confidence,0);
  await assert.rejects(readStatsFrame(frame,{infer:async()=>({})}),/unreadable/);
});

test('captures accumulate only within the same verified month; changed/unknown months do not reuse old stats',()=>{
  let draft=mergeStatsReading(null,reading({constitution:value(70)}));
  draft=mergeStatsReading(draft,reading({stress:value(10)}));
  assert.equal(draft.fields.constitution.value,70);assert.equal(draft.fields.stress.value,10);
  const conflict=mergeStatsReading(draft,reading({stress:value(12)}));
  assert.deepEqual(conflict.fields.stress.conflict,[10,12]);
  assert.throws(()=>mergeStatsReading(draft,{fields:{strength:value(30)}}),/month is unreadable/);
  const attested=mergeStatsReading(draft,{fields:{strength:value(30)}},{sameMonth:true});
  assert.equal(attested.fields.strength.value,30);
  const next=mergeStatsReading(draft,reading({month:value(12),gold:value(500)}));
  assert(!next.fields.constitution&&!next.fields.stress);assert.equal(next.fields.month.value,12);
});

test('corrections validate every assignment and support Refine aliases without accepting partial malformed input',()=>{
  assert.deepEqual(parseCorrections('stamina=120 stress=10 diet=normal blood=ab'),{constitution:120,stress:10,diet:'normal',blood:'AB'});
  for(const text of ['stress=-1','gold=NaN','month=30','stress=10 rubbish','stress=10, gold=5','stress=10 stress=20','shell=1','gold=','gold=""','stress=1.5'])assert.throws(()=>parseCorrections(text));
});

test('draft scans and corrections never update the solver until a complete conflict-free review is accepted',async()=>{
  await temporary(async directory=>{
    const imports=[],notebook=createStatsNotebook({directory,solverStore:{importStats:async(...args)=>{imports.push(args);return 'saved';}}});
    await notebook.scan(picture(),reading({constitution:value(70)}));
    assert.match(await notebook.review(),/check image/);assert.equal(imports.length,0);
    await assert.rejects(notebook.accept(),/incomplete/);
    const state=actual(),fields={};for(const key of FIELD_KEYS)fields[key]=value(key in state?state[key]:state.stats[key]);
    await notebook.scan(picture(),{fields,observation:'next-page'});
    await notebook.scan(picture(),reading({constitution:value(80)}));
    await assert.rejects(notebook.accept(),/constitution/);
    await notebook.correct('stamina=70');
    assert.equal(imports.length,0);
    await notebook.accept();assert.equal(imports.length,1);assert.deepEqual(imports[0][0],state);
    assert.match(await readFile(join(directory,'stats-readings.jsonl'),'utf8'),/reviewed|accepted/);
    await notebook.correct('month=12');
    assert.match(await notebook.review(),/constitution: \? missing/);
  });
});

test('model stat reads send actual game images to the verified local vision model with constrained fields',async()=>{
  const requests=[];
  const model=createGameModel({fetcher:async(url,options)=>{requests.push({url,body:JSON.parse(options.body)});
    return {ok:true,json:async()=>url.endsWith('show')?{capabilities:['vision','completion']}:{message:{content:'{"readings":[{"field":"age","value":10,"confidence":0.9,"evidence":"AGE 10"}]}'}}};}});
  await model.verify();const result=await model.readStats(picture());
  assert.equal(result.fields.age.value,10);
  const request=requests.at(-1);assert.equal(request.url,'http://127.0.0.1:11434/api/chat');
  assert.deepEqual(request.body.messages[0].images,['AA==']);
  assert.match(request.body.messages[0].content,/BODY MEASUREMENTS/);
  assert.equal(request.body.format.properties.readings.items.additionalProperties,false);
});

test('solver scan pauses and captures the game without arming, clicking or applying readings',async()=>{
  const commands=['solver scan','solver review','solver set stress=10','solver accept','back'],calls=[],steps=[];
  await runGameConsole({reader:{question:async()=>commands.shift()},write:()=>{},
    adapter:{request:async command=>{calls.push(command);return command==='observe'?picture():{};},close:()=>{}},
    model:{verify:async()=>{},inspectFrame:async f=>f,readStats:async()=>reading()},
    statsNotebook:{scan:async()=>{steps.push('scan');return 'proposal';},review:async()=>{steps.push('review');return 'proposal';},
      correct:async s=>{assert.equal(s,'stress=10');steps.push('correct');return 'proposal';},accept:async()=>{steps.push('accept');return 'accepted';}}});
  assert(!calls.includes('arm')&&!calls.includes('act'));
  assert.deepEqual(steps,['scan','review','correct','accept']);assert(calls.includes('observe'));
});

test('reviewed actuals compare with the prior-month forecast without blaming an unverified schedule',async()=>{
  await temporary(async directory=>{
    const solver=createSolverStore({directory});await solver.importStats(actual());
    await solver.context(picture(),{gold:500,age:10,stress:10});
    const next=actual();next.month=12;next.gold=510;next.stats.stress=5;
    await solver.importStats(next,{source:'reviewed screen readings'});
    const report=await readFile(join(directory,'stats-notebook.md'),'utf8');
    assert.match(report,/Forecast comparison/);assert.match(report,/not verified as executed/);assert.match(report,/510/);
    const snapshot=JSON.parse(await readFile(join(directory,'solver-stats.json'),'utf8'));
    assert.equal(snapshot.state.gold,510);assert.equal(snapshot.provenance.source,'reviewed screen readings');
  });
});
