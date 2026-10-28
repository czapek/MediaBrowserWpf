param(
    [string]$SourcePath = (Join-Path $PSScriptRoot '..\MediaBrowserWPF\bin\Debug'),
    [string]$TargetPath = 'C:\Users\SebastianCzapek\SynologyDriveDevelopment\MediaBrowserWpf',
    [switch]$Preview
)

$ErrorActionPreference = 'Stop'

$source = (Resolve-Path -LiteralPath $SourcePath).Path

if (-not (Test-Path -LiteralPath (Join-Path $source 'MediaBrowserWPF.exe'))) {
    throw "Keine kompilierte Anwendung gefunden: $(Join-Path $source 'MediaBrowserWPF.exe')"
}

if (-not (Test-Path -LiteralPath $TargetPath)) {
    New-Item -ItemType Directory -Path $TargetPath -Force | Out-Null
}

if (-not $Preview -and (Get-Process -Name 'MediaBrowserWPF' -ErrorAction SilentlyContinue)) {
    throw 'MediaBrowserWPF läuft noch. Bitte die Anwendung vor dem Deployment schließen.'
}

$arguments = @(
    $source
    $TargetPath
    '/E'
    '/XO'
    '/R:1'
    '/W:1'
    '/NP'
)

if ($Preview) {
    $arguments += '/L'
    Write-Host 'Vorschau: Es werden keine Dateien kopiert.' -ForegroundColor Yellow
}
else {
    Write-Host "Deployment nach $TargetPath" -ForegroundColor Cyan
}

& robocopy @arguments
$exitCode = $LASTEXITCODE

if ($exitCode -ge 8) {
    throw "Robocopy ist mit Exitcode $exitCode fehlgeschlagen."
}

if ($Preview) {
    Write-Host 'Vorschau abgeschlossen.' -ForegroundColor Yellow
}
else {
    Write-Host 'Deployment abgeschlossen. Nur neuere Dateien wurden kopiert.' -ForegroundColor Green
}
