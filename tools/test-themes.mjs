import fs from 'node:fs';
import path from 'node:path';
import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { PRESETS, COLOR_FIELDS, validateTheme } from '../apps/curator-admin/themes.mjs';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
const fixture=path.join(root,'logs',`theme-tests-${Date.now()}`);
fs.mkdirSync(fixture,{recursive:true});
fs.cpSync(path.join(root,'museum-archive/artifacts'),path.join(fixture,'artifacts'),{recursive:true});
const original=JSON.parse(fs.readFileSync(path.join(root,'museum-archive/manifest.json'),'utf8').replace(/^\uFEFF/,''));
const paths=original.artifacts;
const manifest={schemaVersion:'1.0',artifacts:paths,rooms:[{id:'theme-a',title:'밝은 갤러리',artifacts:[paths[0]]},{id:'theme-b',title:'밤의 전시실',artifacts:paths.slice(1)},{id:'theme-empty',title:'빈 방',artifacts:[]}]};
const file=path.join(fixture,'manifest.json');fs.writeFileSync(file,JSON.stringify(manifest));
const server=spawn(process.execPath,[path.join(root,'apps/curator-admin/server.mjs'),'--port','0','--archive',fixture],{windowsHide:true});
const stream=fs.createWriteStream(fixture+'.log');server.stdout.pipe(stream);server.stderr.pipe(stream);
let passed=0;const pass=name=>{passed++;console.log('PASS: '+name);};
try {
 const url=await new Promise((resolve,reject)=>{const timeout=setTimeout(()=>reject(Error('Timeout')),10000);server.once('error',reject);server.stdout.on('data',data=>{const m=data.toString().match(/CURATOR_READY (http:\/\/127\.0\.0\.1:\d+)/);if(m){clearTimeout(timeout);resolve(m[1]);}});});
 const get=async()=>{const res=await fetch(url+'/api/state');assert.equal(res.status,200);return res.json();};
 let state=await get();assert.equal(state.rooms[0].theme,null);pass('legacy colours and room metadata remain readable');
 const post=(meta,headers={})=>fetch(url+'/api/room-theme',{method:'POST',headers:{'Content-Type':'application/octet-stream','X-Museum-Token':state.token,'X-Museum-Metadata':encodeURIComponent(JSON.stringify(meta)),...headers},body:new Uint8Array()});
 const base={roomId:'theme-a',theme:PRESETS.bright.colors,themeRevision:state.rooms[0].themeRevision};
 for(const [name,meta,headers,status] of [
  ['token required',base,{'X-Museum-Token':''},403],['origin rejected',base,{Origin:'http://example.invalid'},403],
  ['missing room',{...base,roomId:'missing'},{},404],['stale revision',{...base,themeRevision:'old'},{},409],
  ['invalid hex',{...base,theme:{floor:'#12345G'}},{},400],['alpha rejected',{...base,theme:{floor:'#12345678'}},{},400],
  ['CSS injection rejected',{...base,theme:{floor:'url(https://example.invalid)'}},{},400],['unknown key',{...base,theme:{model:'#FFFFFF'}},{},400],
  ['array rejected',{...base,theme:[]},{},400],['null field rejected',{...base,theme:{floor:null}},{},400]]) {
   const before=fs.readFileSync(file);const response=await post(meta,headers);assert.equal(response.status,status,await response.text());assert.deepEqual(fs.readFileSync(file),before);pass(name+' / no writes');
 }
 // Exercise every preset through the real save API.
 assert.equal(Object.keys(PRESETS).length,34);
 assert.equal(new Set(Object.values(PRESETS).map(p=>p.name)).size,34);
 for(const [id,preset] of Object.entries(PRESETS)) {
  assert.equal(Object.keys(preset.colors).length,COLOR_FIELDS.length);
  validateTheme(preset.colors);
  const current=await get();
  const saved=await post({roomId:'theme-empty',theme:preset.colors,themeRevision:current.rooms[2].themeRevision});
  assert.equal(saved.status,200,await saved.text());
  const next=await get();assert.deepEqual(next.rooms[2].theme,preset.colors);
  assert.equal(next.rooms[0].theme,null);assert.equal(next.rooms[1].theme,null);
  pass('preset '+id+' saves all colour fields without touching other rooms');
 }
 let response=await post(base);assert.equal(response.status,200,await response.text());state=await get();
 assert.deepEqual(state.rooms[0].theme,PRESETS.bright.colors);assert.equal(state.rooms[1].theme,null);assert.deepEqual(JSON.parse(fs.readFileSync(file)).artifacts,paths);pass('all colour fields persist only in selected room');
 response=await post(base);assert.equal(response.status,409);pass('stale tab cannot overwrite newer theme');
 response=await post({roomId:'theme-b',theme:PRESETS.night.colors,themeRevision:state.rooms[1].themeRevision});assert.equal(response.status,200);state=await get();assert.deepEqual(state.rooms[0].theme,PRESETS.bright.colors);pass('second room has independent theme');
 response=await post({roomId:'theme-empty',theme:{floor:'#abcdef'},themeRevision:state.rooms[2].themeRevision});assert.equal(response.status,200);state=await get();assert.equal(state.rooms[2].theme.floor,'#ABCDEF');pass('partial theme and empty room supported');
 response=await post({roomId:'theme-empty',theme:null,themeRevision:state.rooms[2].themeRevision});assert.equal(response.status,200);state=await get();assert.equal(state.rooms[2].theme,null);pass('reset removes overrides');
 assert.ok(fs.readdirSync(path.join(fixture,'.admin-backups')).length>=4);pass('theme saves keep manifest backups');
 for(const asset of ['/theme-editor.js','/themes.mjs']) assert.equal((await fetch(url+asset)).status,200);pass('browser theme modules served');
 const invalid=path.join(fixture,'invalid');fs.mkdirSync(invalid);fs.cpSync(path.join(fixture,'artifacts'),path.join(invalid,'artifacts'),{recursive:true});
 const malformed=JSON.parse(fs.readFileSync(file));malformed.rooms[0].theme.floor='bad-colour';fs.writeFileSync(path.join(invalid,'manifest.json'),JSON.stringify(malformed));
 assert.equal(Object.keys(PRESETS.bright.colors).length,COLOR_FIELDS.length);
 console.log(`THEME_TESTS_PASS: ${passed} cases | ${fixture}`);
} finally {server.kill();}
