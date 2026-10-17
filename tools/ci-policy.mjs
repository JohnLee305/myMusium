import fs from 'node:fs';
import path from 'node:path';
import {spawnSync} from 'node:child_process';
import {pathToFileURL} from 'node:url';
import assert from 'node:assert/strict';
import {readHandoff,renderHandoff} from './handoff-state.mjs';
import {validateClaim,claimRef} from './task-coordination.mjs';
export function checkChangePolicy({root,files,taskId,headBranch}){
 assert.match(taskId,/^[A-Z]+-\d+$/,'PR title/branch must identify one task ID');
 const record=`docs/handoffs/${taskId}.json`;
 assert.ok(files.includes(record),'Update your task handoff JSON in every PR');
 const {tasks}=readHandoff(root),task=tasks.find(t=>t.taskId===taskId);
 assert.ok(task&&task.status==='validated','Task handoff must describe completed validation');
 if(headBranch)assert.equal(task.branch,headBranch,'Handoff branch must match PR head');
 assert.ok(fs.readFileSync(path.join(root,'TASKS.md'),'utf8').includes(taskId),'Register task ID in TASKS.md');
 if(files.some(f=>/^(apps\/|tools\/|museum-archive\/schemas\/|\.agents\/|\.github\/)/.test(f)))assert.ok(files.includes('TASKS.md'),'Code/config changes must update TASKS.md');
 return true;
}
export function checkPrOwnership(claim,taskId,headBranch,owner){
 validateClaim(claim);assert.equal(claim.taskId,taskId);assert.equal(claim.branch,headBranch);assert.equal(claim.agent,owner);assert.ok(['in_progress','blocked','review'].includes(claim.status),'PR must have an active claimed owner');return true;
}
async function publicClaim(taskId){
 // Read the same ownership ref through public Git transport: shared CI runner IPs
 // can exhaust the anonymous REST quota. No credentials or workflow write scope needed.
 const result=spawnSync('git',['fetch','--no-tags','https://github.com/JohnLee305/myMusium.git','refs/heads/'+claimRef(taskId)],{encoding:'utf8',windowsHide:true,timeout:45000});
 assert.equal(result.status,0,'Cannot fetch shared task owner');
 const commit=spawnSync('git',['show','-s','--format=%B','FETCH_HEAD'],{encoding:'utf8',windowsHide:true});
 assert.equal(commit.status,0);assert.ok(commit.stdout.startsWith('MUSEUM_TASK_CLAIM\n'));
 return JSON.parse(commit.stdout.slice('MUSEUM_TASK_CLAIM\n'.length));
}
if(process.argv[1]&&import.meta.url===pathToFileURL(path.resolve(process.argv[1])).href){
 const root=process.cwd(),state=readHandoff(root);
 assert.equal(fs.readFileSync(path.join(root,'docs/LIVE_STATUS.md'),'utf8').replace(/\r\n/g,'\n'),renderHandoff(state),'Generated LIVE_STATUS differs from handoff sources');
 const base=process.env.CI_BASE_SHA;
 if(base){
  assert.match(base,/^[0-9a-f]{40}$/);
  const diff=spawnSync('git',['diff','--name-only','-z',base,'HEAD'],{encoding:'utf8',windowsHide:true});assert.equal(diff.status,0,'Cannot inspect base diff');
  const files=diff.stdout.split('\0').filter(Boolean);
  const title=process.env.CI_PR_TITLE??'';
  const id=title.match(/\b[A-Z]+-\d+\b/)?.[0]??files.map(f=>f.match(/^docs\/handoffs\/([A-Z]+-\d+)\.json$/)?.[1]).find(Boolean);
  if(files.length){
   checkChangePolicy({root,files,taskId:id,headBranch:process.env.CI_HEAD_BRANCH||undefined});
   if(process.env.GITHUB_ACTIONS==='true'&&process.env.CI_HEAD_BRANCH){const task=state.tasks.find(t=>t.taskId===id);checkPrOwnership(await publicClaim(id),id,process.env.CI_HEAD_BRANCH,task.owner);}
  }
 }
 console.log('CI_POLICY_PASS');
}
