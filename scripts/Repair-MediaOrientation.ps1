[CmdletBinding()]
param(
    [string]$DbHost = '192.168.2.129',
    [int]$DbPort = 3306,
    [string]$DbName = 'mediabrowser-new',
    [string]$DbUser = 'mediabrowser-new',
    [string]$DbPasswordEnvFile = '.vscode\db.env',
    [switch]$DryRun,
    [string]$WorkDir = (Join-Path $env:TEMP 'MediaBrowserWpfOrientationRepair'),
    [string]$MySqlConnectorPath = 'tools\ApiMock.SeedExport\bin\Debug\net9.0\MySqlConnector.dll'
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $WorkDir)) {
    New-Item -ItemType Directory -Path $WorkDir -Force | Out-Null
}

$backupPath = Join-Path $WorkDir 'orientation_updates.csv'

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

function ConvertTo-NormalizedOrientation {
    param([int]$Number)

    if ($Number -ge 0 -and $Number -le 3) {
        return $Number
    }

    if ($Number % 90 -eq 0) {
        $steps = [int](($Number / 90) % 4)
        if ($steps -lt 0) {
            $steps += 4
        }
        return $steps
    }

    return 0
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

try {
    $query = @"
SELECT ID, FILENAME, ORIENTATION
FROM MEDIAFILES
WHERE ORIENTATION IS NOT NULL
  AND ORIENTATION NOT IN (0, 1, 2, 3)
ORDER BY ID
"@

    $command = $connection.CreateCommand()
    $command.CommandText = $query
    $reader = $command.ExecuteReader()

    $updates = New-Object System.Collections.Generic.List[object]
    while ($reader.Read()) {
        $oldValue = [int]$reader.GetValue(2)
        $updates.Add([pscustomobject]@{
            Id = [long]$reader.GetValue(0)
            Filename = [string]$reader.GetValue(1)
            OldOrientation = $oldValue
            NewOrientation = ConvertTo-NormalizedOrientation -Number $oldValue
        })
    }
    $reader.Close()

    if ($updates.Count -eq 0) {
        Write-Output 'Keine ungültigen ORIENTATION-Werte gefunden.'
        return
    }

    $updates | Export-Csv -LiteralPath $backupPath -NoTypeInformation -Encoding UTF8

    foreach ($update in $updates) {
        Write-Output ("{0} | {1} | {2} -> {3}" -f $update.Id, $update.Filename, $update.OldOrientation, $update.NewOrientation)
    }
    Write-Output ("Backup: {0}" -f $backupPath)

    if ($DryRun) {
        Write-Output ("DryRun: {0} Datensätze würden korrigiert." -f $updates.Count)
        return
    }

    $transaction = $connection.BeginTransaction()
    try {
        $updateCommand = $connection.CreateCommand()
        $updateCommand.Transaction = $transaction
        $updateCommand.CommandText = @"
UPDATE MEDIAFILES
SET ORIENTATION = @newOrientation
WHERE ID = @id
  AND ORIENTATION = @oldOrientation
"@
        $idParameter = $updateCommand.Parameters.AddWithValue('@id', [long]0)
        $oldParameter = $updateCommand.Parameters.AddWithValue('@oldOrientation', [int]0)
        $newParameter = $updateCommand.Parameters.AddWithValue('@newOrientation', [int]0)

        $affected = 0
        foreach ($update in $updates) {
            $idParameter.Value = $update.Id
            $oldParameter.Value = $update.OldOrientation
            $newParameter.Value = $update.NewOrientation
            $affected += $updateCommand.ExecuteNonQuery()
        }

        if ($affected -ne $updates.Count) {
            throw ("Es wurden {0} von {1} Datensätzen aktualisiert. Änderungen werden zurückgerollt." -f $affected, $updates.Count)
        }

        $transaction.Commit()
        Write-Output ("Erfolgreich {0} Datensätze korrigiert." -f $affected)
    }
    catch {
        $transaction.Rollback()
        throw
    }
    finally {
        $transaction.Dispose()
    }
}
finally {
    $connection.Close()
    $connection.Dispose()
}
