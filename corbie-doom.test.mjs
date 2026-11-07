import test from 'node:test';
import assert from 'node:assert/strict';
import {EventEmitter} from 'node:events';
import {createDoomLauncher} from './corbie-doom.mjs';
import {runShell} from './corbie-shell.mjs';

test('Doom launcher starts the fixed local player entry point without a shell or user game arguments',async()=>{
  const calls=[];let detached=false;
  const open=createDoomLauncher({platform:'win32',exists:async()=>{},launch:(file,args,options)=>{
    calls.push({file,args,options});const child=new EventEmitter();child.unref=()=>{detached=true;};queueMicrotask(()=>child.emit('spawn'));return child;
  }});
  assert.match(await open(),/player launcher opened/);assert.equal(calls.length,1);assert.match(calls[0].file,/doom[\\/]Corbie-Doom\.exe$/);
  assert.deepEqual(calls[0].args,['--player']);assert.equal(calls[0].options.shell,false);assert.equal(detached,true);
});

test('first launch builds locally and a failed build never opens a game',async()=>{
  const calls=[];
  const open=createDoomLauncher({platform:'win32',exists:async()=>{throw Object.assign(new Error('missing'),{code:'ENOENT'});},launch:(file,args,options)=>{
    calls.push({file,args,options});const child=new EventEmitter();child.unref=()=>{};
    queueMicrotask(()=>file==='powershell.exe'?child.emit('exit',0):child.emit('spawn'));return child;
  }});
  await open();assert.equal(calls.length,2);assert.equal(calls[0].file,'powershell.exe');assert.equal(calls[0].options.windowsHide,true);
  assert(calls[0].args.includes('RemoteSigned'));assert.match(calls[0].args.at(-1),/Build-Doom.ps1$/);
  let launched=0;
  const bad=createDoomLauncher({platform:'win32',exists:async()=>{throw Object.assign(new Error('missing'),{code:'ENOENT'});},launch:()=>{
    launched++;const child=new EventEmitter();queueMicrotask(()=>child.emit('exit',1));return child;
  }});
  await assert.rejects(bad(),/could not build/);assert.equal(launched,1);
});

test('unsupported platforms and denied access do not silently build or launch',async()=>{
  const never=()=>{throw new Error('Must not spawn');};
  await assert.rejects(createDoomLauncher({platform:'linux',launch:never})(),/Windows/);
  await assert.rejects(createDoomLauncher({platform:'win32',launch:never,exists:async()=>{throw Object.assign(new Error('Denied'),{code:'EACCES'});}})(),/Denied/);
});

test('CLI Doom opens the arcade without a model, town call, or mail flow',async()=>{
  const lines=['/doom','/exit'],output=[];let launched=0;
  await runShell({reader:{question:async()=>lines.shift(),close(){}},write:text=>output.push(text),
    chat:{ask:async()=>{throw new Error('No model needed');}},openDoom:async()=>{launched++;return 'Arcade launched.';},
    sendLetter:async()=>{throw new Error('No mail');}});
  assert.equal(launched,1);assert(output.some(line=>line.includes('Arcade launched.')));
});
