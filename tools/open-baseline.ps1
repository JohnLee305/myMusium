[CmdletBinding()]
param([switch]$Vr)
$ErrorActionPreference='Stop'
$baselineRoot='C:\Dev\vr-museum-snapshots\museum-baseline-20261015'
$player=Join-Path $baselineRoot 'Builds\Windows\VRMuseum.exe'
$archive=Join-Path $baselineRoot 'museum-archive'
if(-not(Test-Path -LiteralPath $player)){throw '기준 실행본 백업을 찾을 수 없습니다.'}
$arguments=@('-museum-archive',('"'+$archive+'"'),'-screen-fullscreen','0','-screen-width','1280','-screen-height','800')
if($Vr){$arguments+='-museum-vr'}
# Interactive viewer requested by the user; show its window.
Start-Process -FilePath $player -WorkingDirectory $baselineRoot -ArgumentList $arguments
