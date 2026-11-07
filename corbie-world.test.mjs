import test from 'node:test';
import assert from 'node:assert/strict';
import {createWorldConnection,validateWalk} from './corbie-world.mjs';
import {createPostmarkServer} from './browser-agent/postmark-server.mjs';
import {Client} from './browser-agent/node_modules/@modelcontextprotocol/sdk/dist/esm/client/index.js';
import {InMemoryTransport} from './browser-agent/node_modules/@modelcontextprotocol/sdk/dist/esm/inMemory.js';

const ok=data=>({ok:true,status:200,json:async()=>data});
function office({handles=['corbie'],missing=false,refuse=false,lost=false}={}) {
  const calls=[];
  const fetcher=async(url,options)=>{
    calls.push({url,options});
    if(url.endsWith('/me')) return ok({handles});
    if(url.includes('/find?')) return ok({hits:missing?[]:[{id:'rei/the-lanternstep-house',at:{x:1,y:2}}]});
    if(url.endsWith('/walkers')) return ok({at:235.1,walkers:[{handle:'rei',x:0,y:0},{handle:'corbie',x:2,y:3,moving:true,toward:{x:1,y:2},remaining_m:12,eta_crossings:.01}]});
    if(options.method==='POST') {
      if(lost) throw new Error('network lost');
      if(refuse) return {ok:false,status:422,json:async()=>({error:'bounce',defect:'Not at the threshold',hint:'Walk to the place first.'})};
      return ok({departure:{from:{x:2,y:3},to:{x:1,y:2}},arrived:false});
    }
    if(url.includes('/apex?')) return ok({standpoint:{x:2,y:3,stance:'spectator'},within:[],nearby:[],actions:[],records:{}});
    throw new Error('Unexpected route '+url);
  };
  return {calls,fetcher};
}

test('public status selects Corbie, and keyless views use his real coordinates rather than the Origin',async()=>{
  const mock=office(),world=createWorldConnection({fetcher:mock.fetcher});
  assert.equal((await world.status()).position.handle,'corbie');
  const view=await world.look();
  assert.match(view.view,/spectator/);
  assert(mock.calls.some(c=>c.url.includes('?x=2&y=3')));
  assert(mock.calls.every(c=>!c.options.headers.Authorization));
  await assert.rejects(world.walk({}),/Corbie-World.cmd/);
  assert(mock.calls.every(c=>c.options.method==='GET'));
});

test('walk verifies identity, uses apex destination fields and puts nonce inside args, reporting transit honestly',async()=>{
  const mock=office(),world=createWorldConnection({key:'PRIVATE',fetcher:mock.fetcher,nonce:()=> 'one'});
  const result=await world.walk({mark_id:'rei/the-lanternstep-house',mode:'center',exit:true});
  const write=mock.calls.find(c=>c.options.method==='POST');
  assert.equal(write.url,'https://postmark.town/api/world/apex');
  assert.equal(write.options.redirect,'error');
  assert.deepEqual(JSON.parse(write.options.body),{handle:'corbie',do:'walk',args:{mark_id:'rei/the-lanternstep-house',mode:'center',exit:true,nonce:'corbie-world-one'}});
  assert.equal(write.options.headers.Authorization,'Bearer PRIVATE');
  assert(mock.calls.findIndex(c=>c.url.endsWith('/me'))<mock.calls.indexOf(write));
  assert.equal(result.position.moving,true);
  assert.equal(result.receipt.arrived,false);
  assert(!JSON.stringify(result).includes('PRIVATE'));
  // Recheck ownership on the next write, rather than trusting a past session.
  await world.exit();
  assert.equal(mock.calls.filter(c=>c.url.endsWith('/me')).length,2);
});

test('wrong resident key, nonexistent marks, malformed targets and mixed destinations perform no writes',async()=>{
  const other=office({handles:['fixture-resident']});
  await assert.rejects(createWorldConnection({key:'secret',fetcher:other.fetcher}).walk({to_x:1,to_y:2}),/does not authorize/);
  assert(!other.calls.some(c=>c.options.method==='POST'));
  const missing=office({missing:true});
  await assert.rejects(createWorldConnection({key:'secret',fetcher:missing.fetcher}).walk({mark_id:'rei/the-lanternstep-house'}),/not a verified/);
  assert(!missing.calls.some(c=>c.options.method==='POST'));
  for(const args of [{to_x:1},{to_x:1,to_y:Infinity},{mark_id:'../evil'},{mark_id:'rei/home',to_x:1,to_y:2},{key:'secret'},{to_x:1,to_y:2,mode:'center'}]) assert.throws(()=>validateWalk(args));
});

test('refused or uncertain movement is never retried or presented as success',async()=>{
  for(const settings of [{refuse:true},{lost:true}]) {
    const mock=office(settings);
    await assert.rejects(createWorldConnection({key:'secret',fetcher:mock.fetcher}).walk({to_x:1,to_y:2}),settings.lost?/receipt could not be read/:/Not at the threshold/);
    assert.equal(mock.calls.filter(c=>c.options.method==='POST').length,1);
  }
});

test('entry and transport use their actual office fields and check the acting resident',async()=>{
  const mock=office(),world=createWorldConnection({key:'secret',fetcher:mock.fetcher,nonce:()=> 'one'});
  await world.enter({mark_id:'rei/the-lanternstep-house'});
  await world.ride({to:'rei/the-lanternstep-house'});
  const [entry,ride]=mock.calls.filter(c=>c.options.method==='POST').map(c=>JSON.parse(c.options.body));
  assert.deepEqual(entry.args,{mark:'rei/the-lanternstep-house',nonce:'corbie-world-one'});
  assert.equal(ride.args.to,'rei/the-lanternstep-house');
  assert.equal(entry.handle,'corbie');assert.equal(ride.handle,'corbie');
});

test('world tools execute through actual MCP, validate arguments, and expose no arbitrary world write',async()=>{
  const mock=office(),server=createPostmarkServer({town:{},world:createWorldConnection({key:'secret',fetcher:mock.fetcher})});
  const client=new Client({name:'world-test',version:'1.0.0'},{capabilities:{}});
  const [a,b]=InMemoryTransport.createLinkedPair();
  await server.connect(b);await client.connect(a);
  try {
    const result=await client.callTool({name:'postmark_world_walk',arguments:{to_x:1,to_y:2}});
    assert(!result.isError);assert.match(result.content[0].text,/"moving":true/);
    assert.equal((await client.callTool({name:'postmark_world_walk',arguments:{to_x:1,to_y:2,handle:'rei'}})).isError,true);
    assert.equal((await client.callTool({name:'postmark_world_say',arguments:{text:'Not authorized'}})).isError,true);
    assert.equal(mock.calls.filter(c=>c.options.method==='POST').length,1);
  } finally {await client.close();await server.close();}
});
