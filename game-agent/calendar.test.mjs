import test from 'node:test';
import assert from 'node:assert/strict';
import {readGameCalendar} from './calendar.mjs';
import {readWardrobe} from './inventory.mjs';
import {createGameModel} from './vision.mjs';
import {playGame} from './engine.mjs';

const line=(text,y=20)=>({text,words:[{x:10,y,w:200,h:30}]});
const home=()=>({id:'home',png:'AA==',width:1200,height:900,armed:true,detail:{calendar:'CAL',
  controls:[{id:'schedule',label:'Schedule',x:880,y:540}],ocr:{available:true,lines:[line('11 30')]}}});

test('calendar reads only the current HUD, not the upcoming schedule month or prose',()=>{
  const f=home();f.detail.ocr.lines=[line('1210 Nov'),line('1210 Dec',220),line('January is cold',750)];
  assert.deepEqual(readGameCalendar(f),{year:1210,month:11});
  f.detail.ocr.lines.push(line('Oct',70));
  assert.equal(readGameCalendar(f).month,null);
  f.detail.calendarRead={year:1210,month:12};
  assert.equal(readWardrobe(f).month,12);
  f.detail.calendarRead={year:1210,month:30};
  assert.equal(readGameCalendar(f).month,null);
});

test('unclear home dates use only the focused calendar image and valid results are cached',async()=>{
  const calls=[];
  const model=createGameModel({fetcher:async(url,options)=>{
    const body=JSON.parse(options.body);calls.push(body);
    return {ok:true,json:async()=>({message:{content:JSON.stringify({year:1210,month:11})}})};
  }});
  const f=home();
  const inspected=await model.inspectFrame(f);
  assert.deepEqual(readGameCalendar(inspected),{year:1210,month:11});
  assert(!f.detail.calendarRead);assert.equal(inspected.id,f.id);
  await model.inspectFrame(f);
  assert.equal(calls.length,1);assert.deepEqual(calls[0].messages[0].images,['CAL']);
  const dialog=home();dialog.detail.controls.push({id:'dialog_next',label:'Next dialogue',x:300,y:900});
  await model.inspectFrame(dialog);assert.equal(calls.length,1);
  const menu=home();menu.detail.controls=[];
  await model.inspectFrame(menu);assert.equal(calls.length,1);
  const known=home();known.detail.ocr.lines=[line('1210 Nov')];
  await model.inspectFrame(known);assert.equal(calls.length,1);
});

test('invalid calendar readings stay unknown and are not cached as a valid month',async()=>{
  let calls=0;
  const model=createGameModel({fetcher:async()=>{calls++;return {ok:true,json:async()=>({message:{content:'{"year":1210,"month":30}'}})};}});
  for(let n=0;n<2;n++)assert.deepEqual(readGameCalendar(await model.inspectFrame(home())),{year:null,month:null});
  assert.equal(calls,2);
});

test('a conflicting image date cannot replace a readable part of the current calendar',async()=>{
  const model=createGameModel({fetcher:async()=>({ok:true,json:async()=>({message:{content:'{"year":1211,"month":11}'}})})});
  const f=home();f.detail.ocr.lines=[line('1210')];
  assert.deepEqual(readGameCalendar(await model.inspectFrame(f)),{year:1210,month:null});
});

test('the engine enriches the current date before choosing, journaling and recording a social input',async()=>{
  const frames=[home(),{...home(),id:'after',detail:{controls:[],ocr:{available:true,lines:[]}}}];
  frames[0].detail.ocr.lines.push({text:'Talk',words:[{x:750,y:450,w:150,h:30}]});
  const saved=[],inputs=[];
  const base=createGameModel({fetcher:async()=>({ok:true,json:async()=>({message:{content:'{"year":1210,"month":11}'}})})});
  const result=await playGame({singleStep:true,write:()=>{},delay:async()=>{},model:{...base,verify:async()=>({})},
    adapter:{request:async(command,args)=>{
      if(command==='observe')return frames.shift();
      if(command==='status')return {armed:true};
      if(command==='act')inputs.push(args);
      return {};
    }},store:{load:async()=>({}),save:async s=>saved.push(structuredClone(s)),frame:async()=>{}}});
  assert.equal(result.status,'paused');assert.equal(inputs.length,1);
  assert.equal(inputs[0].observation,'home');
  assert.equal(saved.at(-1).social.active.kind,'daughter');
  assert.equal(saved.at(-1).social.active.month,'1210-11');
  assert(saved.some(s=>s.currentObservation?.calendar?.month===11));
});
