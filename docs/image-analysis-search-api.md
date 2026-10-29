# Image-Analysis-Suche und Facetten-API

Dieses Dokument beschreibt die Backend-Umsetzung der Image-Analysis-Suche. Der verbindliche
HTTP-Vertrag steht in `docs/openapi.yaml` (Version 1.2.0), die C#-DTOs in
`MediaBrowser4Lib\DB\Api\ApiDtos.cs`.

## 1. Ziel

Das Frontend soll MediaItems anhand der KI-/OCR-Analyse finden und die Filter mit passenden
Eingabeelementen aufbauen können.

Neue Endpunkte:

| Endpunkt | Zweck |
|---|---|
| `POST /api/v1/image-analysis/search` | Suche über Volltext und strukturierte Analysefelder, Rückgabe von MediaItems |
| `POST /api/v1/image-analysis/facets` | Distinkte Werte + Anzahl der betroffenen MediaItems für DDLs/Autocomplete |
| `GET /api/v1/image-analysis/filter-fields` | Feldkatalog für den Frontend-Aufbau |
| `GET /api/v1/image-analysis/ranges` | Min/Max für Zähler- und Temperaturfilter |

Der bestehende `POST /api/v1/image-analysis/query` bleibt für analyseorientierte Antworten
bestehen. Die neue Suche liefert bewusst MediaItems.

## 2. Datenmodell

- `IMAGE_ANALYSIS` ist 1:1 zu `MEDIAFILES` über `MEDIA_ID`.
- `IMAGE_OBJECTS` ist 1:n zu `IMAGE_ANALYSIS` über `IMAGE_ANALYSIS_ID`.
- `IMAGE_READABLE_TEXT` ist 1:n zu `IMAGE_ANALYSIS` über `IMAGE_ANALYSIS_ID`.

Wichtige Felder:

| API-Feld | Quelle |
|---|---|
| `models` | `IMAGE_ANALYSIS.MODEL` |
| `objectNames` | `IMAGE_OBJECTS.OBJECT_NAME` |
| `weatherDescriptions` | `IMAGE_ANALYSIS.WEATHER_DESCRIPTION` |
| `locationDescriptions` | `IMAGE_ANALYSIS.LOCATION_DESCRIPTION` |
| `formats` | `IMAGE_ANALYSIS.FORMAT_OF_DOCUMENT_OR_IMAGE` |
| `personCount` | `IMAGE_ANALYSIS.PERSON_COUNT` |
| `animalCount` | `IMAGE_ANALYSIS.ANIMAL_COUNT` |
| `objectCount` | `IMAGE_ANALYSIS.OBJECT_COUNT` |
| `temperaturMinCelsius` | `IMAGE_ANALYSIS.TEMPERATUR_MIN_CELSIUS` |
| `temperaturMaxCelsius` | `IMAGE_ANALYSIS.TEMPERATUR_MAX_CELSIUS` |
| `modelTemperature` | `IMAGE_ANALYSIS.MODEL_TEMPERATURE` |
| `createdInMilliseconds` | `IMAGE_ANALYSIS.CREATED_IN_MILLISECONDS` |
| `creationDate` | `IMAGE_ANALYSIS.CREATION_DATE` |
| `createdAt` | `IMAGE_ANALYSIS.CREATED_AT` |
| `mediaDate` | `MEDIAFILES.MEDIADATE` |
| `mediaTypes` | `MEDIAFILES.TYPE` |
| `folderIds` | `MEDIAFILES.FOLDERS_FK` |
| `categoryIds` | `VARIATIONS` + `CATEGORIZE` |

## 3. Indizes

Die Indizes wurden in `mediabrowser-new` ergänzt und sind auch in
`scripts/Migrate-ImageAnalysisToMariaDb.ps1` für Neuinstallationen enthalten.
Das idempotente Nachziehen für bestehende Datenbanken übernimmt
`scripts/Add-ImageAnalysisIndexes.ps1`.

```sql
ALTER TABLE IMAGE_ANALYSIS
  ADD FULLTEXT KEY FT_WEATHER_DESCRIPTION (WEATHER_DESCRIPTION),
  ADD FULLTEXT KEY FT_LOCATION_DESCRIPTION (LOCATION_DESCRIPTION),
  ADD FULLTEXT KEY FT_FORMAT_OF_DOCUMENT_OR_IMAGE (FORMAT_OF_DOCUMENT_OR_IMAGE),
  ADD KEY IDX_IMAGE_ANALYSIS_MODEL (MODEL),
  ADD KEY IDX_IMAGE_ANALYSIS_CREATED_AT (CREATED_AT);

ALTER TABLE IMAGE_OBJECTS
  ADD KEY IDX_IMAGE_OBJECTS_ANALYSIS_NAME (IMAGE_ANALYSIS_ID, OBJECT_NAME(191)),
  ADD FULLTEXT KEY FT_OBJECT_NAME (OBJECT_NAME);
```

Bereits vorhanden:

- `FT_DETAILED_IMAGE_ANALYSIS` auf `IMAGE_ANALYSIS.DETAILED_IMAGE_ANALYSIS`
- `FT_IMAGE_READABLE_TEXT` auf `IMAGE_READABLE_TEXT.TEXT_LINE`
- `IDX_IMAGE_OBJECTS_OBJECT_NAME` als B-Tree-Präfixindex
- `IDX_IMAGE_TEXT_ANALYSIS` auf `IMAGE_READABLE_TEXT.IMAGE_ANALYSIS_ID`
- `UQ_IMAGE_ANALYSIS_MEDIA_ID` auf `IMAGE_ANALYSIS.MEDIA_ID`

Hinweis: FULLTEXT-Indizes auf großen Textspalten können beim Anlegen lange dauern. Das
Nachziehen sollte außerhalb der Hauptnutzungszeit erfolgen.

## 4. Suchanfrage

Request (`ImageAnalysisSearchRequest`):

```json
{
  "filter": {
    "text": "Skelett",
    "textMode": "NATURAL",
    "textFields": [
      "DETAILED_IMAGE_ANALYSIS",
      "READABLE_TEXT",
      "WEATHER_DESCRIPTION",
      "LOCATION_DESCRIPTION",
      "FORMAT_OF_DOCUMENT_OR_IMAGE",
      "OBJECT_NAME"
    ],
    "models": ["qwen2.5vl:7b"],
    "objectNames": ["Straße", "Berge"],
    "objectMatch": "ANY",
    "weatherDescriptions": [],
    "locationDescriptions": [],
    "formats": [],
    "mediaTypes": ["rgb"],
    "personCount": { "min": 0, "max": 3 },
    "animalCount": { "min": 0, "max": 10 },
    "objectCount": { "min": 0, "max": 100 },
    "temperaturMinCelsius": { "min": -10, "max": 30 },
    "temperaturMaxCelsius": { "min": -10, "max": 40 },
    "creationDate": { "from": "2025-01-01T00:00:00Z", "to": "2025-12-31T23:59:59Z" },
    "mediaDate": { "from": "2020-01-01", "to": "2026-12-31" },
    "mediaItemIds": [],
    "folderIds": [],
    "categoryIds": [],
    "categoryRecursive": false,
    "includeDeleted": false
  },
  "sort": "MEDIA_DATE_DESC",
  "offset": 0,
  "limit": 100,
  "includeObjects": false,
  "includeReadableText": false
}
```

Fehlt `filter`, werden keine Filter angewendet. `includeDeleted` ist dann standardmäßig `false`.

Antwort (`ImageAnalysisSearchResult`):

```json
{
  "total": 1234,
  "offset": 0,
  "limit": 100,
  "items": [
    {
      "id": 157858,
      "filename": "250302-1523-47.jpg",
      "foldername": "D:\\Photos\\Alle Bilder\\2025\\03_März",
      "mediaDate": "2025-03-02T15:23:47Z",
      "type": "rgb",
      "width": 6000,
      "height": 4000,
      "variationId": 123,
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
        "modelTemperature": 0.1,
        "createdInMilliseconds": 77801,
        "createdAt": "2025-09-12T01:42:29Z",
        "creationDate": "2025-09-12T01:42:28Z",
        "weatherDescription": "sonnig",
        "locationDescription": "Weinberge",
        "formatOfDocumentOrImage": "Bild"
      }
    }
  ]
}
```

`objects` und `readableText` werden nur gefüllt, wenn `includeObjects` bzw.
`includeReadableText` gesetzt ist.

## 5. Suchlogik

Grundabfrage:

```sql
SELECT m.ID, m.FILENAME, f.FOLDERNAME, m.MEDIADATE, m.TYPE, m.WIDTH, m.HEIGHT,
       m.CURRENTVARIATION, m.ISDELETED, m.ISBOOKMARKED,
       a.ID AS ANALYSIS_ID, a.MODEL, a.PERSON_COUNT, a.ANIMAL_COUNT, a.OBJECT_COUNT,
       a.TEMPERATUR_MIN_CELSIUS, a.TEMPERATUR_MAX_CELSIUS, a.MODEL_TEMPERATURE,
       a.CREATED_IN_MILLISECONDS, a.CREATED_AT, a.CREATION_DATE,
       a.WEATHER_DESCRIPTION, a.LOCATION_DESCRIPTION, a.FORMAT_OF_DOCUMENT_OR_IMAGE
FROM IMAGE_ANALYSIS a
JOIN MEDIAFILES m ON m.ID = a.MEDIA_ID
JOIN FOLDERS f ON f.ID = m.FOLDERS_FK
WHERE ...
ORDER BY ...
LIMIT ? OFFSET ?
```

Standardmäßig gilt `m.ISDELETED = 0`, außer `includeDeleted=true`.

### 5.1 Volltext

Der Service baut die Volltextbedingung nur aus den in `textFields` angegebenen Feldern. Fehlt
`textFields`, werden alle unterstützten Felder verwendet.

```sql
AND (
  MATCH(a.DETAILED_IMAGE_ANALYSIS) AGAINST (:text IN NATURAL LANGUAGE MODE)
  OR EXISTS (
    SELECT 1 FROM IMAGE_READABLE_TEXT t
    WHERE t.IMAGE_ANALYSIS_ID = a.ID
      AND MATCH(t.TEXT_LINE) AGAINST (:text IN NATURAL LANGUAGE MODE)
  )
  OR MATCH(a.WEATHER_DESCRIPTION) AGAINST (:text IN NATURAL LANGUAGE MODE)
  OR MATCH(a.LOCATION_DESCRIPTION) AGAINST (:text IN NATURAL LANGUAGE MODE)
  OR MATCH(a.FORMAT_OF_DOCUMENT_OR_IMAGE) AGAINST (:text IN NATURAL LANGUAGE MODE)
  OR EXISTS (
    SELECT 1 FROM IMAGE_OBJECTS o
    WHERE o.IMAGE_ANALYSIS_ID = a.ID
      AND MATCH(o.OBJECT_NAME) AGAINST (:text IN NATURAL LANGUAGE MODE)
  )
)
```

- `textMode=NATURAL` verwendet `IN NATURAL LANGUAGE MODE`.
- `textMode=BOOLEAN` verwendet `IN BOOLEAN MODE`. Benutzereingaben müssen serverseitig
  validiert/escaped werden.
- Leere Suchbegriffe oder Begriffe unter der FULLTEXT-Mindestlänge werden verworfen.
- `RELEVANCE_DESC` ist nur sinnvoll, wenn `text` gesetzt ist. Die Relevanz kann über den
  `MATCH`-Score ermittelt werden.

### 5.2 Objektnamen

`objectNames` ist eine exakte Suche über `IMAGE_OBJECTS.OBJECT_NAME`.

`objectMatch=ANY`:

```sql
AND EXISTS (
  SELECT 1 FROM IMAGE_OBJECTS o
  WHERE o.IMAGE_ANALYSIS_ID = a.ID
    AND o.OBJECT_NAME IN (:objectNames)
)
```

`objectMatch=ALL`:

```sql
AND (
  SELECT COUNT(DISTINCT o.OBJECT_NAME)
  FROM IMAGE_OBJECTS o
  WHERE o.IMAGE_ANALYSIS_ID = a.ID
    AND o.OBJECT_NAME IN (:objectNames)
) = :objectNamesCount
```

### 5.3 Weitere Filter

- `models`: `a.MODEL IN (:models)`
- `weatherDescriptions`: `a.WEATHER_DESCRIPTION IN (:weatherDescriptions)`
- `locationDescriptions`: `a.LOCATION_DESCRIPTION IN (:locationDescriptions)`
- `formats`: `a.FORMAT_OF_DOCUMENT_OR_IMAGE IN (:formats)`
- `mediaTypes`: `m.TYPE IN (:mediaTypes)`
- `mediaItemIds`: `m.ID IN (:mediaItemIds)`
- `folderIds`: `m.FOLDERS_FK IN (:folderIds)`
- `categoryIds`: `EXISTS` über `VARIATIONS` und `CATEGORIZE`
- `hasObjects`/`hasReadableText`: `EXISTS` bzw. `NOT EXISTS`
- Numerische Bereiche: `>= min` und `<= max`, jeweils nur wenn gesetzt.
- Datumsbereiche: `>= from` und `<= to`, jeweils nur wenn gesetzt.

Kategorien:

```sql
AND EXISTS (
  SELECT 1 FROM VARIATIONS v
  JOIN CATEGORIZE c ON c.VARIATIONS_FK = v.ID
  WHERE v.MEDIAFILES_FK = m.ID
    AND c.CATEGORY_FK IN (:categoryIds)
)
```

Bei `categoryRecursive=true` müssen die Kategorien vorher über den Kategoriebaum expandiert
werden. Die bestehende Kategorie-Logik des Backends wiederverwenden.

### 5.4 Sortierung

Nur Whitelist-Werte zulassen:

- `MEDIA_DATE_DESC`, `MEDIA_DATE_ASC`
- `CREATION_DATE_DESC`, `CREATION_DATE_ASC`
- `CREATED_AT_DESC`, `CREATED_AT_ASC`
- `PERSON_COUNT_DESC`, `PERSON_COUNT_ASC`
- `ANIMAL_COUNT_DESC`, `ANIMAL_COUNT_ASC`
- `OBJECT_COUNT_DESC`, `OBJECT_COUNT_ASC`
- `ID_ASC`, `ID_DESC`
- `RELEVANCE_DESC`

Unbekannte Sortierwerte → `400`.

### 5.5 Pagination und Gesamtzahl

- `limit`: Standard 100, Maximum 500.
- `offset`: Standard 0.
- `total`: separate `COUNT(*)`-Abfrage mit identischen Filtern.
- Bei Volltext kann die Count-Abfrage teuer sein. Ergebnis kurzzeitig cachen oder alternativ nur
  `hasMore` liefern (aktuell nicht im Vertrag).

## 6. Facetten

Nur diese Felder sind für DDLs/Autocomplete vorgesehen:

- `OBJECT_NAME`
- `WEATHER_DESCRIPTION`
- `LOCATION_DESCRIPTION`

Zähler und Temperaturen sind keine Facetten, sondern Range-/Number-Inputs.

Request (`ImageAnalysisFacetRequest`):

```json
{
  "filter": {
    "text": "Skelett",
    "personCount": { "min": 0, "max": 3 }
  },
  "fields": ["OBJECT_NAME", "WEATHER_DESCRIPTION", "LOCATION_DESCRIPTION"],
  "valueSearch": "Stra",
  "limitPerField": 100,
  "minCount": 1,
  "contextual": true
}
```

Antwort (`ImageAnalysisFacetResult`):

```json
{
  "objectName": [
    { "value": "Straße", "count": 42 }
  ],
  "weatherDescription": [
    { "value": "sonnig", "count": 420 }
  ],
  "locationDescription": [
    { "value": "Weinberge", "count": 120 }
  ]
}
```

### 6.1 Zählweise

Immer `COUNT(DISTINCT a.MEDIA_ID)`, damit die Anzahl zu den zurückgegebenen MediaItems passt.
Nicht die Join-Zeilen zählen.

### 6.2 Objektnamen

```sql
SELECT o.OBJECT_NAME AS value, COUNT(DISTINCT a.MEDIA_ID) AS count
FROM IMAGE_OBJECTS o
JOIN IMAGE_ANALYSIS a ON a.ID = o.IMAGE_ANALYSIS_ID
JOIN MEDIAFILES m ON m.ID = a.MEDIA_ID
WHERE o.OBJECT_NAME IS NOT NULL
  AND o.OBJECT_NAME <> ''
  AND <filterbedingungen>
GROUP BY o.OBJECT_NAME
HAVING COUNT(DISTINCT a.MEDIA_ID) >= :minCount
ORDER BY count DESC, value ASC
LIMIT :limitPerField
```

Bei `valueSearch`:

```sql
AND MATCH(o.OBJECT_NAME) AGAINST (:valueSearch IN NATURAL LANGUAGE MODE)
```

Alternativ ist für kurze Präfixe `o.OBJECT_NAME LIKE CONCAT(:valueSearch, '%')` möglich.

### 6.3 Wetter und Ort

```sql
SELECT a.WEATHER_DESCRIPTION AS value, COUNT(DISTINCT a.MEDIA_ID) AS count
FROM IMAGE_ANALYSIS a
JOIN MEDIAFILES m ON m.ID = a.MEDIA_ID
WHERE a.WEATHER_DESCRIPTION IS NOT NULL
  AND a.WEATHER_DESCRIPTION <> ''
  AND <filterbedingungen>
GROUP BY a.WEATHER_DESCRIPTION
HAVING COUNT(DISTINCT a.MEDIA_ID) >= :minCount
ORDER BY count DESC, value ASC
LIMIT :limitPerField
```

`LOCATION_DESCRIPTION` analog.

Bei `valueSearch`:

```sql
AND MATCH(a.WEATHER_DESCRIPTION) AGAINST (:valueSearch IN NATURAL LANGUAGE MODE)
```

### 6.4 Kontext

- `contextual=true`: Die Filter aus `filter` werden auf die Facettenberechnung angewendet.
- `contextual=false`: Die Filter werden ignoriert; es werden globale Verteilungen geliefert.
  `includeDeleted` sollte weiterhin berücksichtigt werden.

## 7. Wertebereiche

`GET /api/v1/image-analysis/ranges` liefert Min/Max für Range-Controls:

```json
{
  "personCount": { "min": 0, "max": 24 },
  "animalCount": { "min": 0, "max": 12 },
  "objectCount": { "min": 0, "max": 634 },
  "temperaturMinCelsius": { "min": -20, "max": 32 },
  "temperaturMaxCelsius": { "min": -10, "max": 41 },
  "modelTemperature": { "min": 0.0, "max": 1.0 },
  "createdInMilliseconds": { "min": 1000, "max": 900000 }
}
```

Umsetzung: `MIN`/`MAX` über die jeweiligen Spalten. `NULL`-Werte ignorieren. Die Abfrage ist
read-only und kann kurz gecacht werden.

## 8. Feldkatalog

`GET /api/v1/image-analysis/filter-fields` liefert eine feste Liste. Das Frontend baut daraus
die Filter-UI.

| key | label | type | control | facetable | source |
|---|---|---|---|---|---|
| `model` | Modell | STRING | TEXT | false | IMAGE_ANALYSIS |
| `objectName` | Objekt | STRING | AUTOCOMPLETE | true | IMAGE_OBJECTS |
| `weatherDescription` | Wetter | STRING | AUTOCOMPLETE | true | IMAGE_ANALYSIS |
| `locationDescription` | Ort | STRING | AUTOCOMPLETE | true | IMAGE_ANALYSIS |
| `formatOfDocumentOrImage` | Dokumenttyp | STRING | TEXT | false | IMAGE_ANALYSIS |
| `mediaType` | Medientyp | STRING | SELECT | false | MEDIAFILES |
| `personCount` | Personen | INTEGER | RANGE | false | IMAGE_ANALYSIS |
| `animalCount` | Tiere | INTEGER | RANGE | false | IMAGE_ANALYSIS |
| `objectCount` | Objekte | INTEGER | RANGE | false | IMAGE_ANALYSIS |
| `temperaturMinCelsius` | Temperatur min. | INTEGER | RANGE | false | IMAGE_ANALYSIS |
| `temperaturMaxCelsius` | Temperatur max. | INTEGER | RANGE | false | IMAGE_ANALYSIS |
| `modelTemperature` | Modell-Temperatur | NUMBER | RANGE | false | IMAGE_ANALYSIS |
| `createdInMilliseconds` | Analysedauer | INTEGER | RANGE | false | IMAGE_ANALYSIS |
| `creationDate` | Analysedatum | DATE | DATE_RANGE | false | IMAGE_ANALYSIS |
| `createdAt` | Erstellt am | DATE | DATE_RANGE | false | IMAGE_ANALYSIS |
| `mediaDate` | Mediendatum | DATE | DATE_RANGE | false | MEDIAFILES |

`mediaType` liefert feste Werte: `rgb`, `video`, `audio`.

## 9. Fehler und Sicherheit

- `400`: unbekanntes Feld, ungültige Sortierung, ungültiger Bereich, `from > to`,
  ungültiger `textMode`, `limit > 500`.
- `401`: fehlender oder ungültiger Bearer-Token.
- `403`: Mandant/Rolle nicht erlaubt.
- `500`: unerwarteter Fehler.
- Nur Lesezugriff; es wird keine Schreibrolle benötigt.
- Keine Dateisystemzugriffe.
- Mandant über den Token bzw. die Datenquelle, nicht über einen Client-Parameter.
- SQL nur mit benannten Parametern; keine String-Verkettung von Benutzereingaben.
- Sortier- und Feldnamen nur über Whitelists.

## 10. Tests

- Suche über jedes Einzelfeld.
- Volltext über alle `textFields`, Natural und Boolean.
- `objectMatch=ANY` und `ALL`.
- Kombination aus Volltext, Objekt, Modell, Zähler, Temperatur und Datum.
- Facetten mit und ohne Filter, `valueSearch`, `minCount`, `limitPerField`.
- Counts als `COUNT(DISTINCT MEDIA_ID)`.
- `includeObjects`/`includeReadableText`.
- Pagination, Sortierung und `total`.
- Leere Ergebnisse.
- `includeDeleted`.
- Kategorie rekursiv und nicht rekursiv.
- Berechtigungen und Tenant-Isolation.
- `EXPLAIN` für die wichtigsten Abfragen: FULLTEXT- und B-Tree-Indizes müssen genutzt werden.
- Lasttest mit dem aktuellen Datenbestand (ca. 66.000 Analysen, 383.000 Objekte,
  79.000 Textzeilen).

## 11. Abnahmekriterien

- `search` liefert MediaItems in der vertraglich definierten Struktur.
- `facets` liefert nur `OBJECT_NAME`, `WEATHER_DESCRIPTION`, `LOCATION_DESCRIPTION`.
- Zähler/Temperaturen werden über `ranges` und Range-Controls abgedeckt.
- `filter-fields` liefert die Controls für das Frontend.
- Volltextsuche nutzt die FULLTEXT-Indizes.
- Objekt-/Textfilter erzeugen keine Duplikate in den Suchergebnissen.
- `total` passt zur Anzahl der Treffer.
- Keine Dateisystem-Schreiboperationen.
