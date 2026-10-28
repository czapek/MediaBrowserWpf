# AGENTS.md

Diese Datei enthält Arbeitshinweise für Coding-Agenten im Repository `MediaBrowserWpf`.

## Projektkontext

- Windows-Desktop-Anwendung auf Basis von WPF und .NET Framework 4.8.
- Alte, nicht-SDK-basierte `.csproj`-Projekte.
- Hauptprojekte: `MediaBrowserWPF`, `MediaBrowser4Lib`, `MediaProcessing`, `MediaBrowserLauncher`.
- Datenbank-Backends: SQLite (klassisch), MariaDB (neuer Provider) und Spring-Boot-API-Provider.
- Ein SQL-Server-Provider ist im Library-Code vorhanden, wird vom aktuellen Startpfad aber nicht angeboten.
- Die Oberfläche ist deutschsprachig.
- Lizenz: GPLv3.

Vor Änderungen lesen:

1. `README.md`
2. `MariaDB-Provider-Plan.md` bei Datenbank- oder Migrationsthemen
3. Diese `AGENTS.md`

## Grundregeln

- Keine Commits, Push-Operationen oder PRs ohne ausdrückliche Aufforderung.
- Keine destruktiven Git-Operationen.
- Keine Kommentare im Code ergänzen, außer der Benutzer verlangt es ausdrücklich.
- Bestehenden Code-Stil, Namensschema und Schreibweise übernehmen.
- Keine unnötigen Refactorings oder Umformatierungen.
- Keine Secrets, Passwörter oder Zugangsdaten in Dateien, Commits oder Ausgaben schreiben.
- Keine bestehenden Benutzerdaten oder Testdaten löschen.
- Nach Möglichkeit nur die betroffenen Dateien ändern.

## Encoding und Zeilenenden

- Mehrere bestehende Dateien verwenden historische Kodierungen und gemischte Zeilenenden.
- Keine pauschale Konvertierung von Encoding oder Zeilenenden durchführen.
- Neue Dateien UTF-8 erstellen.
- Gezielt mit Edit-Tools arbeiten, nicht komplette Dateien neu schreiben, wenn nur kleine Änderungen nötig sind.

## Build

Build-Befehl unter Windows:

```powershell
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' `
  MediaBrowser4.sln /t:Build /p:Configuration=Debug /p:Platform="Any CPU" /v:minimal /nologo
```

Alternativ Visual Studio 2022 oder MSBuild aus einer anderen Installation verwenden.

Bekannte, bereits vorhandene Warnung:

- `CS0618` in `MediaBrowser4Lib\Objects\MediaItem.cs` wegen `FormattedText`.

## Deployment

Wenn der Benutzer „deploy“ sagt, nach einem erfolgreichen Build ausführen:

```powershell
.\scripts\Deploy-ToSynology.ps1
```

Das Skript kopiert die neueren Build-Dateien nach
`C:\Users\SebastianCzapek\SynologyDriveDevelopment\MediaBrowserWpf`. Vorher sicherstellen, dass
`MediaBrowserWPF` nicht läuft. Für eine Vorschau `-Preview` verwenden.

Die Warnung ist nicht durch neue Änderungen verursacht und muss nicht behoben werden, sofern die
Aufgabe nichts anderes verlangt.

## Tests und Verifikation

- Es gibt derzeit kein automatisches Testprojekt.
- Mindestens den Build ausführen.
- Bei UI-Änderungen die Anwendung starten und den betroffenen Ablauf manuell prüfen.
- Bei Datenbankänderungen zusätzlich `--update` und einen passenden Smoke-Test durchführen.
- Keine GUI-Prozesse unbeaufsichtigt laufen lassen. Nur selbst gestartete Prozesse beenden.
- Temporäre Test-Harnesses, Logs und Hilfsdateien nach der Prüfung entfernen.

## Datenbank-Sicherheit

Besonders vorsichtig bei:

- `scripts\Drop-All-MariaDbTables.ps1`
- Migrationsläufen über `SqliteToMariaDbMigration`
- `--update`
- direkten SQL-Schreiboperationen gegen MariaDB
- Löschen oder Überschreiben von SQLite-Dateien

Regeln:

- Die SQLite-Migration ist destruktiv: Die MariaDB-Zieldatenbank wird vor dem Import geleert.
- Ohne ausdrückliche Bestätigung des Benutzers keine Migration und keine schreibenden Datenbanktests
  gegen echte Ziele ausführen.
- Read-only-Preflight und Schema-Validierung sind zulässig, wenn keine Daten verändert werden.
- Quelldatenbanken unter `temp\` nur lesend öffnen.
- Für Tests keine echten Zugangsdaten in Dateien hinterlegen; Umgebungsvariablen oder
  Passwortdateien verwenden.
- Vor Schema-Änderungen prüfen, ob eine Migration bestehender Zieldatenbanken nötig ist.
- Nach fehlgeschlagener oder abgebrochener Migration ist die Zieldatenbank unvollständig. Nicht als
  gültigen Zustand behandeln.

## Logs

Laufzeitfehler der WPF-Anwendung:

```text
%LOCALAPPDATA%\MediaBrowserWpf\logfiles
```

Relevante Dateien:

- `error_YYYY_MM_DD.log`
- `errorStackTrace_YYYY_MM_DD.log`
- `info_YYYY_MM_DD.log`

Bei Migrationsfehlern zuerst diese Logs prüfen. Migrationsexceptionen werden mit dem Kommentar
`SQLite-Migration` protokolliert.

## Architektur

- `MediaBrowserWPF`: WPF-UI, Dialoge, Viewer, UserControls, Anwendungseinstieg (`App.xaml.cs`).
- `MediaBrowser4Lib`: Domänenobjekte, `MediaBrowserContext`, DB-Abstraktion, Provider, Utilities,
  SmartRename und Migration.
- `MediaProcessing`: Bild-/Videoverarbeitung, DirectShow, Effekte, Gesichtserkennung.
- `MediaBrowserLauncher`: deployment-spezifischer Launcher.
- `Resources`: native und Drittanbieter-Assemblies.
- `packages`: NuGet-Pakete.

Zentrale Datenbanktypen:

- `DBProvider`
- `ICommandHelper`
- `ITransaction`
- `MediaBrowserContext`

Neue Datenbankfunktionen zuerst in der Abstraktion und danach in allen relevanten Providern ergänzen.

## WPF und Projektdateien

- Die Projekte verwenden das alte `.csproj`-Format.
- Neue XAML-Dateien müssen sowohl als `<Page>` als auch die Codebehind-Datei als `<Compile>` in
  `MediaBrowserWPF.csproj` eingetragen werden.
- Neue C#-Dateien in `MediaBrowser4Lib` müssen in `MediaBrowser4Lib.csproj` eingetragen werden.
- UI-Texte sind deutsch und werden überwiegend direkt in XAML oder Codebehind verwendet.
- Beim Schließen der Anwendung werden DB-Properties gespeichert. Nach einer Migration muss das
  Überschreiben der importierten Properties verhindert werden; dafür existiert
  `App.SuppressPropertySaveOnExit`.

## Startparameter

Wichtige Startpfade:

- SQLite: `--provider sqlite --db-file <path>`
- MariaDB: `--provider mariadb --db-host ... --db-name ... --db-user ...`
- API: `--provider api --api-base-url ... [--api-tenant ...] [--api-key ... | --api-user ... --api-password ...]`
- Passwortquellen: `--db-password`, `--db-password-env`, `--db-password-file`
- Erststart/Schema-Update: `--update` (nur MariaDB)

Ohne Passwortquelle wird `MEDIABROWSER_DB_PASSWORD` verwendet. Für die API gibt es analog
`--api-password`, `--api-password-env`, `--api-password-file` und `MEDIABROWSER_API_PASSWORD`.
Passwörter nicht in der Kommandozeile dokumentieren oder loggen, wenn vermeidbar.

## API-Provider

Relevante Dateien:

- `MediaBrowser4Lib\DB\Api\ApiProvider.cs`
- `MediaBrowser4Lib\DB\Api\ApiInsert.cs`
- `MediaBrowser4Lib\DB\Api\SpringApiClient.cs`
- `MediaBrowser4Lib\DB\Api\ApiDtos.cs`
- `MediaBrowser4Lib\DB\Api\ApiConnectionOptions.cs`
- `docs/openapi.yaml`

Regeln:

- Der API-Vertrag ist in `docs/openapi.yaml` definiert. Änderungen an Endpunkten oder DTOs zuerst dort
  und in `ApiDtos.cs` nachziehen.
- Das Spring-Boot-Backend wird separat implementiert und ist nicht Teil dieses Repositories.
- Die MediaBrowser-API darf keine Schreiboperationen auf dem Medien-Dateisystem ausführen:
  keine Uploads, kein Copy/Move/Rename/Delete/Recycle von Original-, Sidecar- oder Preview-Dateien.
  Datenbank-Schreiboperationen bleiben erlaubt. Der geplante Umgang steht in
  `docs/api-filesystem-access-plan.md`.
- `ApiProvider.MBCommand` und `ApiProvider.MBTransaction` werfen bewusst `NotSupportedException`.
  Keine direkten SQL-Aufrufe in API-Pfade einbauen.
- `MediaBrowserContext` lädt Katalogproperties über `DBProvider.GetDBProperties()`. Der API-Provider
  überschreibt diese Methode; bei neuen Providern denselben Weg verwenden.
- `ApiInsert` lädt Thumbnails und Metadaten in den Insert-DTOs hoch. Die Antwort muss
  `ApiInsertResultDto` mit Medien- und Variations-IDs liefern.
- Authentifizierung: Login über `/auth/login`, danach Bearer-Token. Abgelaufene oder mit `401`
  abgelehnte Access-Tokens werden über `/auth/refresh` erneuert; bei ungültigem Refresh-Token erfolgt
  ein erneuter Login. Keine Tokens oder Passwörter loggen.
- API-Fehler werden als `ApiException` mit HTTP-Status und Response-Body geworfen.

## MariaDB-Schema

Relevante Dateien:

- `MediaBrowser4Lib\DB\Maria\MariaDbAdministration.cs`
- `MediaBrowser4Lib\DB\Maria\MariaDbProvider.cs`
- `MediaBrowser4Lib\DB\Maria\MariaDbConnectionOptions.cs`
- `MediaBrowser4Lib\DB\UpdateDb.cs`

Bei Schemaänderungen:

1. DDL in `MariaDbAdministration` aktualisieren.
2. Sicherstellen, dass `--update` bestehende Datenbanken erweitert, ohne Daten zu löschen.
3. Spaltenbreiten und `TEXT`-Kapazitäten in `EnsureColumnCapacities` berücksichtigen.
4. Prüfungen in `SqliteToMariaDbMigration` anpassen.
5. Schema-Version in `UpdateDb.DbVersion` nur bewusst ändern.
6. Migration und Anwendung gegen SQLite und MariaDB testen.

Wichtige MariaDB-Eigenschaften:

- `utf8mb4`
- keine harten Fremdschlüssel; Trigger und Anwendungslogik übernehmen Pflege und Löschkaskaden
- `THUMBS` liegt in derselben Datenbank
- `DBPROPERTIES` enthält katalogweite Einstellungen
- `FACES` bleibt bei der SQLite-Migration leer

## SQLite-nach-MariaDB-Migration

Relevante Datei:

- `MediaBrowser4Lib\DB\Migration\SqliteToMariaDbMigration.cs`

Verhalten, das beibehalten werden muss:

- Quelldatenbanken werden nur lesend geöffnet.
- Die Zieldatenbank wird vor dem Import vollständig geleert.
- `VARIATIONS.DESCRIPTION` und `CATEGORIZE.USERDEFINED` werden übernommen.
- Haupt-Thumbnails werden in `THUMBS` mit `PREVIEW_WIDTH=0` und `PREVIEW_HEIGHT=0` abgelegt.
  `VARIATIONS.THUMB` wird vom MariaDB-Provider nicht für die normale Thumbnail-Anzeige verwendet.
- `CATEGORY.DATE`-Werte vor `1000-01-01` werden auf `NULL` gesetzt.
- Verwaiste Datensätze werden nicht importiert:
  - `VARIATIONS` ohne gültige `MEDIAFILES`-Referenz
  - `CATEGORIZE` ohne gültige `CATEGORY`-/`VARIATIONS`-Referenz
  - `METADATA` ohne gültige `MEDIAFILES`-/`METADATANAME`-Referenz
  - `LAYERS` und `THUMBS` ohne gültige `VARIATIONS`-Referenz
- Ungültige `MEDIAFILES.CURRENTVARIATION`-Werte werden auf `NULL` gesetzt.
- Orphan-Filterung darf kaskadieren: Wird eine Variation ausgeschlossen, dürfen abhängige
  `THUMBS`, `LAYERS` und `CATEGORIZE`-Einträge ebenfalls nicht importiert werden.
- Nach dem Import werden Zielzeilenzahlen validiert.
- `ReadTargetCounts` muss `CommandTimeout = 0` verwenden, da große Tabellen den Standard-Timeout
  überschreiten können.
- Bulk-Import wird nur bei ausreichend großem `max_allowed_packet` verwendet. Bei kleinen Paketen
  erfolgt ein gestückelter Fallback mit adaptiven Batches.
- Die Migration darf nicht auf veraltete Quell- oder Zielpfade aus einem alten Preflight vertrauen.
- Nach erfolgreicher Migration wird die Anwendung neu gestartet.

## Bekannte Fallstricke

- `rg` ist in der Umgebung nicht installiert. Stattdessen die verfügbaren Suchwerkzeuge bzw.
  PowerShell nutzen.
- `temp\` enthält lokale, nicht versionierte Testdatenbanken. Nicht löschen und nicht committen.
- `MediaBrowserLauncher` enthält einen festen Deployment-Pfad.
- Der MariaDB-Provider ist ein Port. Nicht davon ausgehen, dass jeder Codepfad bereits vollständig
  gegen MariaDB validiert ist; bei Änderungen gezielt Smoke-Tests durchführen.
- SQL-Fehler können in den Logs unter `%LOCALAPPDATA%\MediaBrowserWpf\logfiles` liegen.
- `max_allowed_packet` des MariaDB-Servers kann klein sein. Migrationstests nicht nur gegen Server
  mit großen Paketen durchführen.
- Nach einem fehlgeschlagenen Migrationslauf ist `DBPROPERTIES` möglicherweise leer; dann ist ein
  erneutes `--update` nötig, bevor die Migration wiederholt werden kann.
- Beim Testen keine Benutzeroberfläche unbeaufsichtigt offen lassen.
- Temporäre Harnesses nicht im Repository oder in `bin` liegen lassen.

## Definition of Done

Vor Abschluss einer Aufgabe:

- Build erfolgreich.
- Keine neuen Compilerfehler.
- Betroffene Abläufe manuell geprüft, sofern möglich.
- Keine Debug-Ausgaben, Test-Harnesses oder temporären Dateien im Arbeitsverzeichnis hinterlassen.
- Dokumentation aktualisieren, wenn sich Startparameter, Schema, Migration oder Verhalten ändern.
- Änderungen an Datenbank oder Migrationslogik ausdrücklich im Abschlussbericht nennen.
