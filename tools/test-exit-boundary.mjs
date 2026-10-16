import fs from 'node:fs';import path from 'node:path';import assert from 'node:assert/strict';import {spawn} from 'node:child_process';import {fileURLToPath} from 'node:url';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
const fixture=path.join(root,'logs','exit-boundary-tests-'+Date.now());
for(const [name,count] of [['single',1],['four-directions',4]]){
 const archive=path.join(fixture,name);fs.mkdirSync(archive,{recursive:true});
 const sides=['front','left','back','right'];
 const rooms=Array.from({length:count},(_,i)=>({id:'room-'+i,title:'출입문 안전 시험 '+(i+1),artifacts:[],cells:[],navigation:{entrance:{side:sides[i],slot:2},exit:{side:sides[(i+1)%4],slot:2},blockedCells:[],visitOrder:[]}}));
 fs.writeFileSync(path.join(archive,'manifest.json'),JSON.stringify({schemaVersion:'1.0',artifacts:[],rooms}));
 fs.writeFileSync(path.join(archive,'visitors.json'),JSON.stringify({schemaVersion:'1.0',enabled:false,count:0,speed:1,appearances:[]}));
 const log=path.join(archive,'player.log'),png=path.join(archive,'player.png');
 const player=spawn(path.join(root,'Builds/Windows/VRMuseum.exe'),['-screen-fullscreen','0','-screen-width','1280','-screen-height','800','-museum-archive',archive,'-museum-boundary-smoke','-museum-screenshot',png,'-logFile',log]);
 const code=await new Promise((resolve,reject)=>{const timer=setTimeout(()=>{player.kill();reject(Error('Boundary player timeout: '+log));},120000);player.once('error',reject);player.once('exit',code=>{clearTimeout(timer);resolve(code);});});
 const output=fs.readFileSync(log,'utf8');
 assert.equal(code,0,log);assert.ok(output.includes('EXIT_BOUNDARY_SMOKE_PASS')&&output.includes('MUSEUM_SMOKE_PASS'),log);
 assert.ok(!/(NullReferenceException|MissingReferenceException):/.test(output),log);
 if(count===1)assert.ok(output.includes('EXIT_TRAFFIC_CHECK: passed=True'),log);
 const checks=output.split('\n').filter(line=>line.startsWith('EXIT_BOUNDARY_CHECK:'));
 assert.equal(checks.length,count*2);assert.ok(checks.every(line=>line.includes('supported=True')&&line.includes('destination=True')));
 assert.equal(output.split('PLAYER_BOUNDARY_RECOVERED').length-1,count*2);
 console.log(`EXIT_BOUNDARY_PASS: ${name} | ${count*2} physical walks, cooldown, transitions/completion, recovery, reentry | ${png}`);
}
console.log('EXIT_BOUNDARY_TESTS_PASS: '+fixture);
