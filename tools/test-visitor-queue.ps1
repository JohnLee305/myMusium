$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
# Compile the production C# source, rather than a second implementation of the algorithm.
Add-Type -Path (Join-Path $root 'apps/quest-viewer/Assets/Museum/Runtime/VisitorQueue.cs')
$script:checks = 0
function Check([bool]$condition, [string]$name) {
    if (-not $condition) { throw "FAIL: $name" }
    $script:checks++
    Write-Output "PASS: $name"
}
$q = [FamilyMuseum.VisitorQueue]::new()
Check (-not $q.TryAcquire('unregistered')) 'unregistered guest cannot acquire'
$a = $q.Join('A'); $b = $q.Join('B'); $c = $q.Join('C')
Check ($a -lt $b -and $b -lt $c) 'monotonic FIFO tickets'
Check ($q.Join('B') -eq $b -and $q.WaitingCount -eq 3) 'retry preserves ticket without duplicate'
Check ($q.WaitingRank('A') -eq 0 -and $q.WaitingRank('C') -eq 2 -and $q.WaitingRank('absent') -eq -1) 'waiting ranks follow ticket order'
Check (-not $q.TryAcquire('C') -and -not $q.TryAcquire('B')) 'later arrivals cannot jump queue'
Check ($q.TryAcquire('A') -and $q.Owner -eq 'A') 'head owns place'
Check ($q.WaitingRank('B') -eq 0 -and $q.WaitingRank('A') -eq -1 -and $q.Contains('A')) 'rank advances while owner remains registered'
Check ($q.TryAcquire('A') -and $q.Join('A') -eq $a -and $q.WaitingCount -eq 2) 'owner retry is idempotent'
Check (-not $q.Release('B') -and -not $q.TryAcquire('B')) 'other guest cannot release occupied place'
# Viewing completion alone does nothing: caller must explicitly confirm clearance.
Check ($q.Owner -eq 'A' -and -not $q.TryAcquire('B')) 'ownership held until explicit clearance'
Check ($q.Release('A') -and $q.TryAcquire('B')) 'next head acquires after clearance'
Check ($q.Cancel('C') -and $q.WaitingCount -eq 0 -and -not $q.Cancel('C')) 'waiting removal leaves no stale ticket'
Check ($q.Cancel('B') -and $null -eq $q.Owner) 'removed owner frees place'
$d = $q.Join('D'); $e = $q.Join('E')
Check ($q.Cancel('D') -and $q.TryAcquire('E')) 'cancelled head does not stall followers'
$other = [FamilyMuseum.VisitorQueue]::new(); $null = $other.Join('E')
Check ($other.TryAcquire('E') -and $q.Owner -eq 'E') 'separate exhibit queues are independent'
$q.Clear(); $f = $q.Join('F')
Check ($null -eq $q.Owner -and $q.WaitingCount -eq 1 -and $f -gt $e) 'clear removes entries without ticket reuse'
$invalidRejected = $false
try { $null = $q.Join('') } catch { $invalidRejected = $true }
Check $invalidRejected 'invalid ID rejected'
$many = [FamilyMuseum.VisitorQueue]::new()
for ($i = 0; $i -lt 120; $i++) { $null = $many.Join("guest-$i") }
$ordered = $true
for ($i = 0; $i -lt 120; $i++) {
    if ($i -lt 119 -and $many.TryAcquire("guest-$($i + 1)")) { $ordered = $false }
    if (-not $many.TryAcquire("guest-$i")) { $ordered = $false }
    if (-not $many.Release("guest-$i")) { $ordered = $false }
}
Check ($ordered -and $many.WaitingCount -eq 0 -and $null -eq $many.Owner) '120 guests served in order without leaks'
Write-Output "VISITOR_QUEUE_PASS checks=$script:checks"
