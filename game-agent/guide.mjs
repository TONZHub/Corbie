import {readFile,writeFile,rename,mkdir,unlink} from 'node:fs/promises';
import {dirname,extname,resolve} from 'node:path';
import {fileURLToPath} from 'node:url';
import {randomUUID} from 'node:crypto';
import {visibleControls} from './controls.mjs';

export const JOBS=['Housework','Babysitter','Inn','Farm','Church','Restaurant'];
const defaultGuide=fileURLToPath(new URL('./strategy-guide.md',import.meta.url));
const defaultNotes=fileURLToPath(new URL('./session/strategy-notes.txt',import.meta.url));
const MAX_BYTES=256*1024;

function decode(bytes) {
  if(!bytes.length||bytes.length>MAX_BYTES)throw new Error('Use a nonempty guide of at most 256 KB.');
  let text;try{text=new TextDecoder('utf-8',{fatal:true}).decode(bytes);}catch{throw new Error('Save the guide as UTF-8 text or Markdown.');}
  if(!text.trim()||/[\x00-\x08\x0e-\x1f]/.test(text))throw new Error('The guide must contain readable text, not a binary document.');
  return text.trim();
}

export function isStrategyChoice(frame) {
  const labels=visibleControls(frame).filter(c=>c.y>200).map(c=>c.label.trim().toLowerCase());
  return ['study','work','time off'].every(s=>labels.includes(s))||
    JOBS.some(j=>labels.includes(j.toLowerCase()));
}

export function recentJobChoices(history=[]) {
  return history.filter(h=>h.result==='changed'&&h.action==='click'&&JOBS.some(j=>j.toLowerCase()===String(h.target).toLowerCase()))
    .slice(-8).map(h=>({job:h.target,at:h.at||null,note:'Menu choice changed the screen; not proof of completed work or income.'}));
}

// Keep whole nearby paragraphs and bound context for the small local model.
export function selectGuideNotes(text,frame,budget=3500) {
  const terms=new Set(('health stress stamina constitution money gold rest recovery schedule '+
    visibleControls(frame).map(c=>c.label).join(' ')).toLowerCase().match(/[a-z]{3,}/g)||[]);
  const chunks=[];
  for(const paragraph of text.split(/\n\s*\n/))for(let n=0;n<paragraph.length;n+=900)chunks.push(paragraph.slice(n,n+900));
  const ranked=chunks.map((text,index)=>({text,index,score:(index===0?2:0)+[...new Set(text.toLowerCase().match(/[a-z]{3,}/g)||[])].filter(t=>terms.has(t)).length}))
    .sort((a,b)=>b.score-a.score||a.index-b.index);
  const picked=[];let remaining=budget;
  for(const chunk of ranked)if(chunk.text.length+2<=remaining){picked.push(chunk);remaining-=chunk.text.length+2;}
  return picked.sort((a,b)=>a.index-b.index).map(c=>c.text).join('\n\n');
}

export function createGuideStore({guidePath=defaultGuide,notesPath=defaultNotes}={}) {
  async function read() {
    const starter=decode(await readFile(guidePath));
    let notes='';try{notes=decode(await readFile(notesPath));}catch(e){if(e.code!=='ENOENT')throw e;}
    return {starter,notes};
  }
  return {
    async context(frame) {
      const {starter,notes}=await read();
      return {starterPath:guidePath,starter:selectGuideNotes(starter,frame,4500),starterCharacters:starter.length,notesPath:notes?notesPath:null,
        notesExcerpt:notes?selectGuideNotes(notes,frame):'',notesCharacters:notes.length};
    },
    async describe() {
      const {notes}=await read();
      return 'Starter guide: '+guidePath+'\n'+(notes?'Your notes: '+notesPath+' ('+notes.length+' characters)':'No extra notes loaded.')+
        '\nEdit the starter guide, or use guide load "C:\\path\\notes.md". Changes apply to the next strategy decision. guide clear removes only imported notes.';
    },
    async importFile(input) {
      const source=resolve(input.trim().replace(/^"(.*)"$/s,'$1').replace(/^'(.*)'$/s,'$1'));
      if(!['.txt','.md'].includes(extname(source).toLowerCase()))throw new Error('Choose a .txt or .md guide. Export PDF/web guides to text first.');
      const text=decode(await readFile(source));
      await mkdir(dirname(notesPath),{recursive:true});
      const temporary=notesPath+'.'+randomUUID()+'.tmp';
      try{await writeFile(temporary,text,'utf8');await rename(temporary,notesPath);}
      finally{await unlink(temporary).catch(e=>{if(e.code!=='ENOENT')throw e;});}
      return 'Loaded '+text.length+' characters of local strategy notes. Relevant excerpts will accompany the next strategy decision; nothing is uploaded.';
    },
    async clear(){await unlink(notesPath).catch(e=>{if(e.code!=='ENOENT')throw e;});return 'Imported notes cleared. The starter guide remains active.';}
  };
}
