import {access} from 'node:fs/promises';
import {fileURLToPath} from 'node:url';
import {spawn} from 'node:child_process';

const executable=fileURLToPath(new URL('./doom/Corbie-Doom.exe',import.meta.url));
const buildScript=fileURLToPath(new URL('./doom/Build-Doom.ps1',import.meta.url));
export function createDoomLauncher({launch=spawn,exists=access,platform=process.platform}={}) {
  return async()=>{
    if(platform!=='win32')throw new Error('Corbie’s Doom arcade currently runs on Windows.');
    try{await exists(executable);}catch(error){
      if(error.code!=='ENOENT')throw error;
      await new Promise((resolve,reject)=>{
        const child=launch('powershell.exe',['-NoProfile','-NonInteractive','-ExecutionPolicy','RemoteSigned','-File',buildScript],{shell:false,windowsHide:true,stdio:'ignore'});
        child.once('error',reject);child.once('exit',code=>code===0?resolve():reject(new Error('The arcade could not build. Open Corbie-Doom.cmd to see the error.')));
      });
    }
    await new Promise((resolve,reject)=>{
      const child=launch(executable,['--player'],{shell:false,detached:true,windowsHide:false,stdio:'ignore'});
      child.once('error',reject);child.once('spawn',()=>{child.unref();resolve();});
    });
    return 'Corbie’s Doom player launcher opened. Type play in his Doom console to watch him explore and fight. Space pauses; F8 returns to the console. If setup is missing, run Setup-Corbie-Doom-Player.cmd.';
  };
}
