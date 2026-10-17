import fs from 'node:fs';
import path from 'node:path';
import assert from 'node:assert/strict';
import {spawn} from 'node:child_process';
import {fileURLToPath} from 'node:url';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
const fixture=path.join(root,'logs','visitor-admin-'+Date.now());fs.mkdirSync(fixture,{recursive:true});
const file=path.join(fixture,'visitors.json'),catalogFile=path.join(fixture,'catalog.json');
const manifest={schemaVersion:'1.0',artifacts:[],rooms:[{id:'empty',title:'관람객 설정 시험실',artifacts:[],cells:[]}]};
fs.writeFileSync(path.join(fixture,'manifest.json'),JSON.stringify(manifest));
const catalog={schemaVersion:'1.0',appearances:[{id:'quaternius-casual2',displayName:'일상복 관람객',available:true,weight:1,creator:'Quaternius',license:'CC0 1.0'},{id:'second-appearance',displayName:'다른 외형 시험',available:true,weight:1,creator:'Test fixture',license:'test only'}]};
fs.writeFileSync(catalogFile,JSON.stringify(catalog));
let child,url,token,passed=0;
function pass(label){passed++;console.log('PASS '+label);}
async function start(){
    child=spawn(process.execPath,[path.join(root,'apps/curator-admin/server.mjs'),'--port','0','--archive',fixture,'--visitor-catalog',catalogFile],{windowsHide:true});
    url=await new Promise((resolve,reject)=>{const timer=setTimeout(()=>reject(Error('start timeout')),10000);child.once('error',reject);child.stdout.on('data',chunk=>{const match=chunk.toString().match(/CURATOR_READY (http:\/\/127\.0\.0\.1:\d+)/);if(match){clearTimeout(timer);resolve(match[1]);}});});
    token=(await(await fetch(url+'/api/session')).json()).token;
}
async function get(){const response=await fetch(url+'/api/visitors');assert.equal(response.status,200);return response.json();}
async function post(body,headers={}){return fetch(url+'/api/visitors',{method:'POST',headers:{'Content-Type':'application/json','X-Museum-Token':token,...headers},body:JSON.stringify(body)});}
async function stop(){const exited=new Promise(resolve=>child.once('exit',resolve));const response=await fetch(url+'/api/shutdown',{method:'POST',headers:{'Content-Type':'application/octet-stream','X-Museum-Token':token,'X-Museum-Metadata':'%7B%7D'},body:new Uint8Array()});assert.equal(response.status,200);assert.equal(await exited,0);}
try{
    await start();let state=await get();
    assert.equal(state.settings.count,1);assert.equal(state.settings.speed,1);assert.equal(state.revision,'missing');assert.ok(!fs.existsSync(file));pass('defaults without writes');
    assert.equal(state.catalog.length,2);pass('catalog supports additional appearances');
    let value={...state.settings,count:4,speed:1.2,appearances:[{id:'quaternius-casual2',enabled:true,weight:1},{id:'second-appearance',enabled:true,weight:3}]};
    for(const [name,settings,headers,status] of [
        ['token required',value,{'X-Museum-Token':''},403],['foreign origin',value,{Origin:'http://example.invalid'},403],['wrong content type',value,{'Content-Type':'text/plain'},415],
        ['count upper bound',{...value,count:7},{},400],['fractional count',{...value,count:1.5},{},400],['string count',{...value,count:'2'},{},400],['negative count',{...value,count:-1},{},400],
        ['speed lower bound',{...value,speed:.4},{},400],['speed upper bound',{...value,speed:1.6},{},400],['enabled type',{...value,enabled:'yes'},{},400],['schema version',{...value,schemaVersion:'2.0'},{},400],
        ['unknown field',{...value,script:'oops'},{},400],['unknown appearance',{...value,appearances:[{id:'missing',enabled:true,weight:1}]},{},400],['duplicate appearance',{...value,appearances:[value.appearances[0],value.appearances[0]]},{},400],
        ['invalid appearance id',{...value,appearances:[{id:'../x',enabled:true,weight:1}]},{},400],['negative weight',{...value,appearances:[{id:'quaternius-casual2',enabled:true,weight:-1}]},{},400],['weight upper bound',{...value,appearances:[{id:'quaternius-casual2',enabled:true,weight:101}]},{},400],['appearance enabled type',{...value,appearances:[{id:'quaternius-casual2',enabled:1,weight:1}]},{},400]
    ]){const response=await post({revision:state.revision,settings},headers);assert.equal(response.status,status,await response.text());assert.ok(!fs.existsSync(file));pass(name+' / no writes');}
    let response=await post({revision:state.revision,settings:value});assert.equal(response.status,200,await response.text());state=await get();assert.deepEqual(state.settings,value);assert.equal(JSON.parse(fs.readFileSync(path.join(fixture,'manifest.json'))).artifacts.length,0);pass('save roundtrip leaves exhibitions unchanged');
    response=await post({revision:'missing',settings:{...value,count:0}});assert.equal(response.status,409);assert.deepEqual((await get()).settings,value);pass('stale tab cannot overwrite');
    let saved=fs.readFileSync(file);
    await stop();await start();state=await get();assert.deepEqual(state.settings,value);assert.deepEqual(fs.readFileSync(file),saved);pass('settings persist across server restart');
    response=await post({revision:state.revision,settings:{...value,enabled:false,count:0,speed:.5,appearances:value.appearances.map(entry=>({...entry,enabled:false,weight:0}))}});assert.equal(response.status,200);state=await get();assert.equal(state.settings.enabled,false);pass('off and zero count / all appearances off allowed');
    assert.ok(fs.readdirSync(path.join(fixture,'.admin-backups')).some(name=>name.startsWith('visitors-')));pass('prior settings backed up');
    saved=fs.readFileSync(file);
    response=await fetch(url+'/api/visitors',{method:'POST',headers:{'Content-Type':'application/json','X-Museum-Token':token},body:' '.repeat(65537)});assert.equal(response.status,413);assert.deepEqual(fs.readFileSync(file),saved);pass('oversized settings rejected / no writes');
    response=await fetch(url+'/api/visitors',{method:'POST',headers:{'Content-Type':'application/json','X-Museum-Token':token},body:'{'});assert.equal(response.status,400);assert.deepEqual(fs.readFileSync(file),saved);pass('malformed JSON rejected / no writes');
    fs.writeFileSync(file,'{');assert.equal((await fetch(url+'/api/visitors')).status,409);fs.writeFileSync(file,saved);pass('corrupt disk settings reported');
    fs.writeFileSync(catalogFile,JSON.stringify({...catalog,appearances:[catalog.appearances[0]]}));state=await get();assert.equal(state.catalog.length,1);assert.equal(state.settings.appearances.length,2);pass('removed appearance preserved on read for recovery');
    fs.writeFileSync(catalogFile,JSON.stringify(catalog));
    for(const asset of ['/visitor-editor.js','/visitor-style.css'])assert.equal((await fetch(url+asset)).status,200);
    const html=await(await fetch(url)).text();assert.ok(html.includes('visitors-heading')&&html.includes('박물관 관리'));pass('management UI and assets served');
    console.log(`VISITOR_ADMIN_TESTS_PASS: ${passed} cases | ${fixture}`);
}finally{if(child && child.exitCode===null)child.kill();}
