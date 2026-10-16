$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$fixtures=Join-Path $root "logs/grid-player-$(Get-Date -Format yyyyMMdd-HHmmss)"
$player=Join-Path $root 'Builds/Windows/VRMuseum.exe'
foreach($case in @('sparse','empty','duplicate','legacy')) {
    $archive=Join-Path $fixtures $case
    New-Item -ItemType Directory -Force "$archive/artifacts" | Out-Null
    $entries=@()
    foreach($id in @('sample-vase','sample-books','sample-sculpture')) {
        Copy-Item -LiteralPath "$root/museum-archive/artifacts/$id" -Destination "$archive/artifacts/$id" -Recurse
        $entries+="artifacts/$id/artifact.json"
    }
    $room=@{id='grid-room';title='격자 배치 검증';artifacts=$entries;cells=@(0,17,29)}
    $manifest=@{schemaVersion='1.0';artifacts=$entries;rooms=@($room)}
    if($case -eq 'empty'){$manifest.artifacts=@();$room.artifacts=@();$room.cells=@()}
    if($case -eq 'duplicate'){$room.cells=@(0,0,29)}
    if($case -eq 'legacy'){$manifest.Remove('rooms')}
    $manifest | ConvertTo-Json -Depth 12 | Set-Content "$archive/manifest.json" -Encoding utf8
    $log="$fixtures/$case.log"
    $args=@('-screen-fullscreen','0','-screen-width','1280','-screen-height','800','-museum-archive',"`"$archive`"",'-museum-screenshot',"`"$fixtures/$case.png`"",'-logFile',"`"$log`"")
    if($case -eq 'duplicate'){$args+='-museum-expect-artifact-failure'}
    $process=Start-Process $player -ArgumentList $args -PassThru -WindowStyle Hidden
    $null=$process.Handle
    if(-not $process.WaitForExit(120000)){throw "Timed out: $case"}
    $text=Get-Content $log -Raw
    if($process.ExitCode -ne 0 -or -not $text.Contains('MUSEUM_SMOKE_PASS')){throw "Failed $case : $log"}
    if($case -eq 'sparse') {
        foreach($expected in @('cell=0 x=-6 z=0','cell=17 x=6 z=5.6','cell=29 x=6 z=11.2')) {if(-not $text.Contains($expected)){throw "Missing position: $expected"}}
    }
    if($case -eq 'empty' -and -not $text.Contains('loaded=0 failed=0 activeSlots=0')){throw 'Empty room failed'}
    if($case -eq 'duplicate' -and -not $text.Contains('ROOM_CELLS_INVALID')){throw 'Wrong invalid grid failure'}
    Write-Output "PASS: $case"
}
Write-Output "GRID_PLAYER_PASS: 4 cases | $fixtures"
