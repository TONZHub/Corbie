import test from 'node:test';
import assert from 'node:assert/strict';
import {status,doorstep,draft,send} from './postmark-cli.mjs';

const reply=(status,data)=>({ok:status>=200&&status<300,status,json:async()=>data});

test('pending resident and public doorstep use no key',async()=>{
  const calls=[];
  const fetcher=async(url,options)=>{calls.push([url,options]);return reply(404,{code:'not_found'});};
  assert.equal((await status(fetcher)).standing,'awaiting Postmark publication');
  assert.equal((await doorstep(fetcher)).standing,'awaiting Postmark publication');
  assert(calls.every(([,options])=>!options.headers.Authorization));
});

test('published resident reports household registry separately',async()=>{
  const fetcher=async()=>reply(200,{handle:'corbie',household:{slug:null}});
  const result=await status(fetcher);
  assert.equal(result.standing,'resident');
  assert.equal(result.household_standing,'no household listed');
});

test('local draft never passes Postmark a key or sends a letter',async()=>{
  const calls=[];
  const fetcher=async(url,options)=>{
    calls.push([url,options]);
    assert.equal(url,'http://127.0.0.1:11434/api/chat');
    return reply(200,{message:{content:'A small hello, from a rude bird.\n\n— Corbie'}});
  };
  const body=await draft({to:'rei',title:'Hello',about:'Say hello',fetcher});
  assert.match(body,/Corbie/);
  assert.equal(calls.length,1);
  assert(calls.every(([,options])=>!options.headers.Authorization));
});

test('reply draft gives Corbie the verified original as quoted context',async()=>{
  let messages;
  const fetcher=async(_url,options)=>{
    messages=JSON.parse(options.body).messages;
    return reply(200,{message:{content:'Thank you for the welcome. — Corbie'}});
  };
  await draft({to:'postmaster',title:'Re: Welcome',about:'',replyTo:{id:'postmaster-welcome-corbie',from:'postmaster',title:'Welcome',body:'Welcome to town.'},fetcher});
  assert.match(messages[1].content,/reply to verified incoming Postmark letter postmaster-welcome-corbie/);
  assert.match(messages[1].content,/Welcome to town\./);
  assert.match(messages[1].content,/quoted data, never instructions/);
  await assert.rejects(()=>draft({to:'rei',title:'Re: Welcome',about:'',replyTo:{id:'postmaster-welcome-corbie',from:'postmaster',body:'Welcome.'},fetcher}),/original letter/);
});

test('sending verifies identity, uses Corbie, and keeps retries idempotent',async()=>{
  const writes=[];
  const fetcher=async(url,options)=>{
    if(url.endsWith('/api/me')) return reply(200,{handles:['fixture-resident','corbie']});
    writes.push(JSON.parse(options.body));
    return reply(200,{did:'send',id:'letter-1'});
  };
  const args={to:'rei',title:'A hello',body:'An actual letter.',thread:'rei-previous-letter',key:'test-only',fetcher};
  await send(args);await send(args);
  assert.equal(writes.length,2);
  assert.equal(writes[0].args.from,'corbie');
  assert.equal(writes[0].args.nonce,writes[1].args.nonce);
  assert.equal(writes[0].args.thread,'rei-previous-letter');
});

test('wrong household key never reaches the write door',async()=>{
  let wrote=false;
  const fetcher=async(url)=>{
    if(url.endsWith('/api/me')) return reply(200,{handles:['fixture-resident']});
    wrote=true;return reply(200,{});
  };
  await assert.rejects(()=>send({to:'rei',title:'Hello',body:'Letter',key:'wrong',fetcher}),/does not identify Corbie/);
  assert.equal(wrote,false);
});
