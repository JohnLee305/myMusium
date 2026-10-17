import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import assert from 'node:assert/strict';
import {spawn,spawnSync} from 'node:child_process';
import {fileURLToPath} from 'node:url';
import {acquire,owned,update,release,claimRef,listClaims,checkOwnership,receiptPath} from './task-coordination.mjs';
const self=fileURLToPath(import.meta.url),root=path.resolve(path.dirname(self),'..');
const git=(dir,...args)=>{const r=spawnSync('git',args,{cwd:dir,encoding:'utf8',windowsHide:true});if(r.status!==0)throw Error('fixture git rejected: '+args[0]);return r.stdout.trim();};
function backend(repo){return {
 list:()=>git(repo,'for-each-ref','--format=%(refname:short)','refs/heads/codex/claims/').split('\n').filter(Boolean),
 read:ref=>{const r=spawnSync('git',['show-ref','--verify','--hash','refs/heads/'+ref],{cwd:repo,encoding:'utf8',windowsHide:true});if(r.status!==0)return null;const sha=r.stdout.trim();return {sha,claim:JSON.parse(git(repo,'show','-s','--format=%B',sha))};},
 commit:(c,parent)=>{const env={...process.env,GIT_AUTHOR_NAME:'Fixture',GIT_AUTHOR_EMAIL:'fixture@example.invalid',GIT_COMMITTER_NAME:'Fixture',GIT_COMMITTER_EMAIL:'fixture@example.invalid'};const r=spawnSync('git',['commit-tree',git(repo,'rev-parse',parent+'^{tree}'),'-p',parent],{cwd:repo,input:JSON.stringify(c),encoding:'utf8',env,windowsHide:true});assert.equal(r.status,0,r.stderr);return r.stdout.trim();},
 create:(ref,sha)=>git(repo,'update-ref','refs/heads/'+ref,sha,'0'.repeat(40)),
 update:(ref,sha)=>{const old=git(repo,'rev-parse',sha+'^');git(repo,'update-ref','refs/heads/'+ref,sha,old);},
 };}
const tokenHash=t=>crypto.createHash('sha256').update(t).digest('hex');
function claim(base,token,agent='agent-a',taskId='OPS-901'){return {schemaVersion:1,taskId,agent,account:'fixture',branch:'codex/'+taskId.toLowerCase()+'-'+agent,baseSha:base,summary:'Public fixture work',nextAction:'Test',status:'in_progress',updatedAt:new Date().toISOString(),tokenHash:tokenHash(token),issue:null};}
if(process.argv[2]==='--worker'){
 try{const [repo,base,token,agent]=process.argv.slice(3);acquire(backend(repo),claim(base,token,agent),token);process.exitCode=0;}catch{process.exitCode=1;}
}else{
 const fixture=path.join(root,'logs','task-coordination-tests-'+Date.now());fs.mkdirSync(fixture,{recursive:true});const repo=path.join(fixture,'origin.git');git(fixture,'init','--bare',repo);
 const seed=path.join(fixture,'seed');git(fixture,'init',seed);git(seed,'config','user.name','Fixture');git(seed,'config','user.email','fixture@example.invalid');git(seed,'config','commit.gpgsign','false');git(seed,'config','core.hooksPath',path.join(fixture,'no-hooks'));fs.writeFileSync(path.join(seed,'public.txt'),'fixture');git(seed,'add','.');git(seed,'commit','-m','base');git(seed,'push',repo,'HEAD:refs/heads/main');const base=git(seed,'rev-parse','HEAD'),b=backend(repo);
 let count=0;const pass=s=>{count++;console.log('PASS '+s);};
 const worker=(agent,token)=>new Promise(resolve=>{const p=spawn(process.execPath,[self,'--worker',repo,base,token,agent],{windowsHide:true});p.once('exit',resolve);p.once('error',()=>resolve(99));});
 const results=await Promise.all([worker('agent-a','token-a'),worker('agent-b','token-b')]);assert.deepEqual([...results].sort(),[0,1]);const winner=b.read(claimRef('OPS-901')),token=winner.claim.agent==='agent-a'?'token-a':'token-b';pass('two real processes race for same task: exactly one wins');
 assert.equal(acquire(b,winner.claim,token).sha,winner.sha);pass('retry after lost response is idempotent for the same session');
 assert.throws(()=>acquire(b,claim(base,'other','agent-c'),'other'));pass('duplicate claim rejected without replacing owner');
 assert.throws(()=>owned(b,'OPS-901','wrong'));assert.throws(()=>update(b,'OPS-901','wrong',{status:'blocked'}));assert.throws(()=>release(b,'OPS-901','wrong'));assert.equal(b.read(claimRef('OPS-901')).sha,winner.sha);pass('other session cannot update or release');
 const other=acquire(b,claim(base,'token-d','agent-d','OPS-902'),'token-d');assert.equal(listClaims(b).length,2);pass('different task runs concurrently and list shows both owners');
 const file=receiptPath(fixture,'OPS-901');fs.mkdirSync(path.dirname(file),{recursive:true});fs.writeFileSync(file,JSON.stringify({token}));assert.equal(checkOwnership(fixture,'OPS-901',winner.claim.branch,b).ok,true);assert.equal(checkOwnership(fixture,'OPS-901','codex/wrong',b).ok,false);assert.equal(checkOwnership(fixture,'OPS-902',other.claim.branch,b).ok,false);pass('start gate requires matching branch and local session receipt');
 update(b,'OPS-901',token,{status:'blocked',nextAction:'Need user decision'});assert.equal(b.read(claimRef('OPS-901')).claim.status,'blocked');assert.throws(()=>acquire(b,claim(base,'steal'),'steal'));pass('blocked or old task never expires or auto-transfers');
 assert.throws(()=>release(b,'OPS-901',token));assert.throws(()=>update(b,'OPS-901',token,{status:'released'}));pass('unfinished work cannot release or bypass release through update');
 // Two updates from the same parent: CAS allows only the first, preserving history.
 const current=b.read(claimRef('OPS-901'));const a=b.commit({...current.claim,nextAction:'A'},current.sha),c=b.commit({...current.claim,nextAction:'B'},current.sha);b.update(claimRef('OPS-901'),a);assert.throws(()=>b.update(claimRef('OPS-901'),c));assert.equal(b.read(claimRef('OPS-901')).sha,a);pass('concurrent status updates cannot overwrite each other');
 update(b,'OPS-901',token,{status:'done'});release(b,'OPS-901',token);assert.equal(checkOwnership(fixture,'OPS-901',winner.claim.branch,b).code,'TASK_UNCLAIMED');assert.throws(()=>update(b,'OPS-901',token,{status:'in_progress'}));pass('done releases through an auditable tombstone; old token invalid');
 const next=acquire(b,claim(base,'new-token','agent-next'),'new-token');assert.equal(next.claim.agent,'agent-next');assert.throws(()=>release(b,'OPS-901',token));pass('reclaim after release keeps history and rejects previous session');
 assert.throws(()=>acquire(b,claim(base,'bad','agent','../bad'),'bad'));pass('invalid task IDs rejected');
 assert.equal(git(seed,'rev-parse','HEAD'),base);assert.equal(fs.readFileSync(path.join(seed,'public.txt'),'utf8'),'fixture');pass('coordination never changes source worktree or integration branch');
 console.log(`TASK_COORDINATION_TESTS_PASS: ${count} cases | ${fixture}`);
}
