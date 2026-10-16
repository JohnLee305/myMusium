[CmdletBinding()]
param([string]$ArchivePath = (Join-Path (Split-Path $PSScriptRoot -Parent) 'museum-archive'))
$ErrorActionPreference = 'Stop'
$schemaRoot = Join-Path (Split-Path $PSScriptRoot -Parent) 'museum-archive/schemas'
function Resolve-ArchiveFile([string]$Relative) {
    if ([string]::IsNullOrWhiteSpace($Relative) -or $Relative.Contains('\') -or $Relative.Contains(':') -or
        $Relative.StartsWith('/') -or ($Relative.Split('/') | Where-Object { $_ -in @('', '.', '..') })) { throw "UNSAFE_PATH: $Relative" }
    $full = [IO.Path]::GetFullPath((Join-Path $script:archiveRoot $Relative))
    if (-not $full.StartsWith($script:archiveRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw "OUTSIDE_ARCHIVE: $Relative" }
    $part = $script:archiveRoot
    foreach ($segment in $Relative.Split('/')) {
        $part = Join-Path $part $segment
        if (-not (Test-Path -LiteralPath $part)) { throw "FILE_NOT_FOUND: $Relative" }
        if ((Get-Item -LiteralPath $part -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "LINK_NOT_ALLOWED: $Relative" }
    }
    if (-not (Test-Path -LiteralPath $full -PathType Leaf)) { throw "NOT_A_FILE: $Relative" }
    return $full
}
function Read-ValidatedJson([string]$Path, [string]$Schema) {
    $limit = if ($Schema -eq 'manifest.schema.json') { 4MB } else { 1MB }
    if ((Get-Item -LiteralPath $Path).Length -gt $limit) { throw 'JSON_TOO_LARGE' }
    $raw = Get-Content -Raw -LiteralPath $Path
    if (-not (Test-Json -Json $raw -SchemaFile (Join-Path $schemaRoot $Schema) -ErrorAction Stop)) { throw "SCHEMA_INVALID: $Path" }
    return ($raw | ConvertFrom-Json)
}
function Test-Glb([string]$Path) {
    $length = (Get-Item -LiteralPath $Path).Length
    if ($length -lt 20 -or $length -gt 64MB) { throw 'GLB_SIZE_INVALID' }
    $bytes = [IO.File]::ReadAllBytes($Path)
    if ([Text.Encoding]::ASCII.GetString($bytes,0,4) -ne 'glTF' -or [BitConverter]::ToUInt32($bytes,4) -ne 2 -or [BitConverter]::ToUInt32($bytes,8) -ne $length) { throw 'GLB_HEADER_INVALID' }
    $jsonLength = [BitConverter]::ToUInt32($bytes,12)
    if ($jsonLength -gt $length-20 -or [BitConverter]::ToUInt32($bytes,16) -ne 0x4e4f534a) { throw 'GLB_JSON_CHUNK_INVALID' }
    $gltf = [Text.Encoding]::UTF8.GetString($bytes,20,$jsonLength) | ConvertFrom-Json
    if ($gltf.asset.version -ne '2.0') { throw 'GLTF_VERSION_INVALID' }
    foreach ($resource in @($gltf.buffers) + @($gltf.images)) {
        if ($resource -and $resource.uri -and -not $resource.uri.StartsWith('data:')) { throw 'GLB_EXTERNAL_RESOURCE_NOT_ALLOWED' }
    }
}
try {
    $script:archiveRoot = [IO.Path]::GetFullPath($ArchivePath).TrimEnd('\','/')
    $manifest = Read-ValidatedJson (Resolve-ArchiveFile 'manifest.json') 'manifest.schema.json'
    if ($null -ne $manifest.rooms) {
        $ids = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        $assigned = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        foreach ($room in $manifest.rooms) {
            if (-not $ids.Add($room.id)) { throw 'DUPLICATE_ROOM_ID' }
            if ($null -ne $room.cells -and $room.cells.Count -ne $room.artifacts.Count) { throw 'ROOM_CELLS_INVALID' }
            foreach ($entry in $room.artifacts) {
                if ($manifest.artifacts -cnotcontains $entry -or -not $assigned.Add($entry)) { throw 'ROOM_MEMBERSHIP_INVALID' }
            }
        }
        if ($assigned.Count -ne $manifest.artifacts.Count) { throw 'ROOM_UNASSIGNED_ARTIFACT' }
        Write-Output "ROOMS_VALID: $($manifest.rooms.Count)"
    } else { Write-Output "ROOMS_AUTO: $([math]::Ceiling($manifest.artifacts.Count / 30))" }
    $trashed=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach($item in $manifest.trash) {
        if($manifest.artifacts -ccontains $item.entry -or -not $trashed.Add($item.entry)){throw 'TRASH_MEMBERSHIP_INVALID'}
        $artifact=Read-ValidatedJson (Resolve-ArchiveFile $item.entry) 'artifact.schema.json'
        if($item.entry -cne "artifacts/$($artifact.id)/artifact.json"){throw 'TRASH_ID_MISMATCH'}
    }
    foreach ($relative in $manifest.artifacts) {
        $artifact = Read-ValidatedJson (Resolve-ArchiveFile $relative) 'artifact.schema.json'
        if ($relative -cne "artifacts/$($artifact.id)/artifact.json" -or -not $artifact.model.StartsWith("artifacts/$($artifact.id)/models/",[StringComparison]::Ordinal)) { throw 'ARTIFACT_ID_PATH_MISMATCH' }
        Test-Glb (Resolve-ArchiveFile $artifact.model)
        Write-Output "ARTIFACT_VALID: $($artifact.id)"
    }
    if ($null -ne $manifest.roomOrder -or @($manifest.rooms | Where-Object { $null -ne $_.navigation }).Count -gt 0) {
        & node (Join-Path $PSScriptRoot 'validate-navigation.mjs') (Resolve-ArchiveFile 'manifest.json')
        if ($LASTEXITCODE -ne 0) { throw 'TOUR_NAVIGATION_INVALID' }
    }
    Write-Output 'ARCHIVE_VALID'
    exit 0
} catch { Write-Output "ARCHIVE_INVALID: $($_.Exception.Message)"; exit 1 }
