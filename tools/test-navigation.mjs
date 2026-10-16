import fs from 'node:fs';import path from 'node:path';import assert from 'node:assert/strict';import {spawn} from 'node:child_process';import {fileURLToPath} from 'node:url';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
const {normalizeRooms}=await import('../apps/curator-admin/rooms.mjs');
const {navigationFor,checkReachable}=await import('../apps/curator-admin/navigation.mjs');
const fixture=path.join(root,'logs','navigation-tests-'+Date.now());fs.mkdirSync(fixture,{recursive:true});
const refs=['sample-books','sample-vase','sample-sculpture'].map(id=>`artifacts/${id}/artifact.json`);
for(const id of ['sample-books','sample-vase','sample-sculpture'])fs.cpSync(path.join(root,'museum-archive/artifacts',id),path.join(fixture,'artifacts',id),{recursive:true});
const data={schemaVersion:'1.0',artifacts:refs,rooms:[{id:'room-a',title:'순차 관람 시험',artifacts:refs.slice(0,2),cells:[0,29]},{id:'empty',title:'빈 연결 방',artifacts:[],cells:[]},{id:'room-b',title:'마지막 방',artifacts:refs.slice(2),cells:[14]}]};
normalizeRooms(data);data.roomOrder=['room-a','empty','room-b'];
for(const room of data.rooms)room.navigation=navigationFor(room);
data.rooms[0].navigation={entrance:{side:'front',slot:2},exit:{side:'back',slot:2},blockedCells:[6,7,8,9,10,19,20,21,22,23],visitOrder:[refs[1],refs[0]]};
normalizeRooms(data);
fs.writeFileSync(path.join(fixture,'manifest.json'),JSON.stringify(data,null,2));
let passed=0;const pass=s=>{passed++;console.log('PASS: '+s);};pass('serpentine maze and reversed exhibit order accepted');
const contracts={valid:{legacy:d=>{delete d.roomOrder;for(const r of d.rooms)delete r.navigation;},maze:()=>{}},invalid:{
 'door-overlap':d=>d.rooms[0].navigation.exit={...d.rooms[0].navigation.entrance},
 'door-side':d=>d.rooms[0].navigation.exit.side='ceiling',
 'door-slot':d=>d.rooms[0].navigation.exit.slot=6,
 'duplicate-block':d=>d.rooms[0].navigation.blockedCells.push(6),
 'occupied-block':d=>d.rooms[0].navigation.blockedCells.push(0),
 'outside-block':d=>d.rooms[0].navigation.blockedCells.push(30),
 'missing-exhibit':d=>d.rooms[0].navigation.visitOrder.pop(),
 'duplicate-exhibit':d=>d.rooms[0].navigation.visitOrder[1]=d.rooms[0].navigation.visitOrder[0],
 'duplicate-room':d=>d.roomOrder[1]='room-a',
 'missing-room':d=>d.roomOrder.pop(),
 'unknown-room':d=>d.roomOrder[1]='absent',
 'blocked-exit':d=>d.rooms[0].navigation.blockedCells.push(26),
 'sealed-maze':d=>d.rooms[0].navigation.blockedCells.push(11)
}};
for(const [kind,cases] of Object.entries(contracts))for(const [name,change] of Object.entries(cases)){const d=structuredClone(data);change(d);if(kind==='valid')normalizeRooms(d);else assert.throws(()=>normalizeRooms(d));const folder=path.join(fixture,'route-contract-'+kind,name);fs.mkdirSync(folder,{recursive:true});fs.writeFileSync(path.join(folder,'manifest.json'),JSON.stringify(d));pass('contract '+kind+' '+name);}
let server,url,state;
async function read(){const r=await fetch(url+'/api/state');assert.equal(r.status,200);state=await r.json();return state;}
async function post(meta,status=200,token=state.token){const r=await fetch(url+'/api/navigation',{method:'POST',headers:{'Content-Type':'application/json','X-Museum-Token':token},body:JSON.stringify({roomId:'room-a',revision:state.revision,roomOrder:state.roomOrder,navigation:state.rooms[0].navigation,...meta})});assert.equal(r.status,status,await r.clone().text());}
try{
 server=spawn(process.execPath,[path.join(root,'apps/curator-admin/server.mjs'),'--port','0','--archive',fixture],{windowsHide:true});url=await new Promise((resolve,reject)=>{const timer=setTimeout(()=>reject(Error('server timeout')),10000);server.once('error',reject);server.stdout.on('data',b=>{const m=b.toString().match(/CURATOR_READY (http:\/\/127.0.0.1:\d+)/);if(m){clearTimeout(timer);resolve(m[1]);}});});await read();
 assert.deepEqual(state.roomOrder,data.roomOrder);assert.deepEqual(state.rooms[0].navigation.visitOrder,[refs[1],refs[0]]);pass('API exposes order and door positions');
 const before=fs.readFileSync(path.join(fixture,'manifest.json'));await post({},403,'wrong');assert.deepEqual(before,fs.readFileSync(path.join(fixture,'manifest.json')));pass('authorization protects settings');
 const unchanged=fs.readFileSync(path.join(fixture,'manifest.json'));
 for(const [name,body,headers,status] of [
  ['wrong content type','{}',{'Content-Type':'text/plain'},415],
  ['malformed JSON','{',{},400],
  ['oversized JSON',' '.repeat(262145),{},413],
  ['foreign origin','{}',{'Origin':'https://example.invalid'},403]
 ]){const r=await fetch(url+'/api/navigation',{method:'POST',headers:{'Content-Type':'application/json','X-Museum-Token':state.token,...headers},body});assert.equal(r.status,status);assert.deepEqual(unchanged,fs.readFileSync(path.join(fixture,'manifest.json')));pass(name+' rejected without writes');}
 await post({navigation:{...state.rooms[0].navigation,extra:true}},409);assert.deepEqual(unchanged,fs.readFileSync(path.join(fixture,'manifest.json')));pass('unknown navigation fields rejected');
 const stale=state.revision;await post({navigation:{...state.rooms[0].navigation,exit:{side:'back',slot:3}}});await read();assert.equal(state.rooms[0].navigation.exit.slot,3);assert.ok(fs.readdirSync(path.join(fixture,'.admin-backups')).some(n=>n.startsWith('manifest-')));pass('save persists doors and creates backup');
 await post({revision:stale},409);pass('stale revision rejected');
 const saved=fs.readFileSync(path.join(fixture,'manifest.json'));await post({navigation:{...state.rooms[0].navigation,blockedCells:[...state.rooms[0].navigation.blockedCells,11]}},409);assert.deepEqual(saved,fs.readFileSync(path.join(fixture,'manifest.json')));pass('unreachable exit rejected without changing archive');
 await post({roomOrder:['room-b','room-a','empty']});await read();assert.deepEqual(state.roomOrder,['room-b','room-a','empty']);pass('museum entry room and next-room order configurable');
 await post({roomOrder:data.roomOrder,navigation:data.rooms[0].navigation});await read();
 for(const asset of ['/navigation-editor.js','/app.js'])assert.equal((await fetch(url+asset)).status,200);pass('editor assets served');
 const shutdown=await fetch(url+'/api/shutdown',{method:'POST',headers:{'Content-Type':'application/octet-stream','X-Museum-Token':state.token,'X-Museum-Metadata':'{}'},body:new Uint8Array()});assert.equal(shutdown.status,200);
 await new Promise(resolve=>server.exitCode!==null?resolve():server.once('exit',resolve));
 assert.deepEqual(JSON.parse(fs.readFileSync(path.join(fixture,'manifest.json'))).rooms[0].navigation,data.rooms[0].navigation);pass('reloaded disk preserves full route');
 fs.writeFileSync(path.join(root,'logs/navigation-fixture-path.txt'),fixture);
 console.log(`NAVIGATION_TESTS_PASS: ${passed} cases | ${fixture}`);
}finally{if(server&&server.exitCode===null)server.kill();}
