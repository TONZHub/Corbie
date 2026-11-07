import test from 'node:test';
import assert from 'node:assert/strict';
import {schedulePlan as rawSchedulePlan} from './schedule.mjs';
import {groundClick} from './controls.mjs';
import {createGameModel} from './player.mjs';
const schedulePlan=(frame,options)=>{const p=rawSchedulePlan(frame,options);return p?groundClick(p,frame):null;};

const frame=(period,labels=['Study','Work','Errantry','Time off'])=>({png:'AA==',width:1200,height:900,detail:{ocr:{available:true,lines:[
  {text:'(Period '+period+')',words:[{x:20,y:800,w:100,h:24}]},
  ...labels.map((text,i)=>({text,words:[{x:760,y:500+i*40,w:100,h:24}]}))
]}}});

test('normal periods are strategy decisions rather than a fixed Work Work Rest pattern',()=>{
  for(const period of [1,2,3])assert.equal(schedulePlan(frame(period)),null);
});
test('completed month chooses Run schedule rather than Cancel without model inference',async()=>{
  const f=frame(1,['Run schedule','Cancel']);
  f.detail.ocr.lines[0].text='This is the schedule for this month';
  assert.equal(schedulePlan(f).target,'Run schedule');
  const model=createGameModel({fetcher:()=>{throw new Error('Final schedule must not require a model.');}});
  const p=await model.plan(f,[{target:'Cancel',result:'changed'}]);
  assert.equal(groundClick(p,f).target,'Run schedule');
  f.detail.ocr.lines[0].text='Overwrite your save this month?';
  assert.equal(schedulePlan(f),null);
});
test('pocket money prompt resolves its visible decline choice rather than waiting',()=>{
  const f=frame(1,['Give','Don t give']);
  f.detail.ocr.lines[0].text='Give her scme pocket money?';
  assert.equal(schedulePlan(f).target,'Don t give');
  f.detail.ocr.lines[0].text='Give her a different dress?';
  assert.equal(schedulePlan(f),null);
});
test('an explicit tiredness report can change the first work block to rest',()=>{
  const p=schedulePlan(frame(1),{needsRest:true});
  assert.equal(p.target,'Time off');assert.match(p.reason,/said she was tired/);
});
test('farm block introduction advances only with its currently detected dialogue marker',()=>{
  const f=frame(1,[]);
  f.detail.ocr.lines=[{text:'Fran today, Corbia will do: Part time work at a farm on the outskirts of town.',words:[{x:50,y:810,w:400,h:60}]}];
  assert.equal(schedulePlan(f),null);
  f.detail.controls=[{id:'dialog_next',label:'Next dialogue',x:292,y:975}];
  assert.equal(schedulePlan(f).target,'Next dialogue');
  f.detail.ocr.lines[0].text='Should we change into summer clothing?';
  assert.equal(schedulePlan(f),null);
});
test('only the current period prompt and actual menu can select a schedule or job',()=>{
  assert.equal(schedulePlan(frame(1,['Farm','Housework'])),null);
  assert.equal(schedulePlan(frame(2,['Farm'])),null);
  assert.equal(schedulePlan(frame(1,['Study','Work'])),null);
  assert.equal(schedulePlan(frame(1,['Farm','Farm'])),null);
  assert.equal(schedulePlan({}),null);
  const unrelated=frame(1);unrelated.detail.ocr.lines[0].words[0].y=100;
  assert.equal(schedulePlan(unrelated),null);
});
test('the six-job grid does not bypass strategy by automatically selecting Farm',()=>{
  const jobs=frame(1,['Housework','Babysitter','Inn','Farm','Church','Restaurant']);
  jobs.detail.ocr.lines.shift();
  assert.equal(schedulePlan(jobs),null);
  const unrelated=frame(1,['Housework','Farm']);unrelated.detail.ocr.lines.shift();
  assert.equal(schedulePlan(unrelated),null);
});
test('Farm confirmation adds the chosen activity, without confusing generic confirmations',()=>{
  const confirmation=frame(1,['Add to schedule','Cancel']);
  confirmation.detail.ocr.lines[0].text='Farm.';
  assert.equal(schedulePlan(confirmation).target,'Add to schedule');
  confirmation.detail.ocr.lines[0].text='Overwrite save?';
  assert.equal(schedulePlan(confirmation),null);
  confirmation.detail.ocr.lines[0].text='Adventure';
  assert.equal(schedulePlan(confirmation),null);
});
test('the observed rest menu picks free time rather than a paid vacation',()=>{
  const rest=frame(1,['Free time','Vacation']);rest.detail.ocr.lines.shift();
  assert.equal(schedulePlan(rest).target,'Free time');
  const confirmation=frame(1,['Add to schedule','Cancel']);confirmation.detail.ocr.lines[0].text='Free time.';
  assert.equal(schedulePlan(confirmation).target,'Add to schedule');
});
test('rest menu mechanics do not repeat Schedule from model history or require inference',async()=>{
  const model=createGameModel({fetcher:()=>{throw new Error('No model call needed.');}});
  const f=frame(1,['Free time','Vacation']);
  const p=await model.plan(f,[{target:'Schedule',reason:'Open schedule'}]);
  assert.equal(groundClick(p,f).target,'Free time');
});
test('current high stress or illness changes the routine to rest and cancels a pending Farm block',()=>{
  assert.equal(schedulePlan(frame(1),{vitals:{stress:75}}).target,'Time off');
  assert.equal(schedulePlan(frame(2),{vitals:{health:'ill'}}).target,'Time off');
  const confirmation=frame(1,['Add to schedule','Cancel']);confirmation.detail.ocr.lines[0].text='Farm.';
  assert.equal(schedulePlan(confirmation,{vitals:{health:'ill'}}).target,'Cancel');
  for(const job of ['Inn','Restaurant','Church','Babysitter','Housework']) {
    confirmation.detail.ocr.lines[0].text=job+'.';
    assert.equal(schedulePlan(confirmation).target,'Add to schedule');
    assert.equal(schedulePlan(confirmation,{vitals:{health:'ill'}}).target,'Cancel');
  }
});
