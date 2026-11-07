import {open} from 'node:fs/promises';
import {resolve,basename,extname} from 'node:path';

export const MAX_IMAGE_BYTES=10*1024*1024;
export const DEFAULT_IMAGE_QUESTION='What do you notice in this picture?';

// Quoted paths also cover Windows terminal drag-and-drop and spaces in names.
export function parseImageInput(input) {
  const text=String(input||'').trim();
  const command=/^\/image(?:\s|$)/i.test(text);
  const value=command?text.slice(6).trim():text;
  if(!value) return command?{path:'',question:''}:null;
  let path,question;
  if(value[0]==='"' || value[0]==="'") {
    const end=value.indexOf(value[0],1);
    if(end<0) { if(command) throw new Error('Close the quotes around the image path.'); return null; }
    path=value.slice(1,end); question=value.slice(end+1).trim();
  } else {
    // A bare path can contain spaces; a question after it needs a quoted path.
    path=value; question='';
  }
  if(!command && !/\.(png|jpe?g|webp)$/i.test(path)) return null;
  if(!command && question) return null;
  return {path,question};
}

export async function readLocalImage(path) {
  if(!path || /^https?:\/\//i.test(path) || /^file:/i.test(path) || path.startsWith('\\\\') || path.startsWith('//')) {
    throw new Error('Choose a picture saved on this PC: PNG, JPG or WebP. Paste its local file path.');
  }
  if(!/\.(png|jpe?g|webp)$/i.test(path)) throw new Error('Use a PNG, JPG or WebP picture.');
  const full=resolve(path);
  let file;
  try {
    file=await open(full,'r');
    const info=await file.stat();
    if(!info.isFile()) throw new Error('That path is not a picture file.');
    if(!info.size || info.size>MAX_IMAGE_BYTES) throw new Error('Choose a non-empty picture smaller than 10 MB.');
    const data=await file.readFile();
    if(!data.length || data.length>MAX_IMAGE_BYTES) throw new Error('Choose a non-empty picture smaller than 10 MB.');
    const png=data.length>=33 && data.subarray(0,8).equals(Buffer.from([137,80,78,71,13,10,26,10])) && data.toString('ascii',12,16)==='IHDR';
    const jpeg=data.length>=4 && data[0]===255 && data[1]===216 && data[2]===255;
    const webp=data.length>=20 && data.toString('ascii',0,4)==='RIFF' && data.toString('ascii',8,12)==='WEBP';
    const extension=extname(path).toLowerCase();
    if(!(extension==='.png'?png:extension==='.webp'?webp:jpeg)) throw new Error('That file does not contain the expected image format. Try exporting it as PNG or JPG.');
    return {name:basename(full),base64:data.toString('base64')};
  } catch(error) {
    if(error.code==='ENOENT') throw new Error('I cannot find that picture. Paste its full file path, or drag it into the terminal.');
    if(error.code==='EACCES' || error.code==='EPERM') throw new Error('I cannot read that picture. Choose a file you can open on this PC.');
    throw error;
  } finally { await file?.close(); }
}
