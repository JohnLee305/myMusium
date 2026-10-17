import fs from 'node:fs';
import path from 'node:path';
import {spawnSync} from 'node:child_process';
import assert from 'node:assert/strict';
import {checkChangePolicy,checkPrOwnership} from './ci-policy.mjs';
import {readHandoff,renderHandoff} from './handoff-state.mjs';
const fixture=path.resolve('logs','ci-policy-tests-'+Date.now());fs.mkdirSync(fixture,{recursive:true});
const git=(dir,...args)=>spawnSync('git',args,{cwd:dir,encoding:'utf8',windowsHide:true});
const must=(dir,...args)=>{const r=git(dir,...args);assert.equal(r.status,0,r.stderr);return r.stdout.trim();};
for(const task of ['OPS-101','OPS-102']){
 const d=path.join(fixture,'docs/handoffs');fs.mkdirSync(d,{recursive:true});
 const value=JSON.parse(fs.readFileSync('docs/handoffs/OPS-003.json','utf8'));Object.assign(value,{taskId:task,branch:'codex/'+task.toLowerCase(),status:'validated'});
 fs.writeFileSync(path.join(d,task+'.json'),JSON.stringify(value));
}
fs.copyFileSync('docs/PROJECT_STATE.json',path.join(fixture,'docs/PROJECT_STATE.json'));fs.writeFileSync(path.join(fixture,'TASKS.md'),'OPS-101 OPS-102');
const options={root:fixture,taskId:'OPS-101',headBranch:'codex/ops-101',files:['tools/example.mjs','TASKS.md','docs/handoffs/OPS-101.json']};
let count=0;const pass=name=>{count++;console.log('PASS '+name);};
assert.equal(checkChangePolicy(options),true);pass('code plus task/evidence accepted');
assert.throws(()=>checkChangePolicy({...options,files:options.files.filter(f=>f!=='TASKS.md')}));pass('missing task update fails');
assert.throws(()=>checkChangePolicy({...options,files:['tools/example.mjs','TASKS.md']}));pass('missing handoff evidence fails');
assert.throws(()=>checkChangePolicy({...options,headBranch:'codex/other'}));pass('wrong handoff branch fails');
const record=path.join(fixture,'docs/handoffs/OPS-101.json');const saved=fs.readFileSync(record,'utf8');const pending=JSON.parse(saved);pending.status='in_progress';fs.writeFileSync(record,JSON.stringify(pending));assert.throws(()=>checkChangePolicy(options));fs.writeFileSync(record,saved);pass('unvalidated work fails');
const claim={schemaVersion:1,taskId:'OPS-101',agent:'Codex',account:'fixture',branch:'codex/ops-101',baseSha:'a'.repeat(40),summary:'test',nextAction:'test',status:'review',updatedAt:new Date().toISOString(),tokenHash:'b'.repeat(64),issue:1};
assert.equal(checkPrOwnership(claim,'OPS-101','codex/ops-101','Codex'),true);assert.throws(()=>checkPrOwnership(claim,'OPS-101','codex/other','Codex'));assert.throws(()=>checkPrOwnership(claim,'OPS-101','codex/ops-101','OtherAgent'));assert.throws(()=>checkPrOwnership({...claim,status:'released'},'OPS-101','codex/ops-101','Codex'));pass('PR ownership must match active task, branch and handoff owner');
const rendered=renderHandoff(readHandoff(fixture));assert.match(rendered,/OPS-101/);assert.equal(rendered,renderHandoff(readHandoff(fixture)));pass('handoff rendering deterministic');
// Retiring a baseline must not remove protection from the sole integration branch.
const policyFile=path.join(fixture,'docs/PROJECT_STATE.json'),singlePolicy=fs.readFileSync(policyFile,'utf8');
const invalidPolicy=JSON.parse(singlePolicy);invalidPolicy.protectedRefs=[];fs.writeFileSync(policyFile,JSON.stringify(invalidPolicy));
assert.throws(()=>readHandoff(fixture),/single integration branch/);fs.writeFileSync(policyFile,singlePolicy);assert.equal(readHandoff(fixture).project.integrationBranch,'codex/museum-tycoon');pass('single version still requires integration branch protection');
// Two real branches: non-overlapping work merges, shared-file edits conflict.
must(fixture,'init','--initial-branch','codex/museum-tycoon');must(fixture,'config','user.name','CI Fixture');must(fixture,'config','user.email','fixture@example.invalid');must(fixture,'config','commit.gpgsign','false');must(fixture,'config','core.autocrlf','false');must(fixture,'config','core.hooksPath',path.join(fixture,'no-hooks'));
fs.writeFileSync(path.join(fixture,'shared.txt'),'base\n');must(fixture,'add','.');must(fixture,'commit','-m','base');const base=must(fixture,'rev-parse','HEAD');
const commit=(name,value)=>{fs.writeFileSync(path.join(fixture,name),value);must(fixture,'add',name);must(fixture,'commit','-m',name);};
must(fixture,'switch','-c','codex/parallel-a');commit('a.txt','A');must(fixture,'switch','-c','codex/parallel-b',base);commit('b.txt','B');must(fixture,'switch','codex/museum-tycoon');must(fixture,'merge','--ff-only','codex/parallel-a');must(fixture,'merge','--no-edit','codex/parallel-b');assert.ok(fs.existsSync(path.join(fixture,'a.txt'))&&fs.existsSync(path.join(fixture,'b.txt')));pass('two parallel branches merge in sequence without conflict');
must(fixture,'switch','-c','codex/conflict-a',base);commit('shared.txt','A\n');must(fixture,'switch','-c','codex/conflict-b',base);commit('shared.txt','B\n');const conflict=git(fixture,'merge','--no-edit','codex/conflict-a');assert.notEqual(conflict.status,0);assert.ok(must(fixture,'diff','--name-only','--diff-filter=U').includes('shared.txt'));must(fixture,'merge','--abort');assert.equal(fs.readFileSync(path.join(fixture,'shared.txt'),'utf8'),'B\n');pass('conflicting parallel branches block and preserve prior content');
console.log(`CI_POLICY_TESTS_PASS: ${count} cases | ${fixture}`);
