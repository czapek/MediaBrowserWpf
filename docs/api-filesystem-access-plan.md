# API-Modus: Dateisystemzugriffe und Migrationsplan

## 1. Ziel und Leitentscheidung

Im API-Modus liegt der Medienkatalog auf einem Spring-Boot-Backend. Der Client darf nicht davon
ausgehen, dass die Originaldateien eines `MediaItem` lokal vorhanden oder über einen
Windows-Pfad erreichbar sind.

Verbindliche Leitentscheidung:

- **Die MediaBrowser-API führt keine Schreiboperationen auf dem Medien-Dateisystem aus.**
  Das betrifft insbesondere: keinen Upload von Originaldateien, kein Anlegen, Kopieren,
  Verschieben, Umbenennen, Löschen oder Recyceln von Medien-, Sidecar- oder Preview-Dateien.
- Die API darf weiterhin **Datenbank-Schreiboperationen** ausführen, z. B. Katalogfelder,
  Kategorien, Metadaten, Beschreibungen, Rollen, Thumbnail-BLOBs in `THUMBS`, GPS- und
  Meteorologiedaten. „Keine Schreiboperationen“ bezieht sich auf das Medien-Dateisystem,
  nicht auf MariaDB.
- Die Image-Analyse-Tabellen `IMAGE_ANALYSIS`, `IMAGE_OBJECTS` und `IMAGE_READABLE_TEXT` sind
  reine Datenbankdaten. `GET`, `PUT` und `DELETE` der Image-Analyse-API lesen und schreiben nur
  MariaDB und führen keine Dateisystemoperationen aus.
- Originalmedien müssen, wenn sie im API-Modus lesbar sein sollen, bereits in einem vom Backend
  **read-only** eingebundenen Content Store liegen. Das Backend liest und streamt nur.
- Das Bereitstellen oder Aktualisieren der Originaldateien erfolgt außerhalb der MediaBrowser-API,
  z. B. durch einen NAS-/Syncthing-/rsync-Prozess oder einen separaten Ingest-Service.
- Kann das Backend keinen Content Store bereitstellen, ist der API-Modus katalog- und
  thumbnailbasiert; Originalansicht, Video, Export und Dateiaktionen werden deaktiviert.

## 2. Annahmen

1. MariaDB ist die zentrale Katalogdatenbank.
2. Thumbnails liegen als BLOBs in `THUMBS`.
3. Originalmedien liegen in einem read-only Content Store oder sind im API-Modus nicht verfügbar.
4. Der Client darf lokale Cache-Dateien anlegen; das ist ein Client-Cache, keine API-Schreiboperation.
5. Der Client darf local exportierte Dateien schreiben; auch das ist keine API-Schreiboperation.
6. Dateiänderungen am Originalbestand sind im API-Modus nicht möglich. Sie sind nur im
   SQLite-/MariaDB-Dateimodus oder über einen separaten, nicht zur API gehörenden Ingest-Prozess
   zulässig.

## 3. Zugriffspunkte im aktuellen Code

### 3.1 Medienmodell

- `MediaBrowser4Lib\Objects\MediaItem.cs`
  - `FullName` setzt sich aus `Foldername` und `Filename` zusammen.
  - `FileObject` erzeugt bei Bedarf ein `FileInfo` aus `FullName`.
  - `SetCachedImage`, `Rename` und Tooltip-/Anzeigepfade verwenden lokale Pfade.
- `MediaBrowser4Lib\MediaBrowserContext.cs`
  - `GetMediaItemFromFullname`
  - `GetMediaItems` für virtuelle Medien
  - `CopyToFolder`
  - `RemoveAndRecycle`
  - `SetDeleted`
- `MediaBrowser4Lib\DB\Maria\MariaDbProvider.cs`
  - `GetMediaItemsWithMissingFiles` prüft `File.Exists`.
  - `RemoveAndRecycle` verschiebt Dateien in den Papierkorb.
  - `RenameMediaItem` erzeugt ein neues `FileInfo`.
  - `LoadMediaItems` prüft Dateiexistenz und setzt `FileObject` zurück.

Konsequenz: `FileObject` und `FullName` dürfen im API-Modus nicht als verlässliche
Originaldatei-Referenz dienen.

### 3.2 Thumbnails und Vorschauen

- `MediaBrowser4Lib\Objects\MediaItemBitmap.cs`
  - `GetThumbnail` liest EXIF und das Originalbild.
  - `GetImage` liest das Originalbild.
- `MediaBrowser4Lib\Objects\MediaItemVideo.cs`
  - `GetThumbnail` liest Videos über NReco, DirectShow oder MediaInfo.
- `MediaBrowser4Lib\DB\Api\ApiProvider.cs`
  - `GetThumbJpegData`, `GetThumbsForAllMediaItems` und `GetImagePreviewDB` greifen bereits auf
    API-Endpunkte zu.
- `MediaBrowserWPF\Utilities\TakeSnapshot.xaml.cs`
  - Export und Snapshot benötigen Originaldaten.

Konsequenz: Thumbnails können aus der API geliefert werden. Große Vorschauen, Snapshots und
Exporte brauchen entweder einen read-only Content-Stream oder eine lokale Kopie.

### 3.3 Bild-, Video- und Spezialviewer

Direkte Dateizugriffe:

- `MediaBrowserWPF\UserControls\RgbImage\ImageControl.xaml.cs`
  - `File.Exists`, `new Uri(FileObject.FullName)`, `LoadImage(FileObject.FullName)`
- `MediaBrowserWPF\UserControls\RgbImage\ViewerSource.cs`
  - `FileObject.Exists`, `Image.FromFile`
- `MediaBrowserWPF\Viewer\MediaViewer.xaml.cs`
  - `File.Exists`, fehlende-Datei-Anzeige, `Directory.GetFiles` für Sidecars
- `MediaBrowserWPF\Viewer\ViewerBaseControl.xaml.cs`
  - `File.Exists`
- `MediaBrowserWPF\UserControls\Video\VideoPlayer.xaml.cs`
  - `File.Exists`, `MediaElement.Source = FileObject.FullName`
- `MediaBrowserWPF\UserControls\Video\nVlc.xaml.cs`
- `MediaBrowserWPF\UserControls\Video\VideoLanDotNet.xaml.cs`
- `MediaBrowserWPF\UserControls\PanoView.xaml.cs`
  - `new Uri(FileObject.FullName)` für Bild und Video
- `MediaBrowserWPF\Viewer\SphereViewer.xaml.cs`
  - `new Uri(FullName)`
- `MediaBrowserWPF\Viewer\Cube3D.xaml.cs`
  - `Image.FromFile(FullName)`
- `MediaBrowserWPF\UserControls\Levels\LevelsToolbox.xaml.cs`
  - `new Uri(FileObject.FullName)`

Konsequenz: Diese Viewer müssen auf eine Content-URL oder einen lokalen Cache-Pfad umgestellt
werden. WPF-`MediaElement` kann Authorization-Header nicht zuverlässig selbst senden; für Video
ist ein kurzlebiger signierter Read-only-Link oder ein lokaler Client-Cache sinnvoll.

### 3.4 Metadaten, Checksummen und Datumswerte

- `MediaBrowser4Lib\Utilities\Crypto.cs`
  - MD5 wird aus der Originaldatei gelesen.
- `MediaBrowser4Lib\Utilities\DateAndTime.cs`
  - MediaDate wird aus Dateiname und Dateizeit abgeleitet.
- `MediaBrowser4Lib\Utilities\FilesAndFolders.cs`
  - Dateiexistenz, Sidecar-Dateien, Kopieren und Verschieben.
- `MediaBrowser4Lib\Objects\ExtraData.cs`
  - Sucht Begleitdateien neben dem Original.
- `MediaBrowser4Lib\DB\Api\ApiInsert.cs`
  - `GetThumbnail`, Dateizeit und Dateigröße werden clientseitig gelesen.
- `MediaBrowser4Lib\API\MediafileInsert.cs`
  - älterer dateibasierter Insert-Pfad.

Konsequenz: Beim Anlegen eines Katalogeintrags kann der Client diese Werte einmalig aus einer
lokal vorhandenen Datei ermitteln und im Insert-DTO mitschicken. Die Originaldatei selbst wird
dadurch nicht von der API gespeichert.

### 3.5 Import und Scan

- `MediaBrowserWPF\MainWindow.xaml.cs`
  - `MenuItemAddSingle_Click`
  - `MenuItemAddDirectory_Click`
  - `MenuItemAddFromClipboard_Click`
  - `MenuItemAddSDCard_Click`
  - `MenuItemImportMetadata_Click`
  - `MenuItemImportGeoData_Click`
  - `LoadVirtual`
  - `ScanRecursive`
- `MediaBrowserWPF\Utilities\FilesAndFolders.cs`
  - Sammelt Dateien und startet den Import.
- `MediaBrowserWPF\Dialogs\CopySdCardDialog.xaml.cs`
  - Kopiert Dateien von der Speicherkarte und liest EXIF.
- `MediaBrowserWPF\UserControls\FolderContainer\FolderTree.xaml.cs`
  - Prüft lokale Ordner und zählt Dateien.

Konsequenz: Der bisherige Import setzt einen beschreibbaren Dateispeicher voraus und ist im
API-Modus **nicht unverändert nutzbar**. Möglichkeiten:

1. API-Modus ist zunächst nur Katalog-/Lesezugriff; Import bleibt dem Dateimodus vorbehalten.
2. Ein separater Ingest-Prozess außerhalb der MediaBrowser-API übernimmt das Kopieren/Uploading.
3. Ein späterer Client-Upload geht direkt in einen externen Objektspeicher; die MediaBrowser-API
   bekommt nur den bereits vorhandenen Content-Verweis und schreibt keine Dateien.

### 3.6 Ändernde Dateioperationen

- `MediaBrowser4Lib\Utilities\FilesAndFolders.cs`
  - `File.Copy`, `File.Move`, `File.Delete`, ReadOnly-Attribute.
- `MediaBrowser4Lib\SmartRename\RenameFile.cs`
  - `File.Move`, Kollisionsprüfung.
- `MediaBrowser4Lib\DB\Maria\MariaDbProvider.cs`
  - `RemoveAndRecycle`, Attachment-Recycling, Rename.
- `MediaBrowserWPF\UserControls\ThumbListContainer\ThumblistContainer.xaml.cs`
  - lokale Existenzprüfung, Rename, OpenAs.
- `MediaBrowserWPF\UserControls\ThumbListContainer\ThumblistContainerContextMenu.xaml.cs`
  - Copy, Move, Export, ffmpeg-Aufrufe, Dateistreams.
- `MediaBrowserWPF\UserControls\ThumbListContainer\CopyMenuItem.xaml.cs`
  - `FileInfo.CopyTo`.
- `MediaBrowserWPF\UserControls\AttachmentsContainer\AttachmentsContainer.xaml.cs`
  - Anhänge öffnen, verschieben, löschen, recyceln.

Konsequenz: Diese Aktionen dürfen im API-Modus nicht gegen den Content Store ausgeführt werden.
Sie werden deaktiviert. Reine Katalogänderungen wie Favorit, Soft-Delete, Kategorie, Rolle und
Priorität bleiben möglich. Ein „Löschen“ im API-Modus ist ausschließlich ein Soft-Delete oder
eine Katalogbereinigung, kein Dateilöschen.

### 3.7 Anhänge und Sidecar-Dateien

- `MediaBrowser4Lib\Objects\Attachment.cs`
  - `FullName`, `FileInfo`, Existenzprüfung.
- `MediaBrowser4Lib\Utilities\FilesAndFolders.cs`
  - Anhänge kopieren/verschieben.
- `MediaBrowserWPF\UserControls\AttachmentsContainer\AttachmentsContainer.xaml.cs`
- `MediaBrowserWPF\Utilities\CategoryTreeWebPage.cs`
  - `.ht_`, `.mp3`, `.wav`, Preview-Ordner und relative Pfade.
- `MediaBrowser4Lib\Objects\ExtraData.cs`
  - `.wav`, `.mp3`, `.fcd` und weitere Begleitdateien.
- `MediaBrowser4Lib\Objects\MediaItemVideo.cs`
  - `.THM`-Dateien.
- `MediaBrowser4Lib\DB\Maria\MariaDbProvider.cs`
  - `WriteToPreviewDB` liest `.fcd` und Bilddateien.

Konsequenz: Anhänge und Sidecars können im API-Modus nur **gelesen** werden, wenn sie bereits im
read-only Content Store liegen. Upload, Anlegen, Verschieben und Löschen sind deaktiviert.
Read-only-Endpunkte für Anhänge und Sidecars sind möglich; schreibende Endpunkte nicht.

### 3.8 Export, Snapshot und Webgalerie

- `MediaBrowserWPF\Utilities\TakeSnapshot.xaml.cs`
  - Kopiert Originale und schreibt exportierte Bilder.
- `MediaBrowserWPF\UserControls\ThumbListContainer\CopyMenuItem.xaml.cs`
  - Dateinamen und Pfade für Export.
- `MediaBrowserWPF\Utilities\CategoryTreeWebPage.cs`
  - Erzeugt eine Webgalerie mit lokalen Dateipfaden und Thumbnails.
- `MediaBrowserWPF\UserControls\ThumbListContainer\ThumblistContainerContextMenu.xaml.cs`
  - ffmpeg-/Batch-Exporte mit `FullName`.

Konsequenz: Export und Snapshot bleiben **Client-Funktionen**. Sie schreiben nur in einen lokalen
Exportordner. Voraussetzung ist, dass der Client das Original zuvor über den read-only
Content-Endpunkt heruntergeladen hat.

### 3.9 Fehlende-Dateien-Erkennung

- `MediaBrowserWPF\UserControls\ThumbListContainer\ThumblistContainer.xaml.cs`
- `MediaBrowserWPF\Viewer\MediaViewer.xaml.cs`
- `MediaBrowserWPF\Viewer\ViewerBaseControl.xaml.cs`
- `MediaBrowser4Lib\DB\Maria\MariaDbProvider.cs`
  - `GetMediaItemsWithMissingFiles`
- `MediaBrowser4Lib\DB\Api\ApiProvider.cs`
  - `GetMediaItemsWithMissingFiles` ist aktuell nicht implementiert.

Konsequenz: Im API-Modus ist „fehlende Datei“ eine serverseitige read-only Prüfung. Das Backend
darf den Content Store lesen und `ContentAvailable` im DTO melden. Der Client darf nicht
`File.Exists` auf einem lokalen Katalogpfad ausführen.

### 3.10 Lokaler Cache und Vorschaudatenbank

- `MediaBrowser4Lib\Objects\MediaItem.cs`
  - `SetCachedImage`, `ImageCachePath`
- `MediaBrowserWPF\UserControls\RgbImage\ImageControl.xaml.cs`
  - lokaler Image-Cache
- `MediaBrowser4Lib\DB\Maria\MariaDbProvider.cs`
  - Preview-Dateien unter `TempPath`
- `MediaBrowserWPF\MainWindow.xaml.cs`
  - „Lokalen Medien-Cache leeren“

Konsequenz: Der Client-Cache bleibt erlaubt. Er wird ausschließlich vom Client geschrieben,
niemals von der API. Das Backend darf keine Preview-/Temp-Dateien anlegen; Vorschauen müssen aus
der Datenbank, dem read-only Content Store oder speicherintern erzeugt werden.

## 4. Content-Strategien

### 4.1 Read-only Content Store

Das Backend bekommt einen read-only eingebundenen Speicher:

```text
/mnt/media-readonly
```

oder einen read-only Object Store. Die API löst `FOLDERS.FOLDERNAME` + `MEDIAFILES.FILENAME`
gegen diesen Store auf und streamt die Datei. Sie schreibt nichts.

Vorteile:

- Originale bleiben zentral.
- Keine API-Schreiboperationen auf dem Dateisystem.
- Range-Streaming für große Dateien möglich.

Voraussetzungen:

- Pfadnormalisierung und Schutz vor Directory Traversal.
- Konfigurierbarer Content-Root.
- Read-only-Mount und restriktive Rechte für den API-Prozess.

### 4.2 Kein Content Store

Das Backend liefert nur Katalogdaten und Thumbnails. Originalansicht, Video und Export werden
deaktiviert. Das ist der einfachste und sicherste Modus.

### 4.3 Externer Ingest

Ein separater Prozess außerhalb der MediaBrowser-API schreibt Dateien in den Content Store:

- NAS-Sync
- Syncthing/rsync
- dedizierter Upload-Service
- Objektspeicher-Ingest

Die MediaBrowser-API liest anschließend nur den bereits vorhandenen Content. Sie erhält keine
Upload-Endpunkte und führt keine Dateiänderungen aus.

## 5. Backend-Endpunkte

Die folgenden Endpunkte bilden den API-Modus ab. **Alle Content-Endpunkte sind read-only.**
Es gibt keine Upload-, Copy-, Move-, Rename-, Delete- oder Recycle-Endpunkte für Dateien.

### 5.1 Authentifizierung

| Methode | Pfad | Zweck | Request | Response | Dateisystem |
| --- | --- | --- | --- | --- | --- |
| POST | `/api/v1/auth/login` | Login | `LoginRequest` | `TokenResponse` | keine |
| POST | `/api/v1/auth/refresh` | Access-Token erneuern | `RefreshRequest` | `TokenResponse` | keine |
| POST | `/api/v1/auth/logout` | Refresh-Token widerrufen | `RefreshRequest` | `204` | keine |

`TokenResponse`:

```json
{
  "accessToken": "...",
  "refreshToken": "...",
  "tokenType": "Bearer",
  "expiresIn": 600
}
```

### 5.2 Katalog und Metadaten

| Methode | Pfad | Zweck | Request | Response | DB | Dateisystem |
| --- | --- | --- | --- | --- | --- | --- |
| GET | `/api/v1/catalog/validate` | Katalog prüfen | – | `CatalogInfo` | lesen | keine |
| POST | `/api/v1/catalog/create` | Schema/Defaults anlegen | – | `204` | schreiben | keine |
| GET | `/api/v1/catalog/properties` | Properties lesen | – | `map<string,string>` | lesen | keine |
| PUT | `/api/v1/catalog/properties` | Properties speichern | `map<string,string>` | `204` | schreiben | keine |
| GET | `/api/v1/catalog/last-folders` | letzte Ordner | – | `map<string,date-time>` | lesen | keine |
| GET | `/api/v1/catalog/capabilities` | API-Fähigkeiten | – | `Capabilities` | lesen | keine |

`Capabilities`:

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

### 5.3 Ordner und Kategorien

| Methode | Pfad | Zweck | Request | Response | DB | Dateisystem |
| --- | --- | --- | --- | --- | --- | --- |
| GET | `/api/v1/folders` | Ordnerliste | – | `Folder[]` | lesen | keine |
| POST | `/api/v1/folders/ensure` | Ordner anlegen/finden | `Folder` | `Folder` | schreiben | keine |
| DELETE | `/api/v1/folders/{id}` | Ordnerdatensatz löschen | – | `204` | schreiben | keine |
| GET | `/api/v1/folders/{id}/count` | Medienanzahl | – | `integer` | lesen | keine |
| GET | `/api/v1/categories/tree` | Kategoriebaum | – | `Category[]` | lesen | keine |
| GET | `/api/v1/categories?variationId=...` | Kategorien einer Variation | – | `Category[]` | lesen | keine |
| POST | `/api/v1/categories` | Kategorie speichern | `Category` | `Category` | schreiben | keine |
| POST | `/api/v1/categories/by-path` | Kategorie per Pfad | `PathRequest` | `Category` | schreiben | keine |
| DELETE | `/api/v1/categories/{id}` | Kategorie löschen | – | `204` | schreiben | keine |
| GET | `/api/v1/categories/geo` | Geo-Kategorien | Query | `Category[]` | lesen | keine |
| POST | `/api/v1/categories/from-media` | Kategorien zählen | `variationIds[]` | `CategoryCount[]` | lesen | keine |
| GET | `/api/v1/categories/identifiers/{variationId}` | Kategorie-IDs | – | `integer[]` | lesen | keine |

### 5.4 Medien lesen und suchen

| Methode | Pfad | Zweck | Request | Response | DB | Dateisystem |
| --- | --- | --- | --- | --- | --- | --- |
| POST | `/api/v1/mediafiles/query` | Medien suchen/filtern | `MediaQuery` | `MediaItem[]` | lesen | keine |
| POST | `/api/v1/mediafiles/sql-query` | kontrollierte SELECT-Abfrage | `SqlQuery` | `MediaItem[]` | lesen | keine |
| GET | `/api/v1/mediafiles/{id}` | einzelnes Medium | – | `MediaItem` | lesen | keine |
| GET | `/api/v1/mediafiles/{id}/description` | Beschreibung | – | `Text` | lesen | keine |
| GET | `/api/v1/mediafiles/{id}/variations` | Variationen | Query | `Variation[]` | lesen | keine |
| POST | `/api/v1/mediafiles/insert` | Katalogdatensatz anlegen | `MediaInsert` | `InsertResult` | schreiben | keine Datei |
| POST | `/api/v1/mediafiles/from-xml` | Katalog aus XML anlegen | `XmlImport` | `MediaItem[]` | schreiben | keine Datei |
| POST | `/api/v1/mediafiles/thumbs` | Thumbnails laden | `int[]` | `Thumb[]` | lesen | keine |
| GET | `/api/v1/mediafiles/checksum-count` | Checksum-Treffer | `checksum` | `integer` | lesen | keine |
| GET | `/api/v1/mediafiles/sql-request` | SQL-Request bauen | `mediaItemIds` | `SqlQuery` | lesen | keine |
| GET | `/api/v1/mediafiles/missing-files` | fehlende Originale | Query | `MediaItem[]` | lesen | read-only stat |
| GET | `/api/v1/mediafiles/missing-thumbs` | fehlende Thumbs | Query | `MediaItem[]` | lesen | keine |

`MediaQuery`-Modi bleiben wie in `docs/openapi.yaml`; `MISSING_FILES` darf ausschließlich
read-only gegen den Content Store prüfen.

### 5.5 Originalcontent, Thumbnails und Vorschauen

| Methode | Pfad | Zweck | Request | Response | DB | Dateisystem |
| --- | --- | --- | --- | --- | --- | --- |
| GET/HEAD | `/api/v1/mediafiles/{id}/content` | Original streamen | `Range`, `If-None-Match` | Binärstream | Content-Locator lesen | **nur lesen** |
| GET | `/api/v1/variations/{id}/thumb` | Thumbnail streamen | `width`, `height` | `image/jpeg` | `THUMBS` lesen | keine |
| GET | `/api/v1/previews/{variationId}` | Vorschau | `width`, `height` | `Preview` | lesen oder im Speicher erzeugen | keine Schreibdatei |
| GET | `/api/v1/previews/exists` | Vorschau vorhanden | Query | `boolean` | lesen | keine |
| GET | `/api/v1/previews/variation-ids` | Vorschau-IDs | Query | `integer[]` | lesen | keine |
Content-Streaming-Anforderungen:

- `Content-Type` aus Dateiendung/MIME-Metadaten.
- `Content-Length`.
- `Accept-Ranges: bytes` und `Range`-Support.
- `ETag` und/oder `Last-Modified`.
- `401`/`403` bei fehlender Berechtigung.
- `404` oder `409`, wenn kein Content Store konfiguriert ist.
- Keine Temporärdateien auf dem Server.
- Keine Dateisystem-Schreiboperationen.

Die direkte URL sollte nur mit gültigem Bearer-Token oder einem kurzlebigen signierten
Read-only-Token funktionieren. Für `MediaElement` kann ein signierter Read-only-Link nötig sein.

Nicht bereitgestellt werden:

- `POST /mediafiles/{id}/content`
- `POST /uploads`
- `POST /previews/build`
- `DELETE /previews`
- `PUT /variations/{id}/thumb` (Thumbnail-DB-Schreibpfad bleibt optional, aber kein Dateiupload)

Preview-Dateien und temporäre Dateien dürfen auf dem Server nicht entstehen. Vorschauen müssen
aus der Datenbank, dem read-only Content Store oder speicherintern erzeugt werden.

### 5.6 Variationen, Layer, Metadaten, Beschreibungen

| Methode | Pfad | Zweck | Request | Response | DB | Dateisystem |
| --- | --- | --- | --- | --- | --- | --- |
| POST | `/api/v1/mediafiles/{id}/variations` | Variation anlegen | `VariationCreate` | `Variation` | schreiben | keine Datei |
| DELETE | `/api/v1/mediafiles/{id}/variations?name=...` | Variation löschen | Query | `204` | schreiben | keine Datei |
| POST | `/api/v1/mediafiles/{id}/variations/replace` | Variationen ersetzen | `Variation[]` | `Variation[]` | schreiben | keine Datei |
| PUT | `/api/v1/variations/{id}` | Variation ändern | `Variation` | `204` | schreiben | keine Datei |
| POST | `/api/v1/variations/{id}/default` | Standardvariation | – | `204` | schreiben | keine Datei |
| GET | `/api/v1/variations/{id}/layers` | Layer lesen | – | `Layer[]` | lesen | keine |
| PUT | `/api/v1/variations/{id}/layers` | Layer speichern | `Layer[]` | `204` | schreiben | keine Datei |
| POST | `/api/v1/metadata/query` | Metadaten lesen | `mediaIds[]` | `MetaData[]` | lesen | keine |
| GET | `/api/v1/metadata/keys` | Metadaten-Keys | – | `string[]` | lesen | keine |
| POST | `/api/v1/descriptions/query` | Beschreibungen lesen | `mediaIds[]` | `Description[]` | lesen | keine |
| POST | `/api/v1/descriptions` | Beschreibung speichern | `Description` | `204` | schreiben | keine Datei |

Variationen und Layer sind reine Katalog-/DB-Objekte. Die API erzeugt dabei keine Bilddateien
auf dem Server.

### 5.7 Anhänge und Sidecars

| Methode | Pfad | Zweck | Request | Response | DB | Dateisystem |
| --- | --- | --- | --- | --- | --- | --- |
| POST | `/api/v1/attachments/query` | Anhänge lesen | `mediaIds[]` | `Attachment[]` | lesen | keine |
| GET | `/api/v1/attachments/{id}/content` | Anhang streamen | – | Binärstream | lesen | **nur lesen** |
| GET | `/api/v1/mediafiles/{id}/sidecars` | Sidecars listen | – | `Sidecar[]` | lesen | **nur lesen** |
| GET | `/api/v1/mediafiles/{id}/sidecars/{type}` | Sidecar streamen | – | Binärstream | lesen | **nur lesen** |

Nicht vorgesehen:

- `POST /attachments`
- `PUT /attachments/{id}`
- `DELETE /attachments/{id}`
- `POST /sidecars`
- Datei-Uploads oder Datei-Löschungen

Anhänge und Sidecars müssen bereits im read-only Content Store existieren.

### 5.8 GPS, Meteorologie, Rollen, Requests, Maintenance

| Methode | Pfad | Zweck | Request | Response | DB | Dateisystem |
| --- | --- | --- | --- | --- | --- | --- |
| GET | `/api/v1/roles` | Rollen | – | `Role[]` | lesen | keine |
| GET | `/api/v1/gps/files` | GPS-Dateien | – | `GpsFile[]` | lesen | keine |
| GET | `/api/v1/gps` | GPS-Punkte | `from`,`to` | `GeoPoint[]` | lesen | keine |
| GET | `/api/v1/gps/nearest` | nächster Punkt | `date` | `GeoPoint` | lesen | keine |
| POST | `/api/v1/gps/points` | GPS-Punkte speichern | `GeoPoint[]` | `204` | schreiben | keine Datei |
| DELETE | `/api/v1/gps/files` | GPS-Datensatz löschen | `fileTime` | `204` | schreiben | keine Datei |
| GET | `/api/v1/meteorology` | Meteorologie lesen | `from`,`to` | `Meteorology[]` | lesen | keine |
| POST | `/api/v1/meteorology` | Meteorologie speichern | `Meteorology[]` | `204` | schreiben | keine Datei |
| GET | `/api/v1/requests` | gespeicherte Requests | – | `UserRequest[]` | lesen | keine |
| POST | `/api/v1/requests` | Request speichern | `UserRequest` | `UserRequest` | schreiben | keine Datei |
| DELETE | `/api/v1/requests/{id}` | Request löschen | – | `204` | schreiben | keine Datei |
| GET | `/api/v1/requests/{id}/sql` | Request-SQL | – | `Text` | lesen | keine |
| PUT | `/api/v1/requests/{id}/sql` | Request-SQL speichern | `Text` | `204` | schreiben | keine Datei |
| POST | `/api/v1/maintenance/clean` | Katalog bereinigen | – | `204` | schreiben | **keine Datei** |
| POST | `/api/v1/duplicates/check` | Duplikat prüfen | `MediaAction` | `boolean` | lesen | keine |
| GET | `/api/v1/duplicates/{id}` | Duplikate | `criteria` | `Duplicate[]` | lesen | keine |

`maintenance/clean` darf ausschließlich Datenbankzeilen bereinigen, keine Dateien löschen.

### 5.9 Katalogaktionen ohne Dateisystemzugriff

`POST /api/v1/mediafiles/actions` bleibt für reine Katalogaktionen:

- `SET_ROLE`
- `SET_PRIORITY`
- `SET_BOOKMARK`
- `SET_DELETED` (Soft-Delete)
- `SET_TRASH` (Soft-Delete/Trash-Flag)
- `SET_GEODATA`
- `ADJUST_MEDIA_DATE`
- `ADD_VIEW_TIME`
- `CATEGORIZE`
- `UNCATEGORIZE`
- `UPDATE_DUPLICATE`
- `ROTATE_90` nur als Metadaten-/Variationsaktion, ohne Dateisystemzugriff

Nicht mehr zulässig:

- `RENAME_FILE`
- `MOVE_FILE`
- `COPY_FILE`
- `DELETE_FILE`
- `RECYCLE_FILE`
- `RESTORE_FILE`
- `REMOVE_AND_RECYCLE`

`REMOVE_FROM_DB` darf nur den Katalogdatensatz löschen, nicht die Datei.

## 6. Client-Architektur

### 6.1 Neue Abstraktion

```csharp
public interface IMediaContentReader
{
    bool Exists(MediaItem item);
    Stream OpenRead(MediaItem item);
    Uri GetReadOnlyStreamUri(MediaItem item, ContentPurpose purpose);
    Task<string> EnsureLocalCopyAsync(MediaItem item, ContentPurpose purpose);
    bool CanReadContent { get; }
}
```

Implementierungen:

- `LocalMediaContentReader`: bisheriges Verhalten für SQLite/MariaDB-Dateimodus.
- `ApiMediaContentReader`: read-only Content-Streaming und lokaler Client-Cache.
- `UnavailableMediaContentReader`: Content-Funktionen deaktiviert.

Schreibmethoden gehören nicht in diese Schnittstelle. Schreibende Dateioperationen bleiben
Aufgabe des lokalen Modus oder eines separaten Ingest-Prozesses.

`ContentPurpose` steuert die Strategie, z. B. `Thumbnail`, `Preview`, `ViewImage`, `PlayVideo`,
`Export`, `Metadata`, `Sidecar`.

### 6.2 `MediaItem` anpassen

- `FullName` bleibt die logische Katalogreferenz.
- `FileObject` darf im API-Modus nicht ungeprüft die Originaldatei liefern.
- Neue Eigenschaften:
  - `ContentAvailable`
  - `ContentLength`
  - `ContentType`
  - `CanStream`
- Zugriffe auf `FileObject` in kritischen Pfaden durch `IMediaContentReader` ersetzen.
- `FileObject` kann im API-Modus auf eine Client-Cache-Datei zeigen; bei fehlender Kopie
  muss ein klarer Fehler oder ein asynchroner Download erfolgen.

### 6.3 UI-Verhalten

- Fehlender Content: Thumbnail anzeigen, Original nicht öffnen.
- Video ohne lokalen Cache: read-only Streaming oder Download mit Fortschritt.
- Nicht unterstützte Aktion: Menüpunkt deaktivieren und Tooltip anzeigen.
- Keine `File.Exists`-Prüfung auf Katalogpfaden im API-Modus.
- Keine Windows-Pfade in Webgalerien oder externen Playern verwenden.
- „Datei löschen/umbenennen/verschieben“ im API-Modus ausblenden.
- „Aus Katalog entfernen“ (DB-only) weiterhin anbieten.

## 7. Umsetzungsphasen

### Phase 1: Bestandsaufnahme und Capabilities

- `IMediaContentReader` einführen, zunächst nur `LocalMediaContentReader`.
- `ApiProvider` liefert Capability-Daten.
- Alle direkten `FileObject`-Zugriffe erfassen und als TODO markieren.
- Schreibende Dateifunktionen im API-Modus deaktivieren.

### Phase 2: Read-only Content-API

- `GET/HEAD /mediafiles/{id}/content` mit Range/ETag im Backend.
- Read-only Content Root konfigurieren.
- Client-Download in lokalen Cache.
- Bilder, Panoramen und 3D-Viewer auf den Reader umstellen.

### Phase 3: Video und Streaming

- Signierte Read-only-Streaming-URLs oder lokaler Video-Cache.
- `VideoPlayer`, `nVlc`, `VideoLanDotNet`, `MediaViewer` und `TakeSnapshot` anpassen.
- Range-Requests und Abbruch/Timeout testen.

### Phase 4: Importstrategie

- API-Modus ohne Datei-Upload: Import deaktivieren oder auf vorhandenen Content-Store verweisen.
- Optional separater Ingest-Prozess außerhalb der API.
- `ApiInsert` ohne Originaldatei-Upload; nur Katalog-/Thumbnail-Daten.
- Keine API-Schreiboperation auf dem Content Store.

### Phase 5: Katalogmutationen

- Reine DB-Aktionen weiterhin über `/mediafiles/actions`.
- Dateiaktionen im API-Modus entfernen/deaktivieren.
- `FilesAndFolders`, `RenameFile`, `CopyMenuItem` und `ThumblistContainer` entsprechend schützen.

### Phase 6: Anhänge, Sidecars und Export

- Read-only Attachment-/Sidecar-Endpunkte.
- Export/Snapshot lädt Original in den Client-Cache.
- Webgalerie verwendet API-URLs statt lokaler Pfade.
- Keine Attachment-/Sidecar-Uploads über die API.

### Phase 7: Fehlende Dateien und Cache

- Serverseitige `ContentAvailable`-Prüfung (read-only).
- `GetMediaItemsWithMissingFiles` über API-Status.
- Cache-LRU, ETag-Invalidierung und Aufräumen.
- Verhalten bei Offline-Backend und abgelaufenem Token.

## 8. Akzeptanzkriterien

- MediaBrowserWpf startet und zeigt Katalog/Thumbnails ohne lokale Originaldateien.
- Ein Bild aus einem API-Katalog lässt sich ohne lokale Originaldatei anzeigen.
- Ein Video lässt sich read-only streamen oder in den Client-Cache laden.
- Der API-Modus führt keine Datei-Schreiboperationen aus.
- Import, Rename, Move, Delete, Copy und Recycle von Dateien sind im API-Modus deaktiviert.
- Soft-Delete und Katalogbereinigung ändern nur Datenbankzeilen.
- Anhänge und Sidecars können nur gelesen werden, wenn sie im read-only Content Store liegen.
- Fehlende Serverdateien werden korrekt angezeigt, ohne lokale `File.Exists`-Fehlalarme.
- Der Client schreibt nur in seinen eigenen Cache oder Exportordner.
- Token- und Content-URLs werden nicht geloggt.

## 9. Offene Entscheidungen

1. Wird ein read-only Content Root bereitgestellt oder bleibt der API-Modus katalog-only?
2. Werden Videos gestreamt oder immer lokal gecacht?
3. Werden signierte Read-only-URLs für `MediaElement` benötigt?
4. Wie groß darf der Client-Cache werden und wann wird er geleert?
5. Welcher externe Ingest-Prozess befüllt den Content Store?
6. Werden Anhänge und Sidecars im Content Store oder in der Datenbank gehalten?
