import test from 'node:test';
import assert from 'node:assert/strict';
import {sceneSignature,confirmedHistory,readVitals} from './perception.mjs';
import {statusText,historyText} from './presentation.mjs';

const frame=(text,png='one')=>({png,detail:{ocr:{available:true,lines:[{text,words:[{x:20,y:750,w:120,h:20}]}]}}});
test('sprite changes do not count as menu progress, but a new period does',()=>{
  assert.equal(sceneSignature(frame('Period 1','blink')),sceneSignature(frame('Period 1','no blink')));
  assert.notEqual(sceneSignature(frame('Period 1')),sceneSignature(frame('Period 2')));
});
test('old attempted clicks and failed moves never feed the next decision as successful history',()=>{
  assert.deepEqual(confirmedHistory([{target:'Schedule'},{target:'Work',result:'unchanged'}]),[]);
  assert.equal(confirmedHistory([{target:'Farm',result:'changed',afterScene:'Activity confirmation'}])[0].target,'Farm');
});
test('resource readings require explicit labels rather than age or body measurements',()=>{
  assert.deepEqual(readVitals(frame('146 38 73/53/75')),{gold:null,stress:null,health:null});
  assert.equal(readVitals(frame('Gold: 500G')).gold,500);
  assert.equal(readVitals(frame('Age 10')).gold,null);
  assert.equal(readVitals(frame('Stress: 75')).stress,75);
  assert.equal(readVitals(frame('She is sick.')).health,'ill');
  assert.equal(readVitals(frame('She might become sick.')).health,null);
});
test('the notebook distinguishes input attempts from visible changes',()=>{
  const state={status:'paused',totalInputs:2,verifiedChanges:1,lastReceipt:{target:'Farm',result:'uncertain'},history:[{id:'one',target:'Farm',result:'uncertain'}]};
  assert.match(statusText(state),/2 inputs recorded · 1 visible screen changes/);
  assert.match(historyText(state),/input outcome uncertain/);
});
