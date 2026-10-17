[CmdletBinding()]
param([switch]$ShowWindow,[switch]$FixtureOnly,[ValidateRange(.01,.1)][double]$TimeStep=.016666667,[ValidateRange(180,1800)][int]$Seconds=360)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$fixture=Join-Path $root ('logs/deadlock-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $fixture | Out-Null
$refs=@()
for($i=0;$i -lt 6;$i++) {
    $id="anonymous-$i"; $folder=Join-Path $fixture "artifacts/$id"
    New-Item -ItemType Directory -Path "$folder/models" -Force | Out-Null
    Copy-Item -LiteralPath "$root/museum-archive/artifacts/sample-books/models/pc.glb" -Destination "$folder/models/pc.glb"
    @{schemaVersion='1.0';id=$id;title="회귀 전시물 $i";description='공개 샘플을 사용하는 동선 회귀';model="artifacts/$id/models/pc.glb";scale=1}|ConvertTo-Json|Set-Content "$folder/artifact.json" -Encoding utf8
    $refs+="artifacts/$id/artifact.json"
}
$rooms=@(
    @{id='first';title='혼잡 회귀';artifacts=@($refs[0..3]);cells=@(26,27,29,28);navigation=@{entrance=@{side='left';slot=4};exit=@{side='right';slot=3};blockedCells=@(13,7,15,9,3,17,11,5,19,25,21,22,16,10,4);visitOrder=@($refs[0],$refs[1],$refs[3],$refs[2])}},
    @{id='second';title='후속 방';artifacts=@($refs[4]);cells=@(0)},
    @{id='third';title='마지막 방';artifacts=@($refs[5]);cells=@(0)}
)
@{schemaVersion='1.0';artifacts=$refs;rooms=$rooms;roomOrder=@('first','second','third')}|ConvertTo-Json -Depth 10|Set-Content "$fixture/manifest.json" -Encoding utf8
Set-Content (Join-Path $root 'logs/deadlock-fixture-path.txt') $fixture -Encoding utf8
if($FixtureOnly){Write-Output "DEADLOCK_FIXTURE: $fixture";return}
& "$root/tools/test-navigation-player.ps1" -ArchivePath $fixture -GuestCount 12 -ShowWindow:$ShowWindow -TimeStep $TimeStep -Seconds $Seconds
