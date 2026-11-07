import test from 'node:test';
import assert from 'node:assert/strict';
import {dialoguePlan} from './dialogue.mjs';
const frame=(text,{marker=true,labels=[]}={})=>({detail:{controls:marker?[{id:'dialog_next',label:'Next dialogue',x:402,y:722}]:[],ocr:{available:true,lines:[text,...labels].map((text,i)=>({text,words:[{x:250,y:500+i*40,w:300,h:20}]}))}}});
test('portrait salary dialogue advances at its currently matched elevated marker',()=>{
 const p=dialoguePlan(frame("Today's your last day, isn't it? Here is your salary for the days you worked hard. 20G for 2 days"));
 assert.equal(p.action,'key');assert.equal(p.key,'Enter');assert.equal(p.x,null);assert.equal(p.y,null);
});
test('ordinary dialogue requires a marker; questions and choices remain model decisions',()=>{
 assert.equal(dialoguePlan(frame('Here is your salary. 20G for 2 days',{marker:false})),null);
 assert.equal(dialoguePlan(frame('Would you like to change clothing?')),null);
 assert.equal(dialoguePlan(frame('Here is your salary. 20G for 2 days',{labels:['Yes','No']})),null);
 assert.equal(dialoguePlan(frame('Her final career is now revealed.')),null);
 assert.equal(dialoguePlan(frame('I am happy to see you today.')).key,'Enter');
 assert.equal(dialoguePlan(frame('"I’m off,"')).key,'Enter');
});
