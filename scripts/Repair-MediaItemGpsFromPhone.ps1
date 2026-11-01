[CmdletBinding()]
param(
    [string]$DeviceName = 'Galaxy S24 Kandel',
    [string]$PhoneFolder = 'Interner Speicher\DCIM\Camera',
    [string]$DbHost = '192.168.2.129',
    [int]$DbPort = 3306,
    [string]$DbName = 'mediabrowser-new',
    [string]$DbUser = 'mediabrowser-new',
    [string]$DbPasswordEnvFile = '.vscode\db.env',
    [int]$BatchSize = 100,
    [int]$TimestampToleranceSeconds = 5,
    [switch]$AllMissingGeo,
    [switch]$DryRun,
    [switch]$Restart,
    [int]$MaxItems = 0,
    [string]$WorkDir = (Join-Path $env:TEMP 'MediaBrowserWpfPhoneGps'),
    [string]$MySqlConnectorPath = 'tools\ApiMock.SeedExport\bin\Debug\net9.0\MySqlConnector.dll'
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $WorkDir)) {
    New-Item -ItemType Directory -Path $WorkDir -Force | Out-Null
}

$matchesPath = Join-Path $WorkDir 'matches.json'
$missingPath = Join-Path $WorkDir 'missing.csv'
$ambiguousPath = Join-Path $WorkDir 'ambiguous.csv'
$statePath = Join-Path $WorkDir 'state.jsonl'
$backupPath = Join-Path $WorkDir 'updates.csv'
$batchDir = Join-Path $WorkDir 'batch'

if ($Restart) {
    Remove-Item -LiteralPath $statePath -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $backupPath -Force -ErrorAction SilentlyContinue
}

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
Add-Type -AssemblyName System.Drawing

function Get-PhoneFolder {
    param(
        [object]$Shell,
        [string]$DeviceName,
        [string]$PhoneFolder
    )

    $pc = $Shell.NameSpace(17)
    $device = $pc.Items() | Where-Object { $_.Name -eq $DeviceName }
    if ($null -eq $device) {
        throw "MTP-Gerät nicht gefunden: $DeviceName"
    }

    $folder = $device.GetFolder
    foreach ($segment in ($PhoneFolder -split '\\')) {
        if ([String]::IsNullOrWhiteSpace($segment)) { continue }
        $next = $folder.Items() | Where-Object { $_.Name -eq $segment }
        if ($null -eq $next) {
            throw "Phone-Ordner nicht gefunden: $segment"
        }
        $folder = $next.GetFolder
    }

    return $folder
}

function Get-ExpectedPhonePrefix {
    param([string]$FileName)

    $base = [IO.Path]::GetFileNameWithoutExtension($FileName)
    if ($base -match '^(\d{2})(\d{2})(\d{2})-(\d{2})(\d{2})-(\d{2})') {
        return "20$($matches[1])$($matches[2])$($matches[3])_$($matches[4])$($matches[5])$($matches[6])"
    }

    return $null
}

function Get-RationalValue {
    param(
        [byte[]]$Bytes,
        [int]$Index
    )

    $offset = $Index * 8
    $numerator = [BitConverter]::ToUInt32($Bytes, $offset)
    $denominator = [BitConverter]::ToUInt32($Bytes, $offset + 4)
    if ($denominator -eq 0 -or $numerator -eq [int32]::MinValue -or $denominator -eq [int32]::MinValue) {
        return $null
    }

    return [double]$numerator / [double]$denominator
}

function Get-GpsCoordinate {
    param(
        [System.Drawing.Image]$Image,
        [int]$Tag,
        [bool]$IsLatitude,
        [int]$RefTag
    )

    try {
        $item = $Image.GetPropertyItem($Tag)
    } catch {
        throw 'gps_missing'
    }

    if ($null -eq $item -or $item.Len -ne 24) {
        throw 'gps_rationals_missing'
    }

    $degrees = Get-RationalValue $item.Value 0
    $minutes = Get-RationalValue $item.Value 1
    $seconds = Get-RationalValue $item.Value 2
    if ($null -eq $degrees -or $null -eq $minutes -or $null -eq $seconds) {
        throw 'gps_denominator_zero'
    }

    $maxDegrees = if ($IsLatitude) { 90.0 } else { 180.0 }
    if ($degrees -lt 0 -or $degrees -gt $maxDegrees -or
        $minutes -lt 0 -or $minutes -ge 60 -or
        $seconds -lt 0 -or $seconds -ge 60) {
        throw 'gps_out_of_range'
    }

    $value = $degrees + $minutes / 60.0 + $seconds / 3600.0
    if ($value -gt $maxDegrees) {
        throw 'gps_out_of_range'
    }

    try {
        $refItem = $Image.GetPropertyItem($RefTag)
        $reference = [Text.Encoding]::ASCII.GetString($refItem.Value).Trim([char]0).Trim()
        if (-not [String]::IsNullOrWhiteSpace($reference)) {
            $hemisphere = [char]::ToUpperInvariant($reference[0])
            if (($IsLatitude -and $hemisphere -eq 'S') -or (-not $IsLatitude -and $hemisphere -eq 'W')) {
                $value = -$value
            }
        }
    } catch {
        # Ohne Referenz werden N/E angenommen.
    }

    return $value
}

$shell = New-Object -ComObject Shell.Application
$cameraFolder = Get-PhoneFolder -Shell $shell -DeviceName $DeviceName -PhoneFolder $PhoneFolder
$phoneItems = $cameraFolder.Items()

$phoneByTimestamp = @{}
foreach ($item in $phoneItems) {
    if ($item.Name -notmatch '^(?<ts>\d{8}_\d{6})') { continue }
    $timestamp = $matches['ts']
    $size = 0
    try { $size = [int64]$item.ExtendedProperty('System.Size') } catch { }
    if (-not $phoneByTimestamp.ContainsKey($timestamp)) {
        $phoneByTimestamp[$timestamp] = New-Object System.Collections.ArrayList
    }
    [void]$phoneByTimestamp[$timestamp].Add([pscustomobject]@{
        Name = $item.Name
        Size = $size
    })
}

$connectionBuilder = [MySqlConnector.MySqlConnectionStringBuilder]::new()
$connectionBuilder.Server = $DbHost
$connectionBuilder.Port = $DbPort
$connectionBuilder.Database = $DbName
$connectionBuilder.UserID = $DbUser
$connectionBuilder.Password = $dbPassword
$connectionBuilder.ConnectionTimeout = 30
$connectionBuilder.CharacterSet = 'utf8mb4'

$connection = [MySqlConnector.MySqlConnection]::new($connectionBuilder.ConnectionString)
$connection.Open()

if ($AllMissingGeo) {
    $query = @"
SELECT DISTINCT m.ID, m.FILENAME, m.MEDIADATE, COALESCE(m.LENGTH, 0) AS LENGTH
FROM MEDIAFILES m
WHERE m.HISTORYVERSION = 0
  AND m.ISDELETED = 0
  AND m.LATITUDE IS NULL
  AND m.LONGITUDE IS NULL
ORDER BY m.MEDIADATE
"@
} else {
    $query = @"
SELECT DISTINCT m.ID, m.FILENAME, m.MEDIADATE, COALESCE(m.LENGTH, 0) AS LENGTH
FROM MEDIAFILES m
JOIN METADATA lat ON lat.MEDIAFILES_FK = m.ID
JOIN METADATANAME latn ON latn.ID = lat.METANAME_FK AND latn.NAME = 'GPS Latitude'
JOIN METADATA lon ON lon.MEDIAFILES_FK = m.ID
JOIN METADATANAME lonn ON lonn.ID = lon.METANAME_FK AND lonn.NAME = 'GPS Longitude'
WHERE m.HISTORYVERSION = 0
  AND m.ISDELETED = 0
  AND m.LATITUDE IS NULL
  AND m.LONGITUDE IS NULL
  AND (lat.VALUE LIKE '%NaN%' OR lat.VALUE LIKE '%2147483648%')
  AND (lon.VALUE LIKE '%NaN%' OR lon.VALUE LIKE '%2147483648%')
ORDER BY m.MEDIADATE
"@
}

$command = $connection.CreateCommand()
$command.CommandText = $query
$reader = $command.ExecuteReader()
$dbItems = @()
while ($reader.Read()) {
    $dbItems += [pscustomobject]@{
        ID = $reader.GetInt64(0)
        FileName = $reader.GetString(1)
        MediaDate = $reader.GetDateTime(2)
        Length = $reader.GetInt64(3)
    }
}
$reader.Close()
$connection.Close()

if ($MaxItems -gt 0) {
    $dbItems = @($dbItems | Select-Object -First $MaxItems)
}

$matches = @()
$missing = @()
$ambiguous = @()

foreach ($dbItem in $dbItems) {
    $candidates = @()
    for ($offset = -$TimestampToleranceSeconds; $offset -le $TimestampToleranceSeconds; $offset++) {
        $timestamp = $dbItem.MediaDate.AddSeconds($offset).ToString('yyyyMMdd_HHmmss')
        if ($phoneByTimestamp.ContainsKey($timestamp)) {
            $candidates += $phoneByTimestamp[$timestamp]
        }
    }

    $candidates = @($candidates | Sort-Object Name -Unique)
    if ($candidates.Count -eq 0) {
        $missing += $dbItem
        continue
    }

    $sizeCandidates = @($candidates | Where-Object { $_.Size -eq $dbItem.Length })
    if ($sizeCandidates.Count -eq 1) {
        $matches += [pscustomobject]@{
            DBID = $dbItem.ID
            DBFile = $dbItem.FileName
            DBDate = $dbItem.MediaDate
            DBLength = $dbItem.Length
            Phone = $sizeCandidates[0].Name
            PhoneSize = $sizeCandidates[0].Size
        }
    } elseif ($sizeCandidates.Count -gt 1) {
        $ambiguous += [pscustomobject]@{
            DBID = $dbItem.ID
            DBFile = $dbItem.FileName
            DBDate = $dbItem.MediaDate
            Candidates = ($sizeCandidates.Name -join '; ')
        }
    } elseif ($candidates.Count -eq 1) {
        $matches += [pscustomobject]@{
            DBID = $dbItem.ID
            DBFile = $dbItem.FileName
            DBDate = $dbItem.MediaDate
            DBLength = $dbItem.Length
            Phone = $candidates[0].Name
            PhoneSize = $candidates[0].Size
        }
    } else {
        $ambiguous += [pscustomobject]@{
            DBID = $dbItem.ID
            DBFile = $dbItem.FileName
            DBDate = $dbItem.MediaDate
            Candidates = ($candidates.Name -join '; ')
        }
    }
}

$matches | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $matchesPath -Encoding UTF8
$missing | Export-Csv -LiteralPath $missingPath -NoTypeInformation -Encoding UTF8
$ambiguous | Export-Csv -LiteralPath $ambiguousPath -NoTypeInformation -Encoding UTF8

$processed = @{}
if (Test-Path -LiteralPath $statePath) {
    Get-Content -LiteralPath $statePath | ForEach-Object {
        if (-not [String]::IsNullOrWhiteSpace($_)) {
            $entry = $_.Trim() | ConvertFrom-Json
            $processed[[int64]$entry.DBID] = $true
        }
    }
}

$pending = @($matches | Where-Object { -not $processed.ContainsKey([int64]$_.DBID) } | Select-Object -First $BatchSize)
if ($pending.Count -eq 0) {
    [pscustomobject]@{
        Candidates = $dbItems.Count
        Matched = $matches.Count
        Missing = $missing.Count
        Ambiguous = $ambiguous.Count
        Pending = 0
        Message = 'Keine offenen Einträge.'
        MatchesPath = $matchesPath
        MissingPath = $missingPath
        AmbiguousPath = $ambiguousPath
        StatePath = $statePath
        BackupPath = $backupPath
    } | Format-List
    return
}

if (Test-Path -LiteralPath $batchDir) {
    Remove-Item -LiteralPath $batchDir -Recurse -Force
}
New-Item -ItemType Directory -Path $batchDir -Force | Out-Null
$destinationFolder = $shell.NameSpace($batchDir)

$results = @()
$updates = @()

foreach ($entry in $pending) {
    $phoneName = [string]$entry.Phone
    $status = 'ok'
    $exifDateText = ''
    $latitude = $null
    $longitude = $null
    $destinationFile = Join-Path $batchDir $phoneName

    try {
        if ($phoneName -notmatch '\.(jpg|jpeg)$') {
            throw 'not_image'
        }

        $phoneItem = $cameraFolder.ParseName($phoneName)
        if ($null -eq $phoneItem) {
            throw 'phone_file_missing'
        }

        if (Test-Path -LiteralPath $destinationFile) {
            Remove-Item -LiteralPath $destinationFile -Force
        }
        $destinationFolder.CopyHere($phoneItem, 0x14)

        $copied = $false
        for ($i = 0; $i -lt 120; $i++) {
            if (Test-Path -LiteralPath $destinationFile) {
                if ((Get-Item -LiteralPath $destinationFile).Length -gt 0) {
                    $copied = $true
                    break
                }
            }
            Start-Sleep -Milliseconds 500
        }
        if (-not $copied) {
            throw 'copy_timeout'
        }

        $lastLength = -1
        for ($i = 0; $i -lt 30; $i++) {
            $length = (Get-Item -LiteralPath $destinationFile).Length
            if ($length -eq $lastLength) { break }
            $lastLength = $length
            Start-Sleep -Milliseconds 300
        }

        $image = [System.Drawing.Image]::FromFile($destinationFile)
        try {
            $dateItem = $image.GetPropertyItem(0x9003)
            $exifDateText = [Text.Encoding]::ASCII.GetString($dateItem.Value).Trim([char]0)
            $exifDate = [DateTime]::ParseExact(
                $exifDateText,
                'yyyy:MM:dd HH:mm:ss',
                [Globalization.CultureInfo]::InvariantCulture)
            $dbDate = [DateTime]$entry.DBDate

            $dateMatches = [Math]::Abs(($exifDate - $dbDate).TotalSeconds) -le 2
            $expectedPrefix = Get-ExpectedPhonePrefix $entry.DBFile
            $nameMatches = $expectedPrefix -ne $null -and $phoneName.StartsWith($expectedPrefix)

            if (-not $dateMatches -and -not $nameMatches) {
                throw "date_mismatch:$exifDateText"
            }

            $latitude = Get-GpsCoordinate $image 2 $true 1
            $longitude = Get-GpsCoordinate $image 4 $false 3

            if ([Double]::IsNaN($latitude) -or [Double]::IsInfinity($latitude) -or
                [Double]::IsNaN($longitude) -or [Double]::IsInfinity($longitude)) {
                throw 'gps_not_finite'
            }

            $updates += [pscustomobject]@{
                ID = [int64]$entry.DBID
                Latitude = [double]$latitude
                Longitude = [double]$longitude
            }
        } finally {
            $image.Dispose()
        }
    } catch {
        $status = $_.Exception.Message
    } finally {
        if (Test-Path -LiteralPath $destinationFile) {
            Remove-Item -LiteralPath $destinationFile -Force -ErrorAction SilentlyContinue
        }
    }

    $results += [pscustomobject]@{
        DBID = [int64]$entry.DBID
        DBFile = [string]$entry.DBFile
        Phone = $phoneName
        Status = $status
        Latitude = $latitude
        Longitude = $longitude
        ExifDate = $exifDateText
        UpdatedAt = (Get-Date).ToString('s')
    }
}

$updated = 0
if (-not $DryRun -and $updates.Count -gt 0) {
    $connection.Open()
    $transaction = $connection.BeginTransaction()
    try {
        foreach ($update in $updates) {
            $command = $connection.CreateCommand()
            $command.Transaction = $transaction
            $command.CommandText = @"
UPDATE MEDIAFILES
SET LATITUDE = @lat, LONGITUDE = @lon
WHERE ID = @id
  AND HISTORYVERSION = 0
  AND ISDELETED = 0
  AND LATITUDE IS NULL
  AND LONGITUDE IS NULL
"@
            [void]$command.Parameters.AddWithValue('@lat', [double]$update.Latitude)
            [void]$command.Parameters.AddWithValue('@lon', [double]$update.Longitude)
            [void]$command.Parameters.AddWithValue('@id', [int64]$update.ID)
            $affected = $command.ExecuteNonQuery()
            if ($affected -ne 1) {
                throw "Update fuer ID $($update.ID) betraf $affected Zeilen."
            }
        }
        $transaction.Commit()
        $updated = $updates.Count
    } catch {
        $transaction.Rollback()
        throw
    } finally {
        $connection.Close()
    }
}

if (-not $DryRun) {
    foreach ($result in $results) {
        $result | ConvertTo-Json -Compress | Add-Content -LiteralPath $statePath -Encoding UTF8
    }

    if ($updates.Count -gt 0) {
        $backupRows = $results | Where-Object { $_.Status -eq 'ok' } |
            Select-Object DBID, DBFile, Phone, Latitude, Longitude, ExifDate, UpdatedAt
        if (Test-Path -LiteralPath $backupPath) {
            $backupRows | Export-Csv -LiteralPath $backupPath -Append -NoTypeInformation -Encoding UTF8
        } else {
            $backupRows | Export-Csv -LiteralPath $backupPath -NoTypeInformation -Encoding UTF8
        }
    }
}

Remove-Item -LiteralPath $batchDir -Recurse -Force -ErrorAction SilentlyContinue

[pscustomobject]@{
    Candidates = $dbItems.Count
    Matched = $matches.Count
    Missing = $missing.Count
    Ambiguous = $ambiguous.Count
    Batch = $pending.Count
    Updates = $updates.Count
    Updated = $updated
    GPSInvalid = @($results | Where-Object { $_.Status -like 'gps_*' }).Count
    DateMismatch = @($results | Where-Object { $_.Status -like 'date_mismatch*' }).Count
    PhoneMissing = @($results | Where-Object { $_.Status -eq 'phone_file_missing' }).Count
    CopyErrors = @($results | Where-Object { $_.Status -eq 'copy_timeout' }).Count
    OtherErrors = @($results | Where-Object { $_.Status -notin @('ok', 'phone_file_missing', 'copy_timeout') -and $_.Status -notlike 'gps_*' -and $_.Status -notlike 'date_mismatch*' }).Count
    DryRun = [bool]$DryRun
    MatchesPath = $matchesPath
    MissingPath = $missingPath
    AmbiguousPath = $ambiguousPath
    StatePath = $statePath
    BackupPath = $backupPath
} | Format-List
