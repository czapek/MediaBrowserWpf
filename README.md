# MediaBrowserWpf

MediaBrowserWpf ist eine Windows-Desktop-Anwendung zur Verwaltung, Kategorisierung und Anzeige
großer Foto-, Video- und Medienarchive.

Die Anwendung ist eine WPF-Anwendung auf Basis von .NET Framework 4.8. Sie unterstützt historisch
gewachsene SQLite-Datenbanken, eine MariaDB als zentralen Datenbank-Backend sowie einen
HTTP-Provider für ein Spring-Boot-Backend. Der Schwerpunkt liegt auf einem schnellen Medienkatalog
mit Thumbnails, Metadaten, Kategorien, Variationen, Geodaten und unterschiedlichen Viewern.

Lizenz: GNU General Public License v3 (siehe `LICENSE`).

## Funktionen

- Medienkatalog für Dateien und Ordner
- Thumbnails und Vorschaudaten
- Metadaten, Beschreibungen und Anhänge
- Kategorien, Schlagwörter, Tagebuch- und Ortskategorien
- Favoriten, Lesezeichen, Rollen und Prioritäten
- Suche, Sortierung und benutzerdefinierte Abfragen
- Bild-, Video- und Audioanzeige
- Panorama-, 3D- und Multiplayer-Viewer
- Variationen und Ebenen (Layer) von Medien
- Duplikaterkennung, Rotation und SmartRename
- Import aus Dateien, Ordnern, Zwischenablage und Speicherkarten
- Import von XML-Metadaten und KML-Geodaten
- GPS- und Meteorologie-Daten
- Gesichtserkennung auf Basis von OpenCV
- Client für die MediaFile-API
- SQLite- und MariaDB-Unterstützung
- Einmalige Migration von SQLite nach MariaDB über die Oberfläche

## Projektstruktur

| Projekt | Zweck |
| --- | --- |
| `MediaBrowserWPF` | WPF-Oberfläche, Dialoge, Viewer, Benutzersteuerelemente und Anwendungseinstieg |
| `MediaBrowser4Lib` | Domänenmodell, Datenbankabstraktion, Provider, Migration, Utilities und SmartRename |
| `MediaProcessing` | Bild- und Videoverarbeitung, DirectShow, Effekte und Gesichtserkennung |
| `MediaBrowserLauncher` | Kleiner Launcher, der eine deployment-spezifische `MediaBrowserWPF.exe` startet und Argumente weitergibt |
| `Resources` | Native und Drittanbieter-Assemblies, z. B. DirectShowLib, WPFMediaKit und MediaInfo |
| `WPF-MediaKit` | MediaKit-Quellen und -Ressourcen |
| `scripts` | Hilfsskripte für MariaDB, u. a. zum Löschen aller Tabellen |
| `docs/openapi.yaml` | OpenAPI-Vertrag für den Spring-Boot-API-Provider |
| `docs/backend-implementation-plan.md` | Implementierungsplan für das Spring-Boot-Backend |
| `docs/api-filesystem-access-plan.md` | Bestandsaufnahme und Plan für Dateisystemzugriffe im API-Modus |
| `docs/backend/java` | Java-/JDBC-Service-Vorlagen für Katalogabfragen und Insert-Pipeline |
| `packages` | NuGet-Pakete |
| `MariaDB-Provider-Plan.md` | Ausführlicher Plan und Architekturentscheidungen zum MariaDB-Provider |

## Voraussetzungen

- Windows 10 oder 11
- .NET Framework 4.8 Developer Pack
- Visual Studio 2022 oder passende MSBuild-/NuGet-Tools
- Optional: MariaDB-Server für den MariaDB-Modus
- Die DLLs und Ressourcen unter `Resources` müssen vorhanden sein

Für die Wiedergabe bestimmter Medienformate können zusätzliche System-Codecs oder externe
Werkzeuge erforderlich sein. Die Anwendung nutzt unter anderem DirectShow, MediaInfo, WPFMediaKit
und ffmpeg-nahe Verarbeitung.

Es gibt derzeit kein separates automatisches Testprojekt in der Solution. Prüfungen erfolgen über
Build, Start, Schema-Update und manuelle Smoke-Tests.

## Build

Abhängigkeiten wiederherstellen und die Solution bauen:

```powershell
nuget restore MediaBrowser4.sln
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' `
  MediaBrowser4.sln /t:Build /p:Configuration=Debug /p:Platform="Any CPU" /v:minimal /nologo
```

Alternativ die Solution in Visual Studio 2022 öffnen und `MediaBrowser4.sln` bauen.

Die wichtigsten Ergebnisse liegen unter:

- `MediaBrowserWPF\bin\Debug\MediaBrowserWPF.exe`
- `MediaBrowser4Lib\bin\Debug\MediaBrowser4Lib.dll`
- `MediaProcessing\bin\Debug\MediaProcessing.dll`
- `MediaBrowserLauncher\bin\Debug\MediaBrowserLauncher.exe`

Bekannte, bereits vorhandene Compilerwarnung: `CS0618` in
`MediaBrowser4Lib\Objects\MediaItem.cs` wegen einer veralteten `FormattedText`-Überladung.

### Deployment

Nach dem Build können die neueren Dateien in das lokale Synology-Deployment kopiert werden:

```powershell
.\scripts\Deploy-ToSynology.ps1 -Preview
.\scripts\Deploy-ToSynology.ps1
```

Das Skript kopiert aus `MediaBrowserWPF\bin\Debug` nur Dateien, die im Ziel neuer sind, und löscht
keine zusätzlichen Dateien im Deployment-Ordner.

## Start und Kommandozeile

### SQLite

Ohne expliziten Provider wird SQLite verwendet. Der Pfad kann als Argument oder über `--db-file`
übergeben werden.

```powershell
.\MediaBrowserWPF.exe "C:\Pfad\MediaBrowser4DB.mb4"
.\MediaBrowserWPF.exe --provider sqlite --db-file "C:\Pfad\MediaBrowser4DB.mb4"
```

Optional kann ein Init-Ordner als letztes Argument angegeben werden.

### MariaDB

```powershell
.\MediaBrowserWPF.exe `
  --provider mariadb `
  --db-host <host> `
  --db-port 3306 `
  --db-name <datenbank> `
  --db-user <benutzer> `
  --db-password-env MEDIABROWSER_DB_PASSWORD
```

Unterstützte Optionen:

| Option | Bedeutung |
| --- | --- |
| `--provider sqlite\|mariadb` | Datenbank-Backend |
| `--db-file <path>` | SQLite-Datei |
| `--db-host <host>` | MariaDB-Host |
| `--db-port <port>` | MariaDB-Port, Standard 3306 |
| `--db-name <name>` | MariaDB-Datenbank |
| `--db-user <user>` | MariaDB-Benutzer |
| `--db-password <password>` | Passwort direkt auf der Kommandozeile |
| `--db-password-env <ENV_NAME>` | Passwort aus einer Umgebungsvariable |
| `--db-password-file <path>` | Passwort aus einer Datei |
| `--db-ssl-mode <mode>` | `None`, `Preferred`, `Required`, `VerifyCA` oder `VerifyFull` |
| `--update` | MariaDB-Schema anlegen oder aktualisieren |
| `--api-base-url <url>` | Basis-URL des Spring-Boot-Backends |
| `--api-tenant <tenant>` | Mandant bzw. logischer Katalogname |
| `--api-key <key>` | Statischer Bearer-Token für Service-Zugänge |
| `--api-user <user>` | Benutzer für den Login-Token-Flow |
| `--api-password <password>` | Passwort für den Login-Token-Flow |
| `--api-password-env <ENV_NAME>` | API-Passwort aus einer Umgebungsvariable |
| `--api-password-file <path>` | API-Passwort aus einer Datei |
| `--api-timeout <seconds>` | HTTP-Timeout, Standard 60 Sekunden |
| `--help`, `-h`, `-?` | Hilfe anzeigen |

Ohne Angabe einer Passwortquelle wird `MEDIABROWSER_DB_PASSWORD` verwendet.

Beim ersten Start oder nach Schemaänderungen muss die MariaDB einmalig mit `--update` gestartet
werden:

```powershell
$env:MEDIABROWSER_DB_PASSWORD = '<passwort>'
.\MediaBrowserWPF.exe `
  --provider mariadb `
  --db-host <host> `
  --db-name <datenbank> `
  --db-user <benutzer> `
  --update
```

Danach kann die Anwendung ohne `--update` gestartet werden.

Das direkte Passwort auf der Kommandozeile ist in der Prozessliste sichtbar. Bevorzugt sollten
Umgebungsvariable oder Passwortdatei verwendet werden.

### API-Provider

Der API-Provider verbindet die Anwendung mit einem Spring-Boot-Backend. Die API wird unter
`/api/v1` erwartet. Für den normalen Betrieb meldet sich die Anwendung über `/api/v1/auth/login`
mit Benutzer, Passwort und optionalem Mandanten an und verwendet anschließend den gelieferten
Bearer-Token. Kurzlebige Access-Tokens werden bei Ablauf oder nach einem `401` über den
Refresh-Token erneuert.

```powershell
.\MediaBrowserWPF.exe `
  --provider api `
  --api-base-url http://localhost:8080 `
  --api-tenant <katalog> `
  --api-user <benutzer> `
  --api-password-env MEDIABROWSER_API_PASSWORD
```

Für Service-Zugänge kann alternativ ein statischer Token verwendet werden:

```powershell
.\MediaBrowserWPF.exe `
  --provider api `
  --api-base-url http://localhost:8080 `
  --api-key <token>
```

Der vollständige REST-Vertrag inklusive DTO-Schemas und Auth-Endpunkten liegt in
`docs/openapi.yaml`. Das Backend muss diese Schnittstelle implementieren. Der API-Provider
unterstützt keine direkten SQL-Transaktionen; `MBCommand` und `MBTransaction` werfen bewusst
`NotSupportedException`.

Wichtig für den API-Modus: Das Backend führt keine Schreiboperationen auf dem Medien-Dateisystem
aus. Originaldateien werden nur aus einem read-only Content Store gelesen. Upload, Rename, Move,
Delete und Recycle von Dateien sind nicht Teil der API. Details stehen in
`docs/api-filesystem-access-plan.md`.

### Launcher

`MediaBrowserLauncher` startet standardmäßig:

```text
%USERPROFILE%\SynologyDriveDeployment\MediaBrowserWpf\MediaBrowserWPF.exe
```

und gibt alle Kommandozeilenargumente weiter. Der Launcher ist damit an ein bestimmtes Deployment
angepasst und nicht für jeden Arbeitsplatz unverändert geeignet.

## Datenbanken

### SQLite

Der klassische Modus verwendet eine Hauptdatenbank (`*.mb4`) und eine companion Datenbank für
Thumbnails (`*.mb4t`). Beide Dateien gehören zusammen. Der SQLite-Provider liegt unter
`MediaBrowser4Lib\DB\SQLite`.

### MariaDB

Im MariaDB-Modus liegen Katalogdaten und Thumbnails in einer Datenbank. Das Schema wird durch
`MariaDbAdministration` erzeugt und aktualisiert. Die aktuelle Schema-Version ist in
`UpdateDb.DbVersion` definiert (derzeit `4.10`).

Wichtige Tabellen:

- `DBPROPERTIES`: katalogweite Einstellungen und Identität
- `FOLDERS`, `MEDIAFILES`: Ordner und Medienobjekte
- `VARIATIONS`, `LAYERS`: Variationen und Ebenen
- `THUMBS`: Thumbnail-Binärdaten
- `CATEGORY`, `CATEGORIZE`: Kategorien und Zuordnungen
- `METADATA`, `METADATANAME`: Metadaten und Metadaten-Definitionen
- `DESCRIPTION`, `ATTACHMENTS`, `ATTACHED`: Beschreibungen und Anhänge
- `FAVORITES`, `ROLES`, `REMOTEMAPPING`
- `EXTRADATA`, `EXTRADATAS`, `USERDEFINEDREQUESTS`
- `DATALOGGER_GPS`, `DATALOGGER_METEOROLOGY`
- `FACES`: Gesichtserkennung

Die MariaDB-Tabellen verwenden `utf8mb4`. Referenzielle Lösch- und Pflegeaufgaben werden
überwiegend durch Trigger und Anwendungslogik umgesetzt, nicht durch harte Fremdschlüssel.

Das Schema wird über `--update` angelegt bzw. erweitert. Fehlende Spalten, Indizes und Trigger
werden ergänzt; bestehende Daten werden dabei nicht gelöscht.

Im Projekt ist außerdem ein SQL-Server-Provider vorhanden. Der aktuelle Startpfad der Anwendung
bietet jedoch nur SQLite, MariaDB und den API-Provider an.

### API-Provider

`MediaBrowser4Lib\DB\Api\ApiProvider.cs` implementiert die `DBProvider`-Schnittstelle über HTTP.
`ApiInsert` übernimmt den Insert-Pfad über `POST /api/v1/mediafiles/insert`. Die DTOs liegen in
`MediaBrowser4Lib\DB\Api\ApiDtos.cs`; der OpenAPI-Vertrag liegt in `docs/openapi.yaml`.

Der Provider eignet sich für ein Spring-Boot-Backend, das den Medienkatalog zentral bereitstellt.
Direkte SQL-Aufrufe und Transaktionen sind über diesen Provider nicht möglich. Erweiterungen sollten
zuerst im OpenAPI-Vertrag, danach im Spring-Boot-Backend und anschließend im `ApiProvider` ergänzt
werden.

## Migration von SQLite nach MariaDB

Die Migration ist über die Anwendung erreichbar:

1. MariaDB-Zieldatenbank starten und einmalig mit `--update` auf den aktuellen Schema-Stand bringen.
2. Anwendung im MariaDB-Modus starten.
3. `Import > SQLite-Datenbank nach MariaDB migrieren...` öffnen.
4. Hauptdatenbank (`*.mb4`) und Thumbnail-Datenbank (`*.150.mb4t` bzw. passende `*.mb4t`) auswählen.
5. `Prüfen` ausführen und Zeilenzahlen sowie Warnungen kontrollieren.
6. Löschwarnung bestätigen, Datenbanknamen eingeben und `Migration starten`.

Die Migration:

- liest die SQLite-Hauptdatenbank und die Thumbnail-Datenbank ausschließlich lesend,
- leert die MariaDB-Zieldatenbank vor dem Import,
- übernimmt Katalogdaten, Variationen, Metadaten, Kategorien, GPS-/Meteorologiedaten und Thumbnails,
- übernimmt `VARIATIONS.DESCRIPTION` und `CATEGORIZE.USERDEFINED`,
- legt die Haupt-Thumbnails als `THUMBS`-Einträge mit `PREVIEW_WIDTH=0` und `PREVIEW_HEIGHT=0` ab,
- setzt `CATEGORY.DATE`-Werte vor `1000-01-01` auf `NULL` (typischerweise `0001-01-01`),
- übernimmt keine verwaisten Datensätze,
- validiert am Ende die Zeilenzahlen und startet die Anwendung neu.

Die Spalte `VARIATIONS.THUMB` wird vom MariaDB-Provider nicht für die normale
Thumbnail-Anzeige verwendet. Maßgeblich sind die Einträge in `THUMBS` mit
`PREVIEW_WIDTH=0` und `PREVIEW_HEIGHT=0`.

Verwaiste Datensätze werden nicht importiert. Dazu zählen insbesondere:

- `VARIATIONS` ohne gültige `MEDIAFILES`-Referenz
- `CATEGORIZE` ohne gültige `CATEGORY`- oder `VARIATIONS`-Referenz
- `METADATA` ohne gültige `MEDIAFILES`- oder `METADATANAME`-Referenz
- `LAYERS` und `THUMBS` ohne gültige `VARIATIONS`-Referenz
- ungültige `MEDIAFILES.CURRENTVARIATION`-Referenzen; diese werden auf `NULL` gesetzt

Die Migration ist destruktiv: Die Zieldatenbank wird vollständig geleert. Bei Abbruch oder Fehler
bleibt sie unvollständig und muss erneut migriert werden. Vor der Migration sollte ein Backup der
Zieldatenbank erstellt werden.

Für große Tabellen wird ein Bulk-Import verwendet. Wenn der MariaDB-Server ein kleines
`max_allowed_packet` hat (z. B. 1 MB), wechselt die Migration automatisch auf stapelweise
Einzelinserts. Dadurch funktioniert die Migration auch ohne Änderung der Serverkonfiguration.

Gesichtsdaten (`FACES`) werden nicht aus SQLite migriert. Die Tabelle bleibt nach der Migration leer.

## Konfiguration und Protokolle

Lokale Benutzereinstellungen liegen unter `MediaBrowserWPF\Properties\Settings.settings` bzw. in
den benutzerspezifischen .NET-Einstellungen. Dazu gehören unter anderem:

- letzter SQLite-Datenbankpfad
- RGB-Dateiendungen
- DirectShow-Endungen
- zusätzliche Audiodateien

Katalogweite Einstellungen werden in der Tabelle `DBPROPERTIES` gespeichert, z. B. `GUID`,
`TagFolder`, Thumbnail-Größe, Startordner, Standardrollen und Verhalten bei fehlenden Dateien.

Fehlerprotokolle schreibt die Anwendung nach:

```text
%LOCALAPPDATA%\MediaBrowserWpf\logfiles
```

Relevante Dateien:

- `error_YYYY_MM_DD.log`
- `errorStackTrace_YYYY_MM_DD.log`
- `info_YYYY_MM_DD.log`

## Entwicklungsnotizen

Zentrale Abstraktionen:

- `DBProvider` in `MediaBrowser4Lib\DB\DBProvider.cs`
- `ICommandHelper` und `ITransaction`
- `MediaBrowserContext` als zentraler Einstiegspunkt für Datenbank- und Medienoperationen
- Provider unter `MediaBrowser4Lib\DB\SQLite`, `DB\Maria` und `DB\SQLServer`
- Migration unter `MediaBrowser4Lib\DB\Migration\SqliteToMariaDbMigration.cs`

Die WPF-Oberfläche greift überwiegend über `MediaBrowserContext` auf die Datenbank zu. Neue
Datenbankfunktionen sollten daher zuerst im Provider-Interface und anschließend in den Providern
ergänzt werden.

Beim Ändern des MariaDB-Schemas:

1. DDL in `MariaDbAdministration` aktualisieren.
2. Ggf. Spaltenbreiten und Schema-Evolution in `EnsureColumnsAndIndexes`/`EnsureColumnCapacities`
   ergänzen.
3. `--update` gegen eine Testdatenbank ausführen.
4. Migration und Anwendung gegen SQLite und MariaDB testen.

Der Ordner `scripts` enthält ein destruktives Hilfsskript zum Löschen aller MariaDB-Tabellen. Es
sollte nur in Entwicklungs- oder Testdatenbanken verwendet werden.

## Lizenz

Dieses Projekt steht unter der GNU General Public License v3. Siehe `LICENSE`.
