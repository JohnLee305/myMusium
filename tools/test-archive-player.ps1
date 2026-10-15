$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$player = Join-Path $root 'Builds/Windows/VRMuseum.exe'
$fixtures = Join-Path $root "logs/player-archive-tests-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
New-Item -ItemType Directory -Path $fixtures -Force | Out-Null
function Get-BuildFingerprint {
    # Unity's launcher exe can stay identical after a C# rebuild; include the actual game code and font shader.
    $files = @($player, (Join-Path $root 'Builds/Windows/VRMuseum_Data/Managed/Assembly-CSharp.dll'),
        (Join-Path $root 'Builds/Windows/VRMuseum_Data/resources.assets'))
    return (($files | ForEach-Object { (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash }) -join ':')
}
$before = Get-BuildFingerprint
$cases = @('normal','single','changed-json','missing-model','broken-json','oversize','six','overflow','duplicate','broken-manifest')
foreach ($case in $cases) {
    $caseRoot = Join-Path $fixtures $case
    Copy-Item -LiteralPath (Join-Path $root 'museum-archive') -Destination $caseRoot -Recurse
    $manifestFile = Join-Path $caseRoot 'manifest.json'
    $manifest = Get-Content -LiteralPath $manifestFile -Raw | ConvertFrom-Json
    $file = Join-Path $caseRoot 'artifacts/sample-vase/artifact.json'
    $data = Get-Content -LiteralPath $file -Raw | ConvertFrom-Json
    $loaded=3; $failed=0; $skipped=0
    switch ($case) {
        'single' { $manifest.artifacts=@($manifest.artifacts[0]); $loaded=1 }
        'changed-json' { $data.title='재빌드 없이 바뀐 전시품'; $data.scale=0.65 }
        'missing-model' { $data.model='artifacts/sample-vase/models/missing.glb'; $loaded=2; $failed=1 }
        'broken-json' { $loaded=2; $failed=1 }
        'oversize' { $data.scale=10; $loaded=2; $failed=1 }
        { $_ -in 'six','overflow' } {
            $total = if ($case -eq 'six') { 6 } else { 10 }
            for ($i=3; $i -lt $total; $i++) {
                $id="sample-copy-$i"
                $destination=Join-Path $caseRoot "artifacts/$id"
                Copy-Item -LiteralPath (Join-Path $caseRoot 'artifacts/sample-books') -Destination $destination -Recurse
                $copyFile=Join-Path $destination 'artifact.json'
                $copy=Get-Content -LiteralPath $copyFile -Raw | ConvertFrom-Json
                $copy.id=$id; $copy.title="추가 전시품 $i"; $copy.model="artifacts/$id/models/pc.glb"
                $copy | ConvertTo-Json | Set-Content -LiteralPath $copyFile -Encoding utf8
                $manifest.artifacts += "artifacts/$id/artifact.json"
            }
            $loaded=$total; $skipped=0
        }
        'duplicate' { $manifest.artifacts += $manifest.artifacts[0]; $loaded=0; $failed=1 }
        'broken-manifest' { $loaded=0; $failed=1 }
    }
    $data | ConvertTo-Json | Set-Content -LiteralPath $file -Encoding utf8
    $manifest | ConvertTo-Json | Set-Content -LiteralPath $manifestFile -Encoding utf8
    if ($case -eq 'broken-json') { Set-Content -LiteralPath $file -Value '{ not-json' }
    if ($case -eq 'broken-manifest') { Set-Content -LiteralPath $manifestFile -Value '{ not-json' }
    $log = Join-Path $fixtures "$case.log"
    $png = Join-Path $fixtures "$case.png"
    $arguments = @('-screen-fullscreen','0','-screen-width','1280','-screen-height','800',
        '-museum-archive',"`"$caseRoot`"",'-museum-screenshot',"`"$png`"",'-logFile',"`"$log`"")
    if ($failed -gt 0) { $arguments += '-museum-expect-artifact-failure' }
    $process = Start-Process -FilePath $player -ArgumentList $arguments -WindowStyle Normal -PassThru
    if (-not $process.WaitForExit(60000)) { throw "Player timeout, close process $($process.Id) before retry: $case" }
    $output = Get-Content -LiteralPath $log -Raw
    if ($process.ExitCode -ne 0 -or -not $output.Contains('MUSEUM_SMOKE_PASS') -or -not (Test-Path -LiteralPath $png)) { throw "PLAYER_TEST_FAILED: $case; log: $log" }
    if (-not $output.Contains("GALLERY_READY: loaded=$loaded failed=$failed skipped=$skipped")) { throw "Gallery counts incorrect: $case; log: $log" }
    if ($case -eq 'changed-json' -and -not $output.Contains('재빌드 없이 바뀐 전시품')) { throw 'Changed JSON title did not appear in runtime log' }
    if ($loaded -ge 2) {
        foreach ($id in @('sample-books','sample-sculpture')) {
            if (-not $output.Contains("ARTIFACT_LOADED: $id")) { throw "Other artifact failed unexpectedly: $case / $id" }
        }
    }
    Write-Output "PASS: $case (loaded=$loaded failed=$failed skipped=$skipped)"
}
if ($before -ne (Get-BuildFingerprint)) { throw 'Build files changed during tests' }
Write-Output "PLAYER_ARCHIVE_TESTS_PASS: $($cases.Count) cases; unchanged exe/Assembly-CSharp/resources SHA256 $before | $fixtures"

