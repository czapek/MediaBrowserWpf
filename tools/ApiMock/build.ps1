param(
    [switch]$Export,
    [switch]$SkipPublish,
    [int]$BrowseLimit = 40,
    [int]$GeoLimit = 120
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$repo = Split-Path -Parent $root
$data = Join-Path $PSScriptRoot 'data'
$dist = Join-Path $PSScriptRoot 'dist'

if ($Export) {
    if (-not $env:MEDIABROWSER_DB_PASSWORD) {
        throw 'MEDIABROWSER_DB_PASSWORD ist nicht gesetzt.'
    }

    dotnet run --project (Join-Path $PSScriptRoot '..\ApiMock.SeedExport\ApiMock.SeedExport.csproj') -c Debug -- `
        --out $data `
        --browse-limit $BrowseLimit `
        --geo-limit $GeoLimit

    if ($LASTEXITCODE -ne 0) {
        throw 'Seed-Export fehlgeschlagen.'
    }
}

if (-not (Test-Path (Join-Path $data 'seed.json'))) {
    throw "Seed fehlt: $data\seed.json. Mit -Export und gesetzten Zugangsdaten erzeugen."
}

if (-not $SkipPublish) {
    if (Test-Path $dist) {
        Remove-Item -LiteralPath $dist -Recurse -Force
    }

    dotnet publish (Join-Path $PSScriptRoot 'ApiMock.csproj') `
        -c Release `
        -r win-x64 `
        --self-contained true `
        /p:PublishSingleFile=true `
        /p:IncludeNativeLibrariesForSelfExtract=true `
        -o $dist

    if ($LASTEXITCODE -ne 0) {
        throw 'Publish fehlgeschlagen.'
    }

    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'start.cmd') -Destination $dist -Force
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README.md') -Destination $dist -Force

    $zip = Join-Path $PSScriptRoot 'ApiMock-win-x64.zip'
    if (Test-Path $zip) {
        Remove-Item -LiteralPath $zip -Force
    }

    Compress-Archive -Path (Join-Path $dist '*') -DestinationPath $zip
    Write-Host "Fertig: $dist"
    Write-Host "Zip:    $zip"
}
else {
    Write-Host "Seed vorhanden: $data\seed.json"
}
