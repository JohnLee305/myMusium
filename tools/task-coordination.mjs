import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {spawnSync} from 'node:child_process';
import {pathToFileURL} from 'node:url';
import assert from 'node:assert/strict';
export const REPOSITORY='JohnLee305/myMusium';
export const CLAIM_PREFIX='codex/claims/';
export const claimRef=task=>{assert.match(task,/^[A-Z]+-\d+$/);return CLAIM_PREFIX+task.toLowerCase();};
const hash=s=>crypto.createHash('sha256').update(s).digest('hex');
function run(command,args,cwd,input){
 const r=spawnSync(command,args,{cwd,input,encoding:'utf8',windowsHide:true,timeout:60000,maxBuffer:4*1024*1024});
 if(r.error||r.status!==0)throw Error(`${command} failed; existing task ownership preserved`);
 return r.stdout.trim();
}
export function validateClaim(c){
 assert.equal(c.schemaVersion,1);claimRef(c.taskId);
 for(const k of ['agent','account','summary','nextAction'])assert.ok(typeof c[k]==='string'&&c[k].trim()&&c[k].length<=300);
 assert.ok(c.branch.startsWith('codex/')&&!c.branch.startsWith(CLAIM_PREFIX)&&c.branch.toLowerCase().includes(c.taskId.toLowerCase()));
 assert.match(c.baseSha,/^[a-f0-9]{40}$/);assert.match(c.tokenHash,/^[a-f0-9]{64}$/);
 assert.ok(['in_progress','blocked','review','done','released'].includes(c.status));assert.ok(!Number.isNaN(Date.parse(c.updatedAt)));
 assert.ok(c.issue===null||Number.isInteger(c.issue)&&c.issue>0);
 return c;
}
export function githubBackend(root=process.cwd()){
 const api=(endpoint,payload,method)=>JSON.parse(run('gh',['api',...(method?['--method',method]:[]),endpoint,...(payload?['--input','-']:[])],root,payload?JSON.stringify(payload):undefined)||'null');
 const repo='repos/'+REPOSITORY;
 return {
  account:()=>api('user').login,
  list:()=>{
   const pages=JSON.parse(run('gh',['api','--paginate','--slurp',`${repo}/git/matching-refs/heads/${CLAIM_PREFIX}`],root));return pages.flat().map(r=>r.ref.replace('refs/heads/',''));
  },
  read:ref=>{
   // matching-refs returns [] for an unclaimed task; auth/network errors remain errors.
   const matches=api(`${repo}/git/matching-refs/heads/${ref}`),item=matches.find(r=>r.ref==='refs/heads/'+ref);
   if(!item)return null;
   const commit=api(`${repo}/git/commits/${item.object.sha}`);
   assert.ok(commit.message.startsWith('MUSEUM_TASK_CLAIM\n'));
   return {sha:item.object.sha,claim:validateClaim(JSON.parse(commit.message.slice('MUSEUM_TASK_CLAIM\n'.length)))};
  },
  commit:(claim,parent)=>api(`${repo}/git/commits`,{message:'MUSEUM_TASK_CLAIM\n'+JSON.stringify(validateClaim(claim)),tree:api(`${repo}/git/commits/${parent}`).tree.sha,parents:[parent]}).sha,
  create:(ref,sha)=>api(`${repo}/git/refs`,{ref:'refs/heads/'+ref,sha}),
  update:(ref,sha)=>api(`${repo}/git/refs/heads/${ref}`,{sha,force:false},'PATCH'),
  issue:claim=>api(`${repo}/issues`,{title:`[${claim.taskId}] ${claim.agent} · ${claim.summary}`,body:issueBody(claim),assignees:[claim.account]}).number,
  publish:claim=>api(`${repo}/issues/${claim.issue}`,{title:`[${claim.taskId}] ${claim.agent} · ${claim.status} · ${claim.summary}`,body:issueBody(claim),state:claim.status==='done'?'closed':'open'},'PATCH'),
 };
}
function issueBody(c){return `에이전트 작업 현황 (공개 개발 정보만 기록)\n\n| 항목 | 값 |\n|---|---|\n| 작업 | ${c.taskId} |\n| 담당 | ${c.agent} / ${c.account} |\n| 상태 | ${c.status} |\n| 브랜치 | ${c.branch} |\n| 시작 SHA | ${c.baseSha} |\n| 갱신 UTC | ${c.updatedAt} |\n\n다음 행동: ${c.nextAction}\n\n원자적 담당 정본: \`${claimRef(c.taskId)}\`. Issue가 닫혔다는 이유만으로 담당권한이 해제되었다고 가정하지 말고 \`node tools/task-coordination.mjs list\`로 확인한다.\n`;}
export function acquire(backend,claim,token){
 validateClaim(claim);assert.equal(claim.tokenHash,hash(token));const ref=claimRef(claim.taskId);
 const existing=backend.read(ref);if(existing&&existing.claim.status!=='released'&&existing.claim.tokenHash===hash(token)){assert.equal(existing.claim.branch,claim.branch);return existing;}if(existing&&existing.claim.status!=='released')throw Error(`TASK_ALREADY_CLAIMED: ${claim.taskId} / ${existing.claim.agent} / ${existing.claim.branch}`);
 const sha=backend.commit(claim,existing?.sha??claim.baseSha);
 try{if(existing)backend.update(ref,sha);else backend.create(ref,sha);}catch{throw Error('CLAIM_NOT_ACQUIRED: another agent won or GitHub failed; do not start work');}
 const confirmed=backend.read(ref);assert.equal(confirmed?.sha,sha);return confirmed;
}
export function owned(backend,task,token){
 const record=backend.read(claimRef(task));assert.ok(record,'TASK_NOT_CLAIMED');assert.equal(record.claim.tokenHash,hash(token),'TASK_OWNED_BY_ANOTHER_SESSION');assert.notEqual(record.claim.status,'released','TASK_ALREADY_RELEASED');return record;
}
export function update(backend,task,token,fields,allowRelease=false){
 assert.ok(fields.status!=='released'||allowRelease,'Use release after done; do not skip completion');
 const previous=owned(backend,task,token);
 const claim=validateClaim({...previous.claim,...fields,taskId:task,tokenHash:previous.claim.tokenHash,branch:previous.claim.branch,account:previous.claim.account,baseSha:previous.claim.baseSha,updatedAt:new Date().toISOString()});
 const sha=backend.commit(claim,previous.sha);backend.update(claimRef(task),sha);
 const confirmed=backend.read(claimRef(task));assert.equal(confirmed?.sha,sha,'CONCURRENT_UPDATE');return confirmed;
}
export function release(backend,task,token){
 const current=owned(backend,task,token);assert.equal(current.claim.status,'done','Mark done and publish evidence before releasing');
 return update(backend,task,token,{status:'released'},true);
}
export function listClaims(backend){return backend.list().map(ref=>{const r=backend.read(ref);assert.ok(r);return {...r.claim,claimSha:r.sha,issueUrl:r.claim.issue?`https://github.com/${REPOSITORY}/issues/${r.claim.issue}`:null};});}
export function receiptPath(root,task){claimRef(task);return path.join(root,'logs','task-claims',task+'.json');}
export function checkOwnership(root,task,branch,backend=githubBackend(root)){
 const current=backend.read(claimRef(task));if(!current||current.claim.status==='released')return {ok:false,code:'TASK_UNCLAIMED',message:'먼저 task-coordination claim으로 담당을 등록해 주세요.'};
 const file=receiptPath(root,task);let receipt;try{receipt=JSON.parse(fs.readFileSync(file,'utf8'));}catch{return {ok:false,code:'TASK_OWNED_BY_ANOTHER_SESSION',message:`담당: ${current.claim.agent} / ${current.claim.branch}. 원 담당의 인계를 먼저 확인해 주세요.`};}
 if(current.claim.tokenHash!==hash(receipt.token)||current.claim.branch!==branch||current.claim.status==='done')return {ok:false,code:'TASK_OWNED_BY_ANOTHER_SESSION',message:`담당: ${current.claim.agent} / ${current.claim.branch}. 현재 세션은 작업을 시작할 수 없습니다.`};
 return {ok:true,agent:current.claim.agent,status:current.claim.status,issue:current.claim.issue};
}
function localRoot(root){
 const canonical=p=>process.platform==='win32'?fs.realpathSync(p).toLowerCase():fs.realpathSync(p);
 assert.equal(canonical(root),canonical(run('git',['rev-parse','--show-toplevel'],root)));
 assert.match(run('git',['remote','get-url','origin'],root),/^(?:https:\/\/github\.com\/JohnLee305\/myMusium(?:\.git)?|git@github\.com:JohnLee305\/myMusium(?:\.git)?)\/?$/i);
 assert.equal(run('git',['check-ignore','logs/task-claims/private.json'],root),'logs/task-claims/private.json','logs receipt must be ignored');
 return root;
}
export function main(args=process.argv.slice(2)){
 const action=args.shift(),options={};
 for(let i=0;i<args.length;i++){assert.ok(args[i].startsWith('--')&&args[i+1]);options[args[i].slice(2)]=args[++i];}
 const root=localRoot(path.resolve(options.root??process.cwd())),backend=githubBackend(root);
 if(action==='list'){console.log(JSON.stringify(listClaims(backend).filter(c=>options.all==='true'||c.status!=='released').map(({tokenHash,...c})=>c),null,2));return;}
 const task=options.task;claimRef(task);const file=receiptPath(root,task);
 if(action==='claim'){
  const savedReceipt=fs.existsSync(file)?JSON.parse(fs.readFileSync(file,'utf8')):null;
  const branch=run('git',['branch','--show-current'],root),baseSha=run('git',['rev-parse','HEAD'],root);
  const token=savedReceipt?.token??crypto.randomUUID();if(savedReceipt)assert.equal(savedReceipt.branch,branch,'Existing receipt belongs to a different branch');const claim=validateClaim({schemaVersion:1,taskId:task,agent:options.agent,account:backend.account(),branch,baseSha,summary:options.summary,nextAction:options.next??'구현·검증 진행',status:'in_progress',updatedAt:new Date().toISOString(),tokenHash:hash(token),issue:null});
  // Save the receipt first so a lost response cannot leave an ownerless claim.
  fs.mkdirSync(path.dirname(file),{recursive:true});if(!savedReceipt)fs.writeFileSync(file,JSON.stringify({token,taskId:task,branch}),{flag:'wx',mode:0o600});
  const record=acquire(backend,claim,token);const issue=record.claim.issue??backend.issue(record.claim);const linked=record.claim.issue?record:update(backend,task,token,{issue});backend.publish(linked.claim);
  console.log(JSON.stringify({taskId:task,agent:claim.agent,branch,issueUrl:`https://github.com/${REPOSITORY}/issues/${issue}`,status:'in_progress'}));return;
 }
 const receipt=JSON.parse(fs.readFileSync(file,'utf8'));
 if(action==='update'){
  const record=update(backend,task,receipt.token,{...(options.agent?{agent:options.agent}:{}),...(options.status?{status:options.status}:{}),...(options.next?{nextAction:options.next}:{})});
  let linked=record;if(!record.claim.issue)linked=update(backend,task,receipt.token,{issue:backend.issue(record.claim)});
  backend.publish(linked.claim);console.log(JSON.stringify({taskId:task,status:linked.claim.status,issue:linked.claim.issue}));return;
 }
 if(action==='release'){
  const record=owned(backend,task,receipt.token);assert.equal(record.claim.status,'done');assert.ok(record.claim.issue,'Publish a task issue before release');backend.publish(record.claim);release(backend,task,receipt.token);fs.unlinkSync(file);console.log('TASK_RELEASED: '+task);return;
 }
 throw Error('Usage: task-coordination.mjs list | claim --task ID --agent NAME --summary TEXT | update --task ID --status in_progress|blocked|review|done --next TEXT | release --task ID');
}
if(process.argv[1]&&import.meta.url===pathToFileURL(path.resolve(process.argv[1])).href){try{main();}catch(e){console.error(e.message);process.exitCode=1;}}
