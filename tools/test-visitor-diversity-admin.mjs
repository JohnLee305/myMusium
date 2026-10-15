import fs from 'node:fs';import path from 'node:path';import assert from 'node:assert/strict';import {spawn} from 'node:child_process';import {fileURLToPath} from 'node:url';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');const fixture=path.join(root,'logs','visitor-diversity-admin-'+Date.now());fs.mkdirSync(fixture,{recursive:true});
fs.writeFileSync(path.join(fixture,'manifest.json'),JSON.stringify({schemaVersion:'1.0',artifacts:[],rooms:[{id:'empty',title:'외형 관리 시험',artifacts:[],cells:[]}]}));
const child=spawn(process.execPath,[path.join(root,'apps/curator-admin/server.mjs'),'--port','0','--archive',fixture],{windowsHide:true});
try{
 const url=await new Promise((resolve,reject)=>{const timeout=setTimeout(()=>reject(Error('timeout')),10000);child.once('error',reject);child.stdout.on('data',data=>{const match=data.toString().match(/CURATOR_READY (http:\/\/127\.0\.0\.1:\d+)/);if(match){clearTimeout(timeout);resolve(match[1]);}});});
 const token=(await(await fetch(url+'/api/session')).json()).token;
 const get=async()=>{const r=await fetch(url+'/api/visitors');assert.equal(r.status,200);return r.json();};
 let state=await get();assert.equal(state.catalog.length,101);assert.equal(state.catalog.filter(p=>p.available).length,101);assert.equal(state.catalog.filter(p=>p.id.startsWith('quaternius-men-')).length,50);assert.equal(state.catalog.filter(p=>p.id.startsWith('quaternius-women-')).length,50);
 const settings={schemaVersion:'1.0',enabled:true,count:4,speed:1,appearances:state.catalog.map((p,i)=>({id:p.id,enabled:i%2===0,weight:i%7+1}))};
 const response=await fetch(url+'/api/visitors',{method:'POST',headers:{'Content-Type':'application/json','X-Museum-Token':token},body:JSON.stringify({revision:state.revision,settings})});assert.equal(response.status,200);
 state=await get();assert.deepEqual(state.settings,settings);assert.equal(state.settings.appearances.length,101);assert.deepEqual(JSON.parse(fs.readFileSync(path.join(fixture,'visitors.json'),'utf8')),settings);
 console.log('VISITOR_DIVERSITY_ADMIN_PASS: 101 available / 50 men / 50 women / full settings persist | '+fixture);
}finally{child.kill();}
