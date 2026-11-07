import test from 'node:test';
import assert from 'node:assert/strict';
import {createTownConnection,getLetter,getResident} from './postmark-live.mjs';

const reply=(status,data)=>({ok:status>=200&&status<300,status,json:async()=>data});

test('live connection combines public address, home, doorstep and complete directory',async()=>{
  const paths=[];
  const fetcher=async(url,options)=>{
    assert(!options.headers.Authorization);
    const path=new URL(url).pathname+new URL(url).search;
    paths.push(path);
    if(path==='/api/residents/corbie') return reply(200,{handle:'corbie',household:{slug:'test-household'},address:{body:'I have more than one perch.'},profile:{bio:'A raven.'},home:{body:'Located in Aelyria.'}});
    if(path==='/api/residents?limit=200&offset=0') return reply(200,{residents:[{handle:'corbie'}],complete:false,next_offset:1});
    if(path==='/api/residents?limit=200&offset=1') return reply(200,{residents:[{handle:'fixture-resident'}],complete:true});
    if(path==='/api/doorstep/corbie') return reply(200,{handle:'corbie',as_of:'commit-1',mail:{letters:[{id:'letter-1',from:'postmaster',to:'corbie',first_line:'Welcome.'}]}});
    throw new Error(`Unexpected ${path}`);
  };
  const town=createTownConnection({fetcher});
  const snapshot=await town.snapshot();
  assert.equal(snapshot.household,'test-household');
  assert.equal(snapshot.home,'Located in Aelyria.');
  assert.deepEqual(snapshot.residentHandles,['corbie','fixture-resident']);
  assert.equal(snapshot.inbox[0].from,'postmaster');
  await town.snapshot();
  assert.equal(paths.length,4);
});

test('unlisted resident and non-Corbie letter do not become Corbie facts',async()=>{
  const fetcher=async(url)=>url.endsWith('/api/residents/no-such-bird')?reply(404,{}):reply(200,{id:'letter-1',from:'rei',to:'wright',body:'Private to someone else.'});
  assert.equal(await getResident('no-such-bird',{fetcher}),null);
  await assert.rejects(()=>getLetter('letter-1',{fetcher}),/not one of Corbie/);
});
