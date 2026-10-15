$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$fixtures = Join-Path $root "logs/archive-tests-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
New-Item -ItemType Directory -Path $fixtures -Force | Out-Null
$shell = (Get-Process -Id $PID).Path
$cases = @('valid','missing-model','traversal','invalid-json','id-mismatch','bad-glb','unsupported-version','empty-title','external-resource','duplicate','empty-manifest','too-many')
foreach ($case in $cases) {
    $caseRoot = Join-Path $fixtures $case
    Copy-Item -LiteralPath (Join-Path $root 'museum-archive') -Destination $caseRoot -Recurse
    $artifactPath = Join-Path $caseRoot 'artifacts/sample-vase/artifact.json'
    $data = Get-Content -Raw -LiteralPath $artifactPath | ConvertFrom-Json
    switch ($case) {
        'missing-model' { $data.model = 'artifacts/sample-vase/models/missing.glb' }
        'traversal' { $data.model = '../outside.glb' }
        'id-mismatch' { $data.id = 'another-id' }
        'unsupported-version' { $data.schemaVersion = '99' }
        'empty-title' { $data.title = '   ' }
        'bad-glb' { [IO.File]::WriteAllText((Join-Path $caseRoot $data.model), 'this is not a GLB') }
        'external-resource' {
            $file = Join-Path $caseRoot $data.model
            $bytes = [IO.File]::ReadAllBytes($file)
            $oldLength = [BitConverter]::ToUInt32($bytes,12)
            $gltf = [Text.Encoding]::UTF8.GetString($bytes,20,$oldLength) | ConvertFrom-Json
            $gltf.buffers[0] | Add-Member -NotePropertyName uri -NotePropertyValue 'https://example.invalid/external.bin'
            $jsonBytes = [Text.Encoding]::UTF8.GetBytes(($gltf | ConvertTo-Json -Depth 100 -Compress))
            $padding = (4 - $jsonBytes.Length % 4) % 4
            $chunkLength = $jsonBytes.Length + $padding
            $stream = [IO.MemoryStream]::new()
            $writer = [IO.BinaryWriter]::new($stream)
            $writer.Write([Text.Encoding]::ASCII.GetBytes('glTF')); $writer.Write([uint32]2)
            $writer.Write([uint32]($bytes.Length - $oldLength + $chunkLength))
            $writer.Write([uint32]$chunkLength); $writer.Write([uint32]0x4e4f534a)
            $writer.Write($jsonBytes)
            for ($i=0; $i -lt $padding; $i++) { $writer.Write([byte]32) }
            $writer.Write($bytes,20+$oldLength,$bytes.Length-20-$oldLength)
            [IO.File]::WriteAllBytes($file,$stream.ToArray())
            $writer.Dispose(); $stream.Dispose()
        }
    }
    $data | ConvertTo-Json | Set-Content -LiteralPath $artifactPath -Encoding utf8
    if ($case -eq 'invalid-json') { Set-Content -LiteralPath $artifactPath -Value '{ broken' }
    if ($case -in @('duplicate','empty-manifest','too-many')) {
        $manifestPath = Join-Path $caseRoot 'manifest.json'
        $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
        if ($case -eq 'duplicate') { $manifest.artifacts += $manifest.artifacts[0] }
        if ($case -eq 'empty-manifest') { $manifest.artifacts = @() }
        if ($case -eq 'too-many') { $manifest.artifacts = @(0..10000 | ForEach-Object { "artifacts/item-$_/artifact.json" }) }
        $manifest | ConvertTo-Json | Set-Content -LiteralPath $manifestPath -Encoding utf8
    }
    $result = & $shell -NoProfile -File (Join-Path $PSScriptRoot 'validate-archive.ps1') -ArchivePath $caseRoot 2>&1
    $code = $LASTEXITCODE
    $expected = if ($case -eq 'valid') { 0 } else { 1 }
    if ($code -ne $expected) { throw "$case failed: expected $expected, got $code. $result" }
    Write-Output "PASS: $case (exit $code)"
}
Write-Output "ARCHIVE_TESTS_PASS: $($cases.Count) cases | $fixtures"
