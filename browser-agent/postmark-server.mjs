// Public records plus authenticated world movement; only an explicitly unlocked session receives a key.
import {Server} from '@modelcontextprotocol/sdk/server/index.js';
import {StdioServerTransport} from '@modelcontextprotocol/sdk/server/stdio.js';
import {ListToolsRequestSchema,CallToolRequestSchema} from '@modelcontextprotocol/sdk/types.js';
import Ajv from 'ajv';
import {fileURLToPath} from 'node:url';
import {createTownConnection} from '../postmark-live.mjs';
import {createWorldConnection} from '../corbie-world.mjs';

const string=(description,maxLength=80)=>({type:'string',minLength:1,maxLength,description});
const tool=(name,description,properties={},required=[])=>({name,description,
  inputSchema:{type:'object',properties,required,additionalProperties:false},
  annotations:{readOnlyHint:true,destructiveHint:false,openWorldHint:true}});
export const POSTMARK_TOOLS=[
  tool('postmark_overview','Read Corbie’s published address, home, household and delivered-mail summaries.'),
  tool('postmark_directory','Read a page of verified resident handles and profile links.',{offset:{type:'integer',minimum:0,maximum:100000},limit:{type:'integer',minimum:1,maximum:30}}),
  tool('postmark_find_residents','Find residents by name or handle in the public town directory.',{query:string('Name or handle to search')},['query']),
  tool('postmark_resident','Read a verified resident’s published profile and address.',{handle:string('Exact resident handle')},['handle']),
  tool('postmark_inbox','Read the delivered letters listed in Corbie’s public inbox.'),
  tool('postmark_read_letter','Read a letter to or from Corbie by its exact ID.',{id:string('A verified letter ID',200)},['id']),
  tool('postmark_world_status','Read Corbie’s actual world position, destination, remaining distance and whether he is still travelling.'),
  tool('postmark_world_find','Find real places by name or exact mark ID before travelling.',{query:string('Place name or exact mark ID',200)},['query']),
  tool('postmark_world_look','Look around Corbie’s actual position, read nearby places and available movement actions. Optional mark focus.',{mark_id:string('Exact verified mark ID',200)}),
  tool('postmark_world_identity','Verify that the session key authorizes Corbie; returns no secret.'),
  ...[
    tool('postmark_world_walk','Record a real walk. Empty arguments walk home; otherwise choose a verified mark_id OR both to_x and to_y. Never claim arrival from a departure receipt.',{mark_id:string('Destination mark ID from a world search',200),to_x:{type:'number'},to_y:{type:'number'},mode:{type:'string',enum:['rim','center']},exit:{type:'boolean'}}),
    tool('postmark_world_enter','Enter a verified mark only when at its threshold. Read refusal terms first. accept=true requires human review.',{mark_id:string('Exact mark ID',200),accept:{type:'boolean'}},['mark_id']),
    tool('postmark_world_exit','Step out of the place Corbie currently occupies.'),
    tool('postmark_world_ride','When aboard a vehicle, travel to a verified stop in its timetable. Search first; the office checks whether riding is possible here.',{to:string('Exact destination stop ID',200)},['to'])
  ].map(t=>({...t,annotations:{...t.annotations,readOnlyHint:false}}))
];

export function createPostmarkServer({town=createTownConnection(),world=createWorldConnection({key:process.env.CORBIE_WORLD_ENABLED==='1'?process.env.CORBIE_POSTMARK_KEY||'':''})}={}) {
  const server=new Server({name:'corbie-postmark-public',version:'1.0.0'},{capabilities:{tools:{}}});
  const ajv=new Ajv({strict:false});
  const validators=new Map(POSTMARK_TOOLS.map(tool=>[tool.name,ajv.compile(tool.inputSchema)]));
  server.setRequestHandler(ListToolsRequestSchema,async()=>({tools:POSTMARK_TOOLS}));
  server.setRequestHandler(CallToolRequestSchema,async({params})=>{
    const validate=validators.get(params.name),args=params.arguments||{};
    if(!validate || !validate(args)) return {isError:true,content:[{type:'text',text:'Unknown Postmark tool or invalid arguments. No action was performed.'}]};
    try {
      let data;
      if(params.name.startsWith('postmark_world_')) {
        const action=params.name.slice('postmark_world_'.length);
        if(action==='status' || action==='identity' || action==='exit') data=await world[action]();
        else if(action==='find') data=await world.find(args.query);
        else if(action==='look') data=await world.look(args.mark_id);
        else data=await world[action](args);
      } else if(params.name==='postmark_overview') {
        const {residentHandles,...record}=await town.snapshot({fresh:true});
        data={...record,residentCount:residentHandles.length,page:'https://postmark.town/residents/corbie/'};
      } else if(params.name==='postmark_directory') {
        const snapshot=await town.snapshot();
        const offset=args.offset||0,limit=args.limit||15;
        const handles=snapshot.residentHandles.slice(offset,offset+limit);
        data={asOf:snapshot.asOf,total:snapshot.residentHandles.length,residents:handles.map(handle=>({handle,page:`https://postmark.town/residents/${handle}/`})),
          nextOffset:offset+limit<snapshot.residentHandles.length?offset+limit:null};
      } else if(params.name==='postmark_find_residents') data=await town.find(args.query);
      else if(params.name==='postmark_resident') data=await town.resident(args.handle);
      else if(params.name==='postmark_inbox') data=await town.inbox();
      else data=await town.letter(args.id);
      return {content:[{type:'text',text:JSON.stringify(data??{missing:true})}]};
    } catch(error) { return {isError:true,content:[{type:'text',text:error.message}]}; }
  });
  return server;
}

if(process.argv[1] && fileURLToPath(import.meta.url)===process.argv[1]) {
  const server=createPostmarkServer();
  await server.connect(new StdioServerTransport());
  process.stdin.on('end',async()=>{await server.close();});
}
