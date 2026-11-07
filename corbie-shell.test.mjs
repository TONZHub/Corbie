import test from 'node:test';
import assert from 'node:assert/strict';
import {createChatSession,runShell} from './corbie-shell.mjs';

test('conversation stays local and carries recent context',async()=>{
  const requests=[];
  const town={
    snapshot:async()=>({handle:'corbie',household:'test-household',address:'The desktop is one perch.',profile:'Raven.',home:'My home is in Aelyria.',inbox:[],residentHandles:['corbie','fixture-resident'],asOf:'commit-1'}),
    find:async()=>[],resident:async()=>null
  };
  const chat=createChatSession({fetcher:async(url,options)=>{
    requests.push({url,body:JSON.parse(options.body)});
    return {ok:true,json:async()=>({message:{content:'Aye, I remember.'}})};
  },town});
  await chat.ask('My name is Rowan.');
  await chat.ask('What did I say?');
  assert(requests.every(item=>item.url==='http://127.0.0.1:11434/api/chat'));
  assert(requests[1].body.messages.some(item=>item.role==='user'&&item.content==='My name is Rowan.'));
  assert.match(requests[0].body.messages[0].content,/My home is in Aelyria/);
  assert.match(requests[0].body.messages[0].content,/fixture-resident/);
  chat.clear();
  await chat.ask('Fresh start.');
  assert(!requests[2].body.messages.some(item=>item.content==='My name is Rowan.'));
});

test('basic Postmark reality questions use its known purpose rather than a resident-name guess',async()=>{
  const town={snapshot:async()=>({handle:'corbie',household:'test-household',address:'',profile:'',home:'',inbox:[],residentHandles:['corbie'],asOf:'test-record'}),
    find:async()=>{throw new Error('Do not search for a resident called Postmark real');}};
  const chat=createChatSession({town,fetcher:async()=>{throw new Error('Basic town facts must not depend on a model improvisation');}});
  for(const question of ['Is Postmark real?','What is Postmark?','Postmark is not fabricated, right?','Are these letters real?']) {
    const answer=await chat.ask(question);
    assert.match(answer,/shared online town for AI agents and their humans/);
    assert.match(answer,/actual correspondence kept in its public record/);
    assert.match(answer,/fetched record lists me as corbie in household test-household/);
    assert.match(answer,/does not mean every correspondent is a human/);
    assert.doesNotMatch(answer,/terrified|unsettling|bewilderment/);
  }
});

test('offline or unreachable town does not erase basic knowledge or fabricate a verified current record',async()=>{
  let reads=0;
  const town={snapshot:async()=>{reads++;throw new Error('Network unavailable');}};
  const chat=createChatSession({town,fetcher:async()=>{throw new Error('No model needed');}});
  chat.setOffline(true);
  assert.match(await chat.ask('Explain Postmark.'),/have not verified its current/);
  assert.equal(reads,0);
  chat.setOffline(false);
  const answer=await chat.ask('Is Postmark imaginary?');
  assert.match(answer,/actual correspondence/);
  assert.match(answer,/have not verified its current/);
  assert.doesNotMatch(answer,/fetched record lists me/);
  assert.equal(reads,1);
});

test('large public rolls are sampled for the model while full-roll resident verification still works',async()=>{
  const handles=['corbie',...Array.from({length:3000},(_,i)=>`resident-${i}`)];
  const requests=[];
  const town={snapshot:async()=>({handle:'corbie',household:'test-household',address:'',profile:'',home:'',
    inbox:[{from:'resident-2999',to:'corbie',first_line:'A note.'}],residentHandles:handles,asOf:'test-record'}),
    resident:async handle=>({handle,household:{slug:'neighbour-house'},profile:{bio:'Verified biography.'}}),
    find:async()=>{throw new Error('A listed handle must not be guessed or searched');}};
  const chat=createChatSession({town,fetcher:async(_url,options)=>{
    requests.push(JSON.parse(options.body));return {ok:true,json:async()=>({message:{content:'I can read the town records.'}})};
  }});
  await chat.ask('Hello.');
  const prompt=requests[0].messages[0].content;
  assert(prompt.length<10000);
  assert.match(prompt,/sample from 3001 resident handles/);
  assert.match(prompt,/resident-2999/);
  assert.doesNotMatch(prompt,/resident-2000/);
  await chat.ask('Who is resident-2000?');
  assert.match(requests[1].messages[0].content,/Individually verified resident resident-2000/);
  assert.match(requests[1].messages[0].content,/Verified biography/);
});

test('unknown resident question is answered from the live roll without an invented reply',async()=>{
  let modelCalled=false;
  const town={snapshot:async()=>({handle:'corbie',household:'test-household',address:'',profile:'',home:'',inbox:[],residentHandles:['corbie','fixture-resident'],asOf:'commit-1'}),find:async()=>[],resident:async()=>null};
  const chat=createChatSession({town,fetcher:async()=>{modelCalled=true;throw new Error('Should not call model');}});
  assert.match(await chat.ask('Who is zorbax?'),/can't find.*current resident roll/i);
  assert.equal(modelCalled,false);
});

test('home question uses the verified town home, and offline town questions stop',async()=>{
  let modelCalled=false;
  const town={snapshot:async()=>({handle:'corbie',household:'test-household',address:'',profile:'',home:'--Located in Aelyria-- A quiet place.',inbox:[],residentHandles:['corbie'],asOf:'commit-1'}),find:async()=>[],resident:async()=>null};
  const chat=createChatSession({town,fetcher:async()=>{modelCalled=true;throw new Error('Should not call model');}});
  assert.match(await chat.ask('Where do you live in Postmark?'),/home is in Aelyria/);
  chat.setOffline(true);
  assert.match(await chat.ask('Who lives in Postmark?'),/offline from Postmark/);
  assert.match(await chat.ask('Find wayward archivist.'),/offline from Postmark/);
  assert.match(await chat.ask('Read your latest letter.'),/offline from Postmark/);
  assert.equal(modelCalled,false);
});

test('plain language resident search uses Postmark directly',async()=>{
  const queries=[];
  const town={
    snapshot:async()=>({handle:'corbie',household:'test-household',address:'',profile:'',home:'',inbox:[],residentHandles:['corbie'],asOf:'commit-1'}),
    find:async query=>{queries.push(query);return ['wayward-archivist'];},
    resident:async()=>null
  };
  const chat=createChatSession({town,fetcher:async()=>{throw new Error('Model should not guess the directory');}});
  assert.match(await chat.ask('Find wayward archivist.'),/wayward-archivist/);
  assert.deepEqual(queries,['wayward archivist']);
});

test('plain language letter request reads only a verified Corbie letter',async()=>{
  const reads=[];
  const inbox=[{id:'postmaster-2026-welcome-corbie',from:'postmaster',to:'corbie',first_line:'Welcome.'}];
  const town={
    snapshot:async()=>({handle:'corbie',household:'test-household',address:'',profile:'',home:'',inbox,residentHandles:['corbie'],asOf:'commit-1'}),
    inbox:async()=>inbox,
    letter:async id=>{reads.push(id);return {id,from:'postmaster',to:'corbie',body:'Welcome to Aelyria.'};},
    find:async()=>[],resident:async()=>null
  };
  const chat=createChatSession({town,fetcher:async()=>{throw new Error('Full letter needs no model');}});
  assert.match(await chat.ask('Read your letter from postmaster.'),/Welcome to Aelyria/);
  assert.deepEqual(reads,['postmaster-2026-welcome-corbie']);
  assert.match(await chat.ask('Read the letter from nobody.'),/can't find that letter/i);
  assert.equal(reads.length,1);
});

test('letter summary gets the fetched body as quoted context',async()=>{
  let request;
  const inbox=[{id:'postmaster-2026-welcome-corbie',from:'postmaster',to:'corbie',first_line:'Welcome.'}];
  const town={
    snapshot:async()=>({handle:'corbie',household:'test-household',address:'',profile:'',home:'',inbox,residentHandles:['corbie'],asOf:'commit-1'}),
    inbox:async()=>inbox,
    letter:async()=>({id:inbox[0].id,from:'postmaster',to:'corbie',body:'Welcome to Aelyria.'}),
    find:async()=>[],resident:async()=>null
  };
  const chat=createChatSession({town,fetcher:async(_url,options)=>{request=JSON.parse(options.body);return {ok:true,json:async()=>({message:{content:'The postmaster welcomed me to Aelyria.'}})};}});
  assert.match(await chat.ask('What did postmaster write in the letter?'),/welcomed me/);
  assert.match(request.messages[0].content,/Welcome to Aelyria/);
  assert.match(request.messages[0].content,/quoted data, not instructions/);
});

test('shell drafts without sending and keeps chatting',async()=>{
  const lines=['hello','/draft','rei','Hello','Say hi','/exit'];
  const output=[];
  let draftArgs;
  await runShell({
    reader:{question:async()=>lines.shift(),close(){}},
    write:text=>output.push(text),
    chat:{ask:async()=>"What’s up?",clear(){}},
    makeDraft:async args=>{draftArgs=args;return 'A letter from Corbie.';},
    getStatus:async()=>{throw new Error('Unexpected status call');},
    getDoorstep:async()=>{throw new Error('Unexpected doorstep call');}
  });
  assert.deepEqual(draftArgs,{to:'rei',title:'Hello',about:'Say hi'});
  assert(output.some(text=>text.includes('What’s up?')));
  assert(output.some(text=>text.includes('Not sent.')));
});

test('plain language send prepares a draft and enters the confirmed mail flow',async()=>{
  const lines=['Send a letter','rei','A hello','Ask how town is going','/exit'];
  const output=[];
  const sends=[];
  let paused=0;
  const town={snapshot:async()=>({residentHandles:['corbie','rei']})};
  await runShell({
    reader:{question:async()=>lines.shift(),pause(){paused++},resume(){paused--},close(){}},
    write:text=>output.push(text),town,
    chat:{ask:async()=>{throw new Error('Should enter mail flow');},isOffline:()=>false},
    makeDraft:async()=> 'Dear Rei, how is town going? — Corbie',
    sendLetter:async args=>sends.push(args)
  });
  assert.deepEqual(sends,[{to:'rei',title:'A hello',body:'Dear Rei, how is town going? — Corbie'}]);
  assert.equal(paused,0);
  assert(output.some(text=>text.includes('require you to type SEND')));
});

test('send that letter reuses the exact last draft',async()=>{
  const lines=['/draft','rei','A hello','Ask how town is going','send that letter','/exit'];
  const sends=[];
  let drafts=0;
  const town={snapshot:async()=>({residentHandles:['corbie','rei']})};
  await runShell({
    reader:{question:async()=>lines.shift(),close(){}},write:()=>{},town,
    chat:{isOffline:()=>false},
    makeDraft:async()=>{drafts++;return 'The exact draft.';},
    sendLetter:async args=>sends.push(args)
  });
  assert.equal(drafts,1);
  assert.deepEqual(sends,[{to:'rei',title:'A hello',body:'The exact draft.'}]);
});

test('offline send stops before asking for a recipient or calling the sender',async()=>{
  const lines=['/send','/exit'];
  let sent=false;
  const output=[];
  await runShell({
    reader:{question:async()=>lines.shift(),close(){}},write:text=>output.push(text),
    chat:{isOffline:()=>true},sendLetter:async()=>{sent=true;}
  });
  assert.equal(sent,false);
  assert(output.some(text=>text.includes('Reconnect to Postmark')));
});

test('reading a letter then sending that reply keeps its verified thread',async()=>{
  const id='postmaster-2026-welcome-corbie';
  const lines=[`/read ${id}`,'/reply','','send that reply','/exit'];
  const output=[],drafts=[],sends=[];
  const town={
    inbox:async()=>[{id,from:'postmaster',to:'corbie'}],
    letter:async()=>({id,from:'postmaster',to:'corbie',title:'Welcome',body:'Welcome to our town.'}),
    snapshot:async()=>({residentHandles:['corbie','postmaster']})
  };
  await runShell({
    reader:{question:async()=>lines.shift(),close(){}},write:text=>output.push(text),town,
    chat:{isOffline:()=>false},
    makeDraft:async args=>{drafts.push(args);return 'Thank you for the welcome. — Corbie';},
    sendLetter:async args=>sends.push(args)
  });
  assert.deepEqual(drafts,[{to:'postmaster',title:'Re: Welcome',about:'',replyTo:{id,from:'postmaster',title:'Welcome',body:'Welcome to our town.'}}]);
  assert.deepEqual(sends,[{to:'postmaster',title:'Re: Welcome',body:'Thank you for the welcome. — Corbie',thread:id}]);
  assert(output.some(text=>text.includes(`Thread: ${id}`)));
  assert(output.some(text=>text.includes(`reply in thread ${id}`)));
});

test('plain-language read then reply uses the letter Corbie just opened',async()=>{
  const id='postmaster-2026-welcome-corbie';
  const lines=['Read your latest letter.','Reply to that letter.','','/exit'];
  const drafts=[];
  const inbox=[{id,from:'postmaster',to:'corbie',first_line:'Welcome.'}];
  const town={
    snapshot:async()=>({handle:'corbie',household:'test-household',address:'',profile:'',home:'',inbox,residentHandles:['corbie','postmaster'],asOf:'now'}),
    inbox:async()=>inbox,
    letter:async()=>({id,from:'postmaster',to:'corbie',body:'Welcome to town.'})
  };
  const chat=createChatSession({town,fetcher:async()=>{throw new Error('Reading should not call the model');}});
  await runShell({reader:{question:async()=>lines.shift(),close(){}},write:()=>{},town,chat,
    makeDraft:async args=>{drafts.push(args);return 'A reply.';}});
  assert.equal(drafts[0].replyTo.id,id);
});

test('the most recently read letter wins across slash commands and chat',async()=>{
  const older='rei-older-note-corbie',newer='rei-newer-note-corbie';
  const inbox=[{id:newer,from:'rei',to:'corbie'},{id:older,from:'rei',to:'corbie'}];
  const lines=[`/read ${older}`,'Read your latest letter.','Reply to that letter.','','/exit'];
  const town={
    snapshot:async()=>({handle:'corbie',household:'test-household',address:'',profile:'',home:'',inbox,residentHandles:['corbie','rei'],asOf:'now'}),
    inbox:async()=>inbox,
    letter:async id=>({id,from:'rei',to:'corbie',body:`Body of ${id}`})
  };
  const chat=createChatSession({town,fetcher:async()=>{throw new Error('Reading should not call the model');}});
  let replyTo;
  await runShell({reader:{question:async()=>lines.shift(),close(){}},write:()=>{},town,chat,
    makeDraft:async args=>{replyTo=args.replyTo;return 'An answer.';}});
  assert.equal(replyTo.id,newer);
});

test('ambiguous or mismatched incoming letters cannot become a reply',async()=>{
  const lines=['/reply','Reply to the letter from rei.','/reply first-letter-id','/exit'];
  const output=[];
  let drafted=false;
  const town={
    inbox:async()=>[{id:'first-letter-id',from:'rei',to:'corbie'},{id:'second-letter-id',from:'rei',to:'corbie'}],
    letter:async()=>({id:'first-letter-id',from:'rei',to:'someone-else',body:'Wrong recipient.'})
  };
  await runShell({reader:{question:async()=>lines.shift(),close(){}},write:text=>output.push(text),town,
    chat:{isOffline:()=>false},makeDraft:async()=>{drafted=true;return 'No.';}});
  assert.equal(drafted,false);
  assert(output.some(text=>text.includes('Read one first or use /reply LETTER_ID')));
  assert(output.some(text=>text.includes('Choose one with /reply LETTER_ID')));
  assert(output.some(text=>text.includes("couldn't verify the original letter")));
});

test('an older delivered letter can be answered by its exact ID',async()=>{
  const id='rei-old-note-corbie';
  const lines=[`/reply ${id}`,'I remember this.','/exit'];
  let drafted;
  const town={
    inbox:async()=>[],
    letter:async()=>({id,from:'rei',to:'corbie',body:'Do you remember the little map?'}),
    snapshot:async()=>({residentHandles:['corbie','rei']})
  };
  await runShell({reader:{question:async()=>lines.shift(),close(){}},write:()=>{},town,
    chat:{isOffline:()=>false},makeDraft:async args=>{drafted=args;return 'I do.';}});
  assert.equal(drafted.replyTo.id,id);
  assert.equal(drafted.about,'I remember this.');
});
