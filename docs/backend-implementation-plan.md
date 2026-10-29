# Spring-Boot-Backend für den MediaBrowser-API-Provider

Dieses Dokument beschreibt die geplante Umsetzung des Spring-Boot-Backends für den in
`docs/openapi.yaml` definierten API-Vertrag. Das Backend ist nicht Teil dieses Repositories.

## 1. Ziel

Das Backend stellt den Medienkatalog über HTTP bereit, sodass MediaBrowserWpf mit
`--provider api` gegen eine zentrale Instanz arbeiten kann. Die MariaDB bleibt ausschließlich
serverseitig konfiguriert. Der Client sendet keine Datenbank-Zugangsdaten an die API.

Verbindliche Einschränkung:

- Die API führt **keine Schreiboperationen auf dem Medien-Dateisystem** aus.
- Es gibt keine Upload-, Copy-, Move-, Rename-, Delete- oder Recycle-Endpunkte für Original-,
  Sidecar- oder Preview-Dateien.
- Originalmedien werden nur gelesen, wenn sie bereits in einem read-only Content Store liegen.
- Datenbank-Schreiboperationen bleiben erlaubt.
- Das Befüllen des Content Stores erfolgt außerhalb der MediaBrowser-API durch einen separaten
  Ingest-/Sync-Prozess.

## 2. Technologievorschlag

- Java 21
- Spring Boot 3.x
- Spring Web MVC
- Spring Security
- Spring JDBC oder Spring Data JDBC, nicht zwingend JPA
- MariaDB
- Flyway für Schema-Migrationen
- Jackson
- springdoc-openapi für generierte API-Dokumentation
- JUnit 5, Mockito, Testcontainers für Integrationstests
- Maven oder Gradle

Für das bestehende, historisch gewachsene Tabellenschema ist Spring JDBC meist robuster als JPA,
weil Trigger, zusammengesetzte Schlüssel und BLOB-/TEXT-Spalten exakt abgebildet werden können.

## 3. Paketstruktur

```text
de.mediabrowser.api
  MediaBrowserApiApplication
  config
    SecurityConfig
    OpenApiConfig
    JacksonConfig
    WebConfig
  auth
    AuthController
    AuthService
    TokenService
    RefreshTokenRepository
    JwtProperties
  common
    ApiExceptionHandler
    ApiError
    PageRequestDto
  catalog
    CatalogController
    CatalogService
    CatalogPropertiesRepository
  folder
    FolderController
    FolderService
    FolderRepository
  category
    CategoryController
    CategoryService
    CategoryRepository
  media
    MediaFileController
    MediaFileService
    MediaFileRepository
    MediaQueryService
    DuplicateService
  imageanalysis
    ImageAnalysisController
    ImageAnalysisService
    ImageAnalysisRepository
    ImageObjectRepository
    ImageReadableTextRepository
  variation
    VariationController
    VariationService
    LayerRepository
  thumbnail
    ThumbnailController
    ThumbnailService
  preview
    PreviewController
    FaceController
  metadata
    MetadataController
    MetadataService
  description
    DescriptionController
    DescriptionService
  attachment
    AttachmentController
    AttachmentService
  role
    RoleController
    RoleService
  gps
    GpsController
    GpsService
    MeteorologyService
  request
    UserRequestController
    UserRequestService
  maintenance
    MaintenanceController
    MaintenanceService
```

## 4. Authentifizierung und Token-Lebenszyklus

### 4.1 Login

`POST /api/v1/auth/login`

Request:

```json
{
  "username": "benutzer",
  "password": "geheim",
  "tenant": "mediabrowser-new"
}
```

Response:

```json
{
  "accessToken": "...",
  "refreshToken": "...",
  "tokenType": "Bearer",
  "expiresIn": 600
}
```

Ablauf:

1. Benutzer anhand `username` und optional `tenant` laden.
2. Passwort mit BCrypt oder Argon2 prüfen.
3. Access-Token als JWT signieren.
4. Refresh-Token als zufälligen, langen Wert erzeugen.
5. Nur einen Hash des Refresh-Tokens in MariaDB speichern.
6. Refresh-Token mit Benutzer, Tenant, Ablaufzeit und Status speichern.

### 4.2 Access-Token

- Laufzeit: 10 Minuten.
- Enthält `sub`, `tenant`, Rollen und `jti`.
- Wird als `Authorization: Bearer <token>` gesendet.
- Nach Ablauf oder nach einem serverseitigen `401` wird der Client den Refresh-Endpunkt aufrufen.

### 4.3 Refresh

`POST /api/v1/auth/refresh`

- Refresh-Token gegen den gespeicherten Hash prüfen.
- Abgelaufene oder widerrufene Tokens ablehnen.
- Neuen Access-Token erzeugen.
- Refresh-Token rotieren und den alten Token ungültig machen.
- Bei ungültigem Refresh-Token: `401`, damit der Client sich neu anmeldet.

### 4.4 Logout

`POST /api/v1/auth/logout`

- Refresh-Token widerrufen.
- Access-Token kann zusätzlich über eine Denylist oder kurze Laufzeit ungültig werden.

### 4.5 Sicherheit

- Passwörter niemals loggen.
- Refresh-Tokens niemals im Klartext speichern.
- Token-Signaturschlüssel über Umgebungsvariablen oder Secret Store konfigurieren.
- HTTPS im Produktivbetrieb erzwingen.
- Keine MariaDB-Zugangsdaten im API-Request oder Token.
- Mandantenzuordnung in jedem autorisierten Request prüfen.
- `sql-query`- und `sql-request`-Endpunkte nur für Administration oder ein separates internes
  Service-Konto freigeben. Arbiträre SQL-Ausführung darf nicht öffentlich erreichbar sein.

## 5. Datenhaltung

Das Backend verwendet die bestehende MariaDB-Struktur aus
`MediaBrowser4Lib\DB\Maria\MariaDbAdministration.cs`.

Wichtige Regeln:

- `utf8mb4`
- keine harten Fremdschlüssel; Pflege über Service-Transaktionen und ggf. Trigger
- Thumbnails liegen in `THUMBS`
- Haupt-Thumbnail: `PREVIEW_WIDTH=0`, `PREVIEW_HEIGHT=0`
- `VARIATIONS.THUMB` wird nicht für die normale Thumbnail-Anzeige verwendet
- Katalogweite Einstellungen liegen in `DBPROPERTIES`
- Binärdaten als `LONGBLOB`, große Texte als `TEXT`/`LONGTEXT`
- IDs und AUTO_INCREMENT-Werte aus dem bestehenden Schema übernehmen

### 5.1 Image-Analyse (KI/OCR)

Die Analyse-Tabellen wurden aus der alten Datenbank `mediabrowser` nach `mediabrowser-new`
übernommen. Das einmalige Importskript liegt unter
`scripts/Migrate-ImageAnalysisToMariaDb.ps1`.

Tabellen und Beziehungen:

- `IMAGE_ANALYSIS`: 1:1 zu `MEDIAFILES` über `MEDIA_ID` (UNIQUE, FK, `ON DELETE CASCADE`).
- `IMAGE_OBJECTS`: 1:n zu `IMAGE_ANALYSIS` über `IMAGE_ANALYSIS_ID` (FK, `ON DELETE CASCADE`).
- `IMAGE_READABLE_TEXT`: 1:n zu `IMAGE_ANALYSIS` über `IMAGE_ANALYSIS_ID` (FK, `ON DELETE CASCADE`).

Wichtige Regeln:

- `IMAGE_ANALYSIS.OBJECT_COUNT`, `ANIMAL_COUNT` und `PERSON_COUNT` sind KI-Zähler und werden nicht
  aus den Kindzeilen abgeleitet. Die Service-Schicht darf sie nicht stillschweigend neu berechnen.
- Volltextindizes: `FT_DETAILED_IMAGE_ANALYSIS` auf `DETAILED_IMAGE_ANALYSIS` und
  `FT_IMAGE_READABLE_TEXT` auf `TEXT_LINE`.
- `IMAGE_OBJECTS.OBJECT_NAME` nutzt einen B-Tree-Präfixindex. Eine Suche nach exakten Objektnamen
  erfolgt über `OBJECT_NAME = ?`; für unscharfe Suche kann später ein eigener Volltextindex ergänzt
  werden.
- Schreiboperationen auf diese Tabellen sind reine Datenbank-Schreiboperationen und damit erlaubt.
  Die API darf dabei keine Dateien anlegen, ändern oder löschen.
- Die Tabellen gehören zum Katalog des Mandanten. Der Tenant wird über die Datenquelle des
  authentifizierten Requests bestimmt, nicht über einen Client-Parameter.
- Die Flyway-Baseline muss die drei Tabellen und ihre Indizes enthalten. Der einmalige Import aus
  der Alt-Datenbank ist nicht Teil der Flyway-Historie.

Flyway sollte mit einer Baseline des bestehenden Schemas starten. Spätere Änderungen werden als
versionierte Migrationen ergänzt.

## 6. Endpunktgruppen

### 6.1 Catalog

- `GET /catalog/validate`
- `POST /catalog/create`
- `GET /catalog/properties`
- `PUT /catalog/properties`
- `GET /catalog/last-folders`

Der Tenant wird aus dem Token gelesen. Die Datenbankverbindung selbst bleibt serverseitig.

### 6.2 Folders

- `GET /folders`
- `POST /folders/ensure`
- `DELETE /folders/{id}`
- `GET /folders/{id}/count`

`ensure` ist idempotent und liefert die vorhandene oder neue Ordner-ID.

### 6.3 Categories

- `GET /categories/tree`
- `GET /categories?variationId=...`
- `POST /categories`
- `POST /categories/by-path`
- `DELETE /categories/{id}`
- `GET /categories/geo`
- `POST /categories/from-media`
- `GET /categories/identifiers/{variationId}`

Die Kategoriepfade und `GUID`-Werte müssen mit dem bestehenden Schema kompatibel bleiben.

### 6.4 MediaFiles

- `POST /mediafiles/query`
- `POST /mediafiles/sql-query`
- `GET /mediafiles/{id}`
- `PUT /mediafiles/{id}`
- `POST /mediafiles/actions`
- `POST /mediafiles/thumbs`
- `POST /mediafiles/insert`
- `POST /mediafiles/from-xml`
- `GET /mediafiles/sql-request`
- `GET /mediafiles/checksum-count`

Der `MediaQuery`-Service bildet die Modi aus `ApiMediaQueryDto` auf Repository-Abfragen ab:

- `SEARCH`
- `FOLDERS`
- `CATEGORIES`
- `MISSING_CATEGORIES`
- `MISSING_FILES`
- `MISSING_THUMBS`
- `FILES_FROM_LIST`
- `FILES_FROM_CHECKSUM`
- `SORT`
- `DUPLICATES`
- `GEO`
- `VIRTUAL`
- `TRASH`
- `BOOKMARKS`
- `CHECKSUM`
- `FILENAME`
- `FILE_PATH`
- `REQUEST`

### 6.5 Variations und Layers

- `GET /mediafiles/{id}/variations`
- `POST /mediafiles/{id}/variations`
- `DELETE /mediafiles/{id}/variations?name=...`
- `POST /mediafiles/{id}/variations/replace`
- `PUT /variations/{id}`
- `DELETE /variations/{id}`
- `POST /variations/{id}/default`
- `GET /variations/{id}/layers`
- `PUT /variations/{id}/layers`
- `GET /variations/{id}/thumb`
- `PUT /variations/{id}/thumb`

### 6.6 Previews, Faces, Metadata, Descriptions, Attachments

Diese Gruppen bilden die bestehenden Provider-Methoden ab. Thumbnails werden als Binärdaten
gestreamt oder als Base64 in DTOs übertragen. Metadaten und Beschreibungen werden immer
tenant- und medienbezogen validiert.

Anhänge und Sidecars werden nur gelesen. Upload, Änderung und Löschen von Anhängen oder
Sidecar-Dateien sind nicht Teil der API.

### 6.7 Originalcontent und Capabilities

Read-only Endpunkte:

- `HEAD /mediafiles/{id}/content`
- `GET /mediafiles/{id}/content`
- `GET /variations/{id}/thumb`
- `GET /previews/{variationId}`
- `GET /faces/previews/{variationId}`
- `GET /attachments/{id}/content`
- `GET /mediafiles/{id}/sidecars`
- `GET /catalog/capabilities`

Content-Anforderungen:

- `Content-Type`, `Content-Length`
- `Range`-Requests
- `ETag`/`Last-Modified`
- Pfadnormalisierung und Schutz vor Directory Traversal
- Content Root nur read-only öffnen
- keine temporären Dateien auf dem Server
- `404`/`409`, wenn kein Content Store konfiguriert ist

Capabilities melden mindestens:

```json
{
  "canReadContent": true,
  "canStreamRange": true,
  "canReadAttachments": true,
  "canReadSidecars": true,
  "canWriteCatalog": true,
  "canUploadContent": false,
  "canModifyFiles": false,
  "contentStoreConfigured": true
}
```

### 6.8 GPS, Meteorologie, Requests, Duplicates, Maintenance

- GPS-Dateien und GPS-Punkte
- Meteorologiedaten
- benutzerdefinierte Suchanfragen
- Duplikaterkennung
- Katalogbereinigung

Die Bereinigung darf nur Datenbankzeilen ändern. Sie darf keine Dateien löschen und nur mit
expliziter Berechtigung ausgeführt werden.

### 6.9 Image-Analyse (KI/OCR)

Endpunkte:

- `GET /mediafiles/{id}/image-analysis`
- `PUT /mediafiles/{id}/image-analysis`
- `DELETE /mediafiles/{id}/image-analysis`
- `POST /image-analysis/query`

Verhalten:

- `GET` liefert die Analyse inklusive `objects` und `readableText`. Existiert keine Analyse, wird
  `404` geliefert.
- `PUT` ist ein Upsert: Es legt die Analyse an oder ersetzt sie für das angegebene Medium. Die
  Kindzeilen in `IMAGE_OBJECTS` und `IMAGE_READABLE_TEXT` werden vollständig ersetzt. Das Medium
  muss existieren, sonst `404`.
- `DELETE` entfernt die Analyse. Die Kindzeilen werden über die Fremdschlüssel kaskadiert oder
  explizit in derselben Transaktion gelöscht.
- `POST /image-analysis/query` filtert nach Medien-IDs, Modell, Zählerbereichen,
  Temperaturbereichen, Erstellungsdatum, exaktem Objektnamen, Volltext und dem Vorhandensein von
  Objekten bzw. lesbarem Text. Die Ergebnisse werden mit `offset` und `limit` paginiert; `limit`
  wird serverseitig auf 500 begrenzt.
- Sortierung: `CREATED_AT_DESC`, `CREATED_AT_ASC`, `MEDIA_DATE_DESC`, `MEDIA_DATE_ASC`, `ID_DESC`,
  `ID_ASC`. Standard ist `CREATED_AT_DESC`.

Volltextsuche:

- Der Suchtext wird ausschließlich über `MATCH ... AGAINST` auf
  `IMAGE_ANALYSIS.DETAILED_IMAGE_ANALYSIS` und `IMAGE_READABLE_TEXT.TEXT_LINE` ausgeführt.
- Leere oder zu kurze Suchbegriffe werden verworfen. Es werden keine Suchbegriffe in dynamisches
  SQL interpoliert.
- Die Treffer aus beiden Tabellen werden mit den übrigen Filtern kombiniert. Bei mehreren
  Suchbegriffen entscheidet der Service über `OR` innerhalb der Volltextbedingungen und `AND`
  zwischen den Filtergruppen.

Berechtigungen:

- Lesen: authentifizierter Benutzer mit Lesezugriff auf den Mandanten.
- Schreiben/Löschen: Rolle `CATALOG_WRITE` oder eine eigene Berechtigung
  `IMAGE_ANALYSIS_WRITE`. Die Entscheidung muss zentral im `ImageAnalysisService` bzw. in
  Spring Security erfolgen.
- Alle Zugriffe bleiben auf die Datenquelle des Mandanten beschränkt.

## 7. DTO- und Fehlerbehandlung

- DTOs entsprechen den Schemas in `docs/openapi.yaml`.
- Jackson verwendet camelCase und ISO-8601-Datumsangaben.
- `ApiExceptionHandler` liefert ein einheitliches Fehlerobjekt:

```json
{
  "code": "VALIDATION_ERROR",
  "message": "Ungültige Anfrage",
  "details": []
}
```

- HTTP-Status:
  - `400` ungültige Eingabe
  - `401` fehlende oder ungültige Authentifizierung
  - `403` fehlende Berechtigung oder falscher Tenant
  - `404` Ressource nicht gefunden
  - `409` Konflikt, z. B. doppelter Schlüssel
  - `413` zu große Anfrage
  - `500` unerwarteter Serverfehler

## 8. Transaktionen und Konsistenz

- Jeder schreibende Endpunkt läuft in einer eigenen Transaktion.
- `POST /mediafiles/insert` ist eine Transaktion über Ordner, Medium, Variation und Thumbnail.
- `POST /mediafiles/actions` verarbeitet eine Aktionsliste atomar, sofern fachlich möglich.
- Duplikaterkennung und anschließende Flags werden innerhalb einer Transaktion gesetzt.
- `PUT /mediafiles/{id}/image-analysis` prüft das Medium, upsertet `IMAGE_ANALYSIS` und ersetzt
  `IMAGE_OBJECTS` und `IMAGE_READABLE_TEXT` in einer Transaktion.
- `DELETE /mediafiles/{id}/image-analysis` löscht Analyse und Kindzeilen in einer Transaktion.
- Bei Massenimporten werden Batch-Grenzen und Fehler pro Batch protokolliert.

## 9. Performance

- Indizes aus dem bestehenden MariaDB-Schema übernehmen.
- Media-Abfragen paginieren und `Limit` serverseitig begrenzen.
- Thumbnails nur bei Bedarf laden; nicht in jeder Listenabfrage einbetten.
- Bulk-Inserts für große Imports verwenden.
- `max_allowed_packet` des MariaDB-Servers dokumentieren und im Deployment ausreichend dimensionieren.
  Der aktuelle Server kann mit 1 MB konfiguriert sein; Kindzeilen der Image-Analyse daher in
  Batches schreiben.
- Große DTOs mit Base64-Thumbnails vermeiden, wo Streaming möglich ist.
- Die Image-Analyse-Suche nutzt die vorhandenen B-Tree- und Volltextindizes. Volltext wird nur
  ausgeführt, wenn `text` gesetzt ist.
- `POST /image-analysis/query` lädt `objects` und `readableText` nur, wenn `includeObjects` bzw.
  `includeReadableText` gesetzt sind. `limit` wird auf 500 begrenzt.

## 10. Tests

### Unit-Tests

- AuthService, TokenService, Refresh-Rotation
- MediaQueryService
- DuplicateService
- ImageAnalysisService, Query-Builder und Volltextfilter
- DTO-Mapping
- Rechte- und Tenant-Prüfungen

### Integrationstests

- Testcontainers mit MariaDB
- Flyway-Migration von leerer Datenbank
- Login, Refresh, Logout
- CRUD für Ordner, Kategorien, Medien
- Insert mit Thumbnail und Metadaten
- Suche und Query-Modi
- Thumbnail-Abruf
- Image-Analyse: GET, PUT, DELETE, Kaskade und vollständiges Ersetzen der Kindzeilen
- Image-Analyse-Suche mit Filtern, Volltext und Pagination
- Image-Analyse-Write mit kleinem `max_allowed_packet` (Batch-Fallback)
- Tenant-Isolation
- Image-Analyse-Endpunkte führen keine Dateisystem-Schreiboperationen aus

### Vertragstests

- OpenAPI-Validierung in der CI
- Optional: generierte Client-/Server-Contract-Tests gegen `docs/openapi.yaml`

## 11. Deployment

- Docker-Image oder ausführbares Spring-Boot-JAR
- MariaDB separat betreiben
- Konfiguration über Umgebungsvariablen:

```text
SPRING_DATASOURCE_URL
SPRING_DATASOURCE_USERNAME
SPRING_DATASOURCE_PASSWORD
MEDIABROWSER_JWT_SECRET
MEDIABROWSER_ACCESS_TOKEN_SECONDS=600
MEDIABROWSER_REFRESH_TOKEN_SECONDS=28800
MEDIABROWSER_CONTENT_ROOT=/mnt/media-readonly
```

- `MEDIABROWSER_CONTENT_ROOT` muss read-only eingebunden sein.
- Ohne Content Root liefert `/catalog/capabilities` `canReadContent=false`.
- Secrets nicht im Repository speichern.
- HTTPS über Reverse Proxy oder Ingress.
- Health- und Metrics-Endpunkte für Betrieb bereitstellen.

## 12. Umsetzungsphasen

### Phase 1: Grundgerüst und Auth

- Spring-Boot-Projekt anlegen
- OpenAPI als Contract einbinden
- MariaDB-Anbindung und Flyway-Baseline
- `/auth/login`, `/auth/refresh`, `/auth/logout`
- Bearer-Security und Tenant-Kontext
- Fehlerbehandlung und Logging ohne Secrets

### Phase 2: Katalog, Ordner und Kategorien

- Catalog-Properties
- Ordnerliste und `ensure`
- Kategoriebaum und Kategorie-Zuordnung
- Erste Contract-Tests

### Phase 3: Medien lesen und suchen

- MediaQuery-Modi
- Suche, Sortierung, Filter
- Trash, Bookmarks, Missing Files/Thumbs
- Missing Files nur als read-only Content-Store-Prüfung
- Thumbnail-Batch-Abruf

### Phase 4: Insert und Katalog schreiben

- Insert-Endpunkt für Katalogmetadaten und Variation/Thumbnail
- Originalcontent muss bereits im read-only Content Store existieren
- Kein Upload von Originaldateien über die API
- Update, Aktionen, Rollen, Prioritäten
- Duplikaterkennung
- Keine Dateioperationen (Rename/Move/Delete/Recycle)

### Phase 5: Variationen und Vorschauen

- Variations-CRUD
- Layers
- Preview-/Face-Endpunkte ohne serverseitige Dateischreibungen
- Thumbnail-Streaming

### Phase 6: Metadaten und Zusatzdaten

- Metadaten
- Beschreibungen
- Anhänge nur lesend
- GPS und Meteorologie
- Benutzerdefinierte Requests

### Phase 7: Image-Analyse (KI/OCR)

- Repository und Service für `IMAGE_ANALYSIS`, `IMAGE_OBJECTS` und `IMAGE_READABLE_TEXT`
- `GET /mediafiles/{id}/image-analysis` inklusive Kindzeilen
- `PUT /mediafiles/{id}/image-analysis` als transaktionales Upsert mit vollständigem Ersetzen der
  Kindzeilen
- `DELETE /mediafiles/{id}/image-analysis` mit Kaskade
- `POST /image-analysis/query` mit Filtern, Volltext, Pagination und optionalen Kindzeilen
- Berechtigungsprüfung für Schreiben und Löschen
- Integrationstests inklusive Cascade, Volltext und Mandantenisolierung

### Phase 8: Härtung und Betrieb

- Lasttests
- Tenant-Isolation
- SQL-Endpunkte absichern
- Sicherstellen, dass keine API-Route Dateien schreibt
- Content-Root read-only prüfen
- Backup-/Restore-Konzept
- Monitoring und Alerting
- Deployment-Dokumentation

## 13. Definition of Done

- Alle Endpunkte aus `docs/openapi.yaml` sind implementiert.
- Login, Refresh und Logout funktionieren mit Token-Rotation.
- MediaBrowserWpf kann mit `--provider api` starten, suchen und Katalog/Thumbnails lesen.
- Originalmedien werden nur aus einem read-only Content Store gelesen.
- Die API führt keine Schreiboperationen auf dem Medien-Dateisystem aus.
- Keine Upload-, Rename-, Move-, Delete- oder Recycle-Endpunkte für Dateien.
- Thumbnails werden mit `PREVIEW_WIDTH=0` und `PREVIEW_HEIGHT=0` ausgeliefert.
- Image-Analyse: `GET`, `PUT` und `DELETE` funktionieren; `PUT` ersetzt Analyse und Kindzeilen
  transaktional.
- Die Image-Analyse-Suche unterstützt Filter, Volltext und Pagination.
- Image-Analyse-Endpunkte führen keine Schreiboperationen auf dem Dateisystem aus.
- Keine MariaDB-Zugangsdaten im Client oder in API-Logs.
- Integrationstests laufen gegen eine echte MariaDB in Testcontainers.
- Content-Tests prüfen Range, ETag und fehlende Dateien ohne Schreibzugriffe.
- OpenAPI-Validierung und Contract-Tests laufen in der CI.
- Betriebs- und Backup-Dokumentation ist vorhanden.

## 14. SQL-Service-Vorlagen

Die Dateien unter `docs/backend/java` enthalten bereits Java-/JDBC-Vorlagen aus dem
MariaDB-Provider und dem MariaDB-Insert:

- `MediaBrowserCatalogService.java`: Katalog-, Such-, Kategorie-, Thumbnail-, GPS- und
  Metadatenabfragen.
- `MediaBrowserInsertService.java`: Insert-Pipeline mit Ordner-, Duplikat-, History-,
  Thumbnail-, Layer-, Kategorie- und Metadatenlogik.

Ergänzende Artefakte für die Image-Analyse:

- `docs/openapi.yaml`: Schemas `ImageAnalysis`, `ImageAnalysisUpsert`, `ImageAnalysisQuery`,
  `ImageObject` und `ImageReadableText`.
- `MediaBrowser4Lib\DB\Api\ApiDtos.cs`: `ApiImageAnalysisDto`, `ApiImageAnalysisUpsertDto`,
  `ApiImageAnalysisQueryDto`, `ApiImageObjectDto` und `ApiImageReadableTextDto`.
- `scripts/Migrate-ImageAnalysisToMariaDb.ps1`: einmaliger Import der drei Analyse-Tabellen aus
  `mediabrowser` nach `mediabrowser-new` mit Zuordnung über Ordner/Dateiname und eindeutigen
  Dateinamen-Fallback.

Die detaillierten Backend-Endpunkte und das Verbot von Dateisystem-Schreiboperationen sind in
`docs/api-filesystem-access-plan.md` beschrieben.

Diese Klassen sind als Datenzugriffsschicht gedacht und müssen noch in die Controller-,
Security- und DTO-Schicht des Backends eingebunden werden.

## 15. Image-Analyse: konkrete Umsetzung

### 15.1 Klassen und Verantwortlichkeiten

- `ImageAnalysisController`: Mapping der vier Endpunkte, Pfad-/Query-Validierung.
- `ImageAnalysisService`: Berechtigungen, Transaktionsgrenzen, DTO-Mapping.
- `ImageAnalysisRepository`: SQL für `IMAGE_ANALYSIS`.
- `ImageObjectRepository`: SQL für `IMAGE_OBJECTS`.
- `ImageReadableTextRepository`: SQL für `IMAGE_READABLE_TEXT`.
- `ImageAnalysisQueryBuilder`: dynamische Filter und Sortierung mit benannten Parametern.

### 15.2 Lesen

Ablauf für `GET /mediafiles/{id}/image-analysis`:

1. Prüfen, ob `MEDIAFILES.ID` existiert; sonst `404`.
2. Analyse über `MEDIA_ID` laden.
3. `IMAGE_OBJECTS` und `IMAGE_READABLE_TEXT` über `IMAGE_ANALYSIS_ID` laden.
4. Auf `ImageAnalysis` mappen.

Beispielabfrage:

```sql
SELECT a.ID, a.MEDIA_ID, a.ANIMAL_COUNT, a.WEATHER_DESCRIPTION, a.OBJECT_COUNT,
       a.CREATION_DATE, a.LOCATION_DESCRIPTION, a.FORMAT_OF_DOCUMENT_OR_IMAGE,
       a.DETAILED_IMAGE_ANALYSIS, a.TEMPERATUR_MIN_CELSIUS, a.TEMPERATUR_MAX_CELSIUS,
       a.MODEL_TEMPERATURE, a.CREATED_IN_MILLISECONDS, a.MODEL, a.PERSON_COUNT,
       a.CREATED_AT, m.FILENAME, f.FOLDERNAME, m.TYPE AS MEDIA_TYPE, m.MEDIADATE
FROM IMAGE_ANALYSIS a
JOIN MEDIAFILES m ON m.ID = a.MEDIA_ID
JOIN FOLDERS f ON f.ID = m.FOLDERS_FK
WHERE a.MEDIA_ID = ?
```

Kindzeilen:

```sql
SELECT ID, OBJECT_NAME
FROM IMAGE_OBJECTS
WHERE IMAGE_ANALYSIS_ID = ?
ORDER BY ID;

SELECT ID, TEXT_LINE
FROM IMAGE_READABLE_TEXT
WHERE IMAGE_ANALYSIS_ID = ?
ORDER BY ID;
```

### 15.3 Schreiben (Upsert)

Ablauf für `PUT /mediafiles/{id}/image-analysis`:

1. Medium prüfen; sonst `404`.
2. Transaktion starten.
3. `SELECT ID FROM IMAGE_ANALYSIS WHERE MEDIA_ID = ? FOR UPDATE`.
4. Vorhandene Zeile aktualisieren oder neue Zeile einfügen. `MEDIA_ID` darf nicht auf ein anderes
   Medium geändert werden.
5. `DELETE FROM IMAGE_OBJECTS WHERE IMAGE_ANALYSIS_ID = ?`.
6. Objekte in Batches einfügen.
7. `DELETE FROM IMAGE_READABLE_TEXT WHERE IMAGE_ANALYSIS_ID = ?`.
8. Lesbaren Text in Batches einfügen.
9. Commit; Antwort `204`.

Regeln:

- `OBJECT_COUNT`, `ANIMAL_COUNT` und `PERSON_COUNT` werden so gespeichert, wie sie geliefert
  werden. Sie werden nicht aus den Kindzeilen neu berechnet.
- `CREATED_AT` wird nur gesetzt, wenn der Client einen Wert liefert; sonst gilt der
  Datenbank-Default.
- Batch-Größe an `max_allowed_packet` anpassen, z. B. 500 Zeilen oder 256 KB pro Batch.
- Leere Listen bedeuten: keine Kindzeilen. `null` wird wie eine leere Liste behandelt.

### 15.4 Löschen

Ablauf für `DELETE /mediafiles/{id}/image-analysis`:

1. Medium prüfen; sonst `404`.
2. `DELETE FROM IMAGE_ANALYSIS WHERE MEDIA_ID = ?` in einer Transaktion.
3. Kindzeilen werden über `ON DELETE CASCADE` entfernt.
4. Antwort `204`.

### 15.5 Suche

Ablauf für `POST /image-analysis/query`:

- Aus allen gesetzten Filtern eine `WHERE`-Klausel mit benannten Parametern bauen. Keine
  String-Verkettung von Benutzerwerten.
- `MEDIAFILES` und `FOLDERS` für Dateiname, Ordner und Mediendatum joinen.
- `objectName`: `EXISTS (SELECT 1 FROM IMAGE_OBJECTS o WHERE o.IMAGE_ANALYSIS_ID = a.ID AND o.OBJECT_NAME = :objectName)`.
- `hasObjects`/`hasReadableText`: `EXISTS` bzw. `NOT EXISTS`.
- `text`:
  `MATCH(a.DETAILED_IMAGE_ANALYSIS) AGAINST (:text IN NATURAL LANGUAGE MODE)` oder
  `EXISTS (SELECT 1 FROM IMAGE_READABLE_TEXT t WHERE t.IMAGE_ANALYSIS_ID = a.ID AND MATCH(t.TEXT_LINE) AGAINST (:text IN NATURAL LANGUAGE MODE))`.
- `includeObjects` und `includeReadableText`: Kindzeilen in einer zweiten Abfrage für die
  Ergebnis-IDs laden, nicht als N+1-Abfrage pro Treffer.
- Sortierung nur über eine Whitelist der erlaubten Werte.
- `limit` auf 500 begrenzen, `offset` validieren.

### 15.6 Tests

- Unit: Query-Builder, DTO-Mapping, Validierung der Zähler und Berechtigungen.
- Integration mit Testcontainers:
  - Anlegen, Lesen, Ersetzen und Löschen einer Analyse.
  - Cascade beim Löschen.
  - Volltextsuche über `DETAILED_IMAGE_ANALYSIS` und `TEXT_LINE`.
  - Filterkombinationen und Pagination.
  - Kleines `max_allowed_packet` mit Batch-Fallback.
  - Mandantenisolierung und keine Dateisystem-Schreiboperationen.
- Vertragstests gegen `docs/openapi.yaml`.

### 15.7 Abnahmekriterien

- `GET`, `PUT` und `DELETE` entsprechen dem OpenAPI-Vertrag.
- `PUT` ersetzt Analyse und Kindzeilen atomar.
- Die Suche liefert reproduzierbare, paginierte Ergebnisse.
- Kindzeilen werden nur bei Bedarf geladen.
- Es werden keine Dateien gelesen, geschrieben oder gelöscht.
