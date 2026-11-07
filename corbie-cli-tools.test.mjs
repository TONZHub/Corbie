import test from 'node:test';
import assert from 'node:assert/strict';
import {createChatSession,runShell} from './corbie-shell.mjs';
import {wantsCliTools} from './corbie-cli-tools.mjs';

test('plain browser task routes to tools without a failed town connection blocking it',async()=>{
  const tasks=[];
  const chat=createChatSession({town:{snapshot:async()=>{throw new Error('Town must not be queried first');}},
    runTools:async(task,settings)=>{tasks.push({task,settings});return 'I read the requested page.';}});
  chat.setOffline(true);
  assert.match(await chat.ask('Could you browse the latest project page?'),/read the requested page/);
  assert.equal(tasks[0].settings.postmarkEnabled,false);
  assert(wantsCliTools('Open https://example.com'));
  assert.equal(wantsCliTools('Do you like shiny things?'),false);
  assert(wantsCliTools('Could you walk to the Post Office?'));
  assert(wantsCliTools('Where are you now?'));
});

test('world commands keep destination and identity out of ordinary chat and report travelling rather than arrival',async()=>{
  const commands=['/where','/places lantern','/walk rei/the-lanternstep-house','/walk 10 -20','/home','/enter rei/the-lanternstep-house','/leave','/ride the-town/the-post-office','/exit'];
  const calls=[],out=[];
  const tools={close:async()=>{},call:async(name,args,settings)=>{
    calls.push({name,args,settings});
    if(name==='postmark_world_status')return {position:{x:1,y:2,moving:true,toward:{x:3,y:4},remaining_m:20,eta_crossings:.02}};
    if(name==='postmark_world_find')return {hits:[{id:'rei/the-lanternstep-house',name:'Lanternstep'}]};
    return {receipt:{recorded:true},position:{moving:true}};
  }};
  await runShell({reader:{question:async()=>commands.shift(),close(){}},write:x=>out.push(x),chat:{isOffline:()=>false},tools});
  assert(out.some(x=>x.includes('Travelling toward')));
  assert.deepEqual(calls[2].args,{mark_id:'rei/the-lanternstep-house',mode:'center'});
  assert.deepEqual(calls[3].args,{to_x:10,to_y:-20});
  assert.deepEqual(calls[4].args,{});
  assert.equal(calls[5].name,'postmark_world_enter');
  assert.equal(calls[6].name,'postmark_world_exit');
  assert.equal(calls[7].args.to,'the-town/the-post-office');
});

test('shell lists tools, continues a task, disconnects and closes tools on exit',async()=>{
  const lines=['/tools','/browse Read a page.','/continue I signed in.','/mcp off','/exit'];
  const runs=[],output=[];let closes=0;
  const tools={list:async()=>[{name:'browser_snapshot',description:'Read a page'}],run:async(...args)=>{runs.push(args);return 'Done reading.';},close:async()=>{closes++;}};
  await runShell({reader:{question:async()=>lines.shift(),close(){}},write:line=>output.push(line),chat:{isOffline:()=>false},tools});
  assert(output.some(line=>line.includes('browser_snapshot')));
  assert.deepEqual(runs,[['Read a page.',{postmarkEnabled:true}],['I signed in.',{postmarkEnabled:true,resume:true}]]);
  assert.equal(closes,2);
});
