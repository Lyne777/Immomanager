# Immomanager - Hinweise für Claude

Blazor-Server-App (.NET 8, MudBlazor, SQLite) für private Immobilienverwaltung, läuft als Docker-Container
auf einer Synology NAS im Heimnetz (bewusst nicht aus dem Internet erreichbar, nur ausgehende
Verbindungen). Doku zu allen Features: [README.md](README.md).

## Daten für Claude über OneDrive (Export)

Ziel: Claude (Desktop, Web, Handy, Projekte) soll die Objektdaten und alle Unterlagen kennen und per
Outlook/M365 Mails vorbereiten können, ohne dass die NAS aus dem Internet erreichbar wird und ohne den
eingebauten Chat-Agenten (Armin Asset) weiter auszubauen. Umgesetzt als täglicher Lese-Export, Details im
README unter "Export für Claude".

- **Die Live-Datenbank (SQLite, WAL) bleibt auf der NAS und liegt NIE in OneDrive.** Cloud-Sync und
  SQLite-Dateien (inkl. `-wal`/`-shm`) vertragen sich nicht (Konfliktkopien/Korruptionsrisiko).
- Ein `BackgroundService` in der App schreibt täglich 03:00 Uhr (Europe/Berlin; Nachholen nach Neustart,
  Button "Jetzt exportieren" unter Einstellungen) nach `./data/claude-export`; Synology Cloud Sync gleicht
  genau diesen Ordner (nur Upload, NAS -> OneDrive) mit OneDrive/SharePoint ab:
  - `LIESMICH.md` - erklärt Struktur, Formeln, "nur lesen"
  - `uebersicht.md` - Portfolio-Summen und eine Zeile je Objekt
  - `objekte/<name>.md` - alles zu einem Objekt inkl. **berechneter Kennzahlen** (Cashflow, LTV, Ertragswert
    stehen nicht in der DB, sondern werden in C# berechnet), Mieter, Logbuch, Dokumentenliste
  - `objekte.csv`, `mietverhaeltnisse.csv` - tabellarisch
  - `dokumente/<name>/...` - Kopien der hochgeladenen Unterlagen (Einweg-Spiegel, die App speichert
    unverändert in ihren eigenen Ordnern). **Standardmäßig aus** (`ClaudeExport__MirrorDocuments=true`
    schaltet ihn ein): Die Unterlagen liegen in einem strukturierten OneDrive-Ordner des Nutzers, Kopien
    würden Claude doppelte Fassungen zeigen. Ohne Spiegel listet `objekte/<name>.md` nur Beschreibung und
    Dateiname der hinterlegten Dokumente - die Dateien selbst bei Bedarf per SharePoint/OneDrive-Suche finden
  - `immomanager-snapshot.db` - konsistente DB-Kopie für `sqlite3` in Claude Code
- **Claude liest** Export und Dokumente über den Microsoft-365-Connector (SharePoint/OneDrive-Suche),
  Mail-Entwürfe über Outlook. Der Export ist ein **Lese-Stand** (Zeitstempel in jeder Datei): Änderungen an
  den Daten passieren nur in Immomanager selbst (Handy-Zugriff per Tailscale), nie durch Schreiben in Export
  oder Snapshot.

Für Claude Code in diesem Repo: Für Auswertungen den Snapshot bzw. eine DB-Kopie verwenden, nie die
Produktiv-DB der NAS direkt anfassen. Der Export enthält personenbezogene Mieterdaten - nicht weitergeben.

Ergänzende Quelle: Der Nutzer führt Mieter, Mieten und den Bankabgleich zusätzlich in **immocloud**
(PSD2-Bankanbindung, Mietbuchhaltung, EÜR/DATEV). immocloud hat keine öffentliche API und keine Webhooks;
der Support wurde um eine lesende API bzw. automatisierte Exporte gebeten. Bis dahin gilt: Zahlungsdaten
stammen aus immocloud-Exporten (manuell nach OneDrive gelegt), Objekt-, Finanzierungs- und Kennzahlendaten
aus diesem Export. Bankabgleich oder Mieteingänge werden in Immomanager bewusst nicht nachgebaut.

Noch nicht umgesetzt: Tailscale/VPN für den Handy-Zugriff auf die App, Fristen-Datei (`fristen.md`) plus
wöchentliche Claude-Routine, Login/MCP-Endpunkt für Claude im Web mit Live-Daten.
