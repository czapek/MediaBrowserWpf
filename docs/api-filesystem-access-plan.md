# API-Modus: Dateisystemzugriffe und Migrationsplan

## 1. Ziel

Im API-Modus liegt der Medienkatalog auf einem Spring-Boot-Backend. Der Client darf nicht davon
ausgehen, dass die Originaldateien eines `MediaItem` lokal vorhanden oder über einen
Windows-Pfad erreichbar sind.

Dieses Dokument sammelt die Stellen, an denen MediaBrowserWpf heute direkt auf Dateien zugreift,
und beschreibt eine schrittweise Lösung.

Leitentscheidung:

- **Import:** Der Client liest die vom Benutzer ausgewählten Dateien lokal und lädt sie zum Backend
  hoch.
- **Anzeige:** Originalmedien werden über die API gestreamt oder bei Bedarf in einen lokalen Cache
  geladen.
- **Schreiboperationen:** Rename, Move, Delete, Recycle, Attachment-Änderungen usw. laufen als
  API-Aktionen gegen den serverseitigen Speicher.
- **Kein stilles Failover auf lokale Pfade:** Ist im API-Modus kein Content-Endpunkt verfügbar,
  wird die Funktion deaktiviert oder verständlich als nicht verfügbar gemeldet.

## 2. Zentrale Zugriffspunkte

### 2.1 Medienmodell

- `MediaBrowser4Lib\Objects\MediaItem.cs`
  - `FullName` setzt sich aus `Foldername` und `Filename` zusammen.
  - `FileObject` erzeugt bei Bedarf ein `FileInfo` aus `FullName`.
  - `SetCachedImage`, `Rename` und verschiedene Anzeige-/Tooltip-Pfade verwenden lokale Pfade.
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

Konsequenz: `FileObject` und `FullName` dürfen im API-Modus nicht mehr als verlässliche
Originaldatei-Referenz dienen.

### 2.2 Thumbnails und Vorschauen

- `MediaBrowser4Lib\Objects\MediaItemBitmap.cs`
  - `GetThumbnail` liest EXIF und das Originalbild.
  - `GetImage` liest das Originalbild.
- `MediaBrowser4Lib\Objects\MediaItemVideo.cs`
  - `GetThumbnail` liest Videos über NReco, DirectShow oder MediaInfo.
- `MediaBrowser4Lib\DB\Api\ApiProvider.cs`
  - `GetThumbJpegData`, `GetThumbsForAllMediaItems`, `GetImagePreviewDB`,
    `GetFaceDetectionPreviewDB` greifen bereits auf API-Endpunkte zu.
- `MediaBrowserWPF\Utilities\TakeSnapshot.xaml.cs`
  - Export und Snapshot benötigen Originaldaten.

Konsequenz: Thumbnails können aus `THUMBS`/API geliefert werden. Große Vorschauen, Snapshots und
Exporte brauchen entweder einen Content-Stream oder eine lokale Kopie.

### 2.3 Bild-, Video- und Spezialviewer

Direkte Dateizugriffe:

- `MediaBrowserWPF\UserControls\RgbImage\ImageControl.xaml.cs`
  - `File.Exists`, `new Uri(FileObject.FullName)`, `LoadImage(FileObject.FullName)`
- `MediaBrowserWPF\UserControls\RgbImage\ViewerSource.cs`
  - `FileObject.Exists`, `Image.FromFile`
- `MediaBrowserWPF\Viewer\MediaViewer.xaml.cs`
  - `File.Exists`, fehlende Datei-Anzeige, `Directory.GetFiles` für Sidecars
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
ist ein kurzlebiger signierter Download-/Streaming-Link oder ein lokaler Cache sinnvoll.

### 2.4 Metadaten, Checksummen und Datumswerte

- `MediaBrowser4Lib\Utilities\Crypto.cs`
  - MD5 wird aus der Originaldatei gelesen.
- `MediaBrowser4Lib\Utilities\DateAndTime.cs`
  - MediaDate wird aus Dateiname und Dateizeit abgeleitet.
- `MediaBrowser4Lib\Utilities\FilesAndFolders.cs`
  - Dateiexistenz, Sidecar-Dateien, Kopieren und Verschieben.
- `MediaBrowser4Lib\Objects\ExtraData.cs`
  - Sucht Begleitdateien neben dem Original.
- `MediaBrowser4Lib\DB\Api\ApiInsert.cs`
  - `GetThumbnail`, Dateizeit und Dateigröße werden aktuell clientseitig gelesen.
- `MediaBrowser4Lib\API\MediafileInsert.cs`
  - älterer Insert-Pfad, ebenfalls dateibasiert.

Konsequenz: Beim Upload kann der Client diese Werte einmalig lokal ermitteln und im Insert-DTO
mitschicken. Spätere serverseitige Prüfungen müssen auf gespeicherte Metadaten und Checksummen
zurückgreifen.

### 2.5 Import und Scan

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

Konsequenz: Der Import bleibt lokal initiiert, benötigt aber einen Upload-Schritt. Der Server darf
nicht erwarten, dass ein Windows-Pfad des Clients auf dem Backend existiert.

### 2.6 Ändernde Dateioperationen

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

Konsequenz: Diese Aktionen müssen im API-Modus über API-Aktionen laufen. Lokale Dateioperationen
sind nur für den lokalen Cache oder für den ursprünglichen Upload erlaubt.

### 2.7 Anhänge und Sidecar-Dateien

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

Konsequenz: Anhänge und Sidecars brauchen eigene Upload-/Download-Endpunkte oder müssen in das
Medienobjekt eingebettet werden. Die Existenz von Nachbardateien im selben Verzeichnis darf im
API-Modus nicht vorausgesetzt werden.

### 2.8 Export, Snapshot und Webgalerie

- `MediaBrowserWPF\Utilities\TakeSnapshot.xaml.cs`
  - Kopiert Originale und schreibt exportierte Bilder.
- `MediaBrowserWPF\UserControls\ThumbListContainer\CopyMenuItem.xaml.cs`
  - Dateinamen und Pfade für Export.
- `MediaBrowserWPF\Utilities\CategoryTreeWebPage.cs`
  - Erzeugt eine Webgalerie mit lokalen Dateipfaden und Thumbnails.
- `MediaBrowserWPF\UserControls\ThumbListContainer\ThumblistContainerContextMenu.xaml.cs`
  - ffmpeg-/Batch-Exporte mit `FullName`.

Konsequenz: Export kann lokal bleiben, benötigt aber zuerst einen Download der Originale. Die
Webgalerie muss auf API-URLs statt auf lokale Pfade umgestellt werden.

### 2.9 Fehlende-Dateien-Erkennung

- `MediaBrowserWPF\UserControls\ThumbListContainer\ThumblistContainer.xaml.cs`
- `MediaBrowserWPF\Viewer\MediaViewer.xaml.cs`
- `MediaBrowserWPF\Viewer\ViewerBaseControl.xaml.cs`
- `MediaBrowser4Lib\DB\Maria\MariaDbProvider.cs`
  - `GetMediaItemsWithMissingFiles`
- `MediaBrowser4Lib\DB\Api\ApiProvider.cs`
  - `GetMediaItemsWithMissingFiles` ist aktuell nicht implementiert.

Konsequenz: Im API-Modus ist „fehlende Datei“ eine serverseitige Eigenschaft. Das Backend muss
den Status der gespeicherten Originaldatei prüfen und im MediaItem/DTO melden. Der Client darf
nicht `File.Exists` auf einem lokalen Pfad ausführen.

### 2.10 Lokaler Cache und Vorschaudatenbank

- `MediaBrowser4Lib\Objects\MediaItem.cs`
  - `SetCachedImage`, `ImageCachePath`
- `MediaBrowserWPF\UserControls\RgbImage\ImageControl.xaml.cs`
  - lokaler Image-Cache
- `MediaBrowser4Lib\DB\Maria\MariaDbProvider.cs`
  - Preview-Dateien unter `TempPath`
- `MediaBrowserWPF\MainWindow.xaml.cs`
  - „Lokalen Medien-Cache leeren“

Konsequenz: Der lokale Cache bleibt erlaubt, ist aber nur eine Kopie. Er darf nicht als
Originaldatenquelle missverstanden werden und muss bei API-Änderungen invalidiert werden können.

## 3. Optionen

### Option A: Alles lokal cachen

Der Client lädt Originale bei Bedarf in einen lokalen Cache und die bestehende dateibasierte
Logik bleibt weitgehend unverändert.

Vorteile:

- Wenig Änderungen an Viewern und MediaProcessing.
- Bestehende Export-, Snapshot- und Video-Pfade funktionieren.

Nachteile:

- Große Downloads, besonders bei Videos.
- Cache-Verwaltung, Löschung und Aktualisierung nötig.
- Startzeit und Speicherplatz können stark steigen.

### Option B: Vollständig streamen

Originale werden nur über HTTP gestreamt; lokale Dateizugriffe entfallen.

Vorteile:

- Kein großer lokaler Speicherbedarf.
- Zentrale Datenhaltung.

Nachteile:

- Große Umbauten an Viewern, DirectShow, NPOI/NReco, ffmpeg und Export.
- `MediaElement`/DirectShow brauchen oft einen lokalen Pfad.
- Authentifizierung bei Streaming muss geklärt werden.

### Option C: Hybrider Ansatz (Empfehlung)

- Bilder und Vorschauen: API-Streaming oder kleiner lokaler Cache.
- Videos/Panoramen/3D: bevorzugt lokaler Cache oder signierter Streaming-Link.
- Import: Originaldatei wird hochgeladen.
- Schreiboperationen: API-Aktionen.
- Export/Snapshot: lädt das Original bei Bedarf herunter.
- Nicht unterstützte Funktionen werden im API-Modus deaktiviert.

Dieser Ansatz ist mit der bestehenden Codebasis am pragmatischsten.

## 4. Benötigte API-Erweiterungen

### 4.1 Originalinhalt

```text
GET /api/v1/mediafiles/{id}/content
```

Anforderungen:

- `Content-Type`, `Content-Length`
- `Range`-Requests für Video und große Dateien
- `ETag`/`Last-Modified`
- Autorisierung über Bearer-Token
- optional signierte, kurzlebige URLs für `MediaElement`/externe Player

Optional:

```text
GET /api/v1/mediafiles/{id}/content?variationId=...
GET /api/v1/variations/{id}/content
```

### 4.2 Upload

```text
POST /api/v1/mediafiles/insert
POST /api/v1/mediafiles/{id}/content
POST /api/v1/uploads
```

Der Upload muss Originaldaten, Dateiname, Größe, Checksumme, MIME-Typ und optional Thumbnail
enthalten. Für große Dateien ist ein mehrstufiger Upload oder ein Pre-Signed-Upload sinnvoll.

### 4.3 Dateiaktionen

```text
POST /api/v1/mediafiles/actions
```

Neue Aktionen:

- `RENAME_FILE`
- `MOVE_FILE`
- `DELETE_FILE`
- `RECYCLE_FILE`
- `COPY_FILE`
- `RESTORE_FILE`

Die Aktion muss den serverseitigen Speicher ändern und den Katalog aktualisieren.

### 4.4 Anhänge und Sidecars

```text
GET /api/v1/attachments/{id}/content
POST /api/v1/attachments
PUT /api/v1/attachments/{id}
DELETE /api/v1/attachments/{id}
GET /api/v1/mediafiles/{id}/sidecars
POST /api/v1/mediafiles/{id}/sidecars
```

Sidecars sind u. a. `.wav`, `.mp3`, `.fcd`, `.ht_`, `.THM` und Preview-Dateien.

### 4.5 Status und Capabilities

```text
GET /api/v1/catalog/capabilities
```

Beispiel:

```json
{
  "canReadContent": true,
  "canStreamVideo": true,
  "canUploadContent": true,
  "canMoveFiles": true,
  "canDeleteFiles": true,
  "supportsRange": true
}
```

Der Client kann damit lokale Funktionen gezielt ausblenden oder deaktivieren.

## 5. Client-Architektur

### 5.1 Neue Abstraktion

```csharp
public interface IMediaContentStore
{
    bool Exists(MediaItem item);
    string GetLocalPath(MediaItem item, ContentPurpose purpose);
    Stream OpenRead(MediaItem item);
    Uri GetStreamUri(MediaItem item, ContentPurpose purpose);
    Task<string> EnsureLocalCopyAsync(MediaItem item, ContentPurpose purpose);
    Task UploadAsync(MediaItem item, string localPath);
    Task DeleteAsync(MediaItem item);
}
```

Implementierungen:

- `LocalMediaContentStore`: bisheriges Verhalten für SQLite und MariaDB.
- `ApiMediaContentStore`: API-Streaming, Cache und Upload.
- `ReadOnlyApiMediaContentStore`: nur Anzeige, keine schreibenden Dateioperationen.

`ContentPurpose` steuert die Strategie, z. B. `Thumbnail`, `Preview`, `ViewImage`, `PlayVideo`,
`Export`, `Metadata`, `Sidecar`.

### 5.2 `MediaItem` anpassen

- `FullName` bleibt die logische Katalogreferenz.
- `FileObject` darf im API-Modus nicht ungeprüft die Originaldatei liefern.
- Neue Eigenschaften:
  - `ContentAvailable`
  - `ContentLength`
  - `ContentType`
  - `CanStream`
- Zugriffe auf `FileObject` in kritischen Pfaden durch `IMediaContentStore` ersetzen.
- Übergangsweise kann `FileObject` im API-Modus auf eine Cache-Datei zeigen; bei fehlender Kopie
  muss ein klarer Fehler oder ein asynchroner Download erfolgen.

### 5.3 UI-Verhalten

- Fehlender Content: Vorschau/Thumbnail anzeigen, Original nicht öffnen.
- Video ohne lokalen Cache: Streaming oder Download mit Fortschritt.
- Nicht unterstützte Aktion: Menüpunkt deaktivieren und Tooltip anzeigen.
- Keine `File.Exists`-Prüfung auf Katalogpfaden im API-Modus.
- Keine Windows-Pfade in Webgalerien oder externen Playern verwenden.

## 6. Umsetzungsphasen

### Phase 1: Bestandsaufnahme und Capabilities

- `IMediaContentStore` einführen, zunächst nur `LocalMediaContentStore`.
- `ApiProvider` liefert Capability-Daten.
- Alle direkten `FileObject`-Zugriffe in den o. g. Dateien erfassen und als TODO markieren.

### Phase 2: Original-Content-API

- `GET /mediafiles/{id}/content` mit Range und ETag im Backend.
- Client-Download in Cache.
- `ApiMediaContentStore.EnsureLocalCopyAsync`.
- Bilder, Panoramen und 3D-Viewer auf den Store umstellen.

### Phase 3: Video und Streaming

- Signierte Streaming-URLs oder lokaler Video-Cache.
- `VideoPlayer`, `nVlc`, `VideoLanDotNet`, `MediaViewer` und `TakeSnapshot` anpassen.
- Range-Requests und Abbruch/Timeout testen.

### Phase 4: Import/Upload

- Upload-Endpunkt und Pre-Signed-Uploads.
- `ApiInsert` um Originaldatei und Upload-Fortschritt erweitern.
- MD5, Thumbnail, EXIF und Datumswerte weiterhin clientseitig erzeugen und mitsenden.
- Server speichert Content und Katalogmetadaten konsistent.

### Phase 5: Mutationen

- Rename/Move/Delete/Recycle über `/mediafiles/actions`.
- `FilesAndFolders`, `RenameFile`, `CopyMenuItem` und `ThumblistContainer` im API-Modus umleiten.
- Lokale Dateioperationen nur noch auf Cache-Dateien zulassen.

### Phase 6: Anhänge, Sidecars und Export

- Attachment-Content-Endpunkte.
- Sidecar-Endpunkte.
- Export/Snapshot lädt Original in Cache.
- Webgalerie verwendet API-URLs statt lokaler Pfade.

### Phase 7: Fehlende Dateien und Cache

- Serverseitige `ContentAvailable`-Prüfung.
- `GetMediaItemsWithMissingFiles` über API-Status.
- Cache-LRU, ETag-Invalidierung und Aufräumen.
- Verhalten bei Offline-Backend und abgelaufenem Token.

## 7. Akzeptanzkriterien

- MediaBrowserWpf startet und zeigt Katalog/Thumbnails ohne lokale Originaldateien.
- Ein Bild aus einem API-Katalog lässt sich ohne lokale Originaldatei anzeigen.
- Ein Video lässt sich streamen oder in den Cache laden.
- Ein Import von einem anderen Rechner lädt die Originaldatei zum Backend hoch.
- Rename/Move/Delete funktionieren über API-Aktionen.
- Anhänge und Sidecars können geladen und gespeichert werden.
- Fehlende Serverdateien werden korrekt angezeigt, ohne lokale `File.Exists`-Fehlalarme.
- Keine API-Funktion schreibt versehentlich in einen lokalen Katalogpfad.
- Token- und Content-URLs werden nicht geloggt.

## 8. Offene Entscheidungen

1. Werden Originaldateien im Backend gespeichert oder nur referenziert?
2. Sollen Videos gestreamt oder immer lokal gecacht werden?
3. Werden signierte URLs für `MediaElement` benötigt?
4. Wie groß darf der lokale Cache werden und wann wird er geleert?
5. Sollen Sidecars automatisch beim Upload mitgesendet werden?
6. Sind serverseitige Dateioperationen oder ein externer Objektspeicher vorgesehen?
