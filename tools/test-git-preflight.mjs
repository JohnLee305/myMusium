import fs from 'node:fs';
import path from 'node:path';
import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {fileURLToPath} from 'node:url';
import {preflight,REQUIRED_DOCS,INTEGRATION_BRANCH} from './git-preflight.mjs';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
const fixture=path.join(root,'logs',`git-preflight-tests-${Date.now()}`);fs.mkdirSync(fixture,{recursive:true});
const git=(cwd,...args)=>{
 const v=spawnSync('git',args,{cwd,encoding:'utf8',windowsHide:true,timeout:15000});
 assert.equal(v.status,0,`${args.join(' ')}: ${v.stderr}`);return v.stdout.trim();
};
const origin=path.join(fixture,'origin.git'),seed=path.join(fixture,'seed');
git(fixture,'init','--bare',origin);git(fixture,'init','--initial-branch',INTEGRATION_BRANCH,seed);
function configure(dir){git(dir,'config','user.name','Preflight Fixture');git(dir,'config','user.email','fixture@example.invalid');git(dir,'config','commit.gpgsign','false');git(dir,'config','core.hooksPath',path.join(fixture,'no-hooks'));}
configure(seed);
for(const doc of REQUIRED_DOCS){const p=path.join(seed,doc);fs.mkdirSync(path.dirname(p),{recursive:true});fs.writeFileSync(p,'fixture\n');}
git(seed,'add','.');git(seed,'commit','-m','initial');git(seed,'remote','add','origin',origin);git(seed,'push','-u','origin',INTEGRATION_BRANCH);
let n=0,passed=0;
const clone=()=>{const d=path.join(fixture,'clone-'+(++n));git(fixture,'clone','--branch',INTEGRATION_BRANCH,origin,d);configure(d);return d;};
const check=(dir,options={})=>preflight({root:dir,taskId:'OPS-002',expectedOrigin:origin,queryPullRequests:()=>[],...options});
const fail=(r,code)=>{assert.equal(r.ok,false);assert.ok(r.blockers.some(b=>b.code===code),JSON.stringify(r));};
const pass=name=>{passed++;console.log('PASS '+name);};
const commit=(d,name)=>{fs.writeFileSync(path.join(d,name),name);git(d,'add',name);git(d,'commit','-m',name);};
const advance=()=>{commit(seed,'remote-'+n);git(seed,'push');};
let d=clone();assert.equal(check(d).ok,true);git(d,'switch','-c','codex/ops-002-fixture');assert.equal(check(d).ok,true);pass('fresh clone and current feature branch');
d=clone();advance();const before=git(d,'rev-parse','HEAD');fail(check(d),'BEHIND_REMOTE');assert.equal(git(d,'rev-parse','HEAD'),before);let result=check(d,{update:true});assert.equal(result.ok,true);assert.equal(result.updated,true);assert.equal(result.head,result.remoteHead);pass('stale integration check preserves HEAD; explicit update fast-forwards');
for(const kind of ['tracked','untracked']){
 d=clone();const f=path.join(d,kind==='tracked'?'STATUS.md':'private.txt');fs.writeFileSync(f,'preserve bytes '+kind);const bytes=fs.readFileSync(f),head=git(d,'rev-parse','HEAD');advance();fail(check(d,{update:true}),'DIRTY_WORKTREE');assert.deepEqual(fs.readFileSync(f),bytes);assert.equal(git(d,'rev-parse','HEAD'),head);assert.equal(git(d,'stash','list'),'');pass(kind+' changes never stashed, overwritten or fast-forwarded');
}
d=clone();fs.writeFileSync(path.join(d,'STATUS.md'),'stashed');git(d,'stash','push','-m','fixture');const stash=git(d,'stash','list');fail(check(d,{update:true}),'STASH_PRESENT');assert.equal(git(d,'stash','list'),stash);pass('stash requires review and remains intact');
d=clone();commit(d,'local-ahead');fail(check(d,{update:true}),'LOCAL_INTEGRATION_AHEAD');git(d,'switch','-c','codex/ops-002-ahead');assert.equal(check(d).ok,true);fail(check(d,{update:true}),'UPDATE_BRANCH_REQUIRED');pass('local integration ahead blocked; current feature allowed but never auto-updated');
d=clone();commit(d,'local-diverged');advance();const diverged=git(d,'rev-parse','HEAD');fail(check(d,{update:true}),'DIVERGED');assert.equal(git(d,'rev-parse','HEAD'),diverged);pass('diverged history preserved');
d=clone();git(d,'switch','-c','codex/museum-baseline');const baseline=git(d,'rev-parse','HEAD');fail(check(d,{update:true}),'PROTECTED_BASELINE');assert.equal(git(d,'rev-parse','HEAD'),baseline);pass('protected baseline untouched');
d=clone();git(d,'switch','--detach');fail(check(d),'DETACHED_HEAD');pass('detached HEAD requires task branch');
d=clone();fail(check(d,{queryPullRequests:()=>[{number:1,title:'unlabelled work',headRefName:'codex/ops-002-other'}]}),'DUPLICATE_TASK_PR');assert.equal(check(d,{queryPullRequests:()=>[{title:'OPS-002 own PR',headRefName:INTEGRATION_BRANCH}]}).ok,true);assert.equal(check(d,{queryPullRequests:()=>[{title:'OPS-0020 unrelated',headRefName:'codex/unrelated'}]}).ok,true);pass('duplicate task PR blocks; own and unrelated PRs allowed');
fail(check(d,{queryPullRequests:()=>{throw Error('offline');}}),'PR_CHECK_FAILED');pass('unavailable PR inventory blocks');
d=clone();const missing=path.join(fixture,'missing.git');git(d,'remote','set-url','origin',missing);const intact=git(d,'rev-parse','HEAD');fail(check(d,{expectedOrigin:missing,update:true}),'FETCH_FAILED');assert.equal(git(d,'rev-parse','HEAD'),intact);pass('remote failure preserves local source');
d=clone();fail(check(d,{expectedOrigin:'different'}),'WRONG_ORIGIN');fail(check(d,{root:path.join(d,'docs')}),'WRONG_ROOT');pass('wrong origin and nested directory rejected');
d=clone();fs.unlinkSync(path.join(d,'AGENTS.md'));git(d,'add','.');git(d,'commit','-m','missing doc');git(d,'switch','-c','codex/ops-002-no-doc');fail(check(d),'MISSING_DOCUMENT');pass('missing required document blocks');
d=clone();git(d,'branch','codex/ops-002-other');fail(check(d),'DUPLICATE_TASK_BRANCH');pass('same task branch without a PR blocks');
d=clone();const other=path.join(fixture,'worktree');git(d,'worktree','add','-b','codex/other-fixture',other);git(d,'switch','-c','codex/ops-002-multi');result=check(d);assert.equal(result.ok,true);assert.ok(result.worktrees.includes('codex/other-fixture'));assert.equal(check(other,{taskId:'OPS-003'}).ok,true);assert.equal(git(other,'branch','--show-current'),'codex/other-fixture');pass('multiple worktrees inspected without switching other checkout');
d=clone();git(seed,'push','origin','--delete',INTEGRATION_BRANCH);fail(check(d),'MISSING_REMOTE_BRANCH');pass('missing remote integration branch blocks');
console.log(`GIT_PREFLIGHT_TESTS_PASS: ${passed} cases | ${fixture}`);
