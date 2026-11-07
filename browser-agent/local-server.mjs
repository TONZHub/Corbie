import { Server } from '@modelcontextprotocol/sdk/server/index.js';
import { StdioServerTransport } from '@modelcontextprotocol/sdk/server/stdio.js';
import { ListToolsRequestSchema, CallToolRequestSchema } from '@modelcontextprotocol/sdk/types.js';
import { chromium } from 'playwright';
import { join } from 'node:path';

const profile=process.env.CORBIE_BROWSER_PROFILE || join(process.env.LOCALAPPDATA||process.cwd(),'Corbie','browser-profile');
let context=null,selected=null,lastSnapshot='',lastDialog='';
const prop=(description,type='string')=>({type,description});
const tool=(name,description,properties={},required=[])=>({name,description,inputSchema:{type:'object',properties,required}});
const tools=[
  tool('browser_snapshot','Read the current page and get exact control references.'),
  tool('browser_navigate','Open a web page.',{url:prop('http or https URL')},['url']),
  tool('browser_navigate_back','Go back one page.'),
  tool('browser_tabs','List, open, select, or close a tab.',{action:prop('list, new, select, or close'),index:prop('Zero-based tab index','integer'),url:prop('Optional URL for new tab')},['action']),
  tool('browser_find','Find text in the last page snapshot.',{text:prop('Text to find')},['text']),
  tool('browser_click','Click a control from the current snapshot.',{target:prop('Exact c-number reference'),element:prop('Human-readable control description')},['target','element']),
  tool('browser_hover','Hover over a control from the current snapshot.',{target:prop('Exact c-number reference'),element:prop('Human-readable control description')},['target']),
  tool('browser_type','Type into a control from the current snapshot.',{target:prop('Exact c-number reference'),element:prop('Human-readable control description'),text:prop('Text to enter')},['target','element','text']),
  tool('browser_fill_form','Fill several controls from the current snapshot.',{fields:{type:'array',description:'Fields with target and value',items:{type:'object',properties:{target:prop('Exact c-number reference'),value:prop('Value')},required:['target','value']}}},['fields']),
  tool('browser_select_option','Choose a dropdown option.',{target:prop('Exact c-number reference'),element:prop('Human-readable control description'),values:{type:'array',items:{type:'string'}}},['target','element','values']),
  tool('browser_press_key','Press a key in the current page.',{key:prop('Playwright key name, such as Enter or Tab')},['key']),
  tool('browser_wait_for','Wait briefly for page text or a timed update.',{text:prop('Text to wait for'),time:prop('Milliseconds to wait','integer')})
];

async function openBrowser() {
  if(context) return context;
  context=await chromium.launchPersistentContext(profile,{
    channel:'msedge',headless:process.env.CORBIE_BROWSER_HEADLESS==='1',
    args:['--disable-gpu'],viewport:{width:1280,height:800},acceptDownloads:false
  });
  context.on('page',newPage=>{watchPage(newPage);selected=newPage;});
  for(const existingPage of context.pages()) {watchPage(existingPage);selected=existingPage;}
  return context;
}
function watchPage(page) {
  page.on('dialog',async dialog=>{
    lastDialog=`Browser dialog dismissed: ${dialog.message().slice(0,180)}`;
    try { await dialog.dismiss(); } catch {}
  });
}
async function page() {
  await openBrowser();
  if(selected && !selected.isClosed()) return selected;
  selected=context.pages().find(p=>!p.isClosed()) || await context.newPage();
  return selected;
}
function webUrl(raw) {
  const url=new URL(String(raw||''));
  if(url.protocol!=='http:' && url.protocol!=='https:') throw new Error('Only http and https pages can be opened.');
  return url.href;
}
function target(page,target) {
  if(!/^c\d+$/.test(String(target||''))) throw new Error('Use an exact c-number from the latest snapshot.');
  return page.locator(`[data-corbie-ref="${target}"]`);
}
async function snapshot() {
  const current=await page();
  let body='';
  try { body=(await current.locator('body').innerText({timeout:10000})).slice(0,6500); }
  catch { body='(Page text unavailable.)'; }
  const controls=await current.evaluate(()=>{
    document.querySelectorAll('[data-corbie-ref]').forEach(node=>node.removeAttribute('data-corbie-ref'));
    const nodes=[...document.querySelectorAll('a,button,input,textarea,select,[role="button"],[role="link"],[role="checkbox"],[role="menuitem"],[contenteditable="true"]')];
    const found=[];
    for(const node of nodes) {
      const box=node.getBoundingClientRect(),style=getComputedStyle(node);
      if(box.width<2||box.height<2||style.visibility==='hidden'||style.display==='none') continue;
      const ref=`c${found.length+1}`;node.setAttribute('data-corbie-ref',ref);
      const role=node.getAttribute('role')||node.tagName.toLowerCase();
      const name=(node.getAttribute('aria-label')||node.innerText||node.getAttribute('title')||node.getAttribute('placeholder')||node.getAttribute('value')||'').replace(/\s+/g,' ').trim().slice(0,120);
      const detail=node.tagName.toLowerCase()==='a'?node.getAttribute('href'):node.getAttribute('type');
      found.push(`${ref} ${role}: ${name}${detail?' ['+String(detail).slice(0,120)+']':''}${node.disabled?' (disabled)':''}`);
      if(found.length>=130) break;
    }
    return found;
  });
  lastSnapshot=`URL: ${current.url()}\nTitle: ${await current.title()}\n${lastDialog?lastDialog+'\n':''}\nPage text:\n${body}\n\nControls:\n${controls.join('\n')}`.slice(0,12000);
  return lastSnapshot;
}
async function act(name,args) {
  const current=await page();
  if(name==='browser_snapshot') return snapshot();
  if(name==='browser_navigate') { await current.goto(webUrl(args.url),{waitUntil:'domcontentloaded',timeout:30000}); return snapshot(); }
  if(name==='browser_navigate_back') { await current.goBack({waitUntil:'domcontentloaded',timeout:30000}); return snapshot(); }
  if(name==='browser_tabs') {
    const action=String(args.action||'');
    if(action==='list') return context.pages().map((p,i)=>`${i}: ${p.url()}${p===current?' (selected)':''}`).join('\n')||'(No tabs)';
    if(action==='new') { selected=await context.newPage();if(args.url) await selected.goto(webUrl(args.url),{waitUntil:'domcontentloaded',timeout:30000});return snapshot(); }
    const index=args.index===undefined&&action==='close'?context.pages().indexOf(current):Number(args.index);
    if(!Number.isInteger(index)||index<0||index>=context.pages().length) throw new Error('Choose a listed tab index.');
    if(action==='select') {selected=context.pages()[index];await selected.bringToFront();return snapshot();}
    if(action==='close') {await context.pages()[index].close();selected=null;return snapshot();}
    throw new Error('Tab action must be list, new, select, or close.');
  }
  if(name==='browser_find') return lastSnapshot.split('\n').filter(line=>line.toLowerCase().includes(String(args.text||'').toLowerCase())).slice(0,30).join('\n')||'No match; take a fresh snapshot if the page changed.';
  if(name==='browser_click') { await target(current,args.target).click({timeout:10000}); return snapshot(); }
  if(name==='browser_hover') { await target(current,args.target).hover({timeout:10000}); return snapshot(); }
  if(name==='browser_type') { await target(current,args.target).fill(String(args.text??''),{timeout:10000}); return snapshot(); }
  if(name==='browser_fill_form') {
    if(!Array.isArray(args.fields)||args.fields.length>12) throw new Error('Supply up to 12 fields.');
    for(const field of args.fields) await target(current,field.target).fill(String(field.value??''),{timeout:10000});
    return snapshot();
  }
  if(name==='browser_select_option') {await target(current,args.target).selectOption(args.values,{timeout:10000});return snapshot();}
  if(name==='browser_press_key') {await current.keyboard.press(String(args.key||''));return snapshot();}
  if(name==='browser_wait_for') {
    if(args.text) await current.getByText(String(args.text),{exact:false}).first().waitFor({timeout:10000});
    else await new Promise(resolve=>setTimeout(resolve,Math.min(10000,Math.max(0,Number(args.time)||1000))));
    return snapshot();
  }
  throw new Error('That browser action is unavailable.');
}

const server=new Server({name:'corbie-persistent-browser',version:'0.84.0'},{capabilities:{tools:{}}});
server.setRequestHandler(ListToolsRequestSchema,async()=>({tools}));
server.setRequestHandler(CallToolRequestSchema,async request=>{
  const name=request.params.name,args=request.params.arguments||{};
  if(!tools.some(tool=>tool.name===name)) return {content:[{type:'text',text:'Unknown browser action.'}],isError:true};
  try {return {content:[{type:'text',text:await act(name,args)}]};}
  catch(error) {return {content:[{type:'text',text:String(error.message||error).slice(0,800)}],isError:true};}
});
const transport=new StdioServerTransport();
await server.connect(transport);
process.stdin.on('end',async()=>{try{await context?.close();}catch{}process.exit(0);});
