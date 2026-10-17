[CmdletBinding()]
param([switch]$ShowWindow,[ValidateSet(6,12)][int]$GuestCount=6)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$sourceFixture = (Get-Content (Join-Path $root 'logs/navigation-fixture-path.txt') -Raw).Trim()
$fixture = Join-Path $root ('logs/queue-compact-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $fixture | Out-Null
Copy-Item -LiteralPath (Join-Path $sourceFixture 'artifacts') -Destination $fixture -Recurse
$manifest = Get-Content (Join-Path $sourceFixture 'manifest.json') -Raw | ConvertFrom-Json
# Two neighbouring exhibits exercise intersecting approaches and departure lanes.
$manifest.rooms[0].cells = @(7,8)
$manifest.rooms[0].navigation.blockedCells = @()
$manifest | ConvertTo-Json -Depth 12 | Set-Content (Join-Path $fixture 'manifest.json') -Encoding utf8
& (Join-Path $root 'tools/test-navigation-player.ps1') -ArchivePath $fixture -ShowWindow:$ShowWindow -GuestCount $GuestCount
$log = Get-ChildItem -LiteralPath $fixture -Filter 'tour-player-*.log' | Sort-Object LastWriteTime -Descending | Select-Object -First 1
$text = Get-Content -LiteralPath $log.FullName -Raw
if ($text -notmatch 'TOUR_QUEUE_CHECK: .*maxDepth=(\d+).*passed=True' -or [int]$Matches[1] -lt 2) { throw "Queue contention was not verified: $($log.FullName)" }
Write-Output "QUEUE_COMPACT_PASS: $($log.FullName)"
