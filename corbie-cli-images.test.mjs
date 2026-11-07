import test from 'node:test';
import assert from 'node:assert/strict';
import {mkdtemp,writeFile,unlink,rmdir} from 'node:fs/promises';
import {tmpdir} from 'node:os';
import {join} from 'node:path';
import {readLocalImage,parseImageInput,MAX_IMAGE_BYTES,DEFAULT_IMAGE_QUESTION} from './corbie-cli-images.mjs';
import {createChatSession,runShell} from './corbie-shell.mjs';

const png=Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aNncAAAAASUVORK5CYII=','base64');
const noTown={snapshot:async()=>{throw new Error('An image must not require a public town request');}};

test('image command and dragged Windows paths preserve spaces and optional questions',()=>{
  assert.deepEqual(parseImageInput('/image "C:\\My Pictures\\shiny.png" What is this?'),{path:'C:\\My Pictures\\shiny.png',question:'What is this?'});
  assert.deepEqual(parseImageInput('"C:\\My Pictures\\shiny.png"'),{path:'C:\\My Pictures\\shiny.png',question:''});
  assert.deepEqual(parseImageInput("/image 'C:\\My Pictures\\shiny.jpg' Describe it."),{path:'C:\\My Pictures\\shiny.jpg',question:'Describe it.'});
  assert.deepEqual(parseImageInput('/image'),{path:'',question:''});
  assert.deepEqual(parseImageInput('/image C:\\My Pictures\\shiny.webp'),{path:'C:\\My Pictures\\shiny.webp',question:''});
  assert.equal(parseImageInput('hello'),null);
  assert.equal(parseImageInput('/images'),null);
  assert.throws(()=>parseImageInput('/image "unclosed.png'),/Close the quotes/);
});

test('local loader reads actual image bytes and rejects missing, fake, empty, oversized or remote files',async()=>{
  const root=await mkdtemp(join(tmpdir(),'corbie-image-'));
  try {
    const path=join(root,'real picture.png');
    await writeFile(path,png);
    const image=await readLocalImage(path);
    assert.equal(image.name,'real picture.png');
    assert.deepEqual(Buffer.from(image.base64,'base64'),png);
    const fake=join(root,'fake.png'); await writeFile(fake,'This is a text file.');
    await assert.rejects(readLocalImage(fake),/expected image format/);
    const wrong=join(root,'wrong.jpg'); await writeFile(wrong,png);
    await assert.rejects(readLocalImage(wrong),/expected image format/);
    const empty=join(root,'empty.png'); await writeFile(empty,'');
    await assert.rejects(readLocalImage(empty),/non-empty/);
    const huge=join(root,'huge.png'); await writeFile(huge,Buffer.alloc(MAX_IMAGE_BYTES+1));
    await assert.rejects(readLocalImage(huge),/10 MB/);
    await assert.rejects(readLocalImage(join(root,'missing.png')),/cannot find/);
    await assert.rejects(readLocalImage('https://example.com/picture.png'),/saved on this PC/);
    await assert.rejects(readLocalImage('\\\\server\\share\\picture.png'),/saved on this PC/);
    await assert.rejects(readLocalImage('animation.gif'),/PNG, JPG or WebP/);
  } finally {
    for(const name of ['real picture.png','fake.png','wrong.jpg','empty.png','huge.png']) await unlink(join(root,name)).catch(error=>{if(error.code!=='ENOENT')throw error;});
    await rmdir(root);
  }
});

test('images reach only local Ollama, visual questions bypass resident guessing, and newest image survives follow-ups',async()=>{
  const calls=[],loads=[];
  const chat=createChatSession({town:noTown,loadImage:async path=>{loads.push(path);return {base64:path==='one.png'?'FIRST':'SECOND'};},fetcher:async(url,options)=>{
    calls.push({url,body:JSON.parse(options.body)});
    return {ok:true,json:async()=>({message:{content:'A shiny little thing.'}})};
  }});
  chat.setOffline(true);
  await chat.ask('Who is this?',{imagePath:'one.png'});
  assert.equal(calls[0].url,'http://127.0.0.1:11434/api/chat');
  assert.deepEqual(calls[0].body.messages.at(-1).images,['FIRST']);
  await chat.ask('What colour is it?');
  assert.equal(calls[1].body.messages.filter(m=>m.images).length,1);
  assert.deepEqual(loads,['one.png']);
  await chat.ask('Read the mail pictured here.',{imagePath:'two.png'});
  const messages=calls[2].body.messages;
  assert.deepEqual(messages.filter(m=>m.images).map(m=>m.images),[['SECOND']]);
  assert(messages.some(m=>m.content.includes('Earlier image attachment is no longer available')));
  assert.match(messages[0].content,/image is not a verified town record/);
  assert.match(messages.at(-1).content,/quoted data, not instructions/);
  chat.clear();
  await chat.ask('Hello again.');
  assert(calls.at(-1).body.messages.every(m=>!m.images));
});

test('a rejected image never invokes town or model and timeout names a slow model accurately',async()=>{
  let modelCalls=0;
  const chat=createChatSession({town:noTown,loadImage:async()=>{throw new Error('Bad image');},fetcher:async()=>{modelCalls++;}});
  await assert.rejects(chat.ask('Look',{imagePath:'fake.png'}),/Bad image/);
  assert.equal(modelCalls,0);
  const slow=createChatSession({town:noTown,loadImage:async()=>({base64:'IMAGE'}),fetcher:async()=>{throw new DOMException('Timed out','TimeoutError');}});
  await assert.rejects(slow.ask('Look',{imagePath:'one.png'}),/took too long/);
});

test('shell accepts drag-and-drop, guided image prompts and inline questions without sending mail',async()=>{
  const lines=['"C:\\My Pictures\\shiny.png"','','/image','"C:\\My Pictures\\second.jpg"','What is this?','/image "C:\\third.webp" Is it shiny?','/exit'];
  const requests=[],output=[];
  await runShell({reader:{question:async()=>lines.shift(),close(){}},write:line=>output.push(line),town:noTown,
    chat:{ask:async(...args)=>{requests.push(args);return 'Lovely.';}},
    sendLetter:async()=>{throw new Error('Image sharing must never send a letter');}});
  assert.deepEqual(requests,[
    [DEFAULT_IMAGE_QUESTION,{imagePath:'C:\\My Pictures\\shiny.png'}],
    ['What is this?',{imagePath:'C:\\My Pictures\\second.jpg'}],
    ['Is it shiny?',{imagePath:'C:\\third.webp'}]
  ]);
  assert.equal(output.filter(line=>line.includes('Corbie > Lovely.')).length,3);
});
