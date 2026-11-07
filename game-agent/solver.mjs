import {spawn} from 'node:child_process';
import {readFile,writeFile,mkdir,rename,unlink,access} from 'node:fs/promises';
import {fileURLToPath} from 'node:url';
import {join,dirname,resolve} from 'node:path';
import {randomUUID} from 'node:crypto';
import {homedir} from 'node:os';
import {readGameCalendar} from './calendar.mjs';
import {visibleControls} from './controls.mjs';
import {JOBS} from './guide.mjs';

const here=dirname(fileURLToPath(import.meta.url));
const statsKeys='constitution strength intelligence refinement charisma morality faith sin sensitivity stress combat_skill combat_attack combat_defense magic_skill magic_attack magic_defense decorum art conversation cooking cleaning temperament'.split(' ');
const jobName=label=>label==='Babysitter'?'babysitting':label.toLowerCase();

export async function runSolver(request,{signal,timeout=20000}={}) {
  let executable='py',prefix=['-3'];
  for(const root of [process.env.LOCALAPPDATA,join(homedir(),'AppData','Local')].filter(Boolean)) {
    const installed=join(root,'Programs','Python','Python312','python.exe');
    try{await access(installed);executable=installed;prefix=[];break;}catch{}
  }
  return new Promise((resolvePromise,reject)=>{
    let output='',errors='',settled=false;
    const child=spawn(executable,[...prefix,'-B',join(here,'solver','bridge.py')],{windowsHide:true,stdio:['pipe','pipe','pipe'],shell:false});
    const finish=(error,value)=>{if(settled)return;settled=true;clearTimeout(timer);signal?.removeEventListener('abort',abort);error?reject(error):resolvePromise(value);};
    const abort=()=>{child.kill();finish(new Error('Solver planning cancelled.'));};
    const timer=setTimeout(()=>{child.kill();finish(new Error('The local solver timed out; no game input was sent.'));},timeout);
    if(signal?.aborted)abort();else signal?.addEventListener('abort',abort,{once:true});
    child.on('error',()=>finish(new Error('Python could not start. Install Python 3 or check the py launcher.')));
    child.stdout.on('data',chunk=>{output+=chunk;if(output.length>1024*1024){child.kill();finish(new Error('Solver reply exceeded its limit.'));}});
    child.stderr.on('data',chunk=>{errors=(errors+chunk).slice(-2000);});
    child.stdin.on('error',()=>{});
    child.on('close',code=>{
      let value;try{value=JSON.parse(output);}catch{finish(new Error('Solver returned an unreadable reply. '+errors.slice(-300)));return;}
      if(code!==0||value.ok!==true){finish(new Error(value.error||'Solver planning failed.'));return;}
      finish(null,value);
    });
    child.stdin.end(JSON.stringify(request));
  });
}

export function createSolverStore({directory=join(here,'session'),runner=runSolver}={}) {
  const snapshotPath=join(directory,'solver-stats.json'),goalPath=join(directory,'solver-goal.txt');
  const cache=new Map();
  async function readSnapshot(){try{return JSON.parse(await readFile(snapshotPath,'utf8'));}catch(e){if(e.code==='ENOENT')return null;throw e;}}
  async function readGoal(){try{return (await readFile(goalPath,'utf8')).trim();}catch(e){if(e.code==='ENOENT')return 'survival';throw e;}}
  async function atomic(path,text){await mkdir(directory,{recursive:true});const tmp=path+'.'+randomUUID()+'.tmp';try{await writeFile(tmp,text);await rename(tmp,path);}finally{await unlink(tmp).catch(e=>{if(e.code!=='ENOENT')throw e;});}}
  async function importStats(obj,provenance={source:'human-supplied JSON'}) {
    const validated=await runner({command:'validate',stats:obj});
    const previous=await readSnapshot(),at=new Date().toISOString();
    let forecast=null;try{forecast=JSON.parse(await readFile(join(directory,'solver-forecast.json'),'utf8'));}catch(e){if(e.code!=='ENOENT')throw e;}
    const actual=validated.state;
    const nextMonth=forecast&&{year:forecast.month===12?forecast.year+1:forecast.year,month:forecast.month===12?1:forecast.month+1};
    const comparison=nextMonth&&nextMonth.year===actual.year&&nextMonth.month===actual.month?{
      gold:{projected:forecast.projected.gold,observed:actual.gold,difference:actual.gold-forecast.projected.gold},
      stress:{projected:forecast.projected.stress,observed:actual.stats.stress,difference:actual.stats.stress-forecast.projected.stress},
      note:'Forecast versus reviewed observations only. The proposed schedule was not verified as executed; other events can affect the difference.'}:null;
    await atomic(snapshotPath,JSON.stringify({state:actual,assumptions:validated.assumptions,importedAt:at,provenance},null,2));
    await writeFile(join(directory,'stats-notebook.md'),'# Corbie’s observation notebook\n\nReviewed '+actual.year+'-'+actual.month+' · '+actual.gold+'G · stress '+actual.stats.stress+'\n\n'+
      (previous?'Previous reviewed snapshot: '+previous.state.year+'-'+previous.state.month+' · '+previous.state.gold+'G\n\n':'')+
      (comparison?'## Forecast comparison\n\n'+JSON.stringify(comparison,null,2)+'\n\n':'No matching prior-month forecast to compare.\n\n')+
      '## Simulation assumptions\n\n'+validated.assumptions.join('\n')+'\n');
    cache.clear();return 'Loaded current stats for '+actual.year+'-'+actual.month+'. '+validated.assumptions.join(' ')+
      (comparison?' Forecast comparison saved to stats-notebook.md.':'');
  }
  async function plan(snapshot,goal,jobs,signal) {
    const request={command:'plan',stats:snapshot.state,goal,...(jobs?.length?{jobs}: {})};
    const key=JSON.stringify(request);
    if(cache.has(key))return cache.get(key);
      const advice=await runner(request,{signal});
    if(cache.size>=8)cache.delete(cache.keys().next().value);cache.set(key,advice);
    return advice;
  }
  return {
    async context(frame,observed={},signal) {
      const names=visibleControls(frame).filter(c=>c.y>200).map(c=>c.label.trim()).filter(s=>[...JOBS,'Work','Time off','Free time'].includes(s));
      const knownNames=names.some(s=>JOBS.includes(s))?names:[...JOBS,'Free time'];
      const knowledgeKey='knowledge:'+JSON.stringify(knownNames);
      let knowledge=cache.get(knowledgeKey);
      if(!knowledge){knowledge=await runner({command:'knowledge',names:knownNames},{signal});cache.set(knowledgeKey,knowledge);}
      const snapshot=await readSnapshot();
      if(!snapshot)return {...knowledge,status:'needs-stats',reason:'Job reference is available. Load actual start-of-month stats with solver load before numerical planning.'};
      const date=readGameCalendar(frame),state=snapshot.state;
      if(date.year!==state.year||date.month!==state.month)return {...knowledge,status:'stale',reason:'Stats date does not match a verified current game month. Reload actual stats; no forecast supplied.'};
      for(const key of ['gold','age','stress']) {
        const supplied=key==='stress'?state.stats.stress:state[key];
        if(observed[key]!=null&&observed[key]!==supplied)return {...knowledge,status:'stale',reason:'Current '+key+' differs from the supplied stats. Reload current stats; no forecast supplied.'};
      }
      const jobs=names.filter(s=>JOBS.includes(s)).map(jobName);
      const advice=await plan(snapshot,await readGoal(),jobs,signal);
      if(advice.projected)await atomic(join(directory,'solver-forecast.json'),JSON.stringify({year:state.year,month:state.month,goal:advice.goal,actions:advice.actions,projected:advice.projected,at:new Date().toISOString()}));
      const periodLabel=visibleControls(frame).find(c=>c.source==='ocr'&&c.x<500&&c.y>680&&/\bperiod\s*[123]\b/i.test(c.label));
      const currentPeriod=periodLabel?Number(periodLabel.label.match(/\bperiod\s*([123])\b/i)[1]):null;
      return {...knowledge,advice,currentPeriod,recommendedAction:currentPeriod?advice.actions?.[currentPeriod-1]??null:null,
        statsSource:'Human-supplied start-of-month snapshot; no simulated advances written back.',status:advice.status};
    },
    async describe(){const snapshot=await readSnapshot();return 'pm2-solver is connected locally. Goal: '+await readGoal()+'.\n'+
      (snapshot?'Stats: '+snapshot.state.year+'-'+String(snapshot.state.month).padStart(2,'0')+'; supplied '+snapshot.importedAt:'No current stats loaded; job reference only.')+
      '\nsolver scan · propose readings from the current game page\nsolver review · review proposal\nsolver set stamina=120 stress=10 · correct readings\nsolver accept · accept reviewed START-OF-MONTH stats\nsolver template · create a stats form\nsolver load "path.json" · load actual start-of-month stats\nsolver plan [survival/queen/scholar/...] · preview one month\nsolver info farm · job/class figures\nsolver clear · remove loaded stats';},
    async template(){
      const path=join(directory,'solver-stats-template.json');
      await mkdir(directory,{recursive:true});
      const template={year:null,month:null,age:null,birth_month:null,blood:null,sign:null,diet:null,gold:null,stats:Object.fromEntries(statsKeys.map(k=>[k,null]))};
      try{await writeFile(path,JSON.stringify(template,null,2),{flag:'wx'});}catch(e){if(e.code!=='EEXIST')throw e;}
      return 'Fill this form with actual game values at the start of a month, then use solver load "'+path+'":\n'+path+
        '\nSigns and diets use solver names (for example capricorn, normal). Existing form preserved. Unknown values must not be guessed.';
    },
    async importFile(input){
      const path=resolve(input.trim().replace(/^"(.*)"$/s,'$1').replace(/^'(.*)'$/s,'$1'));
      const bytes=await readFile(path);if(bytes.length>256*1024)throw new Error('Stats JSON must be smaller than 256 KB.');
      const obj=JSON.parse(bytes.toString('utf8').replace(/^\uFEFF/,''));
      return importStats(obj);
    },
    importStats,
    async preview(goal){
      const snapshot=await readSnapshot();if(!snapshot)throw new Error('Load actual stats first: solver template, then solver load "path.json".');
      const chosen=goal?.trim()||await readGoal();
      const advice=await plan(snapshot,chosen);
      if(goal)await atomic(goalPath,chosen);
      return advice.notice+'\n'+(advice.actions?'Suggested periods: '+advice.actions.join(' → ')+'\nProjected only: '+JSON.stringify(advice.projected):advice.reason)+
        '\n'+advice.assumptions.join(' ')+'\nThis preview sends no game input.';
    },
    async info(name){const result=await runner({command:'knowledge',names:[name]});return result.notice+'\n'+
      (Object.keys(result.knowledge).length?JSON.stringify(result.knowledge,null,2):'No matching job/class. Try farm, church, inn, restaurant, protocol or free time.');},
    async clear(){for(const path of [snapshotPath,goalPath,join(directory,'solver-forecast.json')])await unlink(path).catch(e=>{if(e.code!=='ENOENT')throw e;});cache.clear();return 'Solver stats, target and active forecast cleared. Job reference remains available.';}
  };
}
