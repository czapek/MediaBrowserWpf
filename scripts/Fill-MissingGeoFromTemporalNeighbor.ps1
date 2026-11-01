[CmdletBinding()]
param(
    [string]$DbHost = '192.168.2.129',
    [int]$DbPort = 3306,
    [string]$DbName = 'mediabrowser-new',
    [string]$DbUser = 'mediabrowser-new',
    [string]$DbPasswordEnvFile = '.vscode\db.env',
    [int]$MaxDistanceHours = 24,
    [switch]$IncludeZeroZero,
    [switch]$DryRun,
    [string]$WorkDir = (Join-Path $env:TEMP 'MediaBrowserWpfTemporalGeoFill'),
    [string]$MySqlConnectorPath = 'tools\ApiMock.SeedExport\bin\Debug\net9.0\MySqlConnector.dll'
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $WorkDir)) {
    New-Item -ItemType Directory -Path $WorkDir -Force | Out-Null
}

$candidatesPath = Join-Path $WorkDir 'temporal_geo_candidates.csv'
$skippedPath = Join-Path $WorkDir 'temporal_geo_skipped.csv'
$backupPath = Join-Path $WorkDir 'temporal_geo_updates.csv'

$dbPassword = $env:MEDIABROWSER_DB_PASSWORD
if ([String]::IsNullOrWhiteSpace($dbPassword) -and (Test-Path -LiteralPath $DbPasswordEnvFile)) {
    Get-Content -LiteralPath $DbPasswordEnvFile | ForEach-Object {
        if ($_ -match '^\s*MEDIABROWSER_DB_PASSWORD=(.*)$') {
            $dbPassword = $matches[1].Trim()
        }
    }
}
if ([String]::IsNullOrWhiteSpace($dbPassword)) {
    throw 'Kein Datenbankpasswort gefunden. Setze MEDIABROWSER_DB_PASSWORD oder pflege .vscode\db.env.'
}

if (-not (Test-Path -LiteralPath $MySqlConnectorPath)) {
    throw "MySqlConnector.dll nicht gefunden: $MySqlConnectorPath. Zuerst die Solution bauen."
}
Add-Type -Path (Resolve-Path $MySqlConnectorPath).Path

function Find-InsertionIndex {
    param(
        [long[]]$Ticks,
        [long]$Target
    )

    $low = 0
    $high = $Ticks.Length - 1

    while ($low -le $high) {
        $middle = [int](($low + $high) / 2)
        if ($Ticks[$middle] -eq $Target) {
            return $middle
        }
        if ($Ticks[$middle] -lt $Target) {
            $low = $middle + 1
        } else {
            $high = $middle - 1
        }
    }

    return $low
}

$connectionBuilder = [MySqlConnector.MySqlConnectionStringBuilder]::new()
$connectionBuilder.Server = $DbHost
$connectionBuilder.Port = $DbPort
$connectionBuilder.Database = $DbName
$connectionBuilder.UserID = $DbUser
$connectionBuilder.Password = $dbPassword
$connectionBuilder.ConnectionTimeout = 60
$connectionBuilder.CharacterSet = 'utf8mb4'

$connection = [MySqlConnector.MySqlConnection]::new($connectionBuilder.ConnectionString)
$connection.Open()

$query = @"
SELECT ID, FILENAME, MEDIADATE, LATITUDE, LONGITUDE, ROLES_FK
FROM MEDIAFILES
WHERE HISTORYVERSION = 0
  AND ISDELETED = 0
ORDER BY ROLES_FK, MEDIADATE, ID
"@
$command = $connection.CreateCommand()
$command.CommandText = $query
$command.CommandTimeout = 300
$reader = $command.ExecuteReader()

$validByRole = @{}
$missing = New-Object System.Collections.Generic.List[object]
$activeCount = 0

while ($reader.Read()) {
    $activeCount++
    $item = [pscustomobject]@{
        ID = $reader.GetInt64(0)
        FileName = $reader.GetString(1)
        MediaDate = $reader.GetDateTime(2)
        Latitude = if ($reader.IsDBNull(3)) { $null } else { $reader.GetDouble(3) }
        Longitude = if ($reader.IsDBNull(4)) { $null } else { $reader.GetDouble(4) }
        RoleId = $reader.GetInt64(5)
    }

    $isMissing = $null -eq $item.Latitude -and $null -eq $item.Longitude
    if ($IncludeZeroZero -and $null -ne $item.Latitude -and $null -ne $item.Longitude -and
        $item.Latitude -eq 0 -and $item.Longitude -eq 0) {
        $isMissing = $true
    }

    if ($isMissing) {
        $missing.Add($item)
        continue
    }

    $isValidNeighbor = $null -ne $item.Latitude -and $null -ne $item.Longitude -and
        -not ($item.Latitude -eq 0 -and $item.Longitude -eq 0)

    if ($isValidNeighbor) {
        if (-not $validByRole.ContainsKey($item.RoleId)) {
            $validByRole[$item.RoleId] = New-Object System.Collections.ArrayList
        }
        [void]$validByRole[$item.RoleId].Add($item)
    }
}
$reader.Close()
$connection.Close()

$neighborDates = @{}
$neighborItems = @{}
foreach ($roleId in $validByRole.Keys) {
    $groups = @($validByRole[$roleId] | Group-Object { $_.MediaDate.Ticks } | Sort-Object { [long]$_.Name })
    $ticks = [long[]]@($groups | ForEach-Object { [long]$_.Name })
    $itemsByTicks = @{}
    foreach ($group in $groups) {
        $itemsByTicks[[long]$group.Name] = @($group.Group | Sort-Object ID)
    }
    $neighborDates[$roleId] = $ticks
    $neighborItems[$roleId] = $itemsByTicks
}

$maxDistanceSeconds = [double]$MaxDistanceHours * 3600.0
$candidates = New-Object System.Collections.Generic.List[object]
$skipped = New-Object System.Collections.Generic.List[object]

foreach ($item in $missing) {
    if (-not $neighborDates.ContainsKey($item.RoleId)) {
        $skipped.Add([pscustomobject]@{
            ID = $item.ID
            FileName = $item.FileName
            MediaDate = $item.MediaDate
            OldLatitude = $item.Latitude
            OldLongitude = $item.Longitude
            Reason = 'kein_gueltiger_nachbar_in_rolle'
        })
        continue
    }

    $ticks = $neighborDates[$item.RoleId]
    $targetTicks = $item.MediaDate.Ticks
    $index = Find-InsertionIndex -Ticks $ticks -Target $targetTicks

    $candidateIndexes = @()
    $previousIndex = $index - 1
    $nextIndex = $index + 1
    foreach ($candidateIndex in @($previousIndex, $index, $nextIndex)) {
        if ($candidateIndex -ge 0 -and $candidateIndex -lt $ticks.Length) {
            $candidateIndexes += $candidateIndex
        }
    }

    $bestTicks = $null
    $bestDiff = [long]::MaxValue
    foreach ($candidateIndex in ($candidateIndexes | Sort-Object -Unique)) {
        $candidateTicks = $ticks[$candidateIndex]
        $diff = [Math]::Abs($candidateTicks - $targetTicks)
        if ($diff -lt $bestDiff -or
            ($diff -eq $bestDiff -and $null -ne $bestTicks -and $candidateTicks -lt $bestTicks)) {
            $bestDiff = $diff
            $bestTicks = $candidateTicks
        }
    }

    if ($null -eq $bestTicks -or $bestDiff -gt ([long]($maxDistanceSeconds * [TimeSpan]::TicksPerSecond))) {
        $skipped.Add([pscustomobject]@{
            ID = $item.ID
            FileName = $item.FileName
            MediaDate = $item.MediaDate
            OldLatitude = $item.Latitude
            OldLongitude = $item.Longitude
            Reason = 'kein_nachbar_innerhalb_24h'
        })
        continue
    }

    $neighbors = $neighborItems[$item.RoleId][$bestTicks]
    $neighbor = $neighbors | Sort-Object ID | Select-Object -First 1

    $candidates.Add([pscustomobject]@{
        DBID = $item.ID
        DBFile = $item.FileName
        MediaDate = $item.MediaDate
        OldLatitude = $item.Latitude
        OldLongitude = $item.Longitude
        NeighborID = $neighbor.ID
        NeighborDate = $neighbor.MediaDate
        DeltaSeconds = [Math]::Round([TimeSpan]::FromTicks($bestDiff).TotalSeconds)
        Latitude = $neighbor.Latitude
        Longitude = $neighbor.Longitude
    })
}

$candidates | Export-Csv -LiteralPath $candidatesPath -NoTypeInformation -Encoding UTF8
$skipped | Export-Csv -LiteralPath $skippedPath -NoTypeInformation -Encoding UTF8

$summary = [pscustomobject]@{
    ActiveItems = $activeCount
    Missing = $missing.Count
    Candidates = $candidates.Count
    Skipped = $skipped.Count
    IncludeZeroZero = [bool]$IncludeZeroZero
    MaxDistanceHours = $MaxDistanceHours
    DryRun = [bool]$DryRun
    CandidatesPath = $candidatesPath
    SkippedPath = $skippedPath
    BackupPath = $backupPath
}

if ($DryRun -or $candidates.Count -eq 0) {
    $summary | Format-List
    return
}

$connection.Open()
$transaction = $connection.BeginTransaction()
try {
    $command = $connection.CreateCommand()
    $command.Transaction = $transaction
    $command.CommandText = @"
CREATE TEMPORARY TABLE _temporal_geo_fill (
    ID BIGINT NOT NULL PRIMARY KEY,
    LATITUDE DOUBLE NOT NULL,
    LONGITUDE DOUBLE NOT NULL
)
"@
    [void]$command.ExecuteNonQuery()

    foreach ($candidate in $candidates) {
        $command = $connection.CreateCommand()
        $command.Transaction = $transaction
        $command.CommandText = 'INSERT INTO _temporal_geo_fill (ID, LATITUDE, LONGITUDE) VALUES (@id, @lat, @lon)'
        [void]$command.Parameters.AddWithValue('@id', [int64]$candidate.DBID)
        [void]$command.Parameters.AddWithValue('@lat', [double]$candidate.Latitude)
        [void]$command.Parameters.AddWithValue('@lon', [double]$candidate.Longitude)
        [void]$command.ExecuteNonQuery()
    }

    $targetCondition = if ($IncludeZeroZero) {
        '((m.LATITUDE IS NULL AND m.LONGITUDE IS NULL) OR (m.LATITUDE = 0 AND m.LONGITUDE = 0))'
    } else {
        '(m.LATITUDE IS NULL AND m.LONGITUDE IS NULL)'
    }

    $command = $connection.CreateCommand()
    $command.Transaction = $transaction
    $command.CommandTimeout = 0
    $command.CommandText = @"
UPDATE MEDIAFILES m
JOIN _temporal_geo_fill f ON f.ID = m.ID
SET m.LATITUDE = f.LATITUDE,
    m.LONGITUDE = f.LONGITUDE
WHERE m.HISTORYVERSION = 0
  AND m.ISDELETED = 0
  AND $targetCondition
"@
    $affected = $command.ExecuteNonQuery()
    if ($affected -ne $candidates.Count) {
        throw "Erwartet $($candidates.Count) Updates, betroffen $affected."
    }

    $transaction.Commit()
    $candidates | Select-Object DBID, DBFile, MediaDate, OldLatitude, OldLongitude,
        NeighborID, NeighborDate, DeltaSeconds, Latitude, Longitude |
        Export-Csv -LiteralPath $backupPath -NoTypeInformation -Encoding UTF8

    $summary | Add-Member -NotePropertyName Updated -NotePropertyValue $affected
    $summary | Format-List
} catch {
    $transaction.Rollback()
    throw
} finally {
    $connection.Close()
}
