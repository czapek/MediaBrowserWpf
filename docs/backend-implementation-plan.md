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
- Bei Massenimporten werden Batch-Grenzen und Fehler pro Batch protokolliert.

## 9. Performance

- Indizes aus dem bestehenden MariaDB-Schema übernehmen.
- Media-Abfragen paginieren und `Limit` serverseitig begrenzen.
- Thumbnails nur bei Bedarf laden; nicht in jeder Listenabfrage einbetten.
- Bulk-Inserts für große Imports verwenden.
- `max_allowed_packet` des MariaDB-Servers dokumentieren und im Deployment ausreichend dimensionieren.
- Große DTOs mit Base64-Thumbnails vermeiden, wo Streaming möglich ist.

## 10. Tests

### Unit-Tests

- AuthService, TokenService, Refresh-Rotation
- MediaQueryService
- DuplicateService
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
- Tenant-Isolation

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

### Phase 7: Härtung und Betrieb

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

Die detaillierten Backend-Endpunkte und das Verbot von Dateisystem-Schreiboperationen sind in
`docs/api-filesystem-access-plan.md` beschrieben.

Diese Klassen sind als Datenzugriffsschicht gedacht und müssen noch in die Controller-,
Security- und DTO-Schicht des Backends eingebunden werden.
