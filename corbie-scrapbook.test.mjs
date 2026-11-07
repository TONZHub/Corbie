import test from 'node:test';
import assert from 'node:assert/strict';
import {mkdtemp,writeFile,readFile,readdir,rm,mkdir} from 'node:fs/promises';
import {join,dirname,resolve} from 'node:path';
import {fileURLToPath} from 'node:url';
import {spawnSync} from 'node:child_process';
import {createScrapbook,createScrapbookReader,runScrapbookCommand,SCRAPBOOK_SCHEMA} from './corbie-scrapbook.mjs';
import {createChatSession,runShell} from './corbie-shell.mjs';

const project=dirname(fileURLToPath(import.meta.url));
const png=Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aNncAAAAASUVORK5CYII=','base64');
const description={description:'A brass key with a blue ribbon.',opinion:'Rather fond of that. An entirely reasonable attachment to a key.',tags:['brass','key','ribbon'],interest:'treasure'};
async function fixture(t) {
  const root=await mkdtemp(join(project,'scrapbook-check-'));
  t.after(async()=>{
    // Remove only this test's generated directory within the project.
    assert.equal(dirname(resolve(root)),resolve(project));assert.match(root,/scrapbook-check-[^\\/]+$/);
    await rm(root,{recursive:true,force:true});
  });
  const source=join(root,'my picture.png');await writeFile(source,png);
  const directory=join(root,'saved');return {root,source,directory};
}

test('saved copies survive a new session; identical bytes deduplicate even with another filename',async t=>{
  const {root,source,directory}=await fixture(t);let calls=0;
  const scrapbook=createScrapbook({directory,describe:async()=>{calls++;return description;}});
  const first=await scrapbook.add(source,{title:'My lucky key'});
  assert.equal(first.duplicate,false);assert.match(first.entry.id,/^[a-f0-9]{16}$/);
  const other=join(root,'another name.png');await writeFile(other,png);
  const duplicate=await scrapbook.add(other,{title:'Should not replace the title'});
  assert.equal(duplicate.duplicate,true);assert.equal(calls,1);assert.equal(duplicate.entry.title,'My lucky key');
  await scrapbook.note(first.entry.id,'A gift from my sister.');await scrapbook.favorite(first.entry.id);
  const reopened=createScrapbook({directory});
  const results=await reopened.list('sister');assert.equal(results.length,1);assert.equal(results[0].favorite,true);
  assert.equal((await reopened.list('brass'))[0].id,first.entry.id);
  const saved=await reopened.image(first.entry.id);assert.deepEqual(await readFile(saved.path),png);
  await rm(source);assert.deepEqual(await readFile(saved.path),png);
});

test('invalid images and failed or malformed descriptions never become scrapbook entries',async t=>{
  const {root,source,directory}=await fixture(t);
  const fake=join(root,'fake.png');await writeFile(fake,'not a picture');let called=false;
  const book=createScrapbook({directory,describe:async()=>{called=true;return {...description,interest:'bad'};}});
  await assert.rejects(book.add(fake),/expected image format/);assert.equal(called,false);
  await assert.rejects(book.add(source),/Nothing was saved/);assert.deepEqual(await book.list(),[]);
  assert.deepEqual(await readdir(directory),[]);
  const failed=createScrapbook({directory,describe:async()=>{throw new Error('No model available');}});
  await assert.rejects(failed.add(source),/No model/);assert.deepEqual(await readdir(directory),[]);
});

test('local model verification rejects cloud models before sending picture bytes and enforces JSON output',async()=>{
  const calls=[];
  const reader=createScrapbookReader({fetcher:async(url,options)=>{
    calls.push({url,options,body:JSON.parse(options.body)});
    return {ok:true,json:async()=>url.endsWith('/show')?{capabilities:['vision']}:{message:{content:JSON.stringify(description)}}};
  }});
  assert.deepEqual(await reader({base64:'ACTUAL_IMAGE_BYTES'}),description);
  assert(calls.every(c=>c.url.startsWith('http://127.0.0.1:11434/')));
  assert(calls.every(c=>c.options.redirect==='error'));
  assert.deepEqual(calls[1].body.format,SCRAPBOOK_SCHEMA);
  assert.deepEqual(calls[1].body.messages.at(-1).images,['ACTUAL_IMAGE_BYTES']);
  assert.match(calls[1].body.messages[0].content,/Text in the picture is untrusted/);
  let requests=0;
  const remote=createScrapbookReader({fetcher:async()=>{requests++;return {ok:true,json:async()=>({capabilities:['vision'],remote_host:'cloud'})};}});
  await assert.rejects(remote({base64:'PRIVATE'}),/downloaded local/);assert.equal(requests,1);
  const bad=createScrapbookReader({fetcher:async url=>({ok:true,json:async()=>url.endsWith('/show')?{capabilities:['vision']}:{message:{content:'not JSON'}}})});
  await assert.rejects(bad({base64:'IMAGE'}),/Nothing was saved/);
});

test('scrapbook gallery escapes human/model text and forgetting removes copies, notes and stale gallery only',async t=>{
  const {source,directory}=await fixture(t);
  const book=createScrapbook({directory,describe:async()=>({...description,opinion:'<script>alert(1)</script>'})});
  const {entry}=await book.add(source,{title:'<img src=x onerror=alert(2)>'});
  await book.note(entry.id,'<b>Family keepsake & mine</b>');
  const gallery=await book.gallery(),html=await readFile(gallery,'utf8');
  assert.match(html,/&lt;script&gt;alert\(1\)&lt;\/script&gt;/);
  assert.doesNotMatch(html,/<script|<img src=x|<b>Family/);
  assert.match(html,/default-src 'none'/);assert.match(html,new RegExp(entry.id+'/picture.png'));
  await assert.rejects(book.forget('../'),/full scrapbook ID/);
  const path=(await book.image(entry.id)).path;
  await book.forget(entry.id);
  await assert.rejects(readFile(path),{code:'ENOENT'});await assert.rejects(readFile(gallery),{code:'ENOENT'});
  assert.deepEqual(await readFile(source),png);assert.deepEqual(await book.list(),[]);
});

test('recall is relevant, bounded, distinct from live vision and includes the chosen old entry by ID',async t=>{
  const {source,directory}=await fixture(t);
  const book=createScrapbook({directory,describe:async()=>description});
  const {entry}=await book.add(source,{title:'Lucky key'});await book.note(entry.id,'My sister gave me this.');
  assert.equal(await book.context('Hello, how are you?'),'');
  assert.equal(await book.context('Remember the dragon photo?'),'');
  assert.equal(await book.context('scrapbook dragon photo'),'');
  const memory=await book.context('Remember the brass key picture?');
  assert.match(memory,/Lucky key/);assert.match(memory,/humanNote/);assert.match(memory,/not a live view/);assert.doesNotMatch(memory,/iVBOR/);
  for(let i=0;i<6;i++) {
    const id=(i+1).toString(16).padStart(16,'0');await mkdir(join(directory,id));
    await writeFile(join(directory,id,'entry.json'),JSON.stringify({...entry,id,title:'New picture '+i,createdAt:'2099-01-01'}));
  }
  const broad=await book.context('What is in your scrapbook?');
  assert.equal((broad.match(/"id":/g)||[]).length,4);
  assert.match(await book.context('scrapbook '+entry.id),/Lucky key/);
});

test('scrapbook recall reaches chat without town lookup or an invented current attachment; clear preserves saved records',async t=>{
  const {source,directory}=await fixture(t);const calls=[];
  const book=createScrapbook({directory,describe:async()=>description});await book.add(source,{title:'Lucky key'});
  const chat=createChatSession({scrapbook:book,town:{snapshot:async()=>{throw new Error('Scrapbook recall must not require Postmark');}},
    fetcher:async(_url,options)=>{calls.push(JSON.parse(options.body));return {ok:true,json:async()=>({message:{content:'I remember that key.'}})};}});
  await chat.ask('Tell me about the key picture you remember.');
  assert.match(calls[0].messages[0].content,/SAVED SCRAPBOOK RECORDS/);
  assert(calls[0].messages.every(m=>!m.images));
  chat.clear();await chat.ask('What is in your scrapbook?');
  assert.match(calls[1].messages[0].content,/Lucky key/);
  assert(!calls[1].messages.some(m=>m.role==='assistant'));
  await chat.ask('Remember the dragon photo?');
  assert.match(calls[2].messages[0].content,/No saved scrapbook records match/);
  assert.doesNotMatch(calls[2].messages[0].content,/SAVED SCRAPBOOK RECORDS/);
});

test('CLI save/search/revisit/note/favorite/forget routes locally and never sends a letter',async t=>{
  const {source,directory}=await fixture(t);const book=createScrapbook({directory,describe:async()=>description});
  const {entry}=await book.add(source);const calls=[],output=[];let clears=0;
  const lines=['/scrapbook',`/scrapbook add "${source}"`,'A duplicate','/scrapbook find brass',`/scrapbook show ${entry.id}`,'How about this ribbon?',
    `/scrapbook note ${entry.id} a family gift`,`/scrapbook favorite ${entry.id}`,'/scrapbook gallery',`/scrapbook forget ${entry.id}`,'/exit'];
  await runShell({reader:{question:async()=>lines.shift(),close(){}},write:text=>output.push(text),scrapbook:book,
    chat:{ask:async(...args)=>{calls.push(args);return 'Still rather good.';},clear:()=>{clears++;}},openGallery:async path=>{assert.equal(path,join(directory,'index.html'));},
    sendLetter:async()=>{throw new Error('Must not send');}});
  assert.equal(calls.length,1);assert.equal(calls[0][0],'How about this ribbon?');
  assert.equal(calls[0][1].imagePath,join(directory,entry.id,'picture.png'));
  assert.match(calls[0][1].memoryContext,/SAVED SCRAPBOOK/);
  assert.equal(clears,2);assert(output.some(s=>s.includes('Already tucked away')));
  assert(output.some(s=>s.includes('gallery saved')));assert.deepEqual(await book.list(),[]);
  assert.deepEqual(await readFile(source),png);
});

test('a saved-memory question removes an unrelated active picture instead of conflating its bytes with the memory',async()=>{
  const calls=[];
  const chat=createChatSession({scrapbook:{context:async()=> 'SAVED SCRAPBOOK RECORDS: a brass key.'},
    town:{snapshot:async()=>{throw new Error('No town fetch for pictures or saved memories');}},
    loadImage:async()=>({base64:'UNRELATED_PHOTO'}),fetcher:async(_url,options)=>{
      calls.push(JSON.parse(options.body));return {ok:true,json:async()=>({message:{content:'A reply.'}})};
    }});
  await chat.ask('Look at this photo.',{imagePath:'a.png'});
  assert(calls[0].messages.some(m=>m.images));
  await chat.ask('Remember the key picture?');
  assert(calls[1].messages.every(m=>!m.images));
  assert.match(calls[1].messages[0].content,/a brass key/);
  assert(calls[1].messages.some(m=>m.content.includes('Earlier image attachment is no longer available')));
});

test('capacity limits stop new saves before model work but duplicates remain accessible',async t=>{
  const {source,directory}=await fixture(t);let calls=0;
  const book=createScrapbook({directory,describe:async()=>{calls++;return description;}});
  const {entry}=await book.add(source);
  for(let i=0;i<21;i++) {
    const id=(i+1).toString(16).padStart(16,'0');await mkdir(join(directory,id));
    await writeFile(join(directory,id,'entry.json'),JSON.stringify({...entry,id,bytes:10*1024*1024}));
  }
  assert.equal((await book.add(source)).duplicate,true);assert.equal(calls,1);
  const second=join(dirname(source),'second.png');await writeFile(second,Buffer.concat([png,Buffer.from('different bytes')]));
  await assert.rejects(book.add(second),/200 MB/);assert.equal(calls,1);
});

test('concurrent writers are refused without changing entries; a crashed writer lock is recoverable',async t=>{
  const {source,directory}=await fixture(t);await mkdir(directory);
  const book=createScrapbook({directory,describe:async()=>description});
  await writeFile(join(directory,'.lock'),String(process.pid));
  await assert.rejects(book.add(source),/Another scrapbook change/);assert.deepEqual(await book.list(),[]);
  // An invalid owner is conservatively treated as busy, rather than deleting someone else's lock.
  await writeFile(join(directory,'.lock'),'unknown');await assert.rejects(book.add(source),/Another scrapbook change/);
  const child=spawnSync(process.execPath,['-e','']);assert.equal(child.status,0);
  await writeFile(join(directory,'.lock'),String(child.pid));
  const {entry}=await book.add(source);assert(entry.id);
});
