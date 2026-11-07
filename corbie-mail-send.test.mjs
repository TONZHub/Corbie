import test from 'node:test';
import assert from 'node:assert/strict';
import {access,readFile} from 'node:fs/promises';
import {sendWithSecurePrompt} from './corbie-mail-send.mjs';

test('secure send bridge passes the reviewed letter to the PowerShell flow and removes its temporary copy',async()=>{
  let bodyFile;
  let args;
  await sendWithSecurePrompt({to:'rei',title:'A hello',body:'Dear Rei, hello.',thread:'rei-earlier-letter'}, {
    runner:async values=>{
      args=values;
      bodyFile=values[values.indexOf('--body-file')+1];
      assert.equal(await readFile(bodyFile,'utf8'),'Dear Rei, hello.');
    }
  });
  assert.deepEqual(args.slice(0,5),['send','--to','rei','--title','A hello']);
  assert.deepEqual(args.slice(-2),['--thread','rei-earlier-letter']);
  await assert.rejects(access(bodyFile));
});
