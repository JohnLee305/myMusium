#requires -Version 7.0
param([string]$WorkerAction='', [string]$SaveRoot='', [string]$Marker='', [string]$Stage='')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$decor=Join-Path $root 'apps/quest-viewer/Assets/Museum/Runtime/Decor'
$sources=@(Get-ChildItem -LiteralPath (Join-Path $decor 'Domain') -Filter '*.cs' | ForEach-Object FullName)+@(Get-ChildItem -LiteralPath (Join-Path $decor 'Storage') -Filter '*.cs' | ForEach-Object FullName)+@((Join-Path $root 'tests/decor/DecorStoreTestHooks.cs'))
Add-Type -Path $sources
function Base {
 $d=$null; $r=$null
 if (-not [FamilyMuseum.Decor.DecorJson]::TryRead((Get-Content -LiteralPath (Join-Path $root 'tests/fixtures/decor/valid-placed.json') -Raw -Encoding UTF8),[ref]$d,[ref]$r)) { throw 'Invalid fixture' }
 return $d
}
function OpenStore([string]$path,$fault=$null) {
 $store=$null; $code=$null; $d=Base
 if (-not [FamilyMuseum.Decor.DecorWorldStore]::TryOpen($path,'world.test',$d.catalog,$fault,[ref]$store,[ref]$code)) { throw "Open failed: $code" }
 return $store
}
if ($WorkerAction) {
 $fault=$null
 if ($WorkerAction -eq 'crash') { $fault=[DecorStoreTestHooks]::StopAt([FamilyMuseum.Decor.DecorStoreStage]$Stage,$Marker) }
 $store=OpenStore $SaveRoot $fault
 try {
  if ($WorkerAction -eq 'hold') { [IO.File]::WriteAllText($Marker,'locked'); [Threading.Thread]::Sleep([Threading.Timeout]::Infinite) }
  $load=$store.Load(); $snapshot=if ($load.status -eq 'notFound') { (Base).world } else { $load.world }
  if ($null -eq $snapshot) { throw "Unexpected worker load: $($load.status)" }
  $snapshot.instances[0].location.positionM[0]=.125
  $result=$store.Commit($snapshot.revision,'batch.kill',$snapshot)
  throw "Stage hook not reached: $($result.status) $($result.diagnosticCode)"
 } finally { $store.Dispose() }
}
$script:checks=0
function Check([bool]$condition,[string]$name) { if (-not $condition) { throw "FAIL: $name" }; $script:checks++; Write-Output "PASS: $name" }
$fixtureRoot=Join-Path $root ('logs/decor-store-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixtureRoot -Force | Out-Null
function PathFor([string]$case) { return Join-Path $fixtureRoot $case }
function Seed([string]$path) {
 $s=OpenStore $path
 try { $r=$s.Commit(0,'batch.initial',(Base).world); if ($r.status -ne 'saved' -or $r.revision -ne 1) { throw 'Seed failed' } }
 finally { $s.Dispose() }
}
function Target([string]$path) { return Join-Path $path 'world.test/world.json' }
function SpawnWorker([string]$action,[string]$save,[string]$marker,[string]$stage='') {
 $info=[Diagnostics.ProcessStartInfo]::new(); $info.FileName=(Get-Command pwsh).Source
 $info.UseShellExecute=$false; $info.CreateNoWindow=$true; $info.RedirectStandardOutput=$true; $info.RedirectStandardError=$true
 foreach ($arg in @('-NoProfile','-File',$PSCommandPath,'-WorkerAction',$action,'-SaveRoot',$save,'-Marker',$marker)) { $info.ArgumentList.Add($arg) }
 if ($stage) { $info.ArgumentList.Add('-Stage'); $info.ArgumentList.Add($stage) }
 $process=[Diagnostics.Process]::Start($info)
 $deadline=[DateTime]::UtcNow.AddSeconds(30)
 while (-not (Test-Path -LiteralPath $marker)) {
  if ($process.HasExited -or [DateTime]::UtcNow -gt $deadline) {
   if (-not $process.HasExited) { $process.Kill(); $process.WaitForExit() }
   throw ('Worker did not signal: '+$process.StandardOutput.ReadToEnd()+$process.StandardError.ReadToEnd())
  }
  Start-Sleep -Milliseconds 40
 }
 return $process
}
function StopWorker($process) { if (-not $process.HasExited) { $process.Kill() }; if (-not $process.WaitForExit(10000)) { throw 'Worker failed to exit' }; $process.Dispose() }

$d=Base; $path=PathFor 'basic'; $store=OpenStore $path
try {
 Check ($store.Load().status -eq 'notFound') 'new world is not-found, never a silently created empty snapshot'
 $initial=[FamilyMuseum.Decor.DecorJson]::WriteWorld($d.world)
 $r=$store.Commit(0,'batch.one',$d.world)
 Check ($r.status -eq 'saved' -and $r.revision -eq 1) 'initial snapshot atomically saved with revision one'
 Check ([FamilyMuseum.Decor.DecorJson]::WriteWorld($d.world) -ceq $initial) 'commit does not mutate caller snapshot'
 $load=$store.Load(); Check ($load.status -eq 'loaded' -and $load.world.lastCommandBatchId -eq 'batch.one') 'world-only document reloads saved transaction'
 $wire=[IO.File]::ReadAllText((Target $path)); Check (-not $wire.Contains('"catalog":')) 'save contains world not fixture envelope or catalog'
 $r=$store.Commit(0,'batch.one',$d.world)
 Check ($r.status -eq 'alreadySaved' -and $r.revision -eq 1) 'lost response retry precedes stale expected revision check'
 $retry=(Base).world; $retry.revision=99; $retry.lastCommandBatchId='unrelated.metadata'
 Check ($store.Commit(0,'batch.one',$retry).status -eq 'alreadySaved') 'retry content comparison excludes revision and batch metadata'
 [Array]::Reverse($retry.instances)
 Check ($store.Commit(0,'batch.one',$retry).diagnosticCode -eq 'BATCH_CONTENT_CONFLICT') 'array order remains significant in retry content hash'
 $different=(Base).world; $different.instances[0].variantId='ivory'
 $r=$store.Commit(0,'batch.one',$different); Check ($r.status -eq 'conflict' -and $r.diagnosticCode -eq 'BATCH_CONTENT_CONFLICT') 'same batch with different content is rejected'
 $r=$store.Commit(0,'batch.stale',(Base).world)
 Check ($r.status -eq 'conflict' -and [IO.File]::ReadAllText((Target $path)) -ceq $wire) 'stale new transaction leaves disk unchanged'
 $next=$store.Load().world; $next.instances[1].location.localPositionM=@(-.123,1,.007); $next.instances[0].variantId='ivory'
 $r=$store.Commit(1,'batch.two',$next); Check ($r.status -eq 'saved' -and $r.revision -eq 2) 'second commit validates expected revision'
 $load=$store.Load()
 Check ($load.world.instances[1].location.localPositionM[0] -eq -.123 -and $load.world.instances[1].location.parentId -eq 'plinth.001' -and $load.world.instances[0].variantId -eq 'ivory') 'precision parent ID and variant survive save/reload'
 Check ($load.backups.Count -eq 1 -and $load.backups[0].revision -eq 1) 'previous normal snapshot is backed up'
 $badBackup=Join-Path $path ('world.test/backups/r0000000999-'+[guid]::NewGuid().ToString('N')+'.json')
 [IO.File]::WriteAllText($badBackup,'broken backup')
 Check ($store.Load().backups.Count -eq 1) 'invalid backup is never advertised for recovery'
 $other=$null; $code=$null
 $opened=[FamilyMuseum.Decor.DecorWorldStore]::TryOpen($path,'world.test',$d.catalog,[ref]$other,[ref]$code)
 if ($opened) { $other.Dispose() }
 Check (-not $opened -and $code -eq 'WORLD_IN_USE') 'second writer denied by OS handle'
 $read=[FamilyMuseum.Decor.DecorWorldStore]::ReadOnly($path,'world.test',$d.catalog)
 Check ($read.status -eq 'loaded') 'read-only inspection works while writer is open'
 $invalid=$store.Load().world; $invalid.instances[0].variantId='absent'; $before=[IO.File]::ReadAllText((Target $path))
 $r=$store.Commit(2,'batch.invalid',$invalid)
 Check ($r.status -eq 'failed' -and $r.diagnosticCode -eq 'SNAPSHOT_INVALID' -and [IO.File]::ReadAllText((Target $path)) -ceq $before) 'invalid references cannot overwrite normal file'
 $bad=$store.Load().world; $bad.id='world.other'; $r=$store.Commit(2,'batch.wrongworld',$bad)
 Check ($r.status -eq 'failed' -and $r.diagnosticCode -eq 'SNAPSHOT_INVALID') 'wrong world identity rejected'
} finally { $store.Dispose() }
$path=PathFor 'encoding'; Seed $path; $store=OpenStore $path
try {
 [IO.File]::WriteAllBytes((Target $path),[byte[]]@(0xff,0xfe,0xff))
 $r=$store.Load(); Check ($r.status -eq 'corrupt' -and $null -eq $r.world) 'invalid encoded primary is corruption without an empty replacement'
 $r=$store.Commit(1,'batch.encoding',(Base).world); Check ($r.status -eq 'failed' -and ([IO.File]::ReadAllBytes((Target $path))).Length -eq 3) 'no-backup corrupt primary is not overwritten'
} finally { $store.Dispose() }
$path=PathFor 'exhausted'; Seed $path; $store=OpenStore $path
try {
 $w=$store.Load().world; $w.revision=[int]::MaxValue; [IO.File]::WriteAllText((Target $path),[FamilyMuseum.Decor.DecorJson]::WriteWorld($w))
 $r=$store.Commit([int]::MaxValue,'batch.exhausted',$w)
 Check ($r.status -eq 'failed' -and $r.diagnosticCode -eq 'REVISION_EXHAUSTED') 'revision overflow never wraps to old or negative revision'
} finally { $store.Dispose() }
$path=PathFor 'missing-primary'; Seed $path; $store=OpenStore $path
try {
 $w=$store.Load().world; $r=$store.Commit(1,'batch.beforemissing',$w)
 [IO.File]::Move((Target $path),((Target $path)+'.removed-by-test'))
 Check ($store.Load().status -eq 'recoveryAvailable') 'missing primary with valid backups requires explicit recovery'
 $r=$store.Commit(0,'batch.reinitialize',(Base).world)
 Check ($r.status -eq 'failed' -and -not (Test-Path -LiteralPath (Target $path))) 'missing primary with history is not silently initialized'
} finally { $store.Dispose() }
Check ($store.Load().diagnosticCode -eq 'STORE_CLOSED' -and $store.Commit(2,'batch.closed',$next).diagnosticCode -eq 'STORE_CLOSED') 'closed store rejects I/O'
$store=OpenStore (PathFor 'basic'); try { Check ($store.Load().world.revision -eq 2) 'stale lock filename does not prevent reopening after dispose' } finally { $store.Dispose() }
$missingPath=PathFor 'readonly-missing'; $r=[FamilyMuseum.Decor.DecorWorldStore]::ReadOnly($missingPath,'world.test',$d.catalog)
Check ($r.status -eq 'notFound' -and -not (Test-Path -LiteralPath $missingPath)) 'read-only missing store does not create directories'
$other=$null; $code=$null
Check (-not [FamilyMuseum.Decor.DecorWorldStore]::TryOpen((PathFor 'bad-id'),'../escape',$d.catalog,[ref]$other,[ref]$code)) 'path traversal world ID rejected'

$path=PathFor 'corrupt'; Seed $path; $store=OpenStore $path
try {
 $w=$store.Load().world; $r=$store.Commit(1,'batch.beforecorrupt',$w); $backup=$store.Load().backups[0].id
 [IO.File]::WriteAllText((Target $path),'{ broken')
 $load=$store.Load(); Check ($load.status -eq 'recoveryAvailable' -and $null -eq $load.world -and $load.backups.Count -gt 0) 'corruption exposes backups without silently loading them'
 $r=$store.Commit(2,'batch.erase',$w); Check ($r.status -eq 'failed' -and [IO.File]::ReadAllText((Target $path)) -ceq '{ broken') 'normal commit cannot erase corrupt evidence'
 $r=$store.RestoreBackup($backup,2,'batch.recover')
 Check ($r.status -eq 'saved' -and $r.revision -gt 2 -and $store.Load().status -eq 'loaded') 'explicit recovery saves confirmed backup at newer revision'
 $preserved=@(Get-ChildItem -LiteralPath (Join-Path $path 'world.test/preserved') -Filter '*.json')
 Check ($preserved.Count -eq 1 -and [IO.File]::ReadAllText($preserved[0].FullName) -ceq '{ broken') 'recovery preserves damaged primary file separately'
 $again=$store.RestoreBackup($backup,2,'batch.recover'); Check ($again.status -eq 'alreadySaved' -and $again.revision -eq $r.revision) 'recovery response retry does not increase revision twice'
 $r=$store.RestoreBackup('../world.json',0,'batch.unsafe'); Check ($r.status -eq 'failed' -and $r.diagnosticCode -eq 'RECOVERY_ARGUMENT_INVALID') 'backup traversal rejected'
} finally { $store.Dispose() }
$path=PathFor 'unsupported'; Seed $path; $store=OpenStore $path
try {
 $text=[IO.File]::ReadAllText((Target $path)).Replace('decor-slice-1','decor-slice-99'); [IO.File]::WriteAllText((Target $path),$text)
 Check ($store.Load().status -eq 'unsupportedVersion') 'newer schema version identified separately from corruption'
 $r=$store.Commit(1,'batch.version',(Base).world); Check ($r.status -eq 'failed' -and [IO.File]::ReadAllText((Target $path)) -ceq $text) 'unsupported version never overwritten'
} finally { $store.Dispose() }
$path=PathFor 'catalog'; Seed $path; $otherCatalog=(Base).catalog; $otherCatalog.revision='catalog.other'; $other=$null; $code=$null
Check ([FamilyMuseum.Decor.DecorWorldStore]::TryOpen($path,'world.test',$otherCatalog,[ref]$other,[ref]$code)) 'open with different catalog can inspect mismatch'
try { Check ($other.Load().status -eq 'contentMismatch') 'content pin mismatch is not treated as file corruption' } finally { $other.Dispose() }
$path=PathFor 'retention'; Seed $path; $store=OpenStore $path
try {
 for ($n=2;$n -le 9;$n++) { $w=$store.Load().world; $r=$store.Commit($w.revision,"batch.keep.$n",$w); if ($r.status -ne 'saved') { throw 'Retention commit failed' } }
 $load=$store.Load(); Check ($load.backups.Count -eq 5 -and $load.backups[0].revision -eq 8) 'normal commits retain five recent valid backups'
 $oldest=$load.backups[-1].id; $r=$store.RestoreBackup($oldest,9,'batch.oldrestore')
 Check ($r.status -eq 'saved' -and $store.Load().backups.Count -eq 5) 'explicit old backup restore preserves retention bound'
 Check ($store.RestoreBackup($oldest,9,'batch.oldrestore').status -eq 'alreadySaved') 'selected recovery source retained for immediate retry'
} finally { $store.Dispose() }

foreach ($stage in [Enum]::GetValues([FamilyMuseum.Decor.DecorStoreStage])) {
 $path=PathFor ("fault-$stage"); Seed $path; $before=[IO.File]::ReadAllText((Target $path)); $store=OpenStore $path ([DecorStoreTestHooks]::FailAt($stage))
 try {
  $w=$store.Load().world; $w.instances[0].location.positionM[0]=.5; $r=$store.Commit(1,'batch.fail',$w)
  if ($stage -eq [FamilyMuseum.Decor.DecorStoreStage]::AfterReplace) {
   Check ($r.status -eq 'saved' -and $r.diagnosticCode -eq 'POST_SAVE_WARNING' -and $store.Load().world.revision -eq 2) "post-replace failure reports actual committed state: $stage"
  } else { Check ($r.status -eq 'failed' -and [IO.File]::ReadAllText((Target $path)) -ceq $before) "injected I/O failure preserves old snapshot: $stage" }
 } finally { $store.Dispose() }
}

$path=PathFor 'process-lock'; $marker=Join-Path $fixtureRoot 'lock.marker'; $process=SpawnWorker 'hold' $path $marker
try {
 $other=$null; $code=$null; $opened=[FamilyMuseum.Decor.DecorWorldStore]::TryOpen($path,'world.test',$d.catalog,[ref]$other,[ref]$code)
 if ($opened) { $other.Dispose() }
 Check (-not $opened -and $code -eq 'WORLD_IN_USE') 'different process cannot acquire writer handle'
} finally { StopWorker $process }
$store=OpenStore $path; try { Check ($store.Load().status -eq 'notFound') 'OS releases writer handle after forced process death' } finally { $store.Dispose() }
foreach ($fresh in @($false,$true)) {
 foreach ($stage in [Enum]::GetValues([FamilyMuseum.Decor.DecorStoreStage])) {
  $path=PathFor ("kill-$fresh-$stage"); if (-not $fresh) { Seed $path }
  $marker=Join-Path $fixtureRoot ("kill-$fresh-$stage.marker")
  $process=SpawnWorker 'crash' $path $marker ([string]$stage)
  StopWorker $process
  $store=OpenStore $path
  try {
   $load=$store.Load()
   if ($stage -eq [FamilyMuseum.Decor.DecorStoreStage]::AfterReplace) {
    Check ($load.status -eq 'loaded' -and $load.world.lastCommandBatchId -eq 'batch.kill' -and $load.world.instances[0].location.positionM[0] -eq .125) "kill after replacement loads new normal snapshot fresh=$fresh"
    $request=(Base).world; $request.revision=if ($fresh) {0} else {1}; $request.instances[0].location.positionM[0]=.125
    $retry=$store.Commit($request.revision,'batch.kill',$request)
    Check ($retry.status -eq 'alreadySaved' -and $retry.revision -eq $load.world.revision) "real process death lost response retry is idempotent fresh=$fresh"
   } elseif ($fresh) { Check ($load.status -eq 'notFound') "kill before first publication never loads partial temp: $stage" }
   else { Check ($load.status -eq 'loaded' -and $load.world.revision -eq 1 -and $load.world.instances[0].location.positionM[0] -eq 0) "kill before replacement loads old normal snapshot: $stage" }
  } finally { $store.Dispose() }
 }
}
Write-Output "DECOR_STORE_PASS checks=$script:checks (actual process termination and I/O injection; no power-loss/Unity/geometry test)"
Write-Output "DECOR_STORE_FIXTURES: $fixtureRoot"
