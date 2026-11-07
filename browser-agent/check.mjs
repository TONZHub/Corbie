import {createServer} from 'node:http';
import {spawn} from 'node:child_process';
import {createInterface} from 'node:readline';
import {join,dirname} from 'node:path';
import {fileURLToPath} from 'node:url';
import {once} from 'node:events';
import {chromium} from 'playwright';

const here=dirname(fileURLToPath(import.meta.url));
let humanFinished=false;
const server=createServer((request,response)=>{
  response.writeHead(200,{'Content-Type':'text/html; charset=utf-8'});
  if(request.url==='/verify') return response.end('<title>Profile verification</title>');
  if(request.url==='/state') return response.end(humanFinished?'The red berry is beside the fence.':'The blue acorn is under the oak tree.');
  response.end('<title>Corbie browser check</title><h1>Corbie browser check</h1><p id="clue">The blue acorn is under the oak tree.</p><script>localStorage.setItem("corbie_profile","kept");setInterval(async()=>{document.getElementById("clue").textContent=await (await fetch("/state")).text()},250)</script>');
});
server.listen(0,'127.0.0.1');
await new Promise(resolve=>server.once('listening',resolve));
const url=`http://127.0.0.1:${server.address().port}/`;
const profile=join(here,'test-profile');
const child=spawn(process.execPath,[join(here,'agent.mjs')],{
  cwd:here,env:{...process.env,CORBIE_BROWSER_HEADLESS:'1',CORBIE_BROWSER_PROFILE:profile,CORBIE_BROWSER_LOCALDATA:join(here,'test-localdata')},stdio:['pipe','pipe','pipe']
});
const lines=createInterface({input:child.stdout});
let done=false,trace=[],errorText='',waiting=0;
child.stderr.on('data',chunk=>{errorText=(errorText+chunk.toString()).slice(-1000);});
const timeout=setTimeout(()=>{if(!done){console.error('TIMEOUT',trace,errorText);child.stdin.write('{"type":"cancel"}\n');}},150000);
child.stdin.write(JSON.stringify({type:'run',task:`Open ${url} and tell me where the blue acorn is.`})+'\n');
for await(const line of lines){
  const data=JSON.parse(line);trace.push(data);
  if(data.type==='approval') child.stdin.write(JSON.stringify({type:'approve',id:data.id,yes:false})+'\n');
  if(data.type==='waiting') {
    waiting++;
    if(waiting===1) {
      humanFinished=true;
      await new Promise(resolve=>setTimeout(resolve,900));
      child.stdin.write('{"type":"resume"}\n');
    } else { done=true;child.stdin.write('{"type":"cancel"}\n');break; }
  }
  if(data.type==='error') break;
}
clearTimeout(timeout);
if(child.exitCode===null) await Promise.race([once(child,'exit'),new Promise((_,reject)=>setTimeout(()=>reject(new Error('Browser did not close after Stop')),10000))]);
const reopened=await chromium.launchPersistentContext(profile,{channel:'msedge',headless:true,args:['--disable-gpu']});
const verify=await reopened.newPage();
await verify.goto(url+'verify');
const persisted=await verify.evaluate(()=>localStorage.getItem('corbie_profile'));
await reopened.close();server.close();
console.log(JSON.stringify(trace));
if(!done || waiting!==2 || !/oak tree/i.test(trace.find(x=>x.type==='waiting')?.message||'') || !/berry|fence/i.test(trace.at(-1).message||'') || persisted!=='kept') process.exitCode=1;
