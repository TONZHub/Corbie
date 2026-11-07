import {spawn} from 'node:child_process';
import {createInterface} from 'node:readline';
import {dirname,join} from 'node:path';
import {fileURLToPath} from 'node:url';

const here=dirname(fileURLToPath(import.meta.url));
export const DEFAULT_GAME='C:\\Program Files (x86)\\Steam\\steamapps\\common\\Princess Maker 2 Refine\\pm2.exe';
export function createNativeAdapter({exe=DEFAULT_GAME,spawnProcess=spawn}={}) {
  const child=spawnProcess(join(here,'GameWindow-v6.exe'),[exe],{windowsHide:true,stdio:['pipe','pipe','pipe']});
  let pending,closed=false,diagnostic='';
  const lines=createInterface({input:child.stdout});
  child.stderr.on('data',d=>{diagnostic=(diagnostic+d.toString()).slice(-500);});
  function fail(error){if(pending){clearTimeout(pending.timer);pending.reject(error);pending=null;}}
  lines.on('line',line=>{
    if(!pending)return;
    const item=pending;pending=null;clearTimeout(item.timer);
    try {const response=JSON.parse(line);response.ok?item.resolve(response.result):item.reject(new Error(response.error||'Game adapter failed.'));}
    catch{item.reject(new Error('Unreadable game adapter response.'));}
  });
  child.on('error',e=>{closed=true;fail(new Error('Game adapter unavailable. Run game-agent\\build.ps1. '+e.message));});
  child.on('exit',()=>{closed=true;fail(new Error('Game adapter closed. '+diagnostic));});
  return {
    request(command,args={}) {
      if(closed)return Promise.reject(new Error('Game adapter is closed.'));
      if(pending)return Promise.reject(new Error('A game adapter request is already running.'));
      return new Promise((resolve,reject)=>{
        const timer=setTimeout(()=>{fail(new Error('Game adapter timed out. Input is uncertain; inspect the game before resuming.'));child.kill();closed=true;},10000);
        pending={resolve,reject,timer};
        child.stdin.write(JSON.stringify({...args,command})+'\n',error=>{if(error)fail(error);});
      });
    },
    close(){closed=true;fail(new Error('Game session closed.'));child.stdin.end();child.kill();lines.close();}
  };
}
