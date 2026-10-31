# ApiMock

Lokaler, eigenständiger Mock für den MediaBrowser-API-Provider. Der Mock bildet den Vertrag aus
`docs/openapi.yaml` und `MediaBrowser4Lib\DB\Api\ApiDtos.cs` nach und liefert einen kleinen,
realistischen Testkatalog inklusive Original-Content, Thumbnails, Varianten und Layern.

## Start

Im gebauten Paket:

```powershell
.\start.cmd
```

Standard-URL: `http://localhost:8282`

Frontend:

```powershell
MediaBrowserWPF.exe --provider api --api-base-url http://localhost:8282 --api-key mock
```

Alternativ mit Login-Flow:

```powershell
MediaBrowserWPF.exe --provider api --api-base-url http://localhost:8282 --api-user mock --api-password mock
```

Der Mock akzeptiert jeden nicht-leeren Bearer-Token. Login und Refresh liefern feste Mock-Tokens.

## Paket bauen

```powershell
.\tools\ApiMock\build.ps1
```

Ergebnis:

- `tools\ApiMock\dist\ApiMock.exe` (self-contained Single-File, win-x64)
- `tools\ApiMock\dist\start.cmd`
- `tools\ApiMock\ApiMock-win-x64.zip`

## Seed neu erzeugen

Der Seed wird lokal aus der echten MariaDB und über die echte API erzeugt und **nicht committet**.
Benötigt werden:

- `MEDIABROWSER_DB_PASSWORD`
- `MEDIABROWSER_API_KEY` (nur für den Original-Content)
- `ffmpeg` und `ffprobe` im PATH

```powershell
$env:MEDIABROWSER_DB_PASSWORD = '<passwort>'
$env:MEDIABROWSER_API_KEY = '<api-key>'
.\tools\ApiMock\build.ps1 -Export
```

Ohne API-Key wird für den Content der lokale WPF-Content-Cache verwendet:

```text
%LOCALAPPDATA%\MediaBrowserWpf\content-cache\mediabrowser-new
```

## Seed-Inhalt

- ca. 40–80 Medien aus mehreren Ordnern und Kategorien
- zusätzlich ca. 120 Geo-Medien für die OSM-Karte
- echte Thumbnails aus `THUMBS`
- Metadaten, Beschreibungen, Rollen, Katalog-Properties
- Insta360-, Bild- und Video-Demo-Items mit Original-Content
- Crop-/Rotate-/Trim-Varianten mit generierten Thumbnails
- GPS- und Meteorologie-Beispieldaten

Originale über 10 MB werden verkleinert bzw. recompressed:

- Insta360: max. 8192 px lange Kante, JPEG
- Bilder: max. 4096 px lange Kante, JPEG
- Videos: 720p H.264/AAC

## Mock-Verhalten

- Katalog, Ordner, Kategorien, Rollen und Medien werden aus `seed.json` gelesen.
- `POST /api/v1/mediafiles/geo/clusters` und `POST /api/v1/mediafiles/geo/items` bilden die
  Geo-Cluster-API 1.5.0 inklusive Web-Mercator-Grid, Validierung, Sortierung und Pagination nach.
- Schreibende UI-Aktionen wirken in-memory und werden beim Neustart zurückgesetzt.
- `GET /api/v1/mediafiles/{id}/content` unterstützt HTTP-Range für Video-Streaming.
- Previews und Thumbnails werden direkt aus dem Seed geliefert.
- Original-Content ist als Embedded Resource in der EXE enthalten.
- Der Mock-Katalog heißt `Mock-Katalog`, damit der WPF-Content-Cache nicht mit dem echten
  `mediabrowser-new`-Cache kollidiert.
- Nicht unterstützte Endpunkte liefern bewusst leer, `204`, `404` oder `501` statt `500`.

## Geo-Cluster testen

```powershell
$body = @{
    minLongitude = -180
    minLatitude  = -85.05112878
    maxLongitude = 180
    maxLatitude  = 85.05112878
    zoom         = 4
    cellSize     = 64
    limit        = 2000
    includeSingles = $true
} | ConvertTo-Json

Invoke-RestMethod `
    -Uri http://localhost:8282/api/v1/mediafiles/geo/clusters `
    -Method Post `
    -Headers @{ Authorization = 'Bearer mock' } `
    -ContentType 'application/json' `
    -Body $body
```

`POST /api/v1/mediafiles/geo/items` akzeptiert zusätzlich `limit`, `offset` und `sort`
(`MEDIA_DATE_ASC`, `MEDIA_DATE_DESC`, `ID_ASC`).

## Optionen

```text
--port 8282        Port (Standard 8282)
--listen any       Auf 0.0.0.0 statt localhost binden
--data <dir>       Seed aus einem Verzeichnis statt aus Embedded Resources laden
--urls <url>       Vollständige ASP.NET-URL überschreiben
--verbose          Anfragen und ASP.NET-Logs auf der Konsole ausgeben
```

Der Seed-Export akzeptiert zusätzlich `--geo-limit <n>` (Standard 120), `--browse-limit <n>`
(Standard 80) und `--no-content`.

## Grenzen

- SQL-Query-Endpunkte liefern einen einfachen Auszug aus dem Mock-Katalog, keine echte SQL-Ausführung.
- Insert, XML-Import, Attachments, Sidecars und Image-Analysis sind nicht implementiert.
- Die Daten sind ein Snapshot. Änderungen an der echten Datenbank werden nicht automatisch übernommen.
