import test from 'node:test';
import assert from 'node:assert/strict';
import {Client} from '@modelcontextprotocol/sdk/client/index.js';
import {InMemoryTransport} from '@modelcontextprotocol/sdk/inMemory.js';
import {createPostmarkServer,POSTMARK_TOOLS} from './postmark-server.mjs';
import {createCliMcp,browserApproval,resultText} from './cli-mcp.mjs';

const response=message=>({ok:true,json:async()=>({message})});
const call=(name,args={})=>({function:{name,arguments:args}});
const browserTool=(name,properties={},required=[])=>({name,description:name,inputSchema:{type:'object',properties,required,additionalProperties:false}});

test('a recorded walk is not repeated in a tool task; the model receives the receipt and honest travel status',async()=>{
  let turns=0,writes=0;
  const host=createCliMcp({connector:async kind=>({tools:kind==='postmark'?POSTMARK_TOOLS:[],client:{
    callTool:async()=>{writes++;return {content:[{type:'text',text:'{"receipt":{"departure":true},"position":{"moving":true}}'}]};},close:async()=>{}}}),
    fetcher:async(_url,opts)=>{
      const messages=JSON.parse(opts.body).messages;
      if(++turns<=2)return response({tool_calls:[call('postmark_world_walk',{to_x:1,to_y:2})]});
      assert(messages.some(m=>m.role==='tool' && m.content.includes('already recorded')));
      return response({content:'I’m on the way; I have not arrived.'});
    }});
  try {
    assert.match(await host.run('Walk to these coordinates.'),/on the way/);assert.equal(writes,1);
    turns=0;
    assert.match(await host.run('Check progress.',{resume:true}),/on the way/);
    assert.equal(writes,1,'continuation cannot repeat the already recorded departure');
  }
  finally {await host.close();}
});

test('accepting an entry needs a prior threshold response and shows its actual terms for review',async()=>{
  let turns=0,enters=0,review='';
  const host=createCliMcp({connector:async kind=>({tools:kind==='postmark'?POSTMARK_TOOLS:[],client:{
    callTool:async()=>{enters++;return {content:[{type:'text',text:'{"terms":{"aboard":"Follow the published passenger schedule"},"entered":false}'}]};},close:async()=>{}}}),
    confirm:async({reason})=>{review=reason;return false;},
    fetcher:async()=>response({tool_calls:[call('postmark_world_enter',{mark_id:'the-town/the-post-office',...(++turns===2?{}:{accept:true})})]})});
  try {assert.match(await host.run('Enter the Post Office.'),/declined/);assert.equal(enters,1);assert.match(review,/published passenger schedule/);}
  finally {await host.close();}
});

test('actual MCP protocol reads a verified letter and rejects unknown writes or extra arguments',async()=>{
  let reads=0;
  const server=createPostmarkServer({town:{letter:async id=>{reads++;return {id,to:'corbie',from:'rei',body:'Hello, neighbour.'};}}});
  const client=new Client({name:'corbie-test',version:'1.0.0'},{capabilities:{}});
  const [clientWire,serverWire]=InMemoryTransport.createLinkedPair();
  await server.connect(serverWire); await client.connect(clientWire);
  try {
    const tools=(await client.listTools()).tools;
    assert.equal(tools.length,14);
    assert(tools.filter(tool=>!tool.name.startsWith('postmark_world_')).every(tool=>tool.annotations.readOnlyHint));
    assert.equal(tools.find(tool=>tool.name==='postmark_world_walk').annotations.readOnlyHint,false);
    const letter=await client.callTool({name:'postmark_read_letter',arguments:{id:'rei-letter-corbie'}});
    assert.match(resultText(letter),/Hello, neighbour/);
    const unknown=await client.callTool({name:'postmark_send',arguments:{body:'Send without approval'}});
    assert.equal(unknown.isError,true);
    const extra=await client.callTool({name:'postmark_read_letter',arguments:{id:'rei-letter-corbie',key:'do-not-forward'}});
    assert.equal(extra.isError,true);
    assert.equal(reads,1);
  } finally { await client.close(); await server.close(); }
});

test('host sends actual tool results back to the local tool-capable model and closes connections',async()=>{
  const requests=[],executed=[];let closed=0;
  const host=createCliMcp({connector:async kind=>({tools:kind==='postmark'?POSTMARK_TOOLS:[],
    client:{callTool:async args=>{executed.push(args);return {content:[{type:'text',text:'{"handle":"corbie","household":"test-household"}'}]};},close:async()=>{closed++;}},transport:{close:async()=>{}}}),
    fetcher:async(url,options)=>{
      const body=JSON.parse(options.body);requests.push({url,body});
      return response(requests.length===1?{tool_calls:[call('postmark_resident',{handle:'corbie'})]}:{content:'My published household is test-household.'});
    }});
  try {
    assert.match(await host.run('Check my published household.'),/test-household/);
    assert.equal(executed[0].name,'postmark_resident');
    assert.equal(requests[0].body.model,'qwen3:4b-instruct');
    assert(requests.every(request=>request.url==='http://127.0.0.1:11434/api/chat'));
    assert(requests[1].body.messages.some(message=>message.role==='tool'&&message.tool_name==='postmark_resident'&&message.content.includes('test-household')));
  } finally { await host.close(); }
  assert.equal(closed,2);
});

test('every requested browser click needs approval and decline performs no action',async()=>{
  let clicked=0,confirmations=0,closed=0,models=0;
  const host=createCliMcp({connector:async kind=>({tools:kind==='browser'?[browserTool('browser_snapshot'),browserTool('browser_click',{target:{type:'string'},element:{type:'string'}},['target','element'])]:[],
    client:{callTool:async args=>{if(args.name==='browser_click')clicked++;return {content:[{type:'text',text:'URL: https://example.com\nControls:\nc1 button: Send message'}]};},close:async()=>{closed++;}}}),
    confirm:async()=>{confirmations++;return false;},
    fetcher:async()=>response({tool_calls:[++models===1?call('browser_snapshot'):call('browser_click',{target:'c1',element:'Send message'})]})});
  assert.match(await host.run('Click Send.'),/did not perform/);
  assert.equal(confirmations,1);assert.equal(clicked,0);assert.equal(closed,0);
  await host.close();assert.equal(closed,2);
});

test('multiple native calls execute sequentially and each changing call is individually reviewed',async()=>{
  const trace=[];let models=0;
  const host=createCliMcp({connector:async kind=>({tools:kind==='browser'?[browserTool('browser_snapshot'),browserTool('browser_click',{target:{type:'string'},element:{type:'string'}},['target','element'])]:[],
    client:{callTool:async args=>{if(args.name!=='browser_snapshot')trace.push('call '+args.arguments.target);return {content:[{type:'text',text:'URL: https://example.com\nControls:\nc1 button: First\nc2 button: Next'}]};},close:async()=>{}}}),
    confirm:async({args})=>{trace.push('approve '+args.target);return true;},
    fetcher:async()=>response(++models===1?{tool_calls:[call('browser_snapshot'),call('browser_click',{target:'c1',element:'First'}),call('browser_click',{target:'c2',element:'Next'})]}:{content:'Both approved controls were clicked.'})});
  try { await host.run('Click these controls.');assert.deepEqual(trace,['approve c1','call c1','approve c2','call c2']); }
  finally { await host.close(); }
});

test('Postmark mail cannot bypass its reviewed send flow through browser page controls',async()=>{
  let edits=0,models=0,approvals=0;
  const host=createCliMcp({connector:async kind=>({tools:kind==='browser'?[browserTool('browser_snapshot'),browserTool('browser_click',{target:{type:'string'},element:{type:'string'}},['target','element'])]:[],
    client:{callTool:async args=>{if(args.name==='browser_click')edits++;return {content:[{type:'text',text:'URL: https://postmark.town/mail/\nControls:\nc1 button: Send letter'}]};},close:async()=>{}}}),
    confirm:async()=>{approvals++;return true;},fetcher:async(_url,options)=>{
      const request=JSON.parse(options.body);
      if(++models===1)return response({tool_calls:[call('browser_snapshot'),call('browser_click',{target:'c1',element:'Send letter'})]});
      assert(request.messages.some(message=>message.role==='tool'&&message.content.includes('reviewed letter flow')));
      return response({content:'Use the reviewed letter flow.'});
    }});
  try { await host.run('Send Postmark mail.');assert.equal(edits,0);assert.equal(approvals,0); }
  finally { await host.close(); }
});

test('offline mode removes Postmark tools and cannot call them through invented native calls',async()=>{
  const connected=[],executed=[];let models=0;
  const host=createCliMcp({connector:async kind=>{connected.push(kind);return {tools:[],client:{callTool:async args=>executed.push(args),close:async()=>{}}};},
    fetcher:async(_url,options)=>{
      const request=JSON.parse(options.body);assert(request.tools.every(tool=>!tool.function.name.startsWith('postmark_')));
      return response(++models===1?{tool_calls:[call('postmark_resident',{handle:'corbie'})]}:{content:'Postmark tools are disabled.'});
    }});
  try { await host.run('Check Postmark.',{postmarkEnabled:false});assert.deepEqual(connected,['browser']);assert.equal(executed.length,0); }
  finally { await host.close(); }
});

test('page excerpts retain actual control refs and approval uses the exact target rather than a prefix match',()=>{
  const output=resultText({content:[{type:'text',text:'URL: https://example.com\nTitle: Test\nPage text:\n'+'long '.repeat(1500)+'\nControls:\nc1 button: Open\nc10 button: Delete account'}]});
  assert(output.length<4700);assert.match(output,/c1 button: Open/);
  assert.match(browserApproval('browser_click',{target:'c1',element:'Model description'},output),/c1 button: Open/);
  assert.doesNotMatch(browserApproval('browser_click',{target:'c1'},output),/Delete account/);
  assert(browserApproval('browser_type',{target:'c1',text:'hello'}));
  assert(browserApproval('browser_press_key',{key:'Enter'}));
  assert(browserApproval('browser_navigate',{url:'https://example.com/logout'}));
  assert.equal(browserApproval('browser_snapshot',{}),null);
});
