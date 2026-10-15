$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$fixtures=Join-Path $root "logs/visitor-tests-$(Get-Date -Format yyyyMMdd-HHmmss)"
foreach($case in @('normal','empty','dense','missing')) {
    $archive=Join-Path $fixtures $case
    $entries=@(); $cells=@()
    $count=if($case -eq 'empty'){0}elseif($case -eq 'dense'){30}else{1}
    for($n=0;$n -lt $count;$n++) {
        $id="visitor-test-$n"
        $folder=Join-Path $archive "artifacts/$id"
        New-Item -ItemType Directory -Force "$folder/models" | Out-Null
        Copy-Item "$root/museum-archive/artifacts/sample-books/models/pc.glb" "$folder/models/pc.glb"
        $model=if($case -eq 'missing'){"artifacts/$id/models/missing.glb"}else{"artifacts/$id/models/pc.glb"}
        @{schemaVersion='1.0';id=$id;title="관람객 시험 전시품 $n";description='관람객이 걸어와 구경하고 퇴장하는지 확인합니다.';model=$model;scale=1}|ConvertTo-Json|Set-Content "$folder/artifact.json" -Encoding utf8
        $entries+="artifacts/$id/artifact.json"
        $cells+=if($case -eq 'dense'){$n}else{2}
    }
    New-Item -ItemType Directory -Force $archive | Out-Null
    @{schemaVersion='1.0';artifacts=$entries;rooms=@(@{id='first';title='관람객 시험실';artifacts=$entries;cells=$cells},@{id='empty';title='빈 방';artifacts=@();cells=@()})}|ConvertTo-Json -Depth 10|Set-Content "$archive/manifest.json" -Encoding utf8
    $log="$fixtures/$case.log"; $png="$fixtures/$case.png"
    $playerArgs=@('-screen-fullscreen','0','-screen-width','1280','-screen-height','800','-museum-archive',"`"$archive`"",'-museum-screenshot',"`"$png`"",'-museum-visitor-smoke','-logFile',"`"$log`"")
    if($case -eq 'missing'){$playerArgs+='-museum-expect-artifact-failure'}
    $process=Start-Process "$root/Builds/Windows/VRMuseum.exe" -ArgumentList $playerArgs -PassThru
    $null=$process.Handle
    if(-not $process.WaitForExit(180000)){
        try { $process.Kill() } catch { }
        throw "Visitor timeout: $case"
    }
    $output=Get-Content $log -Raw
    if($process.ExitCode -ne 0 -or -not $output.Contains('VISITOR_SMOKE_PASS') -or -not $output.Contains('MUSEUM_SMOKE_PASS') -or $output.Contains('NullReferenceException:') -or $output.Contains('MissingReferenceException:')){throw "Visitor failed: $case; $log"}
    Write-Output "PASS: $case"
}
Write-Output "VISITOR_TESTS_PASS: 4 cases | $fixtures"
