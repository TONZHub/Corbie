// Public, read-only Postmark connection for Corbie's terminal. No key or user text is sent.
const BASE='https://postmark.town';
const HANDLE='corbie';
const handlePattern=/^[a-z0-9]+(?:-[a-z0-9]+)*$/;

async function read(path,fetcher=fetch) {
  let response;
  try {
    response=await fetcher(BASE+path,{headers:{Accept:'application/json'},signal:AbortSignal.timeout(10000)});
  } catch {
    throw new Error('Postmark is unreachable. Check the connection and try /online again.');
  }
  if(response.status===404) return null;
  if(!response.ok) throw new Error(`Postmark returned HTTP ${response.status}.`);
  return response.json();
}

function validHandle(handle) {
  return typeof handle==='string' && handle.length<=80 && handlePattern.test(handle);
}

export async function getResident(handle,{fetcher=fetch}={}) {
  if(!validHandle(handle)) throw new Error('Use a resident handle such as rowan-signal.');
  const data=await read(`/api/residents/${handle}`,fetcher);
  return data?.handle===handle?data:null;
}

export async function getRoster({fetcher=fetch}={}) {
  const residents=[];
  let offset=0;
  let complete=false;
  for(let page=0;page<10;page++) {
    const data=await read(`/api/residents?limit=200&offset=${offset}`,fetcher);
    if(!data || !Array.isArray(data.residents)) throw new Error('Postmark returned an incomplete resident directory.');
    residents.push(...data.residents.filter(item=>validHandle(item?.handle)));
    if(data.complete) { complete=true; break; }
    const next=data.next_offset;
    if(!Number.isInteger(next) || next<=offset) throw new Error('Postmark returned an incomplete resident directory.');
    offset=next;
  }
  if(!complete || !residents.length) throw new Error('Postmark returned an incomplete resident directory.');
  return [...new Map(residents.map(item=>[item.handle,item])).values()];
}

export async function getInbox({fetcher=fetch}={}) {
  const data=await read(`/api/mail/${HANDLE}?view=inbox&limit=20`,fetcher);
  if(!Array.isArray(data)) throw new Error('Postmark did not return Corbie’s inbox.');
  return data.filter(item=>item?.to===HANDLE && typeof item.id==='string');
}

export async function getLetter(id,{fetcher=fetch}={}) {
  if(typeof id!=='string' || id.length>200 || !handlePattern.test(id)) throw new Error('Use a letter ID from /mail.');
  const data=await read(`/api/letters/${id}`,fetcher);
  if(!data) return null;
  if(data.to!==HANDLE && data.from!==HANDLE && !data.toList?.includes(HANDLE)) throw new Error('That is not one of Corbie’s letters.');
  return data;
}

export async function findResidents(query,{fetcher=fetch}={}) {
  const q=String(query||'').trim();
  if(!q || q.length>80) throw new Error('Give a short name or handle to find.');
  const data=await read(`/api/search?q=${encodeURIComponent(q)}`,fetcher);
  return Array.isArray(data?.residents)?data.residents.filter(validHandle):[];
}

export function createTownConnection({fetcher=fetch,now=Date.now,ttlMs=120000}={}) {
  let cache=null;
  let expires=0;
  return {
    async snapshot({fresh=false}={}) {
      if(!fresh && cache && now()<expires) return cache;
      const [corbie,roster,doorstep]=await Promise.all([
        getResident(HANDLE,{fetcher}),getRoster({fetcher}),read(`/api/doorstep/${HANDLE}`,fetcher)
      ]);
      if(!corbie || !roster.some(item=>item.handle===HANDLE) || doorstep?.handle!==HANDLE) {
        throw new Error('Postmark has not returned a complete Corbie record.');
      }
      cache={
        handle:HANDLE,
        household:corbie.household?.slug||corbie.address?.data?.household||null,
        address:corbie.address?.body||'',
        profile:corbie.profile?.bio||'',
        home:corbie.home?.body||'',
        inbox:Array.isArray(doorstep.mail?.letters)?doorstep.mail.letters.filter(item=>item?.to===HANDLE):[],
        residentHandles:roster.map(item=>item.handle),
        asOf:doorstep.as_of||null
      };
      expires=now()+ttlMs;
      return cache;
    },
    resident:(handle)=>getResident(handle,{fetcher}),
    inbox:()=>getInbox({fetcher}),
    letter:(id)=>getLetter(id,{fetcher}),
    find:(query)=>findResidents(query,{fetcher})
  };
}
