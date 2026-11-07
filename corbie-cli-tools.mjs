// Lazy loading keeps ordinary local chat usable if the optional browser dependencies are missing.
export function wantsCliTools(text) {
  if(/^(?:(?:hey\s+)?corbie[,! ]+)?(?:(?:can|could|would)\s+you\s+|please\s+)?(?:walk\b|wander\b|explore\b|travel\b|head\s+(?:to|home)\b|go\s+home\b|go\s+(?:to|visit)\b|visit\b(?!\s+https?:)|enter\b|step\s+(?:inside|outside)\b|ride\b|look\s+around\b|where\s+are\s+(?:you|we)\b)/i.test(text)) return true;
  return /^(?:(?:hey\s+)?corbie[,! ]+)?(?:(?:can|could|would)\s+you\s+|please\s+)?(?:use\s+(?:your\s+)?(?:mcp|tools)\b|browse\b|search\s+(?:the\s+)?(?:web|internet)\b|(?:open|visit|go\s+to)\s+https?:\/\/|(?:check|look\s+up)\b.*\b(?:online|on\s+the\s+web)\b)/i.test(text);
}

export function createCliTools(options={}) {
  let host;
  async function get() {
    if(!host) {
      try { const {createCliMcp}=await import('./browser-agent/cli-mcp.mjs'); host=createCliMcp(options); }
      catch { throw new Error('Corbie’s MCP dependencies are unavailable. Keep the browser-agent folder and its installed dependencies beside the CLI. Ordinary chat still works.'); }
    }
    return host;
  }
  return {
    async list(options) { return (await get()).list(options); },
    async run(task,options) { return (await get()).run(task,options); },
    async call(name,args,options) { return (await get()).call(name,args,options); },
    async close() { if(host) await host.close(); host=null; }
  };
}
