# MediaBrowser React-Frontend: API- und Design-Leitfaden

Dieses Dokument beschreibt die MediaBrowser-API aus Sicht eines Frontend-Designers und
-entwicklers. Es zeigt, was mit der API möglich ist, welche Datenmengen zu erwarten sind und wie
die bestehende WPF-Anwendung in eine React-Anwendung übertragen werden kann.

Verbindlicher technischer Vertrag: `docs/openapi.yaml` (Version 1.3.0).
C#-DTOs: `MediaBrowser4Lib\DB\Api\ApiDtos.cs`.
Fachliche Backend-Details: `docs/image-analysis-search-api.md`.

## 1. Kurzfassung

Die Anwendung ist ein Medienkatalog für Fotos, Videos und Audio mit:

- ca. **91.700 Medien**
- **1.881 Ordnern** und **6.992 Kategorien**
- **91.741 Variationen** und **91.741 Thumbnails**
- **4,67 Mio. Metadatenzeilen**
- **66.654 KI-/OCR-Analysen** mit **383.067 Objekten** und **79.481 Lesetextzeilen**
- **370.901 GPS-Punkten** und **94.245 Meteorologiezeilen**

Die API ist read-only für Originaldateien. Sie darf Katalogdaten in der Datenbank ändern, aber
keine Dateien anlegen, verschieben, umbenennen, löschen oder recyceln. Uploads, Rename, Move und
Delete von Originalen sind nicht Teil der API.

## 2. Orientierung an der bestehenden WPF-Anwendung

Die aktuelle Anwendung ist eine Desktop-Anwendung mit folgendem Aufbau:

### 2.1 Hauptfenster

- **Linke Navigation** als TabControl:
  - Kategorien
  - Ordner
  - Suche
  - Abfragen
  - Info
  - Lesezeichen
  - Papierkorb
  - Anhänge
- **Rechter Hauptbereich** als TabControl mit Medienlisten/Thumbnail-Ansichten.
- **Viewer** für die ausgewählten Medien:
  - Bildanzeige
  - Videoanzeige
  - Cube3D
  - SphereViewer
  - Multiplayer
- **Info-Bereich** mit aufklappbaren Bereichen:
  - Basis-Informationen
  - Alle vergebenen Kategorien
  - Meta-Daten
  - Image Analysis
- **Menüs**:
  - Datenbank
  - Import
  - Einstellungen
  - Hilfe
- **Statusleiste** am unteren Rand.

### 2.2 Übertragung in eine React-Anwendung

| WPF-Bereich | React-Vorschlag |
|---|---|
| Linke Tab-Navigation | Sidebar/Navigation mit Routen |
| Rechte Medienlisten-Tabs | Mehrere Ansichten: Grid, Liste, Karten, Viewer |
| Info-Tab | Rechte Info-Schublade oder Detailseite mit Accordions |
| Basis-Informationen | `MediaInfoBase` |
| Kategorien im Info-Tab | `MediaInfoCategories` |
| Meta-Daten | `MediaInfoMetadata` |
| Image Analysis | `MediaInfoImageAnalysis` |
| Viewer | `/media/:id` oder Vollbild-Overlay |
| Suche | `/search` mit Filterleiste und Ergebnisgrid |
| Ordner | `/folders/:id` |
| Kategorien | `/categories/:id` |
| Lesezeichen | `/bookmarks` |
| Papierkorb | `/trash` |
| Anhänge | `/attachments` oder Detailbereich |
| Datenbank/Import/Einstellungen | `/settings`, `/import`, `/admin` |
| Statusleiste | globale Status-/Toast-Leiste |

### 2.3 Empfohlene Informationsarchitektur

```text
AppShell
  TopBar
    Katalogname, Benutzer, Verbindung, globale Suche
  Sidebar
    Kategorien, Ordner, Suche, Abfragen, Lesezeichen, Papierkorb, Anhänge
  Main
    Toolbar (Sortierung, Ansicht, Filter-Chips)
    MediaGrid / MediaList / Viewer
  DetailDrawer
    Basis, Kategorien, Meta-Daten, Image Analysis
  StatusBar
```

## 3. API-Grundlagen

### 3.1 Basis-URL

Entwicklung:

```text
http://192.168.2.129:8282/api/v1
```

Die Basis-URL muss im React-Projekt konfigurierbar sein, z. B. über `VITE_API_BASE_URL`. Für
lokale Entwicklung ist ein Vite-Proxy empfehlenswert, um CORS-Probleme zu vermeiden.

### 3.2 Authentifizierung

Login:

```http
POST /auth/login
Content-Type: application/json

{
  "username": "<benutzer>",
  "password": "<passwort>",
  "tenant": "mediabrowser-new"
}
```

Antwort:

```json
{
  "accessToken": "...",
  "refreshToken": "...",
  "tokenType": "Bearer",
  "expiresIn": 600
}
```

Danach wird jeder Request mit folgendem Header gesendet:

```http
Authorization: Bearer <accessToken>
```

Refresh:

```http
POST /auth/refresh
{ "refreshToken": "..." }
```

Logout:

```http
POST /auth/logout
{ "refreshToken": "..." }
```

Wichtig:

- Keine statischen API-Keys im React-Bundle hinterlegen.
- Access-Token nur im Speicher oder in einem sicheren Session-Speicher halten.
- Bei `401` einmalig `/auth/refresh` versuchen; bei Fehlschlag zum Login zurückleiten.
- Passwörter und Tokens niemals loggen.
- Der Mandant ist Teil des Logins und bestimmt die Datenquelle.

### 3.3 Fehlerformat

Die API liefert Problem-Details:

```json
{
  "type": "https://www.jhipster.tech/problem/problem-with-message",
  "title": "Bad Request",
  "status": 400,
  "detail": "Ungültige Anfrage",
  "instance": "/api/v1/...",
  "message": "error.http.400",
  "path": "/api/v1/..."
}
```

Validierungsfehler können zusätzlich `fieldErrors` enthalten.

Statuscodes:

- `400` ungültige Eingabe
- `401` nicht authentifiziert
- `403` keine Berechtigung
- `404` Ressource nicht gefunden
- `409` Konflikt
- `500` unerwarteter Serverfehler

### 3.4 Pagination und Limits

- `POST /image-analysis/search` unterstützt `offset` und `limit` (Maximum 500).
- `POST /mediafiles/query` unterstützt `limit`, aber **kein `offset`**.
  Für echte Blätterung muss das Backend `offset` ergänzen oder die Frontend-Ansicht auf
  Sortierung + `limit` beschränken.
- Standard-Limits im Frontend klein halten: 50–100 Medien pro Seite.
- Thumbnails und Metadaten nie für alle Treffer gleichzeitig laden.

### 3.5 Read-only-Regeln

Die API führt keine Schreiboperationen auf dem Medien-Dateisystem aus:

- kein Upload von Originalen
- kein Copy/Move/Rename/Delete/Recycle von Medien-, Sidecar- oder Preview-Dateien
- Originale werden nur aus einem read-only Content Store gelesen
- Datenbank-Schreiboperationen (Metadaten, Kategorien, Analyse, Thumbnails in der DB) sind erlaubt

`POST /mediafiles/insert` legt nur Katalogdaten, Variation, Thumbnail und Metadaten an. Die
Originaldatei muss bereits im read-only Content Store liegen; die API lädt sie nicht hoch.

## 4. Datenbestand und Mengen

Stand: aktueller Katalog `mediabrowser-new`.

| Bereich | Menge | Konsequenz für das Frontend |
|---|---:|---|
| Medien gesamt | 91.677 | Grid virtualisieren, seitenweise laden |
| Medien aktiv | 91.675 | Standardansicht ohne Papierkorb |
| Lesezeichen | 168 | kleine, schnell ladbare Liste |
| RGB-Medien | 87.473 | aktuell dominanter Medientyp |
| Video/Audio | 0 | im aktuellen Bestand nicht vorhanden |
| Ordner | 1.881 | einmal laden und cachen (~165 KB) |
| Kategorien | 6.992 | Baum virtualisieren/lazy rendern (~2,1 MB) |
| Kategorie-Zuordnungen | 210.892 | Kategorie-Counts nur bei Bedarf laden |
| Variationen | 91.741 | eine Default-Variation pro Medium |
| Thumbnails | 91.741 | batchweise für sichtbare Medien laden |
| Layer | 21.014 | nur im Bearbeitungsdialog laden |
| Metadaten | 4.667.193 Zeilen | nur für ausgewählte Medien laden |
| Metadaten-Schlüssel | 814 | Filtervorschläge daraus aufbauen |
| Beschreibungen | 70 | klein, kann mitgeladen werden |
| Anhänge | 13 | klein, nur bei Bedarf |
| Anhang-Verknüpfungen | 45 | klein |
| Rollen | 2 | für Rollenauswahl |
| Image Analyses | 66.654 | Suche/Info nutzen |
| Image Objects | 383.067 | Objekt-Autocomplete, nicht als globale Liste |
| Image Readable Text | 79.481 | Volltextsuche |
| GPS-Punkte | 370.901 | immer zeitlich begrenzen, Map clustern |
| Meteorologie | 94.245 | nur für Zeitraum/Medium laden |
| Gesichter | 0 | im aktuellen Bestand leer |

### 4.1 Gemessene Payload-Größen

| Endpunkt | Beobachtete Größe |
|---|---|
| `GET /mediafiles/{id}` | ca. 0,7 KB pro Medium, ohne Metadaten/Thumbnail |
| `POST /mediafiles/query` | ca. 0,7 KB pro Medium |
| `POST /mediafiles/thumbs` | ca. 8 KB Base64 pro Thumbnail |
| `POST /metadata/query` für 1 Medium | ca. 48 Zeilen, ca. 5,5 KB |
| `GET /mediafiles/{id}/image-analysis` | ca. 1,3 KB |
| `GET /categories/tree` | 6.992 Einträge, ca. 2,1 MB, ca. 1,1 s |
| `GET /folders` | 1.881 Einträge, ca. 165 KB, ca. 0,37 s |
| `GET /catalog/capabilities` | ca. 195 Byte |

## 5. Wichtige Datenobjekte

### 5.1 MediaItem

Ein MediaItem-Objekt ist bewusst schlank:

```json
{
  "id": 157858,
  "filename": "250302-1523-47.jpg",
  "foldername": "D:\\Photos\\Alle Bilder\\2025\\03_März",
  "sortorder": "250302-1523-47.jpg",
  "md5Value": "...",
  "fileLength": 2622950,
  "width": 4000,
  "height": 2252,
  "viewed": 0,
  "duration": 0.0,
  "frames": 0,
  "orientation": 0,
  "type": "rgb",
  "isBookmarked": false,
  "isDeleted": false,
  "isDuplicate": false,
  "isFileNotFound": 0,
  "priority": 5,
  "roleId": 1,
  "variationId": 150261,
  "defaultVariationId": 150261,
  "currentVariationId": 150261,
  "descriptionId": null,
  "description": null,
  "creationDate": "2025-03-02T15:23:47Z",
  "mediaDate": "2025-03-02T15:23:47Z",
  "editDate": "2025-03-02T15:23:47Z",
  "insertDate": "2025-09-12T01:42:28Z",
  "longitude": null,
  "latitude": null,
  "isThumbOutdated": 0
}
```

### 5.2 ImageAnalysis

Die Kurzfassung in Suchergebnissen enthält:

- `id`, `model`
- `personCount`, `animalCount`, `objectCount`
- `temperaturMinCelsius`, `temperaturMaxCelsius`
- `modelTemperature`, `createdInMilliseconds`
- `createdAt`, `creationDate`
- `weatherDescription`, `locationDescription`, `formatOfDocumentOrImage`
- optional `objects` und `readableText`

Die Detailansicht liefert zusätzlich u. a. `detailedImageAnalysis`.

### 5.3 Weitere Objekte

- `Folder`: ID + Name + ItemCount
- `Category`: ID, Parent, Name, Sortname, Beschreibung, Pfad, GUID, Geo-Flags, Count
- `Variation`: ID, Medien-ID, Position, Name, Beschreibung, Thumb, Layer
- `Layer`: Position, Edit, Action
- `MetaData`: ID, Name, GroupName, Value, Type, IsVisible, IsNull
- `Attachment`: ID, Pfad, Typ, Medien-IDs, Referenzen
- `Role`: ID, Name, Beschreibung
- `GeoPoint`: Longitude, Latitude, Altitude, Zeiten, Distanz
- `GpsFile`: Dateigröße + Zeit
- `Meteorology`: LogTime, Temperature, Humidity
- `Preview`: Binärdaten, Breite, Höhe, Endung
- `Face`: VariationId, X, Y, Width, Height, RelativeSize
- `Thumb`: VariationId, Binärdaten, Endung, Breite, Höhe, IsOutdated
- `Description`: ID, Value, Command, Medien-IDs
- `Duplicate`: Medien-ID + Wert

## 6. Endpunkte nach Funktionsbereich

### 6.1 Auth

| Methode | Pfad | Zweck |
|---|---|---|
| POST | `/auth/login` | Login, Access- und Refresh-Token |
| POST | `/auth/refresh` | Access-Token erneuern |
| POST | `/auth/logout` | Refresh-Token widerrufen |

### 6.2 Katalog

| Methode | Pfad | Zweck |
|---|---|---|
| GET | `/catalog/validate` | Katalogstatus prüfen |
| POST | `/catalog/create` | Katalog anlegen (admin/erststart) |
| GET | `/catalog/properties` | Katalogweite Eigenschaften |
| PUT | `/catalog/properties` | Eigenschaften speichern |
| GET | `/catalog/last-folders` | zuletzt hinzugefügte Ordner |
| GET | `/catalog/capabilities` | verfügbare Funktionen |

`capabilities` ist für das Frontend wichtig, um Funktionen auszublenden:

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

### 6.3 Ordner

| Methode | Pfad | Zweck |
|---|---|---|
| GET | `/folders` | alle Ordner (1.881) |
| POST | `/folders/ensure` | Ordner anlegen/finden |
| DELETE | `/folders/{id}` | Ordner löschen |
| GET | `/folders/{id}/count` | Anzahl Medien im Ordner |

Ordnerliste einmal laden und im Client cachen. Sie ändert sich selten.

### 6.4 Kategorien

| Methode | Pfad | Zweck |
|---|---|---|
| GET | `/categories/tree` | flache Kategorienliste mit Parent (6.992) |
| GET | `/categories?variationId=...` | Kategorien einer Variation |
| POST | `/categories` | Kategorie speichern |
| POST | `/categories/by-path` | Kategorie anhand Pfad |
| DELETE | `/categories/{id}` | Kategorie löschen |
| GET | `/categories/geo` | Kategorien im Geo-Bereich |
| POST | `/categories/from-media` | Kategorien + Counts für Medienliste |
| GET | `/categories/identifiers/{variationId}` | Kategorie-IDs einer Variation |

Der Kategoriebaum ist mit 6.992 Einträgen und ~2,1 MB der größte Baum-Endpunkt. Empfehlung:
einmal laden, cachen und im UI virtualisieren oder lazy expandieren.

### 6.5 Medien

| Methode | Pfad | Zweck |
|---|---|---|
| POST | `/mediafiles/query` | Medien nach Modus/Filtern |
| GET | `/mediafiles/{id}` | ein Medium |
| PUT | `/mediafiles/{id}` | Medium aktualisieren |
| POST | `/mediafiles/insert` | Medium in den Katalog aufnehmen |
| POST | `/mediafiles/actions` | Aktionen auf Medien |
| POST | `/mediafiles/thumbs` | Thumbnails für Variation-IDs |
| GET | `/mediafiles/checksum-count` | Anzahl nach Checksum |
| POST | `/mediafiles/from-xml` | Import aus XML |
| GET | `/mediafiles/{id}/content` | Originalinhalt, read-only |
| HEAD | `/mediafiles/{id}/content` | Content-Metadaten |
| GET | `/mediafiles/{id}/sidecars` | Sidecar-Dateien, read-only |
| GET | `/mediafiles/{id}/description` | Beschreibung |
| POST | `/mediafiles/sql-query` | SQL-Abfrage (admin) |
| GET | `/mediafiles/sql-request` | SQL für Medienliste (admin) |

Wichtige `mode`-Werte für `POST /mediafiles/query`:

- `SEARCH` – Suchtoken
- `FOLDERS` – Ordner-IDs
- `CATEGORIES` – Kategorie-IDs
- `TRASH` – Papierkorb
- `BOOKMARKS` – Lesezeichen
- `DUPLICATES` – Duplikate
- `GEO` – Geo-Bereich
- `SORT` – sortierte Medienliste
- `FILES_FROM_LIST` / `FILES_FROM_CHECKSUM`
- `MISSING_FILES`, `MISSING_THUMBS`, `MISSING_CATEGORIES`
- `CHECKSUM`, `FILENAME`, `FILE_PATH`, `REQUEST`
- `VIRTUAL`

Beispiel:

```json
{
  "mode": "BOOKMARKS",
  "limit": 50
}
```

Antwort: Array von MediaItems.

### 6.6 Thumbnails, Vorschauen und Gesichter

| Methode | Pfad | Zweck |
|---|---|---|
| POST | `/mediafiles/thumbs` | Batch-Thumbnails für Variation-IDs |
| GET | `/variations/{id}/thumb` | Thumbnail als Binärdaten |
| GET | `/previews/{variationId}` | große Vorschau |
| GET | `/previews/exists` | Vorschau vorhanden? |
| GET | `/previews/variation-ids` | Variationen mit Vorschau |
| GET | `/faces/previews/{variationId}` | Gesichter der Vorschau |
| GET | `/faces` | Gesichtsanzahl je Variation |

Empfehlung:

- Im Grid zuerst `POST /mediafiles/thumbs` mit den Variation-IDs der sichtbaren Seite.
- Im Viewer `GET /previews/{variationId}` oder das Original über `/mediafiles/{id}/content`.
- Thumbnails nicht in jeder Medienabfrage einbetten.

### 6.7 Image Analysis

| Methode | Pfad | Zweck |
|---|---|---|
| GET | `/mediafiles/{id}/image-analysis` | Analyse eines Mediums |
| PUT | `/mediafiles/{id}/image-analysis` | Analyse anlegen/ersetzen |
| DELETE | `/mediafiles/{id}/image-analysis` | Analyse löschen |
| POST | `/image-analysis/query` | analyseorientierte Suche |
| POST | `/image-analysis/search` | Suche, Rückgabe von MediaItems |
| GET | `/image-analysis/value-search` | Werte für Autocomplete/DDL |
| GET | `/image-analysis/filter-fields` | Feldkatalog für Filter-UI |
| GET | `/image-analysis/ranges` | Min/Max für Zahlenfilter |

**Wichtig:** Es gibt keine Facetten-Endpunkte und keine globalen Counts. Für Wertelisten wird
ausschließlich `value-search` verwendet.

Unterstützte `value-search`-Felder:

- `OBJECT_NAME`
- `WEATHER_DESCRIPTION`
- `LOCATION_DESCRIPTION`

Beispiel:

```http
GET /image-analysis/value-search?field=OBJECT_NAME&query=Wein&limit=20
```

```json
{
  "values": ["Flasche Wein", "Glas Wein", "Weinberge"]
}
```

Regeln:

- `query` mindestens 3 Zeichen
- `limit` maximal 200
- nur distinkte Werte, keine Counts
- alphabetische Sortierung

Suche:

```json
{
  "filter": {
    "text": "Skelett",
    "textFields": ["DETAILED_IMAGE_ANALYSIS", "READABLE_TEXT"],
    "objectNames": ["Weinberge"],
    "objectMatch": "ANY",
    "personCount": { "min": 0, "max": 3 },
    "mediaTypes": ["rgb"]
  },
  "sort": "MEDIA_DATE_DESC",
  "offset": 0,
  "limit": 50,
  "includeObjects": false,
  "includeReadableText": false
}
```

Antwort:

```json
{
  "total": 42,
  "offset": 0,
  "limit": 50,
  "items": [
    {
      "id": 157858,
      "filename": "250302-1523-47.jpg",
      "foldername": "...",
      "mediaDate": "2025-03-02T15:23:47Z",
      "type": "rgb",
      "width": 4000,
      "height": 2252,
      "variationId": 150261,
      "isDeleted": false,
      "isBookmarked": false,
      "analysis": {
        "id": 901,
        "model": "qwen2.5vl:7b",
        "personCount": 0,
        "animalCount": 0,
        "objectCount": 3,
        "temperaturMinCelsius": 5,
        "temperaturMaxCelsius": 15,
        "weatherDescription": "Bereitschaft",
        "locationDescription": "Landstraße in den Weinbergen",
        "formatOfDocumentOrImage": "Bild"
      }
    }
  ]
}
```

Performance (gemessen):

- `value-search` Objekt: ca. 25 ms
- `value-search` Wetter: ca. 260 ms
- `value-search` Ort: ca. 22 ms
- `image-analysis/search` je nach Filter: ca. 1–3 s
- `image-analysis/filter-fields`: ca. 113 ms
- `image-analysis/ranges`: ca. 216 ms

Datenqualität:

- `ranges` enthält echte Ausreißer (z. B. `personCount` bis 160.000, Temperatur bis 2.891 °C,
  negative Analyse-Dauer).
- Für Slider p1/p99 oder feste, geclampte Bereiche verwenden.
- Objekt-, Wetter- und Ortswerte sind sehr unterschiedlich verteilt:
  - 36.865 verschiedene Objektnamen
  - 9.574 verschiedene Wetterbeschreibungen
  - 48.764 verschiedene Ortsbeschreibungen
  - nur 2 Modelle
- Wetter/Ort daher nur als Autocomplete mit Mindestlänge 3 verwenden, nicht als vollständige DDL.

### 6.8 Variationen und Layer

| Methode | Pfad | Zweck |
|---|---|---|
| GET | `/mediafiles/{id}/variations?includeThumbs=false` | Variationen eines Mediums |
| POST | `/mediafiles/{id}/variations` | Variation anlegen |
| DELETE | `/mediafiles/{id}/variations?name=...` | Variation löschen |
| POST | `/mediafiles/{id}/variations/replace` | Variationen ersetzen |
| PUT | `/variations/{id}` | Variation aktualisieren |
| DELETE | `/variations/{id}` | Variation löschen |
| POST | `/variations/{id}/default` | Default setzen |
| GET | `/variations/{id}/layers` | Layer lesen |
| PUT | `/variations/{id}/layers` | Layer speichern |
| GET | `/variations/{id}/thumb` | Thumbnail |
| PUT | `/variations/{id}/thumb` | Thumbnail speichern |

### 6.9 Metadaten, Beschreibungen, Anhänge

| Methode | Pfad | Zweck |
|---|---|---|
| POST | `/metadata/query` | Metadaten für Medien-IDs |
| GET | `/metadata/keys` | verfügbare Metadaten-Schlüssel |
| GET | `/mediafiles/{id}/description` | Beschreibung eines Mediums |
| POST | `/descriptions/query` | Beschreibungen für Medien-IDs |
| POST | `/descriptions` | Beschreibung speichern |
| POST | `/attachments/query` | Anhänge für Medien-IDs |
| GET | `/attachments/{id}/content` | Anhang read-only |

Metadaten nur für die ausgewählten Medien laden. Ein Medium hat typischerweise ca. 48
Metadatenzeilen (~5,5 KB). Bei 100 Medien wären das bereits ca. 550 KB.

### 6.10 GPS, Meteorologie

| Methode | Pfad | Zweck |
|---|---|---|
| POST | `/gps/points` | GPS-Punkte speichern |
| GET | `/gps?from=...&to=...` | GPS-Punkte im Zeitraum |
| GET | `/gps/nearest?date=...` | nächster GPS-Punkt |
| GET | `/gps/files` | GPS-Dateien |
| DELETE | `/gps/files?fileTime=...` | GPS-Datei löschen |
| GET | `/meteorology` | Meteorologiedaten |
| POST | `/meteorology` | Meteorologiedaten speichern |

`/gps` erfordert `from` und `to`. Wegen 370.901 Punkten immer einen kleinen Zeitraum anfragen
und die Karte clustern.

### 6.11 Requests, Rollen, Duplikate, Maintenance

| Methode | Pfad | Zweck |
|---|---|---|
| GET | `/requests` | benutzerdefinierte Abfragen |
| POST | `/requests` | Abfrage speichern |
| DELETE | `/requests/{id}` | Abfrage löschen |
| GET | `/requests/{id}/sql` | SQL einer Abfrage |
| PUT | `/requests/{id}/sql` | SQL speichern |
| GET | `/roles` | Rollen |
| POST | `/duplicates/check` | Duplikat prüfen |
| GET | `/duplicates/{id}` | Duplikate eines Mediums |
| POST | `/maintenance/clean` | Katalog bereinigen (admin) |

SQL- und Maintenance-Endpunkte nur für Administration/geschützte Bereiche verwenden.

## 7. Empfohlene React-Seiten und Komponenten

### 7.1 Seiten

- `/` – Start/Dashboard mit zuletzt hinzugefügten Ordnern, Lesezeichen, Statistiken
- `/search` – Suche inkl. Image Analysis
- `/categories/:id?` – Kategoriebaum + Medien
- `/folders/:id?` – Ordnerbaum + Medien
- `/bookmarks` – Lesezeichen
- `/trash` – Papierkorb
- `/media/:id` – Viewer + Detailinformationen
- `/media/:id/analysis` – Image-Analysis-Detail
- `/attachments` – Anhangübersicht
- `/requests` – gespeicherte Abfragen
- `/settings` – Katalog-/Verbindungseinstellungen
- `/admin` – Maintenance, SQL, Import (geschützt)

### 7.2 Komponenten

- `AppShell`
- `Sidebar`
- `TopBar`
- `StatusBar`
- `MediaGrid`
- `MediaList`
- `MediaCard`
- `Thumbnail`
- `Viewer`
- `InfoDrawer`
- `InfoAccordion`
- `InfoBase`
- `InfoCategories`
- `InfoMetadata`
- `InfoImageAnalysis`
- `CategoryTree`
- `FolderTree`
- `SearchPanel`
- `ImageAnalysisFilterPanel`
- `ValueAutocomplete`
- `RangeSlider`
- `DateRangePicker`
- `MapView`
- `AttachmentList`
- `Pagination` (nur wo die API es unterstützt)

### 7.3 Design-Empfehlungen

- Medien-Grid mit **Virtualisierung** (z. B. TanStack Virtual, react-window).
- Thumbnails lazy laden; Platzhalter mit fester Aspect Ratio, um Layout-Sprünge zu vermeiden.
- Batch-Thumbnails für die sichtbare Seite verwenden.
- Filter-Chips für aktive Suche/Filter; URL-Parameter als Source of Truth.
- Info-Drawer erst beim Öffnen laden; Accordions einzeln nachladen.
- Skeletons für Grid, Metadaten und Analyse.
- Leere Zustände klar beschreiben („Keine Analyse vorhanden“).
- Fehlerzustände pro Panel, nicht global.
- Dark/Light-Theme; die WPF-App ist funktional, das React-Frontend kann moderner gestaltet werden.
- Tastaturnavigation im Grid und Viewer unterstützen.

## 8. Datenlade-Strategie

| Bereich | Strategie |
|---|---|
| Medienliste | seitenweise, `limit` 50–100, sortiert |
| Thumbnails | batchweise für sichtbare Variation-IDs |
| Metadaten | nur für ausgewählte Medien |
| Kategorien | einmal laden, cachen, virtualisieren |
| Ordner | einmal laden, cachen |
| Image-Analysis-Werte | `value-search` ab 3 Zeichen, debounced |
| Image-Analysis-Suche | `limit` 50, `includeObjects`/`includeReadableText` nur bei Bedarf |
| Analyse-Detail | nur beim Öffnen des Info-Bereichs |
| GPS | Zeitraum begrenzen, clustern |
| Meteorologie | Zeitraum/Medium begrenzen |
| Vorschauen | nur im Viewer, nicht im Grid |
| Originalcontent | nur im Viewer, Range-Requests nutzen |

## 9. Bekannte Lücken und offene Backend-Punkte

1. `POST /mediafiles/query` hat **kein `offset`**. Für echte Pagination muss das Backend
   `offset` ergänzen.
2. `GET /categories/tree` liefert ~2,1 MB. Ein leichterer oder lazy Endpunkt wäre für große
   Kategorienbäume sinnvoll.
3. `GET /image-analysis/ranges` enthält Daten-Ausreißer. Frontend sollte clampen oder das
   Backend p1/p99 liefern.
4. Keine Facetten: Wertelisten ausschließlich über `value-search`.
5. `FACES` ist aktuell leer; Gesichtsfunktionen zeigen keine Daten.
6. Video/Audio sind im aktuellen Bestand nicht vorhanden; die UI sollte trotzdem vorbereitet
   sein, aber nicht mit Demo-Daten dafür rechnen.
7. Admin-Endpunkte (`sql-query`, `sql-request`, `maintenance`) nicht im normalen UI anzeigen.
8. CORS für die React-Dev-URL muss erlaubt sein oder über einen Vite-Proxy umgangen werden.
9. Keine Datei-Schreiboperationen: Import/Upload/Rename/Move/Delete nicht als API-Funktion
   einplanen.

## 10. Beispielaufrufe

### Login

```http
POST /auth/login
Content-Type: application/json

{
  "username": "<benutzer>",
  "password": "<passwort>",
  "tenant": "mediabrowser-new"
}
```

### Lesezeichen laden

```http
POST /mediafiles/query
Authorization: Bearer <token>
Content-Type: application/json

{
  "mode": "BOOKMARKS",
  "limit": 50
}
```

### Thumbnails laden

```http
POST /mediafiles/thumbs
Authorization: Bearer <token>
Content-Type: application/json

[150261, 155543, 155937]
```

### Metadaten laden

```http
POST /metadata/query
Authorization: Bearer <token>
Content-Type: application/json

[157858]
```

### Image-Analysis-Autocomplete

```http
GET /image-analysis/value-search?field=OBJECT_NAME&query=Wein&limit=20
Authorization: Bearer <token>
```

### Image-Analysis-Suche

```http
POST /image-analysis/search
Authorization: Bearer <token>
Content-Type: application/json

{
  "filter": {
    "objectNames": ["Weinberge"],
    "personCount": { "min": 0, "max": 3 }
  },
  "sort": "MEDIA_DATE_DESC",
  "offset": 0,
  "limit": 50
}
```

## 11. Glossar

- **MediaItem**: ein Katalogeintrag für eine Medien-Datei.
- **Variation**: eine Variante eines Mediums (z. B. bearbeitet, anderer Zuschnitt).
- **Thumbnail**: kleines Vorschaubild, in der DB gespeichert.
- **Preview**: größere Vorschau, on demand.
- **Kategorie**: hierarchische Zuordnung; ein Medium kann mehrere Kategorien haben.
- **Ordner**: physischer Ordnerpfad.
- **Image Analysis**: KI-/OCR-Analyse eines Mediums.
- **Value-Search**: liefert distinkte Werte für Autocomplete; keine Counts.
- **Capabilities**: vom Backend gemeldete verfügbare Funktionen.
