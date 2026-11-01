# GPS-Reparatur aus Samsung-Geräten (MTP)

Dieses Dokument beschreibt die Ursache der fehlenden Geodaten bei Samsung-Fotos
und das Skript zur nachträglichen Korrektur aus dem Telefon-Original.

## Ursache

Ein kontrollierter Test mit denselben Samsung-S24-Ultra-Bildern ergab:

- Upload über **DS file**: GPS-Daten bleiben erhalten.
- Upload über **Synology Photos**: `GPSLatitude`, `GPSLongitude` und
  `GPSAltitude` werden auf `0/0` gesetzt; die GPS-Referenzen fehlen danach.
- Die übrigen EXIF-Daten (Aufnahmezeit, Kamera, Software, Belichtung,
  Orientierung) bleiben unverändert.
- Die Dateigröße bleibt gleich, der Hash ändert sich. Die Bilddaten werden also
  nicht neu komprimiert, sondern nur die GPS-Werte werden genullt.

Die Anzeige in Samsung Gallery kann trotzdem einen Standort zeigen, weil dort
die Gallery-/MediaStore-Datenbank des Telefons ausgewertet wird. Beim Upload zu
Synology Photos und beim späteren Download ist dieser Standort nicht mehr in der
Datei enthalten.

**Konsequenz:** Für neue Uploads mit erhaltenen Geodaten DS file, SMB oder
Synology Drive verwenden, nicht Synology Photos.

## Skript

```text
scripts\Repair-MediaItemGpsFromPhone.ps1
```

Das Skript:

1. liest die betroffenen MediaItems aus MariaDB,
2. liest die Originale aus dem per MTP verbundenen Telefon,
3. ordnet Dateien über Aufnahmezeit ± Toleranz und Dateigröße zu,
4. kopiert die Kandidaten temporär, liest EXIF-GPS und validiert DMS-Werte
   sowie N/S/E/W-Referenzen,
5. aktualisiert nur MediaItems, deren `LATITUDE` und `LONGITUDE` noch `NULL`
   sind,
6. schreibt State- und Backup-Dateien.

Voraussetzungen:

- Windows mit PowerShell 7.
- Telefon per USB verbunden, entsperrt und als MTP-Gerät sichtbar.
- Gebaute Solution, insbesondere
  `tools\ApiMock.SeedExport\bin\Debug\net9.0\MySqlConnector.dll`.
- Datenbankpasswort über `MEDIABROWSER_DB_PASSWORD` oder `.vscode\db.env`.

## Verwendung

Standard: nur MediaItems mit ungültigen GPS-Metadaten (`0/0`, `NaN`,
`-2147483648`) und leeren Koordinaten.

```powershell
# Testlauf: kopiert und prüft, schreibt aber nicht in die DB
.\scripts\Repair-MediaItemGpsFromPhone.ps1 -DryRun -BatchSize 20

# Echter Lauf in Batches
.\scripts\Repair-MediaItemGpsFromPhone.ps1 -BatchSize 200

# Bereits verarbeitete Einträge erneut versuchen
.\scripts\Repair-MediaItemGpsFromPhone.ps1 -Restart -BatchSize 200

# Alle aktiven MediaItems ohne Koordinaten prüfen, nicht nur 0/0-GPS
.\scripts\Repair-MediaItemGpsFromPhone.ps1 -AllMissingGeo -BatchSize 200

# Anderes Gerät oder anderer Phone-Ordner
.\scripts\Repair-MediaItemGpsFromPhone.ps1 `
  -DeviceName 'Galaxy S24 Kandel' `
  -PhoneFolder 'Interner Speicher\DCIM\Camera'
```

Wichtige Parameter:

| Parameter | Bedeutung |
|---|---|
| `-DeviceName` | Name des MTP-Geräts unter „Dieser PC“ |
| `-PhoneFolder` | Pfad im Gerät, standardmäßig `Interner Speicher\DCIM\Camera` |
| `-BatchSize` | Anzahl Dateien pro Lauf |
| `-TimestampToleranceSeconds` | Zeitfenster für die Dateizuordnung, Standard 5 |
| `-AllMissingGeo` | alle aktiven Items ohne Koordinaten prüfen |
| `-DryRun` | keine DB-Änderung, keine State-/Backup-Schreibung |
| `-Restart` | State- und Backup-Datei löschen und neu starten |
| `-MaxItems` | Kandidatenzahl begrenzen, z. B. für Tests |

Standard-Arbeitsverzeichnis:

```text
%TEMP%\MediaBrowserWpfPhoneGps
```

Dort liegen:

- `matches.json` – Zuordnung DB-ID zu Phone-Datei
- `missing.csv` – Kandidaten ohne Phone-Datei
- `ambiguous.csv` – mehrdeutige Zuordnungen
- `state.jsonl` – bereits verarbeitete IDs, ermöglicht Fortsetzen
- `updates.csv` – Backup der erfolgreichen Koordinaten-Updates

Das Skript überschreibt keine bereits gesetzten Koordinaten. Es aktualisiert nur
Zeilen mit `LATITUDE IS NULL AND LONGITUDE IS NULL`.

## Letzter Lauf

Stand 01.11.2026:

- 1.731 aktive MediaItems mit ungültigen GPS-Metadaten.
- 1.585 davon aus dem Telefon-Original korrigiert.
- 146 blieben offen:
  - 29 aus 2022
  - 3 aus 2023
  - 7 aus 2025
  - 107 aus 2026

Die offenen Dateien waren im Phone-Ordner `DCIM\Camera` nicht vorhanden. Die
2022/2023er Fälle sind überwiegend insta360-Dateien.

Backup des letzten Laufs:

```text
%TEMP%\MediaBrowserWpfPhoneGps\phone_gps_updates_backup_20261101.csv
```

## Zusätzliche bereits durchgeführte Korrektur

Unabhängig von diesem Skript wurden 611 MediaItems korrigiert, die
`GPS Longitude Ref = W`, aber eine positive `LONGITUDE` hatten. Die Longitude
wurde auf den negativen Wert gesetzt. Backup:

```text
%TEMP%\MediaBrowserWpfPhoneGps\longitude_w_repair_backup_20261101.csv
```

Diese Korrektur ist einmalig. Neue Importe werden durch die
GPS-Validierung in `MediaProcessing` und den Insert-Pfaden verhindert.

## Offene Fälle

Für die verbleibenden 146 Dateien gibt es drei Möglichkeiten:

1. Andere Phone-Ordner prüfen (`Pictures`, `Download`, `WhatsApp` usw.).
2. Android-MediaStore per ADB exportieren und über Dateiname/Aufnahmezeit
   zuordnen.
3. Wenn die Dateien weder auf dem Telefon noch in einem Backup vorhanden sind,
   bleiben sie ohne Geodaten.

## Sicherheit und Daten

- Keine Zugangsdaten im Skript. Das DB-Passwort kommt aus der Umgebung oder aus
  `.vscode\db.env`.
- Backup- und State-Dateien enthalten Dateinamen und Koordinaten. Sie sind
  personenbezogene Daten und gehören nicht ins Repository.
- Das Skript führt keine Schreiboperationen auf dem Telefon oder auf dem
  Medien-Dateisystem aus. Es liest nur per MTP und kopiert temporär.
