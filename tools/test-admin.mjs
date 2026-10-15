import fs from 'node:fs';
import path from 'node:path';
import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { fileURLToPath } from 'node:url';
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const fixture = path.join(root, 'logs', `admin-tests-${Date.now()}`);
fs.cpSync(path.join(root, 'museum-archive'), fixture, { recursive: true, filter: source => path.basename(source) !== '.admin-backups' });
const fixtureManifestPath=path.join(fixture,'manifest.json');
const fixtureManifest=JSON.parse(fs.readFileSync(fixtureManifestPath,'utf8').replace(/^\uFEFF/,''));
delete fixtureManifest.rooms;
fixtureManifest.artifacts=['sample-vase','sample-books','sample-sculpture'].map(id=>`artifacts/${id}/artifact.json`);
delete fixtureManifest.trash;
fs.writeFileSync(fixtureManifestPath,JSON.stringify(fixtureManifest));
const server = spawn(process.execPath, [path.join(root, 'apps/curator-admin/server.mjs'), '--port', '0', '--archive', fixture], { windowsHide: true });
const log = fs.createWriteStream(fixture + '.log');
server.stdout.pipe(log); server.stderr.pipe(log);
const url = await new Promise((resolve, reject) => {
    const timeout = setTimeout(() => reject(new Error('Server start timed out')), 10000);
    server.stdout.on('data', data => { const match = data.toString().match(/CURATOR_READY (http:\/\/127\.0\.0\.1:\d+)/); if (match) { clearTimeout(timeout); resolve(match[1]); } });
    server.once('error', reject);
    server.once('exit', code => { clearTimeout(timeout); reject(new Error('Server exited ' + code)); });
});
let passed = 0;
function pass(name) { passed++; console.log('PASS: ' + name); }
try {
    const state = await (await fetch(url + '/api/state')).json();
    assert.equal(state.artifacts.length, 3); pass('existing list');
    const base = { id: 'test-added', title: '테스트 전시품', description: '<script>alert(1)</script> 한글 이야기도 그대로 보존', scale: 1, sampleId: 'sample-books' };
    const post = (meta, body = new Uint8Array(), headers = {}) => fetch(url + '/api/artifacts', { method: 'POST', headers: { 'Content-Type': 'application/octet-stream', 'X-Museum-Token': state.token, 'X-Museum-Metadata': encodeURIComponent(JSON.stringify(meta)), ...headers }, body });
    const original = fs.readFileSync(path.join(fixture, 'manifest.json'));
    for (const [name, meta, headers, expected] of [
        ['missing token', base, { 'X-Museum-Token': '' }, 403],
        ['foreign origin', base, { Origin: 'http://example.invalid' }, 403],
        ['unsafe id', {...base, id:'../outside'}, {}, 400],
        ['non-string id', {...base, id:123}, {}, 400],
        ['empty title', {...base, title:'   '}, {}, 400],
        ['invalid scale', {...base, scale:0}, {}, 400],
        ['unknown sample', {...base, sampleId:'other'}, {}, 400],
    ]) {
        const response = await post(meta, undefined, headers); assert.equal(response.status, expected, await response.text());
        assert.deepEqual(fs.readFileSync(path.join(fixture, 'manifest.json')), original); pass(name + ' / unchanged manifest');
    }
    let response = await post({...base, sampleId:undefined}, Buffer.from('not a model'));
    assert.equal(response.status, 400); pass('bad GLB rejected');
    response = await post(base); assert.equal(response.status, 201, await response.text());
    const saved = JSON.parse(fs.readFileSync(path.join(fixture, 'artifacts/test-added/artifact.json')));
    assert.equal(saved.title, base.title); assert.equal(saved.description, base.description);
    assert.deepEqual(fs.readFileSync(path.join(fixture, saved.model)), fs.readFileSync(path.join(fixture, 'artifacts/sample-books/models/pc.glb')));
    assert.equal(fs.readdirSync(path.join(fixture, '.admin-backups')).length, 1); pass('sample saved with backup and Korean metadata');
    const savedManifest = fs.readFileSync(path.join(fixture, 'manifest.json'));
    response = await post(base); assert.equal(response.status, 409); assert.deepEqual(fs.readFileSync(path.join(fixture, 'manifest.json')), savedManifest); pass('duplicate preserves existing files');
    const glb = fs.readFileSync(path.join(fixture, 'artifacts/sample-vase/models/pc.glb'));
    response = await post({...base, id:'test-uploaded', sampleId:undefined}, glb); assert.equal(response.status, 201, await response.text()); pass('binary GLB upload');
    const oldLength = glb.readUInt32LE(12);
    const gltf = JSON.parse(glb.toString('utf8',20,20+oldLength)); gltf.buffers[0].uri='https://example.invalid/data.bin';
    const raw = Buffer.from(JSON.stringify(gltf)); const json = Buffer.concat([raw,Buffer.alloc((4-raw.length%4)%4,32)]);
    const external = Buffer.concat([glb.subarray(0,20),json,glb.subarray(20+oldLength)]); external.writeUInt32LE(external.length,8); external.writeUInt32LE(json.length,12);
    response = await post({...base, id:'test-external', sampleId:undefined}, external); assert.equal(response.status,400); pass('external GLB resource rejected');
    const simultaneous = await Promise.all([post({...base,id:'test-concurrent'}),post({...base,id:'test-concurrent'})]);
    assert.deepEqual(simultaneous.map(r=>r.status).sort(),[201,409]); pass('concurrent duplicate cannot overwrite');
    response = await post({...base,id:'test-overflow'}); assert.equal(response.status,201); assert.equal((await response.json()).displayed,true); pass('seventh exhibit remains in first room');
    const final = await (await fetch(url+'/api/state')).json(); assert.equal(final.artifacts.length,7); assert.equal(final.artifacts.filter(x=>x.displayed).length,7);
    for (let i=8;i<=31;i++) { response=await post({...base,id:`test-room-${i}`}); assert.equal(response.status,201); }
    let roomsState=await (await fetch(url+'/api/state')).json();
    assert.deepEqual(roomsState.rooms.map(r=>r.count),[30,1]); pass('31st exhibit automatically creates second room');
    fs.cpSync(fixture,fixture+'-tour',{recursive:true});
    response=await post({...base,id:'full-room-rejected',roomId:roomsState.rooms[0].id}); assert.equal(response.status,409); pass('explicit full room rejects without data loss');
    const manage=(endpoint,meta,extra={})=>fetch(url+endpoint,{method:'POST',headers:{'Content-Type':'application/octet-stream','X-Museum-Token':state.token,'X-Museum-Metadata':encodeURIComponent(JSON.stringify(meta)),...extra},body:new Uint8Array()});
    response=await manage('/api/rooms',{title:'비어 있는 가족 전시실'},{'X-Museum-Token':''}); assert.equal(response.status,403); pass('room creation needs token');
    response=await manage('/api/rooms',{title:'비어 있는 가족 전시실'}); assert.equal(response.status,201); const addedRoom=(await response.json()).room;
    response=await post({...base,id:'chosen-room',roomId:addedRoom.id}); assert.equal(response.status,201); assert.equal((await response.json()).roomId,addedRoom.id); pass('manual room creation and selected room registration');
    response=await manage('/api/move',{artifactId:'test-added',roomId:addedRoom.id}); assert.equal(response.status,200);
    roomsState=await (await fetch(url+'/api/state')).json(); assert.deepEqual(roomsState.rooms.map(r=>r.count),[29,1,2]); pass('existing exhibit moves between rooms');
    response=await manage('/api/move',{artifactId:'test-added',roomId:'missing'}); assert.equal(response.status,409); pass('invalid destination rejected');
    response=await manage('/api/rooms',{title:'빈 방'}); assert.equal(response.status,201); pass('empty room supported');
    assert.equal(fs.readdirSync(path.join(fixture,'artifacts')).some(x=>x.startsWith('.import-')),false); pass('final list and staging cleanup');
    console.log(`ADMIN_TESTS_PASS: ${passed} cases | ${fixture}`);
} finally { server.kill(); }

