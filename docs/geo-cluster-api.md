# Geo-Cluster-API für die OSM-Karte

Dieses Dokument beschreibt die Anforderungen an das Spring-Boot-Backend für die
Karten-Cluster-Komponente. Der verbindliche API-Vertrag steht in
`docs/openapi.yaml` (Version 1.5.0).

## 1. Ziel

Die WPF-Anwendung zeigt auf einer OSM-Karte die im sichtbaren Ausschnitt
vorhandenen MediaItems als Cluster. Beim Hineinzoomen werden die Cluster
feiner. Auf Wunsch kann der Benutzer alle MediaItems des Ausschnitts als Liste
anzeigen.

Dafür werden zwei neue Endpunkte benötigt:

- `POST /mediafiles/geo/clusters` liefert eine leichte Cluster-Antwort für die
  Kartendarstellung.
- `POST /mediafiles/geo/items` liefert die vollständigen MediaItems für die
  Listenansicht, mit Pagination.

Der bestehende Endpunkt `POST /mediafiles/query` mit `mode: GEO` bleibt
unverändert und wird von der neuen Kartenkomponente nicht benötigt.

## 2. Nicht Teil dieses Auftrags

- Keine Schreiboperationen auf dem Medien-Dateisystem.
- Keine Änderung an `MediaBrowserContext`- oder WPF-Komponenten in diesem
  Schritt.
- Keine Thumbnails in der Cluster-Antwort.
- Keine serverseitige Personalisierung oder Heatmap.

## 3. Endpunkte

### 3.1 `POST /mediafiles/geo/clusters`

Liefert Cluster und Einzelpunkte für ein Rechteck.

Request: `GeoClusterQuery`

```json
{
  "minLongitude": 7.80,
  "minLatitude": 48.40,
  "maxLongitude": 8.10,
  "maxLatitude": 48.60,
  "zoom": 12,
  "cellSize": 64,
  "limit": 2000,
  "includeSingles": true
}
```

Response: `GeoClusterResult`

```json
{
  "clusters": [
    {
      "latitude": 48.4696,
      "longitude": 7.9429,
      "count": 42,
      "minLatitude": 48.4601,
      "minLongitude": 7.9302,
      "maxLatitude": 48.4804,
      "maxLongitude": 7.9551,
      "mediaItemId": 12345
    },
    {
      "latitude": 48.5002,
      "longitude": 8.0001,
      "count": 1,
      "minLatitude": 48.5002,
      "minLongitude": 8.0001,
      "maxLatitude": 48.5002,
      "maxLongitude": 8.0001,
      "mediaItemId": 12346
    }
  ],
  "totalCount": 43,
  "truncated": false,
  "zoom": 12,
  "cellSize": 64
}
```

Bedeutung der Felder:

- `clusters`: Grid-Zellen mit mindestens einem MediaItem.
- `count`: Anzahl der MediaItems in dieser Zelle.
- `latitude`, `longitude`: Mittelpunkt des Clusters (Mittelwert der Punkte).
- `minLatitude`, `minLongitude`, `maxLatitude`, `maxLongitude`: Hüllrechteck
  der Punkte im Cluster.
- `mediaItemId`: repräsentatives MediaItem, immer die kleinste ID im Cluster.
  Bei `count = 1` ist das der Einzelpunkt.
- `totalCount`: Gesamtzahl aller MediaItems im Ausschnitt, unabhängig von
  `includeSingles` und `limit`.
- `truncated`: `true`, wenn die Clusterliste wegen `limit` gekürzt wurde.

### 3.2 `POST /mediafiles/geo/items`

Liefert die vollständigen MediaItems für die Listenansicht.

Request: `GeoItemsQuery`

```json
{
  "minLongitude": 7.80,
  "minLatitude": 48.40,
  "maxLongitude": 8.10,
  "maxLatitude": 48.60,
  "limit": 500,
  "offset": 0,
  "sort": "MEDIA_DATE_ASC"
}
```

Response: Array von `MediaItem` gemäß `docs/openapi.yaml`.

Sortierung:

- `MEDIA_DATE_ASC` (Default): älteste zuerst.
- `MEDIA_DATE_DESC`: neueste zuerst.
- `ID_ASC`: kleinste ID zuerst.

`limit` ist auf 10.000 begrenzt. `offset` muss größer oder gleich 0 sein.

## 4. Semantik

### 4.1 Bounding Box

- `minLongitude`/`maxLongitude`: -180 bis 180.
- `minLatitude`/`maxLatitude`: -85.05112878 bis 85.05112878
  (Web-Mercator-Grenzen).
- Es muss gelten: `minLongitude <= maxLongitude` und
  `minLatitude <= maxLatitude`.
- Der Antimeridian (180. Längengrad) wird in Version 1 nicht serverseitig
  unterstützt. Der Client teilt eine solche Ansicht in zwei Anfragen auf.
- Ein Punkt liegt im Ausschnitt, wenn `LONGITUDE` und `LATITUDE` jeweils
  inklusive der Grenzen innerhalb des Rechtecks liegen.

### 4.2 Clustering

Die Clusterung erfolgt auf einem Web-Mercator-Grid bei der angegebenen
OSM-Zoomstufe. `cellSize` ist die Zielzellgröße in Pixeln.

Berechnung pro Punkt:

```text
worldSize = 256 * 2^zoom
x = (longitude + 180) / 360 * worldSize
latRad = latitude * PI / 180
y = (1 - ln(tan(latRad) + 1 / cos(latRad)) / PI) / 2 * worldSize
cellX = floor(x / cellSize)
cellY = floor(y / cellSize)
```

Alle Punkte mit demselben `(cellX, cellY)` bilden einen Cluster.

- Cluster-Mittelpunkt: arithmetisches Mittel der Punkte.
- Hüllrechteck: Minimum und Maximum der Punkte.
- Repräsentative ID: kleinste MediaItem-ID im Cluster.
- `includeSingles = false`: Cluster mit `count = 1` werden nicht ausgeliefert.
  `totalCount` zählt sie trotzdem mit.
- Sortierung der Cluster: `count` absteigend, danach `latitude` absteigend,
  danach `longitude` aufsteigend. Damit ist die Antwort deterministisch.
- `limit` bezieht sich auf die Anzahl der Cluster, nicht auf `totalCount`.
  Bei Überschreitung werden die Cluster nach obiger Sortierung abgeschnitten
  und `truncated = true` gesetzt.

### 4.3 Filter

Es werden nur MediaItems berücksichtigt, die alle Bedingungen erfüllen:

- `HISTORYVERSION = 0`
- `ISDELETED = 0`
- `LATITUDE IS NOT NULL`
- `LONGITUDE IS NOT NULL`
- nicht exakt `LATITUDE = 0 AND LONGITUDE = 0` (ungültige Position)
- `ROLES_FK` gehört zu den Rollen des authentifizierten Benutzers

Die Rollenfilterung muss denselben Regeln folgen wie bei den bestehenden
MediaQuery-Endpunkten. Es dürfen keine MediaItems anderer Rollen ausgeliefert
werden.

### 4.4 Auth

Beide Endpunkte verwenden die globale Bearer-Authentifizierung. Fehlende oder
ungültige Tokens führen zu `401`. Die Rollen werden aus dem Token bzw. der
bestehenden Rollenlogik abgeleitet.

### 4.5 Fehlerfälle

- `400`: ungültige Bounding Box, `zoom` außerhalb 0..22, `cellSize` außerhalb
  16..512, `limit` außerhalb 1..10000, `offset < 0`, unbekannter `sort`-Wert.
- `401`: nicht authentifiziert.
- `200` mit leerem `clusters`-Array bzw. leerem `MediaItem`-Array, wenn keine
  Treffer vorhanden sind.
- `500` nur bei unerwarteten Fehlern.

## 5. Datenbank und Indizes

### 5.1 Abfrage der Punkte

Für das Clustering reicht eine schlanke Abfrage:

```sql
SELECT ID, LATITUDE, LONGITUDE
FROM MEDIAFILES
WHERE HISTORYVERSION = 0
  AND ISDELETED = 0
  AND LATITUDE IS NOT NULL
  AND LONGITUDE IS NOT NULL
  AND NOT (LATITUDE = 0 AND LONGITUDE = 0)
  AND LATITUDE BETWEEN :minLatitude AND :maxLatitude
  AND LONGITUDE BETWEEN :minLongitude AND :maxLongitude
  AND ROLES_FK IN (:roles)
```

Die Clusterung kann in Java oder direkt in SQL erfolgen. Bei SQL-Aggregation
muss die Web-Mercator-Formel verwendet werden, nicht ein einfaches Grad-Grid.

### 5.2 Indizes

Aktuell existieren getrennte Indizes auf `LONGITUDE` und `LATITUDE`. Für kleine
Ausschnitte nutzt MariaDB den Latitude-Index mit Index Condition Pushdown. Für
größere Ausschnitte (z. B. Deutschland oder Berlin) erfolgt ein Full Table Scan
über ca. 91.000 Zeilen.

Empfohlener zusammengesetzter Index:

```sql
ALTER TABLE MEDIAFILES
  ADD INDEX idx_mediafiles_geo
    (HISTORYVERSION, ISDELETED, LATITUDE, LONGITUDE);
```

Optional, wenn die Rollenfilterung selektiv ist:

```sql
ALTER TABLE MEDIAFILES
  ADD INDEX idx_mediafiles_roles_geo
    (ROLES_FK, HISTORYVERSION, ISDELETED, LATITUDE, LONGITUDE);
```

Hinweise:

- Nach der Änderung `ANALYZE TABLE MEDIAFILES` ausführen.
- Mit `EXPLAIN` für kleine und große Bounding Boxes prüfen, ob der Index
  verwendet wird.
- Wenn die Tabelle aus diesem Repository kommt, die DDL zusätzlich in
  `MediaBrowser4Lib/DB/Maria/MariaDbAdministration.cs` ergänzen und die
  Migration in `SqliteToMariaDbMigration` berücksichtigen.
- Keine Schemaänderung ohne vorherige Abstimmung und ohne Test gegen MariaDB.

### 5.3 Erwartete Datenmenge

Aktueller Stand des Katalogs `mediabrowser-new`:

- 91.675 aktive MediaItems
- 65.730 davon mit Geo-Daten
- 43.431 Punkte in einer Deutschland-Bounding-Box

Damit ist die Datenmenge für eine serverseitige Clusterung gut handhabbar.

## 6. Akzeptanzkriterien

- `POST /mediafiles/geo/clusters` liefert für einen gültigen Ausschnitt
  `200` und ein `GeoClusterResult`.
- Gelöschte, historische und MediaItems ohne Koordinaten werden nicht
  ausgeliefert.
- `0/0`-Positionen werden nicht ausgeliefert.
- `totalCount` entspricht der Anzahl aller Treffer im Ausschnitt.
- `includeSingles = false` entfernt nur Cluster mit `count = 1`, nicht die
  Zählung in `totalCount`.
- `limit` und `truncated` verhalten sich wie beschrieben.
- Die Cluster-Sortierung ist deterministisch.
- `POST /mediafiles/geo/items` liefert die vollständigen MediaItems mit
  korrekter Sortierung und Pagination.
- Die Rollenfilterung verhindert die Auslieferung fremder MediaItems.
- Ungültige Parameter führen zu `400`.
- Es werden keine Dateisystem-Operationen ausgeführt.

## 7. Testfälle

1. Leerer Ausschnitt: `clusters = []`, `totalCount = 0`,
   `truncated = false`.
2. Ein Punkt: ein Cluster mit `count = 1` und gesetzter `mediaItemId`.
3. Mehrere Punkte in derselben Zelle: `count` entspricht der Anzahl.
4. Höhere Zoomstufe: Cluster zerfallen in kleinere Cluster.
5. `includeSingles = false`: Einzelcluster fehlen, `totalCount` bleibt gleich.
6. `limit` kleiner als Clusteranzahl: `truncated = true`.
7. Gelöschtes MediaItem wird ignoriert.
8. Historisches MediaItem (`HISTORYVERSION <> 0`) wird ignoriert.
9. MediaItem ohne Koordinaten wird ignoriert.
10. `0/0`-MediaItem wird ignoriert.
11. MediaItem einer fremden Rolle wird ignoriert.
12. Ungültige Bounding Box (`min > max`) liefert `400`.
13. Antimeridian-Fall wird gemäß Client-Aufteilung korrekt behandelt.
14. `geo/items` mit `limit`/`offset` liefert keine Duplikate und keine
    übersprungenen Treffer.

## 8. Checkliste für die Backend-Umsetzung

- [ ] OpenAPI-Vertrag in `docs/openapi.yaml` übernehmen.
- [ ] DTOs im Spring-Projekt anlegen.
- [ ] Controller für `POST /mediafiles/geo/clusters` und
      `POST /mediafiles/geo/items` implementieren.
- [ ] Service/Repository für Bounding-Box-Abfrage und Clustering implementieren.
- [ ] Rollenfilterung an die bestehende Auth-Logik anbinden.
- [ ] `0/0`-Filter und `ISDELETED`/`HISTORYVERSION`-Filter umsetzen.
- [ ] Pagination und Sortierung für `geo/items` umsetzen.
- [ ] Index-Migration vorbereiten und mit `EXPLAIN` prüfen.
- [ ] Unit- und Integrationstests gemäß Testfällen ergänzen.
- [ ] Performance für Stadt-, Deutschland- und Weltausschnitt messen.
- [ ] Contract-Test mit dem WPF-Client durchführen.
- [ ] Dokumentation der Endpunkte im Backend aktualisieren.

## 9. Offene Punkte

- Antimeridian wird in Version 1 vom Client aufgeteilt. Falls serverseitige
  Unterstützung gewünscht ist, muss die Bounding-Box-Logik erweitert werden.
- `mediaItemId` ist bewusst die kleinste ID im Cluster. Falls stattdessen ein
  „bestes“ Bild (z. B. höchste Priorität) verwendet werden soll, muss das
  definiert werden.
- `0/0` wird als ungültig ausgeschlossen. Falls solche Einträge sichtbar sein
  sollen, ist ein separater Schalter nötig.
- Ob `cellSize` vom Client frei wählbar bleibt oder serverseitig auf feste
  Zoomstufen abgebildet wird, ist noch zu entscheiden. Der Vertrag erlaubt
  aktuell 16 bis 512 Pixel.
