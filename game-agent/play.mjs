import {createInterface} from 'node:readline/promises';
import {mkdir,readFile,writeFile,rename,appendFile,access} from 'node:fs/promises';
import {dirname,join,resolve} from 'node:path';
import {fileURLToPath} from 'node:url';
import {spawn} from 'node:child_process';
import {createNativeAdapter,DEFAULT_GAME} from './native.mjs';
import {createGameModel,playGame} from './player.mjs';
import {GAME_HELP,statusText,whyText,historyText,sessionReport} from './presentation.mjs';
import {createGuideStore} from './guide.mjs';
import {createSolverStore} from './solver.mjs';
import {createStatsNotebook} from './stats-notebook.mjs';

const here=dirname(fileURLToPath(import.meta.url));
export function createSessionStore(directory=join(here,'session')) {
  const recorded=new Map();
  return {
    async load(){try{const state=JSON.parse(await readFile(join(directory,'progress.json'),'utf8'));for(const h of state.history||[])if(h.id)recorded.set(h.id,JSON.stringify(h));return state;}catch(e){if(e.code==='ENOENT')return {};throw new Error('The game progress journal cannot be read. Keep it for recovery; start with a separate session folder if needed.');}},
    async save(state) {
      await mkdir(directory,{recursive:true});
      const stamped={...state,updated:new Date().toISOString()};
      await writeFile(join(directory,'progress.tmp'),JSON.stringify(stamped,null,2));
      await rename(join(directory,'progress.tmp'),join(directory,'progress.json'));
      const action=state.history?.at(-1);
      if(action?.id&&recorded.get(action.id)!==JSON.stringify(action)) {
        await appendFile(join(directory,'actions.jsonl'),JSON.stringify({event:'action-update',recordedAt:stamped.updated,...action})+'\n');
        recorded.set(action.id,JSON.stringify(action));
      }
      await writeFile(join(directory,'session.md'),sessionReport(stamped));
    },
    async frame(png,name) {
      if(!['last-frame.png','before-frame.png','ending-frame.png','ending-confirmation.png'].includes(name))throw new Error('Unknown game picture.');
      await mkdir(directory,{recursive:true});await writeFile(join(directory,name),Buffer.from(png,'base64'));
    },
    async observation(summary){await mkdir(directory,{recursive:true});await writeFile(join(directory,'observation.json'),JSON.stringify(summary,null,2));}
  };
}
async function buildAdapter() {
  try {await access(join(here,'GameWindow-v6.exe'));return;}catch{}
  await new Promise((resolve,reject)=>{
    const child=spawn('powershell.exe',['-NoProfile','-ExecutionPolicy','Bypass','-File',join(here,'build.ps1')],{stdio:'inherit',windowsHide:true});
    child.once('error',reject);child.once('exit',code=>code===0?resolve():reject(new Error('The game controls could not compile.')));
  });
}
function openGame() {
  const child=spawn('powershell.exe',['-NoProfile','-Command',"Start-Process -FilePath 'steam://rungameid/523000'"],{windowsHide:true,stdio:'ignore'});
  child.on('error',()=>{});child.unref();
}
export async function runGameConsole({reader,write=console.log,adapter,model,guideStore=createGuideStore(),solverStore=createSolverStore(),statsNotebook,store=createSessionStore(),exe=DEFAULT_GAME}={}) {
  model ||= createGameModel({guideStore,solverStore});
  statsNotebook ||= createStatsNotebook({solverStore});
  const ownsReader=!reader;reader ||= createInterface({input:process.stdin,output:process.stdout});
  write('\nCORBIE · ROYAL MISMANAGEMENT DIVISION\nOne daughter. Eight years. I suppose someone has to be responsible.\n');
  write('Prepare a playthrough in the game first. F8 immediately pauses input; switching apps also pauses. Keep the game visible while he plays.');
  write(GAME_HELP);
  let activeAdapter=adapter,controller;
  const interrupt=()=>controller?.abort();
  process.on('SIGINT',interrupt);
  async function ready() {
    if(!activeAdapter) {await buildAdapter();activeAdapter=createNativeAdapter({exe});}
    await activeAdapter.request('attach');
    return activeAdapter;
  }
  try {
    while(true) {
      let command,raw;try{raw=(await reader.question('\nGame > ')).trim();command=raw.toLowerCase();}catch{break;}
      if(['back','exit','quit'].includes(command))break;
      if(command==='solver'||command.startsWith('solver ')) {
        try {
          let result;
          if(command==='solver')result=await solverStore.describe();
          else if(command==='solver scan'||command==='solver scan same') {
            controller=new AbortController();
            try {
              const a=await ready();await a.request('pause');await a.request('focus');
              await model.verify();
              let picture=await a.request('observe');
              if(model.inspectFrame)picture=await model.inspectFrame(picture,{signal:controller.signal,forStats:true});
              write('Reading visible stats · no game input. Use Ctrl+C to cancel.');
              const reading=await model.readStats(picture,{signal:controller.signal});
              if(controller.signal.aborted)throw new Error('Stat reading cancelled; previous stats kept.');
              result=await statsNotebook.scan(picture,reading,{sameMonth:command==='solver scan same'});
            }finally{controller=null;await activeAdapter?.request('pause').catch(()=>{});}
          }
          else if(command==='solver review')result=await statsNotebook.review();
          else if(command.startsWith('solver set '))result=await statsNotebook.correct(raw.slice(11).trim());
          else if(command==='solver accept')result=await statsNotebook.accept();
          else if(command==='solver draft clear')result=await statsNotebook.clear();
          else if(command==='solver template')result=await solverStore.template();
          else if(command==='solver clear')result=await solverStore.clear();
          else if(command.startsWith('solver load '))result=await solverStore.importFile(raw.slice(12));
          else if(command==='solver plan'||command.startsWith('solver plan '))result=await solverStore.preview(raw.slice(11).trim().toLowerCase());
          else if(command.startsWith('solver info '))result=await solverStore.info(raw.slice(12).trim());
          else result=await solverStore.describe();
          write('Corbie > '+result);
        }catch(error){write('Corbie > Solver: '+error.message);}
        continue;
      }
      if(command==='guide'||command==='guide clear'||command.startsWith('guide load ')) {
        try{write('Corbie > '+(command==='guide'?await guideStore.describe():command==='guide clear'?await guideStore.clear():await guideStore.importFile(raw.slice(11))));}
        catch(error){write('Corbie > Guide not changed: '+error.message);}
        continue;
      }
      if(command==='open'){openGame();write('Corbie > Refine is opening through Steam. Prepare the playthrough, then come back and type play.');continue;}
      if(command==='help'){write(GAME_HELP);continue;}
      if(['status','why','history'].includes(command)) {const s=await store.load();write(({status:statusText,why:whyText,history:historyText}[command])(s));continue;}
      if(command==='resume')command='play';
      if(command==='escape')command='esc';
      if(!['play','watch','step','esc'].includes(command)){write(GAME_HELP);continue;}
      try {
        const a=await ready();controller=new AbortController();
        const selectedModel=command==='esc'?{verify:async()=>({controller:'manual Escape'}),plan:async()=>({
          screen:'stats',confidence:1,summary:'Back out of this option with Escape.',date:null,age:null,gold:null,stress:null,health:null,
          action:'key',target:'key:Escape',x:null,y:null,key:'Escape',reason:'One Escape requested by your human.',risk:'normal',outcome:'none',endingEvidence:''})}:model;
        await playGame({adapter:a,model:selectedModel,store,write,signal:controller.signal,watch:command==='watch',singleStep:['step','esc'].includes(command)});
      } catch(error){write('Corbie > '+error.message);}
      finally {controller=null;}
    }
  } finally {
    controller?.abort();if(activeAdapter){await activeAdapter.request('pause').catch(()=>{});activeAdapter.close();}
    process.removeListener('SIGINT',interrupt);if(ownsReader)reader.close();
  }
}
if(process.argv[1] && resolve(process.argv[1])===fileURLToPath(import.meta.url)) {
  runGameConsole().catch(error=>{console.error(error.message);process.exitCode=1;});
}
