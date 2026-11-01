# Fehlende Geodaten aus zeitlichem Nachbarn füllen

Dieses Dokument beschreibt das Skript
`scripts\Fill-MissingGeoFromTemporalNeighbor.ps1` und die Regeln der
durchgeführten Korrektur.

## Zweck

MediaItems ohne Koordinaten erhalten die Koordinaten des zeitlich nächsten
MediaItems mit gültigen Geodaten – aber nur, wenn der zeitliche Abstand maximal
24 Stunden beträgt.

## Regeln

Berücksichtigt werden nur aktive Einträge:

- `HISTORYVERSION = 0`
- `ISDELETED = 0`

Zielkandidaten:

- Standard: `LATITUDE IS NULL AND LONGITUDE IS NULL`
- Mit `-IncludeZeroZero`: zusätzlich `LATITUDE = 0 AND LONGITUDE = 0`

Nachbarquelle:

- gleiche `ROLES_FK` wie das Ziel
- `HISTORYVERSION = 0`
- `ISDELETED = 0`
- `LATITUDE` und `LONGITUDE` gesetzt
- nicht `0/0`
- anderer Datensatz als das Ziel

Auswahl:

- kleinste absolute Differenz der `MEDIADATE`
- bei Gleichstand: früherer Nachbar
- bei gleichem Zeitstempel: kleinste Nachbar-ID
- maximal 86.400 Sekunden (24 Stunden), konfigurierbar über
  `-MaxDistanceHours`

Wichtig:

- Es wird nur vorhandenes Geodaten-Material als Quelle verwendet. Neu gefüllte
  Items dienen im selben Lauf nicht wieder als Nachbar. Es gibt also keine
  Kettenfortpflanzung.
- Ein **erneuter Lauf** nach dem Füllen verwendet die bereits gefüllten Items
  als Quelle. Dadurch können die verbleibenden 4.871 Items indirekt aus den
  neu gesetzten Koordinaten versorgt werden. Nur bewusst erneut ausführen.
  Ein Testlauf direkt nach der Korrektur ergab 293 solche Ketten-Kandidaten;
  diese wurden bewusst nicht angewandt.
- Die Koordinaten sind eine zeitliche Heuristik, keine echte Ortsbestimmung.
- Die Rolle wird respektiert, damit keine Koordinaten zwischen Rollen
  ausgetauscht werden.

## Verwendung

```powershell
# Nur prüfen, nichts schreiben
.\scripts\Fill-MissingGeoFromTemporalNeighbor.ps1 -DryRun

# Ausführen (Standard: nur NULL-Koordinaten)
.\scripts\Fill-MissingGeoFromTemporalNeighbor.ps1

# Zusätzlich 0/0-Koordinaten füllen
.\scripts\Fill-MissingGeoFromTemporalNeighbor.ps1 -IncludeZeroZero

# Anderer Maximalabstand
.\scripts\Fill-MissingGeoFromTemporalNeighbor.ps1 -MaxDistanceHours 12
```

Wichtige Parameter:

| Parameter | Bedeutung |
|---|---|
| `-MaxDistanceHours` | Maximaler Zeitabstand in Stunden, Standard 24 |
| `-IncludeZeroZero` | auch `0/0`-Einträge als fehlend behandeln |
| `-DryRun` | nur Kandidaten berechnen, keine DB-Änderung |
| `-WorkDir` | Arbeitsverzeichnis für Reports und Backup |
| `-DbHost`, `-DbPort`, `-DbName`, `-DbUser` | MariaDB-Verbindung |
| `-DbPasswordEnvFile` | Datei mit `MEDIABROWSER_DB_PASSWORD` |

Standard-Arbeitsverzeichnis:

```text
%TEMP%\MediaBrowserWpfTemporalGeoFill
```

Dateien:

- `temporal_geo_candidates.csv` – alle geplanten/ausgeführten Zuordnungen
- `temporal_geo_skipped.csv` – Kandidaten ohne Nachbar innerhalb 24 h
- `temporal_geo_updates.csv` – Backup der ausgeführten Updates inklusive
  Nachbar-ID, Zeitdifferenz und alten Werten

Zusätzliche Kopien des Laufs vom 01.11.2026 liegen im lokalen
Workspace-Verzeichnis:

```text
temp\temporal_geo_updates_20261101.csv
temp\temporal_geo_skipped_20261101.csv
```

## Ergebnis des Laufs vom 01.11.2026

Ausgangslage:

- 91.675 aktive MediaItems
- 24.360 ohne Koordinaten (`LATITUDE`/`LONGITUDE` NULL)
- 300 Einträge mit `0/0` (nicht angefasst)

Ergebnis:

- 19.489 Items aus dem zeitlich nächsten Nachbarn gefüllt
- 4.871 Items ohne Nachbar innerhalb von 24 Stunden
- 0 Items mit nur einer Koordinate
- 0 Koordinaten außerhalb gültiger Bereiche
- 300 Einträge mit `0/0` weiterhin unverändert
- Aktiver Stand danach: 86.804 mit Geo-Daten, 4.871 ohne

Verteilung der Zeitabstände:

| Abstand | Anzahl |
|---|---:|
| 0–60 Sekunden | 12.188 |
| 1–5 Minuten | 1.491 |
| 5–60 Minuten | 1.582 |
| 1–6 Stunden | 2.492 |
| 6–24 Stunden | 1.736 |

Die 6–24-Stunden-Fälle sind besonders kritisch: Dort ist die zeitliche Nähe
kein starker Beleg für denselben Ort. Bei Bedarf sollten diese Einträge
stichprobenartig geprüft oder über einen kleineren `-MaxDistanceHours`-Wert
erneut bewertet werden.

## Rückgängig machen

Das Backup enthält `DBID`, `OldLatitude`, `OldLongitude` sowie die neuen Werte.
Für die Standardkorrektur waren die alten Werte `NULL`:

```sql
UPDATE MEDIAFILES
SET LATITUDE = NULL, LONGITUDE = NULL
WHERE ID IN (...);
```

Für `-IncludeZeroZero` müssen die alten Werte aus dem Backup übernommen werden.
Vor einem Rollback immer den aktuellen DB-Stand und das Backup prüfen.

## Sicherheit

- Keine Zugangsdaten im Skript; das DB-Passwort kommt aus der Umgebung oder aus
  `.vscode\db.env`.
- Die CSV-Dateien enthalten Dateinamen, Koordinaten und Zeitstempel. Sie sind
  personenbezogene Daten und gehören nicht ins Repository.
- Das Skript ändert nur `LATITUDE` und `LONGITUDE` in `MEDIAFILES`. Es werden
  keine Dateien, Metadaten oder anderen Tabellen verändert.
- Keine Schemaänderung, keine Migration.
