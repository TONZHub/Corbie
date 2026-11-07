import {mkdir,readFile,writeFile,rename,unlink,appendFile} from 'node:fs/promises';
import {join,dirname} from 'node:path';
import {fileURLToPath} from 'node:url';
import {randomUUID} from 'node:crypto';
import {FIELD_KEYS,META_KEYS,STAT_KEYS,parseCorrections} from './stats-reader.mjs';

const here=dirname(fileURLToPath(import.meta.url));
const monthOf=fields=>fields?.year&&!fields.year.conflict&&fields?.month&&!fields.month.conflict?fields.year.value+'-'+fields.month.value:null;
export function mergeStatsReading(previous,reading,{sameMonth=false}={}) {
  let fields=structuredClone(reading.fields||{});
  const oldMonth=monthOf(previous?.fields),newMonth=monthOf(fields);
  if(!newMonth&&oldMonth) {
    if(!sameMonth)throw new Error('The game month is unreadable. Draft kept. Show its calendar, or use solver scan same only if this is the same game month.');
    for(const k of ['year','month'])fields[k]={...previous.fields[k],source:'human same-month attestation'};
  }
  let draft=previous&&oldMonth&&monthOf(fields)===oldMonth?structuredClone(previous):{id:randomUUID(),fields:{},sources:[]};
  for(const [key,value] of Object.entries(fields)) {
    const prior=draft.fields[key];
    const sourced={...value,observation:reading.observation||null,picture:reading.picture||null};
    if(prior&&prior.value!==value.value)draft.fields[key]={...prior,confidence:0,conflict:[...(prior.conflict||[prior.value]),value.value],latestEvidence:value.evidence,conflictPicture:reading.picture||null};
    else if(!prior||(!prior.conflict&&value.confidence>=prior.confidence))draft.fields[key]=sourced;
  }
  draft.sources=[...draft.sources,{observation:reading.observation,picture:reading.picture,at:new Date().toISOString()}].slice(-50);
  draft.updatedAt=new Date().toISOString();delete draft.acceptedAt;
  return draft;
}

export function statsReview(draft) {
  if(!draft)return 'No stat proposal yet. Open a game stats page, then use solver scan.';
  const lines=['STAT REVIEW · '+(monthOf(draft.fields)||'game month unknown')+(draft.acceptedAt?' · accepted':' · draft only')];
  for(const key of FIELD_KEYS) {
    const field=draft.fields[key];
    lines.push(key+': '+(!field?'? missing':field.conflict?'! CONFLICT '+field.conflict.join(' / '):String(field.value)+(field.source==='human'?' · corrected':field.confidence<.9?' · ? check image':' · labelled reading')));
  }
  const shot=draft.sources.at(-1)?.picture;if(shot)lines.push('Latest source picture: '+shot);
  lines.push('solver set stamina=120 stress=10 · correct values\nScan additional pages in the SAME month to fill missing values.\nsolver accept · after reviewing every value, confirm these are START-OF-MONTH stats. Unknown/conflicting fields block acceptance.');
  return lines.join('\n');
}

export function createStatsNotebook({directory=join(here,'session'),solverStore}={}) {
  const draftPath=join(directory,'stats-draft.json');
  async function load(){try{return JSON.parse(await readFile(draftPath,'utf8'));}catch(e){if(e.code==='ENOENT')return null;throw e;}}
  async function save(draft) {
    await mkdir(directory,{recursive:true});const tmp=draftPath+'.'+randomUUID()+'.tmp';
    try{await writeFile(tmp,JSON.stringify(draft,null,2));await rename(tmp,draftPath);}finally{await unlink(tmp).catch(e=>{if(e.code!=='ENOENT')throw e;});}
    await writeFile(join(directory,'stats-review.txt'),statsReview(draft));
  }
  return {
    async scan(frame,reading,options={}) {
      const previous=await load();
      const path=join(directory,'stats-pictures',randomUUID()+'.png');
      // Validate the merge before saving a picture or changing the previous draft.
      const draft=mergeStatsReading(previous,{...reading,picture:path},options);
      await mkdir(dirname(path),{recursive:true});await writeFile(path,Buffer.from(frame.png,'base64'));
      draft.sources.at(-1).picture=path;
      await save(draft);return statsReview(draft);
    },
    async review(){return statsReview(await load());},
    async correct(text) {
      const changes=parseCorrections(text);let draft=await load()||{id:randomUUID(),fields:{},sources:[]};
      const old=monthOf(draft.fields);
      const dateChanged=old&&['year','month'].some(k=>Object.hasOwn(changes,k)&&changes[k]!==draft.fields[k].value);
      if(dateChanged)draft={id:randomUUID(),fields:{},sources:[]};
      for(const [key,value] of Object.entries(changes))draft.fields[key]={value,confidence:1,evidence:'Corrected by human.',source:'human'};
      draft.updatedAt=new Date().toISOString();delete draft.acceptedAt;await save(draft);
      return (dateChanged?'The game month changed; old readings were cleared.\n':'')+statsReview(draft);
    },
    async accept() {
      const draft=await load();if(!draft)throw new Error('No stat proposal. Use solver scan first.');
      const blocked=FIELD_KEYS.filter(k=>!draft.fields[k]||draft.fields[k].conflict);
      if(blocked.length)throw new Error('Review is incomplete: '+blocked.join(', ')+'. Scan more pages or use solver set.');
      const actual=Object.fromEntries(META_KEYS.map(k=>[k,draft.fields[k].value]));
      actual.stats=Object.fromEntries(STAT_KEYS.map(k=>[k,draft.fields[k].value]));
      const result=await solverStore.importStats(actual,{source:'reviewed screen readings',draftId:draft.id});
      draft.acceptedAt=new Date().toISOString();await save(draft);
      await appendFile(join(directory,'stats-readings.jsonl'),JSON.stringify({event:'accepted',at:draft.acceptedAt,draftId:draft.id,actual,sources:draft.sources})+'\n');
      return 'Reviewed start-of-month stats accepted. '+result;
    },
    async clear(){await unlink(draftPath).catch(e=>{if(e.code!=='ENOENT')throw e;});await unlink(join(directory,'stats-review.txt')).catch(e=>{if(e.code!=='ENOENT')throw e;});return 'Stat draft cleared. Accepted solver stats and source pictures are kept.';}
  };
}
