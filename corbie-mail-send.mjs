import {spawn} from 'node:child_process';
import {mkdtemp,unlink,rmdir,writeFile} from 'node:fs/promises';
import {tmpdir} from 'node:os';
import {join} from 'node:path';
import {fileURLToPath} from 'node:url';

const script=fileURLToPath(new URL('./Corbie-Postmark.ps1',import.meta.url));

function runPowerShell(args) {
  if(process.platform!=='win32') throw new Error('The secure Postmark key prompt needs Windows PowerShell.');
  return new Promise((resolve,reject)=>{
    const child=spawn('powershell.exe',['-NoProfile','-ExecutionPolicy','Bypass','-File',script,...args],{stdio:'inherit'});
    child.once('error',reject);
    child.once('exit',(code)=>code===0?resolve():reject(new Error(`Postmark send stopped (exit ${code}). No successful receipt was reported.`)));
  });
}

export async function sendWithSecurePrompt({to,title,body,thread},{runner=runPowerShell}={}) {
  if(!to || !title || !body) throw new Error('A recipient, title, and letter body are required.');
  const directory=await mkdtemp(join(tmpdir(),'corbie-postmark-'));
  const bodyFile=join(directory,'letter.txt');
  try {
    await writeFile(bodyFile,body,{encoding:'utf8',mode:0o600});
    const args=['send','--to',to,'--title',title,'--body-file',bodyFile];
    if(thread) args.push('--thread',thread);
    await runner(args);
  } finally {
    await unlink(bodyFile).catch(()=>{});
    await rmdir(directory).catch(()=>{});
  }
}
