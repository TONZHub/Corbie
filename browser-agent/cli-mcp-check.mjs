// Harmless end-to-end check: actual SDK transports, Edge, and the installed local tool model.
import {createServer} from 'node:http';
import {dirname,join} from 'node:path';
import {fileURLToPath} from 'node:url';
import assert from 'node:assert/strict';
import {createCliMcp} from './cli-mcp.mjs';

const here=dirname(fileURLToPath(import.meta.url));
let changed=false;
const server=createServer((request,response)=>{
  response.writeHead(200,{'Content-Type':'text/html; charset=utf-8'});
  const clue=changed?'The red berry is beside the fence.':'The blue acorn is under the oak tree.';
  if(request.url==='/state') return response.end(clue);
  response.end('<title>Corbie MCP browser check</title><h1>Corbie MCP browser check</h1><p id="clue">'+clue+'</p><script>setInterval(async()=>{document.getElementById("clue").textContent=await (await fetch("/state")).text()},200)</script>');
});
await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
const url=`http://127.0.0.1:${server.address().port}/`;
const deadline=new AbortController();
const timer=setTimeout(()=>deadline.abort(),180000);
const host=createCliMcp({onProgress:console.log,confirm:async()=>false,
  env:{CORBIE_BROWSER_HEADLESS:'1',LOCALAPPDATA:join(here,'cli-mcp-check-data'),CORBIE_BROWSER_PROFILE:join(here,'cli-mcp-check-profile')}});
try {
  const first=await host.run(`Open ${url} and tell me where the blue acorn is. Read only; do not click or change anything.`,{postmarkEnabled:false,signal:deadline.signal});
  console.log('First:',first);
  assert.match(first,/oak tree/i);
  changed=true;
  await new Promise(resolve=>setTimeout(resolve,700));
  const continued=await host.run('The page has changed. Tell me which object is now beside the fence.',{postmarkEnabled:false,resume:true,signal:deadline.signal});
  console.log('Continued:',continued);
  assert.match(continued,/red berry/i);
  console.log('PASS: actual MCP browser navigation, local tool selection, page reading, retained browser, and continuation.');
} finally {
  clearTimeout(timer);
  await host.close();
  server.closeAllConnections();
  await new Promise(resolve=>server.close(resolve));
}
