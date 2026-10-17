import {spawn} from 'node:child_process';
import http from 'node:http';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
const archive=path.join(root,'logs','control-tests-'+Date.now());fs.mkdirSync(archive,{recursive:true});
fs.writeFileSync(path.join(archive,'manifest.json'),JSON.stringify({schemaVersion:'1.0',artifacts:[],rooms:[{id:'empty',title:'Shutdown fixture',artifacts:[],cells:[]}]}));
const server=spawn(process.execPath,[root+'/apps/curator-admin/server.mjs','--port','0','--archive',archive],{windowsHide:true});
try {
 const url=await new Promise((resolve,reject)=>{const timer=setTimeout(()=>reject(Error('Start timeout')),10000);server.once('error',e=>{clearTimeout(timer);reject(e);});server.once('exit',code=>{clearTimeout(timer);reject(Error('Server exited before ready: '+code));});server.stdout.on('data',data=>{const m=data.toString().match(/CURATOR_READY (http:\/\/127\.0\.0\.1:\d+)/);if(m){clearTimeout(timer);resolve(m[1]);}});});
 const {token}=await (await fetch(url+'/api/session')).json();
 const headers={'Content-Type':'application/octet-stream','X-Museum-Token':token,'X-Museum-Metadata':'%7B%7D'};
 const stop=(extra={})=>fetch(url+'/api/shutdown',{method:'POST',headers:{...headers,...extra},body:new Uint8Array()});
 assert.equal((await stop({'X-Museum-Token':''})).status,403);console.log('PASS shutdown token required');
 assert.equal((await stop({Origin:'http://example.invalid'})).status,403);console.log('PASS foreign origin rejected');
 let upload;
 const uploadResult=new Promise((resolve,reject)=>{
  upload=http.request(url+'/api/artifacts',{method:'POST',headers:{...headers,'Content-Length':'2'}},res=>{res.resume();res.on('end',()=>resolve(res.statusCode));});upload.on('error',reject);upload.write('x');
 });
 await fetch(url+'/api/health');
 assert.equal((await stop()).status,409);console.log('PASS shutdown refuses in-flight save');
 upload.end('y');assert.equal(await uploadResult,400);
 const exited=new Promise(resolve=>server.once('exit',resolve));
 assert.equal((await stop()).status,200);assert.equal(await exited,0);console.log('PASS graceful shutdown after request completes');
 console.log('CONTROL_API_PASS: 4 cases');
} finally {server.kill();}
