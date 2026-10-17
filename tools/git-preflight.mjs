import fs from 'node:fs';
import path from 'node:path';
import {spawnSync} from 'node:child_process';
import {pathToFileURL} from 'node:url';
import {checkOwnership,CLAIM_PREFIX} from './task-coordination.mjs';

export const INTEGRATION_BRANCH='codex/museum-tycoon';
export const REQUIRED_DOCS=['AGENTS.md','docs/GIT_MULTI_AGENT_WORKFLOW.md','docs/LIVE_STATUS.md','docs/PROJECT_STATE.json','PROJECT_STATUS.md','TASKS.md','STATUS.md','WORKLOG.md','docs/WORK_HANDOFF.md'];
const execute=(command,args,cwd)=>{
 const r=spawnSync(command,args,{cwd,encoding:'utf8',windowsHide:true,timeout:60000,maxBuffer:2*1024*1024});
 if(r.error||r.status!==0)throw Error(`${command} failed (${r.error?.code??r.status})`);
 return r.stdout.trim(); // Do not echo remote errors: URLs may contain credentials.
};
const normalizeRoot=p=>process.platform==='win32'?fs.realpathSync(p).toLowerCase():fs.realpathSync(p);
function repositoryId(url){
 const m=url.match(/^(?:https:\/\/github\.com\/|git@github\.com:|ssh:\/\/git@github\.com\/)([^/]+\/[^/]+?)(?:\.git)?\/?$/i);
 return m?.[1].toLowerCase();
}
export function githubPullRequests(root){
 const value=JSON.parse(execute('gh',['pr','list','--repo','JohnLee305/myMusium','--state','open','--limit','1000','--json','number,title,headRefName,url'],root));
 if(!Array.isArray(value)||value.length>=1000)throw Error('PR inventory incomplete');
 return value;
}
export function preflight({root=process.cwd(),taskId,update=false,expectedOrigin,queryPullRequests=githubPullRequests,queryOwnership=checkOwnership}={}){
 const report={ok:false,taskId,root:path.resolve(root),branch:null,head:null,remoteHead:null,ahead:null,behind:null,updated:false,changes:[],stashes:[],worktrees:'',pullRequests:[],blockers:[]};
 const block=(code,message)=>report.blockers.push({code,message});
 const git=(...args)=>execute('git',args,report.root);
 if(!/^[A-Z]+-\d+$/.test(taskId??'')){block('TASK_REQUIRED','작업 ID를 지정해 주세요. 예: OPS-002');return report;}
 if(Object.keys(process.env).some(k=>/^GIT_(DIR|WORK_TREE|INDEX_FILE|COMMON_DIR|OBJECT_DIRECTORY|ALTERNATE_OBJECT_DIRECTORIES)$/i.test(k))){block('GIT_ENV_OVERRIDE','Git 경로 재지정 환경변수를 해제한 별도 환경에서 확인해 주세요.');return report;}
 try{
  if(normalizeRoot(git('rev-parse','--show-toplevel'))!==normalizeRoot(report.root)){block('WRONG_ROOT','저장소 루트를 명시해 주세요. 하위 폴더에서는 진행하지 않습니다.');return report;}
  const origin=git('remote','get-url','origin');
  if(expectedOrigin?origin!==expectedOrigin:repositoryId(origin)!=='johnlee305/mymusium'){block('WRONG_ORIGIN','origin이 지정된 박물관 저장소와 다릅니다.');return report;}
  report.branch=git('branch','--show-current');report.head=git('rev-parse','HEAD');
  report.changes=git('status','--porcelain=v1','--untracked-files=all').split('\n').filter(Boolean);
  report.stashes=git('stash','list','--format=%gd').split('\n').filter(Boolean);
  report.worktrees=git('worktree','list','--porcelain');
  if(!report.branch)block('DETACHED_HEAD','작업 전용 브랜치를 만든 뒤 다시 확인해 주세요.');
  else if(report.branch!==INTEGRATION_BRANCH&&!report.branch.startsWith('codex/'))block('UNSUPPORTED_BRANCH','codex/ 전용 작업 브랜치를 사용해 주세요.');
  if(report.branch==='codex/museum-baseline')block('PROTECTED_BASELINE','보존 기준판에서는 개발을 시작할 수 없습니다.');
  if(report.changes.length)block('DIRTY_WORKTREE','미커밋/미추적 자료를 보존하고 별도 작업폴더를 사용해 주세요. 자동 stash는 하지 않습니다.');
  if(report.stashes.length)block('STASH_PRESENT','저장된 stash의 담당과 내용을 확인해 주세요. 자동 적용·삭제하지 않습니다.');
  // Fetch changes only remote-tracking refs; never switches branches or rewrites files.
  try{git('fetch','--prune','origin');}catch{block('FETCH_FAILED','원격 최신화에 실패했습니다. 기존 파일과 브랜치를 보존합니다.');return report;}
  try{report.remoteHead=git('rev-parse','--verify',`refs/remotes/origin/${INTEGRATION_BRANCH}^{commit}`);}catch{block('MISSING_REMOTE_BRANCH','원격 통합 브랜치가 없습니다.');return report;}
  [report.ahead,report.behind]=git('rev-list','--left-right','--count',`HEAD...${report.remoteHead}`).split(/\s+/).map(Number);
  try{
   report.pullRequests=queryPullRequests(report.root);
   const id=new RegExp(`\\b${taskId}\\b`,'i');
   const other=report.pullRequests.filter(p=>id.test(p.title+' '+p.headRefName)&&p.headRefName!==report.branch);
   if(other.length)block('DUPLICATE_TASK_PR','같은 작업 ID의 다른 브랜치 PR이 열려 있습니다. 담당을 먼저 확인해 주세요.');
  }catch{block('PR_CHECK_FAILED','열린 PR을 확인하지 못했습니다. 인증·연결을 확인하고 다시 실행해 주세요.');}
  try{report.taskOwner=queryOwnership(report.root,taskId,report.branch);if(!report.taskOwner.ok)block(report.taskOwner.code,report.taskOwner.message);}catch{block('TASK_OWNER_CHECK_FAILED','GitHub 담당 정보를 확인하지 못했습니다. 새 작업을 시작하지 않습니다.');}
  const taskPattern=new RegExp(`\\b${taskId}\\b`,'i');
  report.taskBranches=git('for-each-ref','--format=%(refname:short)','refs/heads','refs/remotes/origin').split('\n').filter(ref=>!ref.replace(/^origin\//,'').startsWith(CLAIM_PREFIX)&&taskPattern.test(ref));
  if(report.taskBranches.some(ref=>ref.replace(/^origin\//,'')!==report.branch))block('DUPLICATE_TASK_BRANCH','같은 작업 ID의 다른 로컬/원격 브랜치가 있습니다. 담당·완료 여부를 확인해 주세요.');
  if(report.behind>0&&report.ahead>0)block('DIVERGED','로컬과 통합 브랜치가 분기됐습니다. 자동 병합하지 않습니다.');
  else if(report.behind>0&&!(update&&report.branch===INTEGRATION_BRANCH))block('BEHIND_REMOTE','최신 통합 커밋이 없습니다. 깨끗한 통합 브랜치에서만 --update로 FF할 수 있습니다.');
  if(report.branch===INTEGRATION_BRANCH&&report.ahead>0)block('LOCAL_INTEGRATION_AHEAD','통합 브랜치에 미게시 커밋이 있습니다. PR/기록을 확인해 주세요.');
  if(update&&report.branch!==INTEGRATION_BRANCH)block('UPDATE_BRANCH_REQUIRED','--update는 정확히 codex/museum-tycoon에서만 허용합니다.');
  for(const doc of REQUIRED_DOCS)if(!fs.existsSync(path.join(report.root,doc)))block('MISSING_DOCUMENT',`필수 문서가 없습니다: ${doc}`);
  if(!report.blockers.length&&update&&report.behind>0){
   if(git('status','--porcelain=v1','--untracked-files=all')){block('CHANGED_DURING_CHECK','점검 도중 파일이 변경됐습니다. 동기화하지 않습니다.');return report;}
   git('merge','--ff-only',report.remoteHead);report.head=git('rev-parse','HEAD');
   if(report.head!==report.remoteHead)throw Error('FF verification failed');
   report.updated=true;report.behind=0;report.ahead=0;
  }
  report.ok=report.blockers.length===0;
 }catch{block('GIT_CHECK_FAILED','Git 저장소 점검에 실패했습니다. 경로·Git 설치·접근 권한을 확인해 주세요.');}
 return report;
}
if(process.argv[1]&&import.meta.url===pathToFileURL(path.resolve(process.argv[1])).href){
 try{
  const args=process.argv.slice(2),options={};
  for(let i=0;i<args.length;i++){
   if(args[i]==='--update')options.update=true;
   else if(args[i]==='--root'&&args[i+1])options.root=args[++i];
   else if(args[i]==='--task'&&args[i+1])options.taskId=args[++i];
   else throw Error('사용: node tools/git-preflight.mjs --task OPS-002 [--root 저장소루트] [--update]');
  }
  const result=preflight(options);console.log(JSON.stringify(result,null,2));process.exitCode=result.ok?0:1;
 }catch(e){console.error(e.message);process.exitCode=2;}
}
