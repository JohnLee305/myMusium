import fs from 'node:fs';
import path from 'node:path';
import assert from 'node:assert/strict';
import {spawn} from 'node:child_process';
import {fileURLToPath} from 'node:url';
import {emptyReactions,generateReactions,validateReactions,sourceHash,PERSONAS} from '../apps/curator-admin/reactions.mjs';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
const fixture=path.join(root,'logs','reaction-tests-'+Date.now());fs.mkdirSync(fixture,{recursive:true});
const item={schemaVersion:'1.0',id:'reaction-a',title:'푸른 항아리',description:'직접 빚은 푸른 항아리입니다. 표면의 무늬를 살펴보세요.',model:'artifacts/reaction-a/models/pc.glb',scale:1};
const good=generateReactions(item);let passed=0;
const pass=name=>{passed++;console.log('PASS '+name);};
const clone=v=>structuredClone(v);
for(const dir of ['contract-valid','contract-invalid'])fs.mkdirSync(path.join(fixture,dir));
const maximum=clone(good);for(let n=0;n<8;n++)maximum.reactions.push({id:'extra-'+n,persona:PERSONAS[n%4],tier:'normal',text:'경계 검사용 추가 문구 '+n,evidencePointIds:[],status:'draft'});
for(const [name,document] of [['empty',emptyReactions(item)],['draft',good],['approved',{...clone(good),reactions:good.reactions.map(r=>({...r,status:'approved'}))}],['maximum48',maximum]]){
    validateReactions(document,item.id);fs.writeFileSync(path.join(fixture,'contract-valid',name+'.json'),JSON.stringify(document));pass('contract valid '+name);
}
const mutations={
    version:d=>d.schemaVersion='2.0',id:d=>d.artifactId='other',enabled:d=>delete d.enabled,unknown:d=>d.extra='x',sourceUnknown:d=>d.source.extra='x',fingerprint:d=>d.source.sha256='0'.repeat(64),numericPointId:d=>d.points[0].id=123,numericReactionId:d=>d.reactions[0].id=123,
    evidence:d=>d.points[0].evidence='설명에 없는 희귀한 유물',duplicatePoint:d=>d.points.push(d.points[0]),duplicateReaction:d=>d.reactions.push(d.reactions[0]),
    persona:d=>d.reactions[0].persona='skin-color',tier:d=>d.reactions[0].tier='forever',status:d=>d.reactions[0].status='published',textType:d=>d.reactions[0].text=5,
    long:d=>d.reactions[0].text='가'.repeat(73),html:d=>d.reactions[0].text='<b>문구</b>',control:d=>d.reactions[0].text='문구\u0000',surrogate:d=>d.reactions[0].text='\ud800',
    missingEvidence:d=>d.reactions[0].evidencePointIds=['absent'],duplicateEvidence:d=>d.reactions[0].evidencePointIds.push(d.reactions[0].evidencePointIds[0]),
    many:d=>d.reactions=Array.from({length:49},(_,i)=>({...d.reactions[0],id:'r-'+i})),null:d=>d.points=null,extraReaction:d=>d.reactions[0].secret='x'
};
for(const [name,mutate] of Object.entries(mutations)){const bad=clone(good);mutate(bad);assert.throws(()=>validateReactions(bad,item.id));fs.writeFileSync(path.join(fixture,'contract-invalid',name+'.json'),JSON.stringify(bad));pass('contract rejects '+name);}
for(const desc of ['짧음','가'.repeat(400),'줄바꿈\n둘째 문장','😀'.repeat(150),'모델 설명에 역사·작가 정보는 없습니다.']){
    const generated=generateReactions({...item,description:desc});assert.equal(generated.reactions.length,40);assert.ok(generated.reactions.every(r=>r.status==='draft'&&r.text.length<=72));assert.equal(new Set(generated.reactions.map(r=>r.text)).size,40);pass('bounded distinct generation '+desc.slice(0,8));
}
let approved=clone(good);approved.reactions[0].status='approved';approved.reactions[0].text='직접 검토한 감상';assert.deepEqual(generateReactions(item,approved),approved);pass('regeneration preserves current approved edits');
assert.notEqual(sourceHash({title:'a\nb',description:'c'}),sourceHash({title:'a',description:'b\nc'}));pass('source fingerprint has unambiguous field boundaries');
assert.ok(generateReactions({...item,description:'새로운 이야기'},approved).reactions.every(r=>r.status==='draft'));pass('changed source produces drafts');
const entries=[];
for(const [index,id] of ['reaction-a','reaction-b','reaction-c'].entries()){
    const data={...item,id,title:['푸른 항아리','붉은 미니어처','세 권의 책'][index],description:['직접 빚은 항아리의 푸른 무늬입니다.','직접 도색한 미니어처의 붉은 망토입니다.','여행에서 모은 세 권의 책입니다.'][index],model:`artifacts/${id}/models/pc.glb`};
    const folder=path.join(fixture,'artifacts',id);fs.mkdirSync(path.join(folder,'models'),{recursive:true});fs.copyFileSync(path.join(root,'museum-archive/artifacts/sample-books/models/pc.glb'),path.join(folder,'models/pc.glb'));
    fs.writeFileSync(path.join(folder,'artifact.json'),JSON.stringify(data,null,2));
    const doc=generateReactions(data);doc.reactions.forEach((r,n)=>{r.status='approved';r.text=data.title+' · '+r.persona+' · 감상 '+n;});fs.writeFileSync(path.join(folder,'reactions.json'),JSON.stringify(doc,null,2));entries.push(`artifacts/${id}/artifact.json`);
}
fs.writeFileSync(path.join(fixture,'manifest.json'),JSON.stringify({schemaVersion:'1.0',artifacts:entries,rooms:[{id:'first',title:'반응 시험실',artifacts:entries,cells:[2,8,14]},{id:'empty',title:'빈 방',artifacts:[],cells:[]}]}));
const file=path.join(fixture,'artifacts/reaction-a/reactions.json'),sourceFile=path.join(fixture,'artifacts/reaction-a/artifact.json');
const original=fs.readFileSync(file),originalSource=fs.readFileSync(sourceFile),originalManifest=fs.readFileSync(path.join(fixture,'manifest.json'));
let child,url,token;
async function start(){
    child=spawn(process.execPath,[path.join(root,'apps/curator-admin/server.mjs'),'--port','0','--archive',fixture],{windowsHide:true});
    url=await new Promise((resolve,reject)=>{const timer=setTimeout(()=>reject(Error('start timeout')),15000);child.once('error',reject);child.stdout.on('data',chunk=>{const match=chunk.toString().match(/CURATOR_READY (http:\/\/127\.0\.0\.1:\d+)/);if(match){clearTimeout(timer);resolve(match[1]);}});});token=(await(await fetch(url+'/api/session')).json()).token;
}
async function get(){const response=await fetch(url+'/api/reactions?artifactId=reaction-a');assert.equal(response.status,200);return response.json();}
async function post(state,action='save',overrides={},headers={}){return fetch(url+'/api/reactions',{method:'POST',headers:{'Content-Type':'application/json','X-Museum-Token':token,...headers},body:JSON.stringify({artifactId:'reaction-a',revision:state.revision,sourceHash:state.sourceHash,action,document:state.document,...overrides})});}
try{
    await start();let state=await get();assert.equal(state.stale,false);assert.equal(state.document.reactions.length,40);pass('API read without modifying artifact');
    for(const [headers,status] of [[{'X-Museum-Token':''},403],[{Origin:'http://example.invalid'},403],[{'Content-Type':'text/plain'},415]]){assert.equal((await post(state,'save',{},headers)).status,status);assert.deepEqual(fs.readFileSync(file),original);pass('API authentication/origin/type '+status);}
    for(const [name,mutate] of Object.entries(mutations)){const document=clone(state.document);mutate(document);const response=await post(state,'save',{document});assert.equal(response.status,400,await response.text());assert.deepEqual(fs.readFileSync(file),original);pass('API rejects '+name);}
    state.document.reactions[0].text='새로 승인한 감상';assert.equal((await post(state)).status,200);const oldState=state;state=await get();assert.equal(state.document.reactions[0].text,'새로 승인한 감상');assert.ok(state.history.length);pass('save and backup');
    assert.equal((await post(oldState)).status,409);pass('concurrent revision rejected');
    assert.equal((await post(state,'restore',{backup:'../../manifest.json'})).status,404);pass('backup traversal rejected');
    assert.equal((await post(state,'restore',{backup:state.history[0]})).status,200);state=await get();assert.notEqual(state.document.reactions[0].text,'새로 승인한 감상');pass('restore prior reviewed document');
    const before=fs.readFileSync(file);assert.equal((await post(state,'generate')).status,200);state=await get();assert.deepEqual(state.document,JSON.parse(before));pass('API regeneration non-destructive');
    const modified={...JSON.parse(originalSource),description:'변경된 설명입니다.'};fs.writeFileSync(sourceFile,JSON.stringify(modified));assert.equal((await post(state)).status,409);state=await get();assert.equal(state.stale,true);assert.equal((await post(state)).status,409);pass('changed source stale and save blocked');
    assert.equal((await post(state,'generate')).status,200);state=await get();assert.equal(state.stale,false);assert.ok(state.document.reactions.every(r=>r.status==='draft'));pass('new source requires new review');
    fs.writeFileSync(file,'{');state=await get();assert.ok(state.error);assert.equal((await post(state,'generate')).status,200);pass('corrupt sidecar recoverable with backup');
    state=await get();const saved=fs.readFileSync(file);assert.equal((await fetch(url+'/api/reactions',{method:'POST',headers:{'Content-Type':'application/json','X-Museum-Token':token},body:' '.repeat(65537)})).status,413);assert.deepEqual(fs.readFileSync(file),saved);pass('request size bounded');
    assert.equal((await fetch(url+'/api/reactions',{method:'POST',headers:{'Content-Type':'application/json','X-Museum-Token':token},body:'{'})).status,400);pass('malformed JSON rejected');
    assert.equal((await fetch(url+'/api/reactions?artifactId=../../x')).status,400);assert.equal((await fetch(url+'/api/reactions?artifactId=not-there')).status,404);pass('artifact membership and paths');
    const manifest=JSON.parse(originalManifest);manifest.artifacts=manifest.artifacts.slice(1);manifest.rooms[0].artifacts=manifest.artifacts;manifest.rooms[0].cells=[8,14];fs.writeFileSync(path.join(fixture,'manifest.json'),JSON.stringify(manifest));assert.equal((await fetch(url+'/api/reactions?artifactId=reaction-a')).status,404);fs.writeFileSync(path.join(fixture,'manifest.json'),originalManifest);assert.equal((await fetch(url+'/api/reactions?artifactId=reaction-a')).status,200);pass('trash and restoration retain sidecar');
    for(const asset of ['/reaction-editor.js','/reaction-style.css'])assert.equal((await fetch(url+asset)).status,200);assert.ok((await(await fetch(url)).text()).includes('reactions-heading'));pass('UI assets served');
    assert.deepEqual(fs.readFileSync(path.join(fixture,'manifest.json')),originalManifest);pass('reactions do not mutate layout');
}finally{if(child&&child.exitCode===null)child.kill();fs.writeFileSync(file,original);fs.writeFileSync(sourceFile,originalSource);fs.writeFileSync(path.join(fixture,'manifest.json'),originalManifest);}
console.log(`REACTION_TESTS_PASS: ${passed} cases | ${fixture}`);
fs.writeFileSync(path.join(root,'logs','reaction-fixture-path.txt'),fixture);
