import test from 'node:test';
import assert from 'node:assert/strict';
import {runGameConsole,createSessionStore} from './play.mjs';
import {mkdtemp,readFile,unlink,rmdir} from 'node:fs/promises';
import {join} from 'node:path';
import {fileURLToPath} from 'node:url';

test('game console can inspect, show status and return to Corbie without injecting game input',async()=>{
  const commands=['watch','status','back'],calls=[],output=[];
  const p={screen:'home',summary:'A safe menu.',reason:'Inspecting.',confidence:1,action:'wait',date:null};
  await runGameConsole({reader:{question:async()=>commands.shift()},write:x=>output.push(x),
    adapter:{request:async command=>{calls.push(command);return command==='observe'?{png:'AA==',width:640,height:480}:{};},close:()=>calls.push('close')},
    model:{verify:async()=>{},plan:async()=>p},
    store:{load:async()=>({history:[]}),save:async()=>{},frame:async()=>{}}});
  assert(calls.includes('attach'));assert(calls.includes('focus'));assert(!calls.includes('act'));assert(!calls.includes('arm'));
  assert.equal(calls.at(-1),'close');
  assert(output.some(x=>x.includes('A safe menu.')));
});
test('esc console command presses Escape once, verifies the result and disarms',async()=>{
  const commands=['esc','back'],calls=[];
  await runGameConsole({reader:{question:async()=>commands.shift()},write:()=>{},
    adapter:{request:async(command,args)=>{calls.push({command,args});return command==='observe'?{id:'current',png:'AA==',width:1200,height:900,armed:true}:command==='status'?{armed:true}:{};},close:()=>{}},
    model:{verify:async()=>{throw new Error('Manual Escape must not require a model.');}},
    store:{load:async()=>({history:[]}),save:async()=>{},frame:async()=>{}}});
  const inputs=calls.filter(c=>c.command==='act');
  assert.equal(inputs.length,1);assert.equal(inputs[0].args.key,'Escape');
  assert.equal(inputs[0].args.observation,'current');
  assert.equal(calls.filter(c=>c.command==='observe').length,2);
  assert.equal(calls.at(-1).command,'pause');
});
test('atomic progress journal keeps only game observations and fixed picture names',async()=>{
  const dir=await mkdtemp(join(fileURLToPath(new URL('.',import.meta.url)),'session-check-'));
  const store=createSessionStore(dir);
  try {
    await store.save({steps:1,status:'paused',history:[{id:'move-1',date:'June',target:'Schedule',result:'changed'}]});
    assert.equal((await store.load()).status,'paused');
    assert.match(await readFile(join(dir,'actions.jsonl'),'utf8'),/Schedule/);
    const before=await readFile(join(dir,'actions.jsonl'),'utf8');
    const loaded=await store.load();await store.save({...loaded,phase:'preview'});
    assert.equal(await readFile(join(dir,'actions.jsonl'),'utf8'),before);
    assert.match(await readFile(join(dir,'session.md'),'utf8'),/Corbie’s game notebook/);
    await store.frame('YWJj','last-frame.png');
    assert.equal((await readFile(join(dir,'last-frame.png'))).toString(),'abc');
    await assert.rejects(store.frame('YWJj','../outside.png'));
  } finally {
    for(const name of ['progress.json','progress.tmp','actions.jsonl','last-frame.png','session.md'])await unlink(join(dir,name)).catch(e=>{if(e.code!=='ENOENT')throw e;});
    await rmdir(dir);
  }
});
