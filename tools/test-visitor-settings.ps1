[CmdletBinding()]
param([ValidateSet(6,12)][int]$GuestCount=12)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$fixture=Join-Path $root "logs/visitor-settings-$GuestCount-$(Get-Date -Format yyyyMMdd-HHmmss)"
$folder=Join-Path $fixture 'artifacts/settings-test'
New-Item -ItemType Directory -Force "$folder/models" | Out-Null
Copy-Item "$root/museum-archive/artifacts/sample-books/models/pc.glb" "$folder/models/pc.glb"
@{schemaVersion='1.0';id='settings-test';title='설정 시험';description='관람객 설정 유지 검사';model='artifacts/settings-test/models/pc.glb';scale=1}|ConvertTo-Json|Set-Content "$folder/artifact.json" -Encoding utf8
$entries=@('artifacts/settings-test/artifact.json')
@{schemaVersion='1.0';artifacts=$entries;rooms=@(@{id='first';title='설정 시험실';artifacts=$entries;cells=@(2)})}|ConvertTo-Json -Depth 8|Set-Content "$fixture/manifest.json" -Encoding utf8
New-Item -ItemType File "$fixture/.visitor-test-fixture" | Out-Null
$file=Join-Path $fixture 'visitors.json'
@{schemaVersion='1.0';enabled=$true;count=$GuestCount;speed=1.2;appearances=@(@{id='quaternius-casual2';enabled=$true;weight=2})}|ConvertTo-Json -Depth 6|Set-Content $file -Encoding utf8
# The player restores text using UTF-8 without BOM; compare decoded content.
$original=Get-Content $file -Raw
foreach($case in @('live','restart')) {
    $log="$fixture/$case.log"
    $argsList=@('-screen-fullscreen','0','-screen-width','1280','-screen-height','800','-museum-archive',"`"$fixture`"",'-museum-screenshot',"`"$fixture/$case.png`"",'-museum-visitor-settings-smoke','-logFile',"`"$log`"")
    if($case -eq 'restart'){$argsList+='-museum-visitor-settings-startup-only'}
    $process=Start-Process "$root/Builds/Windows/VRMuseum.exe" -ArgumentList $argsList -WindowStyle Hidden -PassThru
    $null=$process.Handle
    if(-not $process.WaitForExit(120000)){try{$process.Kill()}catch{};throw "Settings timeout: $case"}
    $output=Get-Content $log -Raw
    if($process.ExitCode -ne 0 -or -not $output.Contains('VISITOR_PREFERENCES_SMOKE_PASS') -or -not $output.Contains('MUSEUM_SMOKE_PASS') -or $output -match '(NullReferenceException:|MissingReferenceException:|error CS\d+)'){throw "Settings failed: $case; $log"}
    if((Get-Content $file -Raw) -cne $original){throw 'Fixture settings changed after restore'}
    Write-Output "PASS: $case"
}
Write-Output "VISITOR_SETTINGS_TESTS_PASS: 2 processes | $fixture"
