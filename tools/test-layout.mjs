import fs from 'node:fs';
import path from 'node:path';
import assert from 'node:assert/strict';
import {spawn} from 'node:child_process';
import {fileURLToPath} from 'node:url';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
const fixture=path.join(root,'logs',`layout-tests-${Date.now()}`);
fs.cpSync(path.join(root,'museum-archive'),fixture,{recursive:true,filter:p=>path.basename(p)!=='.admin-backups'});
const file=path.join(fixture,'manifest.json'),refs=['sample-vase','sample-books','sample-sculpture'].map(id=>`artifacts/${id}/artifact.json`);
const original=fs.readFileSync(path.join(root,'museum-archive/manifest.json'));
fs.writeFileSync(file,JSON.stringify({schemaVersion:'1.0',artifacts:refs,rooms:[{id:'room-a',title:'격자 시험실',artifacts:refs.slice(0,2),theme:{floor:'#123456'}},{id:'room-b',title:'이동 시험실',artifacts:refs.slice(2)}]}));
let server,url,state,passed=0;
async function start(){server=spawn(process.execPath,[path.join(root,'apps/curator-admin/server.mjs'),'--port','0','--archive',fixture],{windowsHide:true});url=await new Promise((resolve,reject)=>{const timer=setTimeout(()=>reject(Error('timeout')),10000);server.stdout.on('data',b=>{const m=b.toString().match(/CURATOR_READY (http:\/\/127.0.0.1:\d+)/);if(m){clearTimeout(timer);resolve(m[1]);}});server.once('error',reject);});await read();}
async function read(){state=await(await fetch(url+'/api/state')).json();return state;}
async function post(route,meta,expected=200){const r=await fetch(url+'/api/'+route,{method:'POST',headers:{'Content-Type':'application/octet-stream','X-Museum-Token':state.token,'X-Museum-Metadata':encodeURIComponent(JSON.stringify({revision:state.revision,...meta}))},body:new Uint8Array()});assert.equal(r.status,expected,await r.text());}
function pass(name){console.log('PASS: '+name);passed++;}
async function stop(){await post('shutdown',{});await new Promise(resolve=>{if(server.exitCode!==null)resolve();else server.once('exit',resolve);});}
try{
 await start();assert.deepEqual(state.rooms[0].cells,[0,1]);pass('legacy cells assigned without modifying files');
 const entries=[{entry:refs[0],cell:29},{entry:refs[1],cell:7}];
 await post('layout',{roomId:'room-a',entries});await read();assert.deepEqual(state.rooms[0].cells,[29,7]);assert.equal(state.rooms[0].theme.floor,'#123456');pass('sparse corner placement preserves theme');
 const saved=fs.readFileSync(file);
 for(const cells of [[7,7],[-1,7],[30,7],[1.5,7]])await post('layout',{roomId:'room-a',entries:entries.map((e,i)=>({...e,cell:cells[i]}))},409);
 assert.deepEqual(fs.readFileSync(file),saved);pass('overlap and invalid coordinates rejected without writes');
 await post('layout',{roomId:'room-a',entries:[entries[0],entries[0]]},409);pass('duplicate item rejected');
 const stale=state.revision;await post('layout',{roomId:'room-a',entries:[{entry:refs[0],cell:7},{entry:refs[1],cell:29}]});await read();
 await post('layout',{revision:stale,roomId:'room-a',entries},409);pass('swap and stale-tab protection');
 const book=state.artifacts.find(a=>a.id==='sample-books');
 await post('edit',{artifactId:book.id,itemRevision:book.itemRevision,title:'새 책 이름',description:'새 설명 <b>그대로</b>',scale:1.25});await read();assert.equal(state.artifacts.find(a=>a.id===book.id).title,'새 책 이름');
 await post('edit',{artifactId:book.id,itemRevision:book.itemRevision,title:'오래된 창',description:'덮어쓰기',scale:1},409);pass('metadata edit and stale item protection');
 await post('move',{artifactId:book.id,roomId:'room-b',cell:0},409);
 await post('move',{artifactId:book.id,roomId:'room-b',cell:18});await read();assert.equal(state.artifacts.find(a=>a.id===book.id).cell,18);assert.deepEqual(state.rooms[0].cells,[7]);pass('cross-room move to chosen empty cell');
 await post('trash',{artifactId:book.id});await read();assert.equal(state.trash.length,1);assert.ok(fs.existsSync(path.join(fixture,refs[1])));pass('soft delete preserves model and metadata');
 await post('restore',{artifactId:book.id,roomId:'room-b',cell:0},409);
 await post('restore',{artifactId:book.id,roomId:'room-a',cell:29});await read();assert.equal(state.trash.length,0);assert.equal(state.artifacts.find(a=>a.id===book.id).cell,29);pass('restore to specified vacant cell');
 for(const item of [...state.artifacts]){await post('trash',{artifactId:item.id});await read();}
 assert.equal(state.artifacts.length,0);assert.equal(state.trash.length,3);pass('deleting last exhibit leaves valid empty museum');
 fs.cpSync(fixture,fixture+'-empty',{recursive:true});
 await post('restore',{artifactId:'sample-vase',roomId:'room-a',cell:29});await read();
 await stop();await start();assert.equal(state.artifacts[0].cell,29);assert.equal(state.trash.length,2);pass('server restart preserves sparse placement and trash');
 assert.deepEqual(fs.readFileSync(path.join(root,'museum-archive/manifest.json')),original);pass('real archive unchanged');
 console.log(`LAYOUT_TESTS_PASS: ${passed} cases | ${fixture}`);
}finally{if(server?.exitCode===null)server.kill();}
