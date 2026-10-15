import fs from 'node:fs';
import assert from 'node:assert/strict';
import {spawn} from 'node:child_process';
import {fileURLToPath} from 'node:url';
import {inspectGlb,sizeCheck,LIMITS} from '../apps/curator-admin/preview-checks.mjs';
const root=new URL('../',import.meta.url),fixture=new URL('logs/preview-checks/',root);fs.mkdirSync(fixture,{recursive:true});
const toArray=b=>b.buffer.slice(b.byteOffset,b.byteOffset+b.byteLength);
const sample=fs.readFileSync(new URL('museum-archive/artifacts/sample-vase/models/pc.glb',root));
inspectGlb(toArray(sample));console.log('PASS valid sample');
function glb(data){const text=Buffer.from(JSON.stringify(data)),json=Buffer.concat([text,Buffer.alloc((4-text.length%4)%4,32)]),out=Buffer.alloc(20+json.length);out.write('glTF');out.writeUInt32LE(2,4);out.writeUInt32LE(out.length,8);out.writeUInt32LE(json.length,12);out.writeUInt32LE(0x4e4f534a,16);json.copy(out,20);return out;}
const base={asset:{version:'2.0'},meshes:[{}]};
for(const [name,data] of [['external-buffer',{...base,buffers:[{uri:'https://example.invalid/model.bin'}]}],['external-texture',{...base,images:[{uri:'../secret.png'}]}],['compressed',{...base,extensionsUsed:['KHR_draco_mesh_compression']}],['no-model',{asset:{version:'2.0'}}]]){const bytes=glb(data);assert.throws(()=>inspectGlb(toArray(bytes)));fs.writeFileSync(new URL(name+'.glb',fixture),bytes);console.log('PASS '+name);}
assert.throws(()=>inspectGlb(new ArrayBuffer(LIMITS.bytes+1)));console.log('PASS over64MB');
assert.equal(sizeCheck([1,1,1],1).fits,false);assert.equal(sizeCheck([1,1,1],.94).fits,true);assert.equal(sizeCheck([.95,1.8,.95],1).fits,true);console.log('PASS size boundaries');
for(const scale of [0,.001,11,NaN])assert.equal(sizeCheck([.1,.1,.1],scale).valid,false);console.log('PASS invalid scale');
assert.equal(sizeCheck([1000,1,1],1).suggested,null);console.log('PASS too large even at minimum scale');
const bad=new URL('bad.glb',fixture);fs.writeFileSync(bad,'not a GLB');const big=new URL('over64.glb',fixture);fs.writeFileSync(big,'');fs.truncateSync(big,LIMITS.bytes+1);
const server=spawn(process.execPath,[fileURLToPath(new URL('apps/curator-admin/server.mjs',root)),'--port','0'],{windowsHide:true});
const url=await new Promise((resolve,reject)=>{const timer=setTimeout(()=>{server.kill();reject(Error('Server timeout'));},10000);server.stdout.on('data',b=>{const m=b.toString().match(/CURATOR_READY (http:\/\/127.0.0.1:\d+)/);if(m){clearTimeout(timer);resolve(m[1]);}});server.once('error',reject);});
try {
for(const [endpoint,status] of [['/api/model/sample-vase',200],['/api/model/not-registered',404],['/api/model/%2e%2e',404],['/vendor/three.module.js',200],['/vendor/LICENSE',404]]){const r=await fetch(url+endpoint);assert.equal(r.status,status);console.log('PASS '+endpoint);}
assert.equal((await fetch(url+'/api/model/sample-vase',{headers:{Origin:'http://example.invalid'}})).status,403);console.log('PASS foreign origin');
console.log('PREVIEW_CHECKS_PASS: 15 cases');
} finally {server.kill();}
