[CmdletBinding()]
param([ValidateSet('list','claim','update','release')][string]$Action='list',[string]$TaskId,[string]$Agent,[string]$Summary,[string]$Status,[string]$Next,[string]$Root=(Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference='Stop'
$command=Get-Command node -ErrorAction SilentlyContinue
$node=if($command){$command.Source}else{Join-Path $env:USERPROFILE '.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin/node.exe'}
if(-not(Test-Path -LiteralPath $node)){throw 'Node.js is required.'}
$arguments=@((Join-Path $PSScriptRoot 'task-coordination.mjs'),$Action,'--root',$Root)
foreach($entry in @(@('task',$TaskId),@('agent',$Agent),@('summary',$Summary),@('status',$Status),@('next',$Next))){if($entry[1]){$arguments+=('--'+$entry[0]);$arguments+=[string]$entry[1]}}
& $node @arguments
exit $LASTEXITCODE
