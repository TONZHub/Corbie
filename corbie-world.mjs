// Postmark movement is recorded by the office. The key never goes to the local model.
import {randomUUID} from 'node:crypto';

const BASE='https://postmark.town';
const HANDLE='corbie';
const markPattern=/^[a-z0-9][a-z0-9_-]*\/[a-z0-9][a-z0-9_-]*$/;
export function validMark(id) { return typeof id==='string' && id.length<=200 && markPattern.test(id); }
export function validateWalk(args={}) {
  if(!args || typeof args!=='object' || Array.isArray(args)) throw new Error('Give a walk destination.');
  if(Object.keys(args).some(key=>!['mark_id','to_x','to_y','mode','exit'].includes(key))) throw new Error('Unknown walk field.');
  const mark=args.mark_id!==undefined,point=args.to_x!==undefined || args.to_y!==undefined;
  if(mark && (!validMark(args.mark_id) || point)) throw new Error('Choose one destination: a verified mark ID or a coordinate pair.');
  if(point && (!Number.isFinite(args.to_x) || !Number.isFinite(args.to_y) || args.mode!==undefined)) throw new Error('Walking to coordinates needs both numbers, without a mark mode.');
  if(args.mode!==undefined && (!mark || !['rim','center'].includes(args.mode))) throw new Error('A mark mode must be rim or center.');
  if(args.exit!==undefined && typeof args.exit!=='boolean') throw new Error('exit must be true or false.');
  return args;
}

function holdsCorbie(identity) {
  return [identity?.handle,...(Array.isArray(identity?.handles)?identity.handles:[]),
    ...(Array.isArray(identity?.residents)?identity.residents.map(r=>typeof r==='string'?r:r?.handle):[])].includes(HANDLE);
}
function compactView(data) {
  const ids=new Set([...(data.within||[]).map(m=>m.id),...(data.nearby||[]).slice(0,10).map(m=>m.id)]);
  if(data.focus?.id) ids.add(data.focus.id);
  return {standpoint:data.standpoint,crossing:data.crossing,
    within:data.within,nearby:(data.nearby||[]).slice(0,10),present:data.present,
    actions:(data.actions||[]).filter(a=>['walk','enter','exit','ride'].includes(a.action)),
    records:Object.fromEntries(Object.entries(data.records||{}).filter(([id])=>ids.has(id)).map(([id,r])=>[id,{id,at:r.at,body:String(r.body||'').slice(0,1200),class:r.class,kind:r.kind}])),
    focus:data.focus,disclosed:data.disclosed};
}

export function createWorldConnection({key='',fetcher=fetch,nonce=randomUUID}={}) {
  async function request(path,{auth=false,method='GET',body}={}) {
    if(auth && !key) throw new Error('Movement needs Corbie’s key. Open Corbie-World.cmd and enter it privately; never paste a key into chat.');
    const headers={Accept:'application/json'};
    if(auth) headers.Authorization=`Bearer ${key}`;
    if(body) headers['Content-Type']='application/json';
    let response;
    try { response=await fetcher(BASE+path,{method,headers,redirect:'error',body:body?JSON.stringify(body):undefined,signal:AbortSignal.timeout(30000)}); }
    catch { throw new Error(method==='POST'?'The movement receipt could not be read. Check /where before retrying; the departure may have been recorded.':'Postmark’s world is unreachable. Try again when connected.'); }
    let data;
    try { data=await response.json(); } catch { throw new Error('Postmark returned an unreadable world response. Check /where before retrying a movement.'); }
    if(!response.ok || data?.error) {
      const detail=[data?.defect,data?.hint,data?.terms?'Threshold terms: '+JSON.stringify(data.terms):''].filter(x=>typeof x==='string').join(' ').slice(0,4000);
      const safe=key?detail.replaceAll(key,'[private key]'):detail;
      throw new Error(`Postmark world returned HTTP ${response.status}${safe?`: ${safe}`:'.'}`);
    }
    // Strip accidental credential echoes before any result can reach the model.
    return key?JSON.parse(JSON.stringify(data).replaceAll(key,'[private key]')):data;
  }
  async function identity() {
    const me=await request('/api/me',{auth:true});
    if(!holdsCorbie(me)) throw new Error('This key does not authorize Corbie. No movement was performed.');
    return {handle:HANDLE,connected:true};
  }
  async function find(query) {
    if(typeof query!=='string' || !query.trim() || query.length>200) throw new Error('Give a short place name or mark ID.');
    return request(`/api/world/find?q=${encodeURIComponent(query)}&limit=10`);
  }
  async function verifyMark(id) {
    if(!validMark(id)) throw new Error('Use an exact mark ID from a world search.');
    const result=await find(id);
    if(!result.hits?.some(hit=>hit.id===id)) throw new Error('That destination is not a verified world mark. Search for a place first.');
  }
  async function status() {
    const data=await request('/api/world/walkers');
    if(!Array.isArray(data?.walkers)) throw new Error('Postmark did not return its world positions.');
    const position=data.walkers.find(w=>w.handle===HANDLE)||null;
    return {handle:HANDLE,position,at:data.at,disclosed:data.disclosed,
      note:position?'Position read from the live world record. moving=true means travelling, not arrived.':'Corbie has no published position; do not assume he is at the Origin.'};
  }
  async function look(mark_id) {
    if(mark_id) await verifyMark(mark_id);
    let path,view;
    if(key) { await identity(); path='/api/world/apex?handle=corbie'; view='embodied'; }
    else {
      const {position}=await status();
      if(!position || !Number.isFinite(position.x) || !Number.isFinite(position.y)) throw new Error('Corbie’s position is unavailable. No view was invented.');
      path=`/api/world/apex?x=${position.x}&y=${position.y}`; view='spectator at Corbie’s published position';
    }
    if(mark_id) path+=`&mark=${encodeURIComponent(mark_id)}`;
    return {view,...compactView(await request(path,{auth:!!key}))};
  }
  async function act(action,args) {
    await identity(); // Check ownership before every write, including on a multi-resident key.
    const envelope={handle:HANDLE,do:action,args:{...args,nonce:'corbie-world-'+nonce()}};
    const receipt=await request('/api/world/apex',{auth:true,method:'POST',body:envelope});
    let position=null,statusWarning;
    try { position=(await status()).position; } catch(error) { statusWarning=error.message; }
    return {action,receipt,position,statusWarning,
      note:'Report only this receipt. A recorded departure is not arrival; entry and transport can be refused separately. If position is unavailable, do not invent it.'};
  }
  return {identity,status,find,look,
    async walk(args={}) { validateWalk(args); if(args.mark_id) await verifyMark(args.mark_id); return act('walk',args); },
    async enter({mark_id,accept=false}) { await verifyMark(mark_id); if(typeof accept!=='boolean') throw new Error('accept must be true or false.'); return act('enter',{mark:mark_id,...(accept?{accept:true}:{})}); },
    exit:()=>act('exit',{}),
    async ride({to}) { await verifyMark(to); return act('ride',{to}); }
  };
}
