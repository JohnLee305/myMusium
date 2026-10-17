[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$TaskId,[string]$Root=(Split-Path $PSScriptRoot -Parent),[switch]$Update)
$ErrorActionPreference='Stop'
$nodeCommand=Get-Command node -ErrorAction SilentlyContinue
$node=if($nodeCommand){$nodeCommand.Source}else{Join-Path $env:USERPROFILE '.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin/node.exe'}
if(-not(Test-Path -LiteralPath $node)){throw 'Node.js is required. Install Node.js or add it to PATH.'}
$arguments=@((Join-Path $PSScriptRoot 'git-preflight.mjs'),'--root',$Root,'--task',$TaskId)
if($Update){$arguments+='--update'}
& $node @arguments
exit $LASTEXITCODE
