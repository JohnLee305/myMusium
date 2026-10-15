$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$fixtures = Join-Path $root "logs/room-tests-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
$source = Join-Path $fixtures 'source'
New-Item -ItemType Directory -Path $source -Force | Out-Null
$paths = @()
foreach ($i in 1..31) {
    $id = "room-test-$i"
    $folder = Join-Path $source "artifacts/$id"
    New-Item -ItemType Directory -Path "$folder/models" -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $root 'museum-archive/artifacts/sample-vase/models/pc.glb') -Destination "$folder/models/pc.glb"
    @{schemaVersion='1.0';id=$id;title="전시품 $i";description='방 이동과 모델 해제를 검증하는 샘플입니다.';model="artifacts/$id/models/pc.glb";scale=1} | ConvertTo-Json | Set-Content "$folder/artifact.json" -Encoding utf8
    $paths += "artifacts/$id/artifact.json"
}
@{schemaVersion='1.0';artifacts=$paths} | ConvertTo-Json | Set-Content "$source/manifest.json" -Encoding utf8
$shell=(Get-Process -Id $PID).Path
$player=Join-Path $root 'Builds/Windows/VRMuseum.exe'
foreach ($case in @('legacy','empty','partial','invalid-membership','full-room')) {
    $caseRoot=Join-Path $fixtures $case
    Copy-Item -LiteralPath $source -Destination $caseRoot -Recurse
    $manifest=Get-Content "$caseRoot/manifest.json" -Raw | ConvertFrom-Json
    if ($case -ne 'legacy') {
        $rooms=@(@{id='room-a';title='첫 전시실';artifacts=@($paths[0..29])},@{id='room-b';title='다음 전시실';artifacts=@($paths[30])})
        if ($case -eq 'empty') { $rooms=@($rooms[0],@{id='room-empty';title='빈 전시실';artifacts=@()},$rooms[1]) }
        if ($case -eq 'invalid-membership') { $rooms[1].artifacts=@($paths[0]) }
        if ($case -eq 'full-room') { $rooms=@(@{id='room-full';title='초과 전시실';artifacts=$paths}) }
        $manifest | Add-Member -NotePropertyName rooms -NotePropertyValue $rooms
    }
    $manifest | ConvertTo-Json -Depth 8 | Set-Content "$caseRoot/manifest.json" -Encoding utf8
    if ($case -eq 'partial') {
        $file="$caseRoot/artifacts/room-test-31/artifact.json"
        $item=Get-Content $file -Raw | ConvertFrom-Json; $item.model='artifacts/room-test-31/models/missing.glb'
        $item | ConvertTo-Json | Set-Content $file -Encoding utf8
    }
    $validation=& $shell -NoProfile -File (Join-Path $PSScriptRoot 'validate-archive.ps1') -ArchivePath $caseRoot 2>&1
    $invalid=$case -in @('partial','invalid-membership','full-room')
    if ($LASTEXITCODE -ne [int]$invalid) { throw "Validator wrong result: $case $validation" }
    $log=Join-Path $fixtures "$case.log"; $png=Join-Path $fixtures "$case.png"
    $playerArgs=@('-screen-fullscreen','0','-screen-width','1280','-screen-height','800','-museum-archive',"`"$caseRoot`"",'-museum-screenshot',"`"$png`"",'-logFile',"`"$log`"")
    if ($case -in @('invalid-membership','full-room')) { $playerArgs+='-museum-expect-artifact-failure' } else { $playerArgs+='-museum-tour-smoke' }
    $p=Start-Process -FilePath $player -ArgumentList $playerArgs -PassThru
    if (-not $p.WaitForExit(180000)) { throw "Room test timeout: $case" }
    $output=Get-Content $log -Raw
    if ($p.ExitCode -ne 0 -or -not $output.Contains('MUSEUM_SMOKE_PASS')) { throw "Room player test failed: $case; $log" }
    if ($case -notin @('invalid-membership','full-room')) {
        if (-not $output.Contains('ROOM_TOUR_PASS')) { throw "Tour failed: $case" }
        if (($output | Select-String -Pattern 'ROOM_RELEASED:' -AllMatches).Matches.Count -ne 4) { throw 'Previous rooms were not released' }
    }
    if ($case -eq 'partial' -and -not $output.Contains('room=2/2 loaded=0 failed=1')) { throw 'Expected isolated failure in second room' }
    if ($case -eq 'empty' -and -not $output.Contains('liveSlots=0 imports=0 passed=True')) { throw 'Empty room retained resources' }
    Write-Output "PASS: $case"
}
Write-Output "ROOM_TESTS_PASS: 5 cases | $fixtures"

