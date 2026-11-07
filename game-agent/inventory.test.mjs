import test from 'node:test';
import assert from 'node:assert/strict';
import {readWardrobe,updateWardrobe,clothingContext,wardrobePlan} from './inventory.mjs';
import {createGameModel} from './vision.mjs';
const frame=(labels,month='Jun 6',controls=[])=>({id:'f1',png:'AA==',width:1200,height:900,detail:{controls,ocr:{available:true,lines:[
  {text:month,words:[{x:20,y:40,w:100,h:20}]},...labels.map((text,i)=>({text,words:[{x:600,y:240+i*40,w:180,h:20}]}))]}}});
test('ownership requires inventory evidence, not shop stock or dialogue',()=>{
  assert.deepEqual(readWardrobe(frame(['Tailor','Summer Dress','70G'])).owned,[]);
  assert.deepEqual(readWardrobe(frame(['Do you want a Winter Dress?'])).owned,[]);
  assert.deepEqual(readWardrobe(frame(['Inventory','Summer Dress','Winter Dress'])).owned,['summer dress','winter dress']);
  assert.equal(readWardrobe(frame(['Inventory','Summer Dress'])).equipped,null);
  assert.equal(readWardrobe(frame(['Equipped: Winter Dress'])).equipped,'winter dress');
  assert.deepEqual(readWardrobe(frame(['Owned: no Summer Dress','Inventory','Winter Dress x0'])).owned,[]);
});
test('calendar uses spatial game evidence, not clothing names or ambiguous dates',()=>{
  assert.equal(readWardrobe(frame(['Winter Dress'],'Oct 10')).month,10);
  assert.equal(readWardrobe(frame(['June is hot'],'unreadable')).month,null);
  assert.equal(clothingContext(frame([],'Dec 12')).desired,'winter dress');
  assert.equal(clothingContext(frame([],'Aug 8')).desired,'summer dress');
  assert.equal(clothingContext(frame([],'Apr 4')).desired,null);
});
test('seasonal home check opens only a current unique inventory control',()=>{
  const home=frame(['Inventory'],'Jun 6',[{id:'schedule',label:'Schedule',x:850,y:550}]);
  assert.equal(wardrobePlan(home).action,'click');
  assert.equal(wardrobePlan(frame([],'Jun 6',home.detail.controls)).action,'pause');
  assert.equal(wardrobePlan(frame(['Inventory'],'Oct 10',home.detail.controls)),null);
});
test('inspection is remembered within the month but invalidated by shops or month changes',()=>{
  let state=updateWardrobe({},frame(['Inventory','Summer Dress']));
  assert.equal(clothingContext(frame([]),state).needsCheck,false);
  state=updateWardrobe(state,frame([],'Jul 7'));
  assert.equal(clothingContext(frame([],'Jul 7'),state).needsCheck,true);
  assert(state.seen['summer dress']);
  state=updateWardrobe(state,frame(['Inventory','Summer Dress'],'Jul 7'));
  state=updateWardrobe(state,frame(['Tailor'],'Jul 7'));
  assert.equal(clothingContext(frame([],'Jul 7'),state).needsCheck,true);
});
test('seasonal home guard precedes the model and does not invent missing controls',async()=>{
  const model=createGameModel({fetcher:()=>{throw new Error('should not infer');}});
  const p=await model.plan(frame([],'Jan 1',[{id:'schedule',label:'Schedule',x:850,y:550}]));
  assert.equal(p.action,'pause');assert.match(p.reason,/inventory/);
});
