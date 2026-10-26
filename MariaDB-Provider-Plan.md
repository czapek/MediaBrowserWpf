# MariaDB-Provider – Umsetzungsplan

Stand: 2026-10-25  
Repository: `C:\Users\SebastianCzapek\workspace\MediaBrowserWpf`  
Status: Entwurf, noch keine Implementierung

## 1. Ziel

MediaBrowserWPF soll vollständig gegen MariaDB statt gegen SQLite laufen können. Der Provider wird beim Start über Argumente gewählt. Ein expliziter `--update`-Lauf legt eine fehlende Datenbank sowie alle fehlenden Tabellen, Indizes, Trigger und Seed-Daten an, ohne bestehende Daten zu löschen.

SQLite bleibt der Standard und muss unverändert weiter funktionieren.

## 2. Definition of Done

- `MediaBrowser4.sln` baut ohne temporäre Ausschlüsse mit 0 Errors.
- `--provider sqlite` und der bisherige Aufruf `MediaBrowserWPF.exe <db.mb4>` funktionieren unverändert.
- `--provider mariadb ...` startet mit einer initialisierten MariaDB.
- `--provider mariadb ... --update` ist idempotent und legt alle fehlenden Objekte an.
- Sämtliche abstrakten Member von `DBProvider` sind im MariaDB-Provider implementiert; kein dauerhaftes `NotImplementedException` bleibt übrig.
- MariaDB-Code enthält kein `System.Data.SQLite`, kein `ATTACH`, kein `last_insert_rowid()`, kein `INSERT OR IGNORE`, kein `RANDOM()` und kein `VACUUM`.
- Anlegen, Anzeigen, Suchen, Kategorisieren, Bearbeiten, Verschieben, Umbenennen, Löschen und Recyceln von Medien funktionieren gegen MariaDB.
- Thumbnails, Vorschaudaten, Gesichter, Variationen, Layer, Metadaten, Beschreibungen, Attachments, GPS- und Meteorologiedaten funktionieren.
- SQLite-Regressionstest mit einer bestehenden `.mb4`-Datei ist erfolgreich.
- MySqlConnector und alle benötigten Laufzeitabhängigkeiten liegen im WPF-Ausgabeverzeichnis.

## 3. Ausgangslage

- Solution: `MediaBrowser4.sln`, .NET Framework 4.8, WPF, alte csproj-/`packages.config`-Projekte.
- Projekte: `MediaBrowser4Lib`, `MediaProcessing`, `MediaBrowserWPF`, `MediaBrowserLauncher`.
- NuGet-Pakete sind restored; `MySqlConnector 2.4.0` ist in `MediaBrowser4Lib` referenziert.
- `MediaBrowser4Lib\DB\Maria\MariaDbProvider.cs` ist ein unfertiger Fremdstand:
  - 4 ungültige `override`-Member (`GetConnection`, `DatabaseExists`, `CreateDatabase`, `DropDatabase`), die in `DBProvider` nicht existieren.
  - 105 abstrakte Member von `DBProvider` sind nicht implementiert.
  - Compilerfehler: 111× CS0534 und 4× CS0115.
- `MariaDbProvider.GetConnectionString()` verwendet hartkodiert `localhost`, `mediabrowser`, `root` und ein leeres Passwort.
- `Maria\SimpleConnection.cs` implementiert `IConnectionManager` inkonsistent: `IDbConnection Connection` plus explizites `DbConnection`-Member, das `NotImplementedException` wirft.
- `Maria\CommandHelper.cs` und `Maria\Transaction.cs` werfen fast überall `NotImplementedException`.
- `MariaDbProvider` ist derzeit nirgends instanziiert; der Ausschluss der Maria-Dateien ließ den Build erfolgreich werden.
- MariaDB ist vom Host aktuell nicht erreichbar:
  - `localhost:3306` verweigert die Verbindung.
  - Host `mariadb` ist nicht auflösbar.
- Vom Benutzer genannte Verbindung: DB `mediabrowser-new`, User `mediabrowser-new`, Passwort `Mediabrowser!2027`.
- `MediaBrowserWPF\App.xaml.cs` wertet bisher nur einen SQLite-Datei- oder Ordnerpfad aus.
- `MediaBrowserContext.Init(string dbPath)` erzeugt immer eine SQLite-Datenbank.
- `MediaBrowserContext.DBPath` castet hart auf `SQLiteProvider`; `DBPreviewPath`, `DBTempFolder` und Teile von `WorkingDirectory` sind dateipfadbasiert.
- `MediaBrowser4Lib\Objects\MediaItemSqlRequest.cs` und `SQLite\SearchTokenSql.cs` verwenden `SQLiteParameter`.
- `SQLiteInsert` ist vollständig SQLite-spezifisch und nutzt eine separate Thumbnail-Datenbank.
- SQLite-Trigger `fkd_MEDIAFILES_DELETE` und `fkd_MEDIAFILES_UPDATE` ändern die Tabelle `MEDIAFILES` selbst. MariaDB verbietet das in Triggern.
- Der SQLite-Provider bleibt als Referenz und für Rückwärtskompatibilität unangetastet, soweit nicht für gemeinsame Abstraktionen nötig.

## 4. Annahmen und offene Punkte

Diese Punkte sind vor der Integrationsphase zu klären:

- Erreichbarer MariaDB-Host und Port; aktuell sind weder `localhost:3306` noch `mariadb:3306` nutzbar.
- Soll `mediabrowser-new` als Test- und Ziel-DB dienen, oder wird eine separate Test-DB angelegt?
- Hat der Benutzer `CREATE DATABASE`- und `TRIGGER`-Rechte? Falls nicht, muss die DB vorab durch einen Administrator angelegt werden.
- Passwortübergabe: Umgebungsvariable oder Datei statt Klartext auf der Kommandozeile.
- Ist eine einmalige Datenmigration von SQLite nach MariaDB gewünscht? Dieser Plan behandelt nur den Provider, keine Datenübernahme.
- Wird TLS/SSL für die MariaDB-Verbindung benötigt?
- Sollen Vorschaudaten in mehreren Größen parallel gespeichert werden? Der Plan sieht das vor.
- Wird die Providerwahl zusätzlich in der UI benötigt, oder reicht der Startparameter? Der Plan geht von Startparameter plus optionalem Anzeigen des DB-Namens aus.

## 5. Architekturentscheidungen

### 5.1 Pragmatischer Port statt SQLite-Refactoring

`MariaDbProvider` wird als eigenständige Implementierung aufgebaut, überwiegend durch Portieren der Logik aus `SQLiteProvider`, mit gezielten Dialekt-Anpassungen. `SQLiteProvider` wird nicht in eine große gemeinsame Basisklasse umgebaut, um das Regressionrisiko für bestehende SQLite-Datenbanken klein zu halten.

Gemeinsam genutzt werden nur:

- `DBProvider` als Vertrag,
- `IConnectionManager`, `ICommandHelper`, `ITransaction`,
- eine providerneutrale Parameterbeschreibung,
- kleine, klar abgegrenzte Hilfsklassen für Typkonvertierung und SQL-Dialekt.

Eine spätere Konsolidierung in eine gemeinsame Basisklasse ist möglich, aber nicht Teil dieses Plans.

### 5.2 Minimalinvasive Ergänzungen an `DBProvider`

- `DBPath` wird als virtuelle Eigenschaft ergänzt: SQLite liefert den Dateipfad, MariaDB `null`.
- Der SQLite-`DBPath` wird von `internal` auf `public override` geändert.
- Weitere providerabhängige Pfade werden im `MediaBrowserContext` über den Providertyp oder neue virtuelle Eigenschaften behandelt, nicht über Casts.
- Keine neuen abstrakten Member, damit SQLite nicht unnötig angepasst werden muss.
- `GetConnection`/`DatabaseExists`/`CreateDatabase`/`DropDatabase` aus dem Maria-WIP werden zu internen bzw. providerspezifischen Methoden, nicht zu `DBProvider`-Overrides.

### 5.3 Providerneutrale Parameter

Neue Klasse `MediaBrowser4Lib\DB\SqlParameterInfo.cs`:

- `Name`, `Value`, `DbType?`.

Anpassungen:

- `SearchTokenSql.ParameterList` wird `List<SqlParameterInfo>`.
- `MediaItemSqlRequest` erhält eine neutrale Sicht auf die Parameter, ohne das serialisierte Layout zu brechen.
- Die bestehende private `TupelParameterList` mit `List<Tuple<string, object>>` bleibt als serialisiertes Feld erhalten, damit alte `USERDEFINEDREQUESTS`-Blobs weiterhin per `BinaryFormatter` lesbar sind.
- SQLite konvertiert `SqlParameterInfo` beim Ausführen in `SQLiteParameter`.
- MariaDB konvertiert `SqlParameterInfo` in `MySqlParameter`.
- Kein `SQLiteParameter` im MariaDB-Namespace.

### 5.4 Thumbnails und Vorschaudaten in derselben Datenbank

SQLite nutzt separate Thumbnail-Dateien pro Vorschaugröße. MariaDB speichert alles in der zentralen Datenbank:

- `THUMBS` erhält `PREVIEW_WIDTH` und `PREVIEW_HEIGHT`.
- Eindeutigkeit über `(VARIATIONS_FK, PREVIEW_WIDTH, PREVIEW_HEIGHT)`.
- `FACES` erhält ebenfalls `PREVIEW_WIDTH` und `PREVIEW_HEIGHT`.
- Der Standard-Thumbnail verwendet die konfigurierte `MediaBrowserContext.ThumbnailSize` als Größe.
- `GetThumbJpegData`, `SetThumbForMediaItem` und der Insert-Pfad verwenden die Standardgröße.
- `GetImagePreviewDB`, `WriteToPreviewDB`, `DeleteFromPreviewDB`, `IsImageInPreviewDB`, `GetPreviewDBVariationIdList`, `GetAllFaces` und `GetFaceDetectionPreviewDB` verwenden die übergebene `System.Drawing.Size`.

### 5.5 Trigger und Anwendungslogik

- Kind-Daten werden weiterhin über Trigger gelöscht, da Trigger andere Tabellen ändern dürfen.
- Selbstreferenzielle SQLite-Trigger werden in Anwendungslogik verlagert:
  - Löschen aller Zeilen mit gleichem Ordner und gleichem Dateinamen beim benutzerinitiierten Löschen.
  - Synchronisieren von Duplikaten bei Änderungen an `FILENAME`, `SORTORDER` oder `FOLDERS_FK`.
- Wartungsfunktionen wie `CleanDB` verwenden bewusst kein implizites Mitlöschen gleichnamiger Einträge, um Datenverlust zu vermeiden. Das abweichende Verhalten wird im Test dokumentiert.

### 5.6 Fehler- und Transaktionssemantik

- Pro `MBCommand` und `MBTransaction` wird eine eigene Verbindung geöffnet und beim Dispose geschlossen.
- MariaDB-Transaktionen verhalten sich wie SQLite-Transaktionen: Commit beim Dispose, Rollback nach Fehler oder explizitem Rollback.
- SQL-Fehler werden gespeichert, die Transaktion wird zurückgerollt und der Fehler beim Dispose erneut geworfen.
- `CommandTimeout` beträgt 90 Sekunden, wie bei SQLite.
- `DateTime.MinValue`, `DateTime` vor 1000 und `null` werden zu `DBNull` normalisiert.
- `ExecuteScalar<T>` konvertiert `long`, `int`, `string`, `byte[]`, `DateTime`, `bool`, `double` und `decimal` robust.

## 6. Startparameter und Startablauf

### 6.1 CLI-Syntax

Bisheriger Aufruf bleibt gültig:

```text
MediaBrowserWPF.exe <sqlite-db.mb4> [<init-folder>]
MediaBrowserWPF.exe <init-folder>
MediaBrowserWPF.exe
```

Neue Variante:

```text
MediaBrowserWPF.exe --provider mariadb \
  --db-host <host> --db-port <port> --db-name <database> \
  --db-user <user> \
  [--db-password <password> | --db-password-env <ENV_NAME> | --db-password-file <path>] \
  [--db-ssl-mode None|Preferred|Required|VerifyCA|VerifyFull] \
  [--update] \
  [<init-folder>]
```

Optional:

```text
MediaBrowserWPF.exe --provider sqlite --db-file <sqlite-db.mb4> [--update]
```

Regeln:

- Ohne `--provider` gilt das bisherige Verhalten.
- `--provider` akzeptiert `sqlite` und `mariadb` (case-insensitive).
- Bei `mariadb` sind `--db-host`, `--db-name`, `--db-user` Pflicht; Port default `3306`.
- Ohne Passwortoption wird `MEDIABROWSER_DB_PASSWORD` gelesen.
- Passwort nie loggen, nie in `Settings.Default` speichern, nie in Fehlermeldungen ausgeben.
- `--update` ist erforderlich, wenn die DB oder das Schema fehlt.
- Unbekannte oder unvollständige Argumente führen zu einer klaren Fehlermeldung und einem Exit ohne UI-Start.
- `--help` gibt die Syntax aus.

### 6.2 Startablauf

1. Argumente parsen, bevor `MediaBrowserContext.Init` aufgerufen wird.
2. Bei SQLite: bestehenden Init-Pfad verwenden.
3. Bei MariaDB:
   - Optionen validieren.
   - Provider erzeugen.
   - Bei `--update`: Datenbank bei Bedarf anlegen, dann `EnsureSchema()` idempotent ausführen.
   - Ohne `--update`: nur verbinden und `ValidateDB()` ausführen.
   - Bei fehlender DB oder fehlendem Schema: verständliche Meldung mit Hinweis auf `--update`.
   - `MainDBProvider` setzen, DB-Properties laden, Kontext initialisieren.
4. UI starten; `DBName` im Status und DB-Menü anzeigen.
5. Keine SQLite-Datei erzeugen, wenn MariaDB gewählt wurde.

### 6.3 UI-Anpassungen

- `App.xaml.cs`: Parser und Init-Auswahl ergänzen; DBTempFolder-Cleanup null-sicher lassen.
- `MainWindow.xaml.cs`:
  - Dateibasiertes DB-Öffnen im MariaDB-Modus deaktivieren oder mit Hinweis auf Neustart belegen.
  - `SetDatabaseMenu` mit dem MariaDB-DB-Namen aufrufen.
  - Keine `.mb4`-Dateipfad-Annahmen im MariaDB-Modus.
- `Settings.Default.DBPath` bleibt ausschließlich für SQLite relevant.
- `MediaBrowserLauncher\Program.cs`: Argumente mit korrekter Quotierung weitergeben; aktuelles `String.Join(" ", args)` verliert Quoting.

## 7. Datenbankschema

### 7.1 Allgemeine Festlegungen

- Engine: `InnoDB`.
- Zeichensatz: `utf8mb4`, Collation `utf8mb4_unicode_ci`.
- IDs: `INT NOT NULL AUTO_INCREMENT PRIMARY KEY`.
- Größen: `BIGINT`.
- Boolesche Werte: `TINYINT(1) NOT NULL DEFAULT 0/1`.
- Zeitwerte: `DATETIME(6)` statt `TIMESTAMP`, um den begrenzten `TIMESTAMP`-Bereich und Zero-Date-Probleme zu vermeiden.
- BLOBs: `LONGBLOB`.
- Alle Identifier in MariaDB-SQL mit Backticks quoten, insbesondere `KEY`, `ROLE`, `VALUE`, `TYPE`, `DATE`, `PATH`, `ACTION`, `EDIT`, `POSITION`, `LENGTH`.
- Keine `FOREIGN KEY`-Constraints im ersten Schritt; das Verhalten der SQLite-Trigger bleibt damit kontrollierbar.
- `DUAL` wird nicht als Tabelle angelegt; MariaDB hat `DUAL` bereits.
- `LAST_INSERT_ID()` ersetzt `last_insert_rowid()`.
- `INSERT IGNORE` ersetzt `INSERT OR IGNORE`; nach einem ignorierten Insert wird die vorhandene ID separat gelesen.

### 7.2 Tabellen

Folgende Tabellen werden 1:1 aus dem SQLite-Schema abgeleitet:

- `DBPROPERTIES`
- `ATTACHED`
- `ATTACHMENTS`
- `MEDIAFILES`
- `VARIATIONS`
- `DESCRIPTION`
- `LAYERS`
- `FOLDERS`
- `ROLES`
- `FAVORITES`
- `CATEGORY`
- `CATEGORIZE`
- `REMOTEMAPPING`
- `METADATA`
- `METADATANAME`
- `EXTRADATA`
- `EXTRADATAS`
- `USERDEFINEDREQUESTS`
- `DATALOGGER_METEOROLOGY`
- `DATALOGGER_GPS`
- `THUMBS`
- `FACES`

Wichtige Anpassungen:

- `DBPROPERTIES`: `KEY VARCHAR(50) NOT NULL UNIQUE`, `VALUE VARCHAR(500) NOT NULL`.
- `ATTACHMENTS`: `ID INT NOT NULL AUTO_INCREMENT`, `PATH VARCHAR(8000) NOT NULL`.
- `MEDIAFILES`: `LENGTH BIGINT`, `FILENAME VARCHAR(255)`, `SORTORDER VARCHAR(255)`, `MD5VALUE CHAR(32)`.
- `VARIATIONS`: `THUMB LONGBLOB NULL` immer vorsehen, auch wenn standardmäßig die separate `THUMBS`-Tabelle genutzt wird.
- `THUMBS`: `ID BIGINT AUTO_INCREMENT`, `VARIATIONS_FK INT NOT NULL`, `PREVIEW_WIDTH INT NOT NULL`, `PREVIEW_HEIGHT INT NOT NULL`, `ISOUTDATED TINYINT(1)`, `THUMB LONGBLOB NOT NULL`, `Extension VARCHAR(10)`, `WIDTH INT`, `HEIGHT INT`, `UNIQUE (VARIATIONS_FK, PREVIEW_WIDTH, PREVIEW_HEIGHT)`.
- `FACES`: `VARIATIONS_FK INT`, `PREVIEW_WIDTH INT`, `PREVIEW_HEIGHT INT`, `X/Y/WIDTH/HEIGHT INT`, `REL_SIZE DOUBLE`; Index auf `(VARIATIONS_FK, PREVIEW_WIDTH, PREVIEW_HEIGHT)` und `REL_SIZE`.
- `CATEGORY`: `FULLPATH VARCHAR(8000)`, Index als Prefix `FULLPATH(255)`.
- `DESCRIPTION`: `VALUE VARCHAR(8000)`, Index als Prefix `VALUE(255)`.
- `METADATA`: `VALUE VARCHAR(255)`.
- `EXTRADATA`: `DATA LONGBLOB`.
- `USERDEFINEDREQUESTS`: `REQUEST LONGBLOB NOT NULL`.
- `DATALOGGER_GPS`: `FILESIZE BIGINT`.
- `FAVORITES`: `ACCESS DATETIME(6) NOT NULL`.

Indizes aus dem SQLite-Schema werden übernommen. Wegen MariaDB-InnoDB-Indexlängen werden lange Textspalten nur als Prefix indexiert.

### 7.3 Seed-Daten

Idempotent anzulegen:

- `DBPROPERTIES` mit `DBVersion = UpdateDb.DbVersion` sowie den bestehenden Defaults:
  - `CheckSumMaxLength`, `CommitInsertSeconds`, `DefaultRoleID`, `ThumbnailJPEGQuality`, `ThumbnailSize`, `InitialDBTreePath`, `InitialCategoryTreePath`, `DiaryCategorizeFolder`, `TagFolder`, `MissingFileBehavior`, `AutoCategorizeDate`, `DefaultMediaArchivFolder`, `DefaultMediaTempFolder`.
- `ROLES`: `PUBLIC`, `PRIVATE`.
- `CATEGORY`: `Tagebuch`, `Orte`, `Verschlagwortung` mit stabilen, beim Insert erzeugten GUIDs.
- `GUID`, `HOST`, `USER`, `FILENAME` in `DBPROPERTIES` beim Anlegen setzen.

`DefaultMediaArchivFolder` wird nicht aus einem SQLite-Dateipfad abgeleitet. Es wird ein sinnvoller konfigurierbarer Wert verwendet, standardmäßig `MediaBrowserContext.DefaultMediaArchivRoot`.

### 7.4 Trigger-Matrix

| SQLite-Trigger | MariaDB-Umsetzung |
| --- | --- |
| `fkd_ATTACHMENTS_DELETE` | Trigger löscht `ATTACHED` zu `OLD.ID`. |
| `fkd_MEDIAFILES_DELETE` | Trigger löscht `VARIATIONS`, `EXTRADATAS`, `METADATA`, `ATTACHED` zu `OLD.ID`. Kein Selbst-Delete. |
| `fkd_MEDIAFILES_UPDATE` | Kein Trigger. App-Helfer synchronisiert Duplikate nach Änderungen an `FILENAME`/`SORTORDER`/`FOLDERS_FK`. |
| `fkd_DESCRIPTION_DELETE` | Trigger setzt `MEDIAFILES.DESCRIPTION_FK = NULL`. |
| `fkd_VARIATIONS_DELETE` | Ein Trigger löscht `LAYERS`, `CATEGORIZE`, `THUMBS`, `FACES` zu `OLD.ID`. |
| `fkd_FOLDERS_DELETE` | Trigger löscht `MEDIAFILES` zu `OLD.ID`; deren Trigger räumen Kinder auf. |
| `fkd_CATEGORIZE_DELETE` | Trigger löscht `CATEGORIZE` zu `OLD.ID`. |
| `fkd_CATEGORIZE_VARIATIONS_DELETE` | In `fkd_VARIATIONS_DELETE` integriert. |
| `fkd_METADATANAME_DELETE` | Trigger löscht `METADATA` zu `OLD.ID`. |
| `fkd_EXTRADATA_DELETE` | Trigger löscht `EXTRADATAS` zu `OLD.ID`. |
| `fkd_EXTRADATAS_MEDIAFILES_DELETE` | In `fkd_MEDIAFILES_DELETE` integriert. |
| `fkd_THUMBS_DELETE` | Trigger löscht `FACES` zu `OLD.VARIATIONS_FK` und passender Vorschaugröße. |

Beim `--update` werden Trigger mit `DROP TRIGGER IF EXISTS` und anschließendem `CREATE TRIGGER` neu aufgebaut. Fehlende Trigger werden erkannt; vorhandene werden nicht dupliziert.

### 7.5 Duplikat- und Löschlogik

Anwendungshelfer im MariaDB-Provider:

- `DeleteMediaItemsWithSamePath(long mediaFileId)`:
  - liest `FOLDERS_FK` und `FILENAME` der Zeile,
  - löscht alle `MEDIAFILES` mit gleichem Ordner und gleichem Dateinamen (case-insensitiv),
  - lässt Trigger die Kinddaten aufräumen,
  - löscht anschließend verwaiste `THUMBS`/`FACES` der betroffenen Variationen, falls Trigger dies nicht bereits erledigt haben.
- `SyncDuplicatePathAfterUpdate(long mediaFileId, long oldFolderId, string oldFileName, long newFolderId, string newFileName)`:
  - aktualisiert alle anderen Zeilen mit altem Ordner/Dateinamen auf die neuen Werte,
  - entspricht dem Verhalten von `fkd_MEDIAFILES_UPDATE`.
- Aufrufstellen:
  - `RenameMediaItem`: neuer Dateiname.
  - `CopyToFolder`: neuer Ordner.
  - `SQLiteInsert`-Move-Zweig: neuer Dateiname und Ordner.
  - `RemoveFromDB`, `RemoveAndRecycle`: Pfad-Duplikate mitlöschen.
  - `RemoveFolder`: alle Medien des Ordners werden ohnehin gelöscht.
  - `CleanDB`: kein implizites Pfad-Mitlöschen.

### 7.6 `--update`/`EnsureSchema`

Neue Klasse `MediaBrowser4Lib\DB\Maria\MariaDbAdministration.cs`:

- `EnsureDatabase()`: `CREATE DATABASE IF NOT EXISTS` mit `utf8mb4`/`utf8mb4_unicode_ci`.
- `EnsureSchema()`:
  - `CREATE TABLE IF NOT EXISTS` für alle Tabellen.
  - Fehlende Spalten über `INFORMATION_SCHEMA.COLUMNS` ergänzen, ohne Daten zu löschen.
  - Fehlende Indizes über `INFORMATION_SCHEMA.STATISTICS` ergänzen.
  - Trigger idempotent neu aufbauen.
  - Seed-Daten mit `INSERT IGNORE` bzw. `INSERT ... SELECT ... WHERE NOT EXISTS` anlegen.
  - `DBVersion` auf `UpdateDb.DbVersion` setzen.
- `ValidateSchema()`: prüft Pflicht-Tabellen und `DBVersion`; liefert klare Fehlermeldung.
- `DropDatabase()` nur als explizites Debug-/Testwerkzeug, nie automatisch aufrufen.

`MariaDbProvider.CreateDB()` ruft `EnsureDatabase()` und `EnsureSchema()` auf. `--update` ruft `CreateDB()` auf. Ein normaler Start ruft nur `ValidateDB()`/`ValidateSchema()` auf.

## 8. SQL-Dialekt-Mapping

| SQLite | MariaDB |
| --- | --- |
| `INTEGER PRIMARY KEY` | `INT NOT NULL AUTO_INCREMENT PRIMARY KEY` |
| `BOOLEAN` | `TINYINT(1)` |
| `TIMESTAMP` | `DATETIME(6)` |
| `BLOB` | `LONGBLOB` |
| `nvarchar(...)` | `VARCHAR(...)` |
| `SELECT last_insert_rowid() FROM DUAL` | `SELECT LAST_INSERT_ID()` |
| `INSERT OR IGNORE` | `INSERT IGNORE`, danach ID separat lesen |
| `ATTACH ... AS thmb` | keine ATTACHs; `THUMBS`/`FACES` in derselben DB |
| `RANDOM()` | `RAND()` |
| `a || b` | `CONCAT(a, b)` |
| `date(x)` | `DATE(x)` |
| `CAST(x AS float)` | `CAST(x AS DECIMAL(20,10))` oder Multiplikation mit `1.0` |
| `VACUUM` | kein `VACUUM`; optional `ANALYZE TABLE` |
| `[identifier]` | `` `identifier` `` |
| `CURRENT_TIMESTAMP` | `CURRENT_TIMESTAMP(6)` |
| `CREATE INDEX IF NOT EXISTS` | Existenzprüfung über `INFORMATION_SCHEMA.STATISTICS` |
| `LIKE` mit Backslash-Pfaden | Parameterisieren oder Backslashes korrekt escapen; MariaDB behandelt `\` in String-Literals als Escape |
| `GROUP BY` mit nicht aggregierten Spalten | Queries prüfen; MariaDB/`ONLY_FULL_GROUP_BY`-Kompatibilität sicherstellen |
| `TINYINT`-Werte | `Convert.ToBoolean` bzw. `TreatTinyAsBoolean=true` |
| `NUMERIC`-Lat/Long | `Convert.ToDouble` statt direktem Cast auf `double` |
| `LIMIT` | unverändert |

Zusätzlich:

- `LENGTH`, `KEY`, `ROLE`, `VALUE`, `TYPE`, `DATE`, `PATH` immer quoten.
- Keine mehrzeiligen SQL-Batches über `ExecuteNonQuery` senden; pro Statement ein Command.
- `MySqlConnector` mit `AllowUserVariables=false`, `ConvertZeroDateTime=true` und passendem `SslMode` konfigurieren.
- `DateTime.MinValue` und Jahre vor 1000 als `DBNull` übergeben.
- `LAST_INSERT_ID()` immer auf derselben Verbindung/Transaktion abfragen.

## 9. Provider-Implementierung

### 9.1 Infrastruktur

`MariaDbConnectionOptions`:

- `Host`, `Port`, `Database`, `User`, `Password`, `SslMode`, `ConnectionTimeout`.
- `BuildConnectionString()` über `MySqlConnectionStringBuilder`.
- Keine Defaults für Passwort oder Benutzer außer den CLI-Defaults.

`SimpleConnection`:

- Nur `DbConnection IConnectionManager.Connection` implementieren.
- `MySqlConnection` cachen.
- `Open()` mit Retry und klarer Fehlermeldung.
- `Dispose()` idempotent.
- `BeginTransaction()` und `GetCommandHelper()` als interne Helfer.

`CommandHelper`:

- `MySqlCommand` mit `CommandTimeout = 90`.
- Parameterverwaltung analog SQLite.
- `SetParameter` normalisiert `null`, `DateTime.MinValue` und Typen.
- `ExecuteScalar<T>` mit robustem Converter.
- `GetDataTable` über `MySqlDataAdapter`.
- `Dispose` schließt Command und Connection.
- `CreateCommand` bleibt internes Hilfsmittel, kein Interface-Member.

`Transaction`:

- Erbt nicht zwingend von `CommandHelper`; kann komponieren.
- Beginnt Transaktion auf derselben Verbindung.
- `ExecuteNonQuery`, `ExecuteReader`, `ExecuteScalar`, `GetDataTable`, Parameter-Methoden.
- Bei Fehler: Fehler merken, Rollback markieren, beim Dispose rollbacken und Fehler werfen.
- Commit beim Dispose, wenn kein Rollback markiert wurde.
- Kein `Commit()` im Interface nötig.

### 9.2 Methodengruppen

Die Implementierung erfolgt in dieser Reihenfolge. Die Gruppierung dient als Checkliste gegen `DBProvider`.

1. **Lebenszyklus und Properties**
   - `Guid`, `DBName`, `User`, `Host`, `ValidateDB`, `CreateDB`, `MBCommand`, `MBTransaction`, `Dispose`, Events `OnInsert`/`OnThumbUpdate`.

2. **Rohausführung und Properties**
   - `ExecuteNonQuery`, `SaveDBProperties`, `GetDBProperty`-Nutzung über `DBPROPERTIES`.

3. **Ordner, Rollen, Kategorien**
   - `GetFolderlist`, `SetFolder`, `RemoveFolder`, `CountItemsInFolder`.
   - `GetRoleList`, `SetRole`.
   - `GetCategoryTree`, `GetCategories`, `SetCategory`, `SetCategoryByPath`, `RemoveCategory`, `GetCategoriesFromMediaItems`, `GetCategoryIdentifiersFromMediaItem`.
   - `CategorizeMediaItems`, `UnCategorizeMediaItems`.

4. **Medien lesen und suchen**
   - `GetMediaItems`, `GetMediaItemsFromFolders`, `GetMediaItemsFromCategories`, `GetMediaItemsFromSearchToken`, `GetMediaItemsFromBookmarks`, `GetMediaItemsFromTrashfolder`, `GetMediaItemsFromFileName`, `GetMediaItemFromFile`, `GetMediaItemsFromChecksum`, `GetMediaItemsWithMissingFiles`, `GetMediaItemsWithMissingThumbs`, `GetMediaItemsForRequestTicket`.
   - `GetSqlRequest`, `CanFoundByChecksum`, `IsDublicate`, `GetDublicates` (beide Varianten), `UpdateDublicate`.
   - Sortierung inklusive `RAND()`.
   - `LoadMediaItems` aus SQLite portieren und MariaDB-Typen normalisieren.

5. **Variationen, Layer, Thumbnails, Faces**
   - `GetVariations`, `SetNewVariation`, `SetVariationDefault`, `EditVariation`, `ReplaceVariations`, `RemoveVariation` (beide Varianten).
   - `GetLayers`, `GetLayersForMediaItem`, `SetLayersForMediaItem`, `SetLayersForMediaItems`.
   - `GetThumbJpegData`, `SetThumbForMediaItem`, `GetThumbsForAllMediaItems`.
   - `GetImagePreviewDB`, `IsImageInPreviewDB`, `WriteToPreviewDB`, `DeleteFromPreviewDB`, `GetPreviewDBVariationIdList`.
   - `GetFaceDetectionPreviewDB`, `GetAllFaces`.
   - Vorschaugröße als Teil des Schlüssels.

6. **Metadaten, Beschreibungen, Attachments**
   - `GetMetaDataFromMediaItems`, `GetMetadataKeyList`.
   - `GetDescription` (beide Varianten), `SetDescription`.
   - `GetAttachment`, `SetAttachment`.
   - `CreateMediaItemFromXml`.
   - MetaData-Namenscache providerbezogen aufbauen und bei DB-Wechsel leeren.

7. **GPS und Meteorologie**
   - `DeleteGpsFile`, `InsertGpsPoints`, `GetGpsNearest`, `GetGpsFileList`, `GetGpsList`.
   - `InsertMeteorologyData`, `GetMeteorologyData`.

8. **Schreiben, Verschieben, Löschen, Warten**
   - `AddMediaItemAsync`, `UpdateMediaItem`, `RenameMediaItem`, `CopyToFolder`.
   - `Rotate90`, `AddViewTime`, `AdjustMediaDate`.
   - `SetBookmark`, `SetDeleted`, `SetTrashFolder`, `SetPriority`, `SetGeodata`.
   - `RemoveFromDB`, `RemoveAndRecycle`, `CleanDB`.
   - `GetMediaItemsGeoData`, `GetCategoriesDiaryGeoData`, `GetCategoriesLocationGeoData`, `LastAddedFolders`.

9. **Benutzerdefinierte Requests**
   - `GetUserDefinedRequests`, `DeleteUserDefinedRequest`, `SaveUserDefinedRequest`.
   - `GetSQLFromRequestTicket`, `SetSQLForRequestTicket`.
   - `BinaryFormatter`-Kompatibilität erhalten; Requests mit alten SQLite-Parametern müssen lesbar bleiben.

### 9.3 Insert-Pipeline

Neue Klasse `MediaBrowser4Lib\DB\Maria\MariaDbInsert.cs`, angelehnt an `SQLiteInsert`:

- Warteschlange und Threading aus `InsertItems` übernehmen.
- Eine Transaktion für Hauptdaten und Thumbnails verwenden, da keine separate Thumbnail-DB existiert.
- Ordner per `INSERT IGNORE` anlegen und vorhandene ID separat lesen.
- Dublettenprüfung und `HISTORYVERSION`-Logik portieren.
- `INSERT` in `MEDIAFILES`, danach `LAST_INSERT_ID()`.
- `VARIATIONS` anlegen, danach `LAST_INSERT_ID()`.
- Thumbnail je nach `ThumbnailSize` in `VARIATIONS.THUMB` oder `THUMBS` schreiben.
- `CURRENTVARIATION` setzen.
- Metadaten und `METADATANAME` idempotent anlegen.
- GPS-Koordinaten aus Metadaten aktualisieren.
- Fortschrittsereignisse `OnInsert` weiterreichen.
- Batch-Commit nach `CommitInsertSeconds`.
- Abbrechen über `MediaBrowserContext.abortInsert` berücksichtigen.
- `MediaBrowserContext.GlobalMediaItemCache` und Ordnerzähler wie bei SQLite aktualisieren.

### 9.4 Typ- und Datumskonvertierung

- `DateTime`-Werte aus MariaDB sind `DateTime`; `DBNull` wird je nach Kontext zu `DateTime.MinValue` oder `null`.
- `NUMERIC`/`DECIMAL` für Latitude/Longitude über `Convert.ToDouble`.
- `TINYINT(1)` als `bool` lesen; falls Treiber `sbyte`/`int` liefert, konvertieren.
- `COUNT(*)` als `long` behandeln.
- `LAST_INSERT_ID()` als `long` behandeln.
- BLOBs als `byte[]`.
- Leere Strings und `DBNull` wie im SQLite-Code behandeln.
- Keine Zero-Date-Werte schreiben.

## 10. Kontext- und UI-Anpassungen

`MediaBrowserContext`:

- Neue Überladung `Init(DBProvider provider, bool update)` oder `Init(MariaDbConnectionOptions options, bool update)`.
- Bestehendes `Init(string dbPath)` bleibt unverändert für SQLite.
- Gemeinsame Initialisierung nach Providerwahl extrahieren:
  - DB-Properties laden,
  - `CheckSumMaxLength`, `CommitInsertSeconds`, `DefaultRoleID`, `ThumbnailJPEGQuality`, `ThumbnailSize`, `AutoCategorizeDate` setzen.
- `DBPath`: `mainDBProvider.DBPath` statt Cast.
- `DBPreviewPath`: für MariaDB `null` oder lokaler Cachepfad, nie SQLite-Thumbnailpfad.
- `DBTempFolder`: für MariaDB ein lokales Temp-Verzeichnis unter `%TEMP%\MediaBrowserWpf\<db-name>` anlegen; für SQLite unverändert.
- `WorkingDirectory`: bei fehlendem DBPath auf `MyDocumentsFolder` zurückfallen.
- `DriveMappingLetter` bei MariaDB auf `null` lassen.
- `XmlMetadata.Export`: `DBPath` null-sicher behandeln; bei MariaDB DB-Namen statt Pfad schreiben.
- `MainDBProvider`-Wechsel: Caches (MetaDataNames, GlobalMediaItemCache, Folder-/Category-Singletons) leeren.

`MainWindow`:

- DB-Menü und Status zeigen `DBName`.
- Dateibasiertes Öffnen im MariaDB-Modus sperren.
- Keine `.mb4`-Datei-Filter im MariaDB-Modus erzwingen.
- Fehlermeldung bei fehlender DB/Schema verständlich anzeigen.

`MediaBrowserWPF.csproj`/`app.config`:

- `MySqlConnector` und benötigte Laufzeitabhängigkeiten direkt referenzieren, damit sie nach `MediaBrowserWPF\bin\Debug` kopiert werden.
- Binding Redirects für `MySqlConnector`-Abhängigkeiten prüfen/ergänzen.
- Nach dem Build prüfen: `MySqlConnector.dll`, `Microsoft.Extensions.Logging.Abstractions.dll`, `System.Diagnostics.DiagnosticSource.dll` und weitere Abhängigkeiten liegen im WPF-Ausgabeverzeichnis.

## 11. Arbeitspakete

**Commit-Regel:** Nach jeder abgeschlossenen Phase (inkl. erfüllter Akzeptanzkriterien und grünem Build) wird ein Git-Commit erstellt. Commit-Nachricht im Stil `phase <n>: <kurze Beschreibung>`. Kein Commit bei unvollständiger oder roter Phase.

### Phase 0 – Voraussetzungen

- MariaDB-Erreichbarkeit klären: Host, Port, Benutzer, Datenbank, Rechte.
- Test-Datenbank festlegen, z. B. `mediabrowser-test`.
- Entscheidung zu Passwortübergabe und SSL.
- Keine Codeänderung.

Akzeptanz: Verbindungstest erfolgreich, Test-DB verfügbar.

### Phase 1 – Infrastruktur und buildfähige Provider-Oberfläche

- `MariaDbConnectionOptions` anlegen.
- `SimpleConnection`, `CommandHelper`, `Transaction` korrigieren und implementieren.
- Ungültige `override`-Member in `MariaDbProvider` entfernen.
- Alle abstrakten Member als vollständige Methodenoberfläche anlegen; nicht fertige Member temporär klar markieren.
- Neutrale Parameterklasse einführen und SQLite/MariaDB-Adapter vorbereiten.
- Build ohne Ausschlüsse grün bekommen.

Akzeptanz: Solution baut; MariaDB-Provider ist instanziierbar; kein SQLite-Typ im MariaDB-Namespace.

### Phase 2 – Schema und `--update`

- `MariaDbAdministration` mit `EnsureDatabase`, `EnsureSchema`, `ValidateSchema`.
- Tabellen, Indizes, Trigger, Seed-Daten, DB-Version.
- `CreateDB`/`ValidateDB` im Provider.
- `--update`-Pfad im Startablauf.
- Idempotenztest: zweimal `--update` hintereinander.

Akzeptanz: Leere DB wird vollständig angelegt; zweiter Lauf ändert nichts und löscht keine Daten; fehlende Objekte werden ergänzt.

### Phase 3 – Lesen und Suchen

- Ordner, Rollen, Kategorien.
- Medien laden, Filter, Sortierung, Suche.
- Dubletten- und Checksum-Abfragen.
- `LoadMediaItems`-Port.

Akzeptanz: Hauptfenster zeigt eine leere MariaDB korrekt; nach Testdatenimport sind Listen, Suche und Kategorien korrekt.

### Phase 4 – Thumbnails und Variationen

- Variationen, Layer.
- Standard-Thumbnails und Preview-Größen.
- Faces.
- Caches und Events.

Akzeptanz: Thumbnails werden angezeigt und geschrieben; mehrere Preview-Größen überschreiben sich nicht; Löschen räumt `THUMBS`/`FACES` auf.

### Phase 5 – Schreibpfade

- `MariaDbInsert`.
- Update, Rename, Copy/Move.
- Delete, Recycle, RemoveFolder, RemoveFromDB, CleanDB.
- Duplikatssynchronisation und Pfad-Löschlogik.
- Ordnerzähler und Caches.

Akzeptanz: Medien können angelegt, verschoben, umbenannt, gelöscht und recycelt werden; keine verwaisten Kinddatensätze; SQLite-Verhalten wird fachlich nachgebildet.

### Phase 6 – Restliche Fachfunktionen

- Metadaten, Beschreibungen, Attachments.
- GPS, Meteorologie.
- Benutzerdefinierte Requests, SQL-Tickets.
- Export/Import.
- Geodaten und letzte Ordner.

Akzeptanz: Alle verbleibenden `DBProvider`-Member sind implementiert und in Integrations-Smoke-Tests erreichbar.

### Phase 7 – Kontext, UI und Deployment

- `MediaBrowserContext`-Init und Pfade.
- CLI-Parser und Fehlermeldungen.
- UI-Anpassungen.
- Launcher-Quoting.
- MySqlConnector-Laufzeitabhängigkeiten.

Akzeptanz: Start über CLI in beiden Modi; SQLite unverändert; MariaDB-Name in der UI; keine fehlenden Assemblies zur Laufzeit.

### Phase 8 – Verifikation

- Vollständiger Rebuild.
- SQLite-Regression.
- MariaDB-Integrationsmatrix.
- `--update`-Idempotenz.
- Fehlerfälle: DB down, falsches Passwort, fehlende DB, fehlende Rechte.
- Suche nach verbotenen SQLite-Konstrukten im MariaDB-Code.
- Abschließender Code-Review der Trigger- und Löschlogik.

Akzeptanz: Definition of Done aus Abschnitt 2 vollständig erfüllt.

## 12. Testplan

### 12.1 Build

```powershell
& "$env:TEMP\opencode\nuget.exe" restore MediaBrowser4.sln
& "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" `
  MediaBrowser4.sln /t:Rebuild /p:Configuration=Debug /p:Platform="Any CPU" /m
```

Erwartung: 0 Errors, keine temporären Ausschlüsse.

### 12.2 Verbindungs-Preflight

```powershell
Test-NetConnection <host> -Port <port>
```

Zusätzlich ein minimaler Verbindungstest über den Provider oder `MySqlConnector`.

### 12.3 Schema-Tests

- `--update` auf leerer DB.
- `--update` ein zweites Mal.
- `--update` nach manuellem Löschen eines Index/Triggers.
- Prüfen: alle Tabellen, Indizes, Trigger, Seed-Rollen, Seed-Kategorien, `DBVersion`.
- Prüfen: keine Datenänderung bei wiederholtem Lauf.

### 12.4 Funktionale Smoke-Tests

- Start ohne `--update` bei fehlender DB: klare Fehlermeldung.
- Start mit `--update` und leerer DB.
- Medienordner einlesen.
- Medienliste, Suche, Sortierung, Kategorien, Rollen.
- Thumbnail-Erzeugung und Anzeige.
- Mehrere Preview-Größen.
- Beschreibung, Metadaten, Attachments.
- Variationen, Layer, Gesichter.
- Umbenennen, Verschieben, Kopieren.
- Löschen, Papierkorb, Recyceln.
- Benutzerdefinierte Requests speichern/laden.
- GPS- und Meteorologiedaten.
- Export/Import.
- `CleanDB`.

### 12.5 Datenintegrität

- Keine verwaisten `VARIATIONS`, `THUMBS`, `FACES`, `METADATA`, `EXTRADATAS`, `ATTACHED`.
- `CURRENTVARIATION` zeigt auf eine vorhandene Variation.
- `DESCRIPTION_FK` zeigt auf vorhandene Beschreibung oder `NULL`.
- `CATEGORIZE`-Paare eindeutig.
- `THUMBS` pro Variation und Größe eindeutig.
- Ordnerzähler stimmen mit `COUNT(MEDIAFILES)` überein.

### 12.6 SQLite-Regression

- Bestehende `.mb4`-Datei öffnen.
- Medien anzeigen, suchen, bearbeiten, löschen.
- Sicherstellen, dass keine MariaDB-Abhängigkeit im SQLite-Pfad benötigt wird.
- `Settings.Default.DBPath` weiterhin funktionsfähig.

### 12.7 Fehlerfälle

- MariaDB nicht erreichbar.
- Falsches Passwort.
- Datenbank existiert nicht und `--update` fehlt.
- Benutzer hat keine `CREATE`-/`TRIGGER`-Rechte.
- Schema teilweise vorhanden.
- Verbindungsabbruch während einer Transaktion.

## 13. Risiken und Gegenmaßnahmen

| Risiko | Gegenmaßnahme |
| --- | --- |
| MariaDB vom Host nicht erreichbar | Phase 0 verbindlich; Test-DB und Port klären; keine Implementierung blind als „fertig“ markieren. |
| Sehr große Provider-Oberfläche | Phasenweise Implementierung; Compiler als Checkliste; temporäre Stubs klar verfolgen und am Ende per Suche eliminieren. |
| Unterschiedliche Trigger-Semantik | Kind-Trigger beibehalten, Selbstreferenzen in App-Helfer verlagern, gezielte Lösch-/Rename-Tests. |
| Backslash-Escaping in Pfaden/LIKE | Pfadwerte parametrisieren; `LIKE`-Muster mit definiertem `ESCAPE` behandeln; Tests mit Windows-Pfaden. |
| Typunterschiede (bool, decimal, DateTime) | Zentraler Converter in `CommandHelper`/`Transaction`; Tests mit echten MariaDB-Daten. |
| Indexlängen bei `utf8mb4` | Prefix-Indizes für lange Textspalten; Schema-Test auf MariaDB. |
| Reservierte Wörter | Alle Identifier backtick-quoten. |
| Fehlende Laufzeitassemblies | Direkte Referenzen im WPF-Projekt; Output-Verzeichnis nach Build prüfen. |
| BinaryFormatter-Kompatibilität | Serialisiertes `TupelParameterList`-Layout erhalten; Test mit altem Request-Blob. |
| Datenverlust durch CleanDB/Trigger | Wartungslogik ohne implizites Pfad-Mitlöschen; Backups/Test-DB. |
| Connection-Pooling/Transaktionen | Kurzlebige Verbindungen pro Command/Transaction; Dispose zuverlässig; keine offenen Reader. |
| `ONLY_FULL_GROUP_BY` | Betroffene Queries prüfen und aggregieren; Server-SQL-Mode im Test dokumentieren. |
| Keine Datenmigration | Explizit außerhalb des Scopes; bei Bedarf separater Importplan. |

## 14. Zu ändernde und neue Dateien

### MediaBrowser4Lib

- `DB\DBProvider.cs`: virtuelle `DBPath`-Eigenschaft.
- `DB\SqlParameterInfo.cs` (neu): providerneutrale Parameter.
- `DB\Maria\MariaDbProvider.cs`: vollständige Provider-Implementierung.
- `DB\Maria\MariaDbConnectionOptions.cs` (neu): Verbindungsoptionen.
- `DB\Maria\MariaDbAdministration.cs` (neu): Schema, Update, Seeds.
- `DB\Maria\MariaDbInsert.cs` (neu): Insert-Pipeline.
- `DB\Maria\CommandHelper.cs`: Implementierung.
- `DB\Maria\SimpleConnection.cs`: korrekte Interface-Implementierung.
- `DB\Maria\Transaction.cs`: Implementierung.
- `DB\SQLite\SQLiteProvider.cs`: `DBPath`-Override, neutrale Parameter, ggf. kleine Anpassungen.
- `DB\SQLite\SearchTokenSql.cs`: neutrale Parameter.
- `DB\SQLite\SQLiteInsert.cs`: neutrale Parameter, falls erforderlich.
- `DB\SQLite\CommandHelper.cs`: Adapter für neutrale Parameter.
- `DB\SQLite\Transaction.cs`: Adapter für neutrale Parameter.
- `Objects\MediaItemSqlRequest.cs`: neutrale Parametersicht, Serialisierungsformat erhalten.
- `MediaBrowserContext.cs`: Init-Überladungen, Pfade, Caches, DB-Wechsel.
- `Utilities\XmlMetadata.cs`: `DBPath` null-sicher.
- `MediaBrowser4Lib.csproj`: neue Dateien aufnehmen.
- `app.config`: ggf. Binding Redirects für neue Laufzeitabhängigkeiten.

### MediaBrowserWPF

- `App.xaml.cs`: CLI-Parser, MariaDB-Init, Fehlerbehandlung.
- `MainWindow.xaml.cs`: DB-Menü/Status, Dateiöffnen im MariaDB-Modus.
- `Properties\Settings.settings`/`Settings.Designer.cs`: nur falls neue persistente Einstellungen nötig; Passwort niemals speichern.
- `MediaBrowserWPF.csproj`: direkte MySqlConnector-Referenzen und Laufzeitabhängigkeiten.
- `app.config`: Binding Redirects.

### MediaBrowserLauncher

- `Program.cs`: Argumente korrekt quotieren.

## 15. Beispielaufrufe

```powershell
# SQLite unverändert
.\MediaBrowserWPF.exe "D:\Fotos\DB\mediafiles.mb4"

# MariaDB initialisieren
.\MediaBrowserWPF.exe --provider mariadb `
  --db-host 192.168.1.10 --db-port 3306 `
  --db-name mediabrowser-new --db-user mediabrowser-new `
  --db-password-env MEDIABROWSER_DB_PASSWORD `
  --update

# MariaDB normal starten
.\MediaBrowserWPF.exe --provider mariadb `
  --db-host 192.168.1.10 --db-port 3306 `
  --db-name mediabrowser-new --db-user mediabrowser-new `
  --db-password-env MEDIABROWSER_DB_PASSWORD
```

## 16. Offene Entscheidungen vor Phase 1

1. Welcher Host/Port ist vom Entwicklungsrechner erreichbar?
2. Darf `mediabrowser-new` für Tests verwendet werden, oder soll eine separate Test-DB angelegt werden?
3. Soll das Passwort ausschließlich über Umgebungsvariable/Datei kommen?
4. Wird TLS benötigt?
5. Ist eine Datenmigration aus einer bestehenden SQLite-DB erforderlich?
6. Sollen Vorschaudaten in mehreren Größen zwingend unterstützt werden?
