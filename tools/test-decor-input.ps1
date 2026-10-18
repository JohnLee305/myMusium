#requires -Version 7.0
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$decor=Join-Path $root 'apps/quest-viewer/Assets/Museum/Runtime/Decor'
$files=@('Domain','Storage','Input' | ForEach-Object { Get-ChildItem -LiteralPath (Join-Path $decor $_) -Filter '*.cs' | ForEach-Object FullName })
$files+=Join-Path $root 'tests/decor/DecorInputTests.cs'
Add-Type -Path $files
$output=Join-Path $root ('logs/decor-input-tests-'+[guid]::NewGuid().ToString('N'))
[DecorInputTests]::Run((Join-Path $root 'tests/fixtures/decor/valid-placed.json'),$output) | Write-Output
