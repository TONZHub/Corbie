import {createServer} from 'node:http';
import {spawn} from 'node:child_process';
import {createInterface} from 'node:readline';
import {once} from 'node:events';
import {join,dirname} from 'node:path';
import {fileURLToPath} from 'node:url';

const here=dirname(fileURLToPath(import.meta.url));
const products=[['Moon lamp',28],['Brass compass',22],['Tiny desk fan',17],['Ceramic crow mug',19],['Pocket notebook',9]];
const server=createServer((request,response)=>{
  response.writeHead(200,{'Content-Type':'text/html; charset=utf-8'});
  const id=new URL(request.url,'http://localhost').searchParams.get('p');
  if(id!==null && products[Number(id)]) {
    const [name,price]=products[Number(id)];
    return response.end(`<title>${name}</title><h1>${name}</h1><p>Price: $${price}.00</p><p>In stock.</p><a href="/">Back to catalog</a>`);
  }
  response.end('<title>Mock Market</title><h1>Mock Market</h1><p>Choose anything you like. No checkout in this test.</p>'+products.map(([name,price],i)=>`<p><a href="/?p=${i}">${name}</a> — $${price}.00</p>`).join(''));
});
server.listen(0,'127.0.0.1');await once(server,'listening');
const url=`http://127.0.0.1:${server.address().port}/`;
const child=spawn(process.execPath,[join(here,'agent.mjs')],{
  cwd:here,env:{...process.env,CORBIE_BROWSER_HEADLESS:'1',CORBIE_BROWSER_PROFILE:join(here,'shopping-test-profile'),CORBIE_BROWSER_LOCALDATA:join(here,'test-localdata')},stdio:['pipe','pipe','pipe']
});
const lines=createInterface({input:child.stdout});
let result='',error='',trace=[];
child.stderr.on('data',chunk=>{error=(error+chunk.toString()).slice(-1000);});
const timeout=setTimeout(()=>{child.stdin.write('{"type":"cancel"}\n');console.error('Shopping check timeout',trace,error);},150000);
child.stdin.write(JSON.stringify({type:'run',task:`At ${url}, pick whatever items you want, up to $100 total. You decide. Show me specific picks and prices; do not place an order.`})+'\n');
for await(const line of lines) {
  const data=JSON.parse(line);trace.push(data);
  if(data.type==='approval') child.stdin.write(JSON.stringify({type:'approve',id:data.id,yes:true})+'\n');
  if(data.type==='waiting'||data.type==='error') {result=data.message;child.stdin.write('{"type":"cancel"}\n');break;}
}
clearTimeout(timeout);
if(child.exitCode===null) await Promise.race([once(child,'exit'),new Promise((_,reject)=>setTimeout(()=>reject(new Error('Shop browser did not close')),10000))]);
server.close();
console.log(JSON.stringify({result,steps:trace.filter(x=>x.type==='status').map(x=>x.message),error}));
const linkedPicks=products.filter(([name])=>result.toLowerCase().includes(name.toLowerCase()) && result.includes(`${url}?p=${products.findIndex(item=>item[0]===name)}`));
if(!trace.some(x=>x.type==='waiting') || linkedPicks.length<2 || /what (kind|type|category)|clarif|what would you like|\baye\b|raven pride/i.test(result) || /(?:total|subtotal|that.s)\D*\$(\d+(?:\.\d{1,2})?)/i.exec(result)?.[1]>100) process.exitCode=1;
