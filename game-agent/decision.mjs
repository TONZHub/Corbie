import {GameReadError} from './read-error.mjs';
const screens=['title','setup','home','schedule','work','rest','dialog','stats','ending','credits','save','load','unknown'];
const keys=['Enter','Space','Escape','Tab','ArrowLeft','ArrowRight','ArrowUp','ArrowDown'];
const actions=['click','key','wait','pause'];
export const PLAN_SCHEMA={type:'object',additionalProperties:false,
  required:['screen','confidence','summary','date','age','gold','stress','health','action','target','x','y','key','reason','risk','outcome','endingEvidence'],
  properties:{
    screen:{type:'string',enum:screens},confidence:{type:'number',minimum:0,maximum:1},
    summary:{type:'string'},date:{type:['string','null']},age:{type:['number','null']},
    gold:{type:['number','null']},stress:{type:['number','null']},health:{type:['string','null']},
    action:{type:'string',enum:actions},target:{type:'string'},x:{type:['number','null']},y:{type:['number','null']},
    key:{type:['string','null']},reason:{type:'string'},
    risk:{type:'string',enum:['normal','new_game','save','load','overwrite','delete','quit']},
    outcome:{type:'string',enum:['none','adulthood','death','unknown']},endingEvidence:{type:'string'}
  }};
export function parsePlan(text) {
  let p;
  try { p=JSON.parse(text); } catch {throw new GameReadError('Corbie could not read a reliable game decision. Paused without input.');}
  if(!p || typeof p!=='object' || Array.isArray(p) || Object.keys(p).some(k=>!Object.hasOwn(PLAN_SCHEMA.properties,k)) ||
    PLAN_SCHEMA.required.some(k=>!Object.hasOwn(p,k)) ||
    !screens.includes(p.screen)||!actions.includes(p.action)||!Number.isFinite(p.confidence)||p.confidence<0||p.confidence>1||
    !PLAN_SCHEMA.properties.risk.enum.includes(p.risk)||!PLAN_SCHEMA.properties.outcome.enum.includes(p.outcome)) throw new GameReadError('Invalid game decision. Paused without input.');
  for(const k of ['summary','target','reason','endingEvidence']) if(typeof p[k]!=='string'||p[k].length>1200)throw new GameReadError('Invalid game observation text.');
  for(const k of ['age','gold','stress','x','y']) if(p[k]!==null && !Number.isFinite(p[k]))throw new GameReadError('Invalid observed game number.');
  if(p.date!==null && (typeof p.date!=='string'||p.date.length>80) || p.health!==null && (typeof p.health!=='string'||p.health.length>200))throw new GameReadError('Invalid game status.');
  if(p.key!==null && typeof p.key!=='string')throw new GameReadError('Invalid game key.');
  if(p.action==='click' && (![p.x,p.y].every(v=>Number.isFinite(v)&&v>=0&&v<=1000)||!p.target.trim()))throw new GameReadError('Corbie did not locate a valid game control.');
  if(p.action==='key'&&!keys.includes(p.key))throw new GameReadError('That game key is not allowed.');
  return p;
}
export function decisionIssue(p) {
  if(['title','setup','save','load','unknown'].includes(p.screen))return 'Prepare the playthrough yourself, then resume from the game. No new game or save was selected.';
  if(p.risk!=='normal' || /\b(save|load|overwrite|delete|new game|quit|exit game)\b/i.test(p.target))return 'This action could replace a playthrough or save. Handle it yourself, then resume.';
  if(p.outcome==='death')return 'The game appears to show death. This is not a successful completed playthrough.';
  if(p.confidence<.85)return 'Corbie is not confident enough about this screen. Paused without input.';
  if(p.action==='pause')return p.reason||'Corbie asked for help with this screen.';
  return null;
}
export function isEnding(p) {
  return ['ending','credits'].includes(p.screen)&&p.outcome==='adulthood'&&p.confidence>=.95&&p.endingEvidence.trim().length>=8&&p.action==='pause';
}
