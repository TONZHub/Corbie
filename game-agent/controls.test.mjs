import test from 'node:test';
import assert from 'node:assert/strict';
import {groundClick,visibleControls,clickableControls} from './controls.mjs';

test('Schedule resolves to the currently matched button instead of a crop boundary',()=>{
  const frame={detail:{controls:[{id:'schedule',label:'Schedule',x:875,y:534}]}};
  assert.deepEqual(groundClick({action:'click',target:'SCHEDULE',x:750,y:370,key:'Schedule'},frame),{action:'click',target:'Schedule',x:875,y:534,key:null});
  assert.throws(()=>groundClick({action:'click',target:'Schedule',x:750,y:370},{detail:{controls:[]}}),/Could not locate/);
});
test('calendar, names, measurements and dialogue words remain readable but cannot receive clicks',()=>{
  const labels=[['11',75,80],['Corbia',850,40],['146 58 75/',850,329],['month',50,820],['Cancel',790,558]];
  const frame={detail:{ocr:{available:true,lines:labels.map(([text,x,y])=>({text,words:[{x,y,w:40,h:20}]}))}}};
  assert.equal(visibleControls(frame).length,5);
  assert.deepEqual(clickableControls(frame).map(c=>c.label),['Cancel']);
  for(const target of ['11','Corbia','146 58 75/','month','text_0'])assert.throws(()=>groundClick({action:'click',target},frame),/Could not locate/);
  assert.equal(groundClick({action:'click',target:'Cancel'},frame).target,'Cancel');
});
test('Run schedule is actionable and its adjacent Cancel cannot undo the finished month',()=>{
  const frame={detail:{ocr:{available:true,lines:['Run schedule','Cancel'].map((text,i)=>({text,words:[{x:760,y:500+i*40,w:150,h:20}]}))}}};
  assert.equal(groundClick({action:'click',target:'Run schedule'},frame).target,'Run schedule');
  assert.throws(()=>groundClick({action:'click',target:'Cancel'},frame),/Could not locate/);
  frame.detail.ocr.lines.shift();
  assert.equal(groundClick({action:'click',target:'Cancel'},frame).target,'Cancel');
});
test('text targets use the current word bounds, and ambiguous duplicate labels pause',()=>{
  const frame={detail:{ocr:{available:true,lines:[{text:'Farm',words:[{text:'Farm',x:200,y:300,w:60,h:24}]}]}}};
  assert.equal(visibleControls(frame)[0].label,'Farm');
  assert.deepEqual(groundClick({action:'click',target:'Farm',x:0,y:0},frame),{action:'click',target:'Farm',x:230,y:312,key:null});
  assert.throws(()=>groundClick({action:'click',target:'Church'},frame),/Could not locate/);
  frame.detail.ocr.lines.push({text:'Farm',words:[{text:'Farm',x:400,y:300,w:60,h:24}]});
  assert.throws(()=>groundClick({action:'click',target:'Farm'},frame),/unambiguously/);
  assert.equal(groundClick({action:'wait'},frame).action,'wait');
});
