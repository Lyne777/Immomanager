# Immomanager

Blazor Web App (.NET 8, Interactive Server, MudBlazor) zur Verwaltung eines Immobilienportfolios
inkl. Finanzierungen und KPI-Tracking (Mietrendite, Cashflow, Cash-on-Cash Return, ROI, LTV,
Eigenkapital-Spiegel). Alle Kennzahlen können wahlweise für das Gesamtobjekt oder anteilig nach
Beteiligungsquote berechnet werden.

## Architektur

- **Immomanager.Web** (`src/Immomanager.Web`): Blazor Web App mit Interactive-Server-Rendering.
- **Datenbank**: SQLite via EF Core. Die Datei wird beim ersten Start automatisch angelegt
  (`app.MigrateDatabase()` in `Program.cs`), inkl. Schema-Migrationen.
- **Speicherort**: konfigurierbar über `Storage:DataDirectory` (Standard `./App_Data`, relativ zum
  Content-Root) und `Storage:DatabaseFileName` (Standard `app.db`) in `appsettings.json`, oder per
  Umgebungsvariable `Storage__DataDirectory` überschreibbar — dafür ausgelegt, in Docker/Synology
  als persistentes Volume gemountet zu werden. (Bewusst nicht `./data`: auf Windows ist das
  Dateisystem case-insensitiv und würde sonst mit dem Quellcode-Ordner `Data/` kollidieren.)

## Lokal starten

```bash
dotnet run --project src/Immomanager.Web
```

Die App legt beim ersten Start `./src/Immomanager.Web/App_Data/app.db` an und ist danach unter der
in der Konsole ausgegebenen URL erreichbar.

## Datenbank-Migrationen

Migrationen liegen unter `src/Immomanager.Web/Data/Migrations`. Neue Migration nach Modelländerung:

```bash
dotnet tool install --global dotnet-ef   # einmalig
dotnet ef migrations add <Name> --project src/Immomanager.Web -o Data/Migrations
```

Migrationen werden beim App-Start automatisch angewendet (kein manueller `update`-Schritt nötig).

## Docker / Synology NAS Deployment

Diese App läuft dauerhaft als Docker-Container auf einer Synology NAS. Neue Versionen kommen
**automatisch** dorthin, ohne dass auf der NAS irgendetwas gebaut werden muss. Diese Anleitung ist
bewusst so geschrieben, dass sie auch ohne Docker-/Programmier-Vorwissen nachvollziehbar ist —
falls dieses Wissen mal "verloren geht" (z. B. neuer Rechner, neue Person übernimmt), steht hier
alles Nötige.

### Wie funktioniert das automatische Update?

Kurz gesagt: **Code committen und pushen → fertiges Programm liegt bereit → auf der NAS abholen.**

1. Sobald neuer Code auf den `main`-Branch bei GitHub gepusht wird, startet automatisch ein
   "Workflow" (Skript, das GitHub für uns ausführt, definiert in
   [`.github/workflows/docker-build.yml`](.github/workflows/docker-build.yml)).
2. Dieser Workflow baut daraus ein fertiges, startbereites Docker-Image (eine Art "Paket" mit der
   kompletten App drin) und legt es in einem privaten Lager bei GitHub ab — der "GitHub Container
   Registry", kurz **ghcr.io**. Das Paket heißt `ghcr.io/lyne777/meine-immobilien-app:latest`.
3. Die NAS holt sich dieses fertige Paket nur noch **ab** (herunterladen + starten) — sie baut
   nichts selbst. Das macht Updates auf der NAS schnell und zuverlässig.

Den Baufortschritt/Erfolg kann man jederzeit unter
`https://github.com/Lyne777/Immomanager/actions` einsehen (grüner Haken = erfolgreich, rotes Kreuz
= fehlgeschlagen).

### Einmalige Einrichtung auf der NAS

Das muss nur **einmal** gemacht werden.

**1. Bei GitHub ein Zugriffs-Token erstellen** (auf github.com, nicht auf der NAS):
- Profilbild oben rechts → *Settings* → ganz unten *Developer settings*
- *Personal access tokens* → *Tokens (classic)* → *Generate new token (classic)*
- Als Berechtigung **nur** `read:packages` ankreuzen (mehr braucht es nicht)
- Token erzeugen und sofort kopieren (wird danach nie wieder angezeigt)

**2. Die NAS bei ghcr.io anmelden**, damit sie das (private) Paket abholen darf:
- *Container Manager* öffnen → *Registry* → eigene Registry hinzufügen
- URL: `https://ghcr.io`, Benutzername: `Lyne777`, Passwort: das eben kopierte Token
  (**nicht** das GitHub-Passwort!)

**3. Projekt anlegen**, damit der Container das erste Mal startet:
- *Container Manager* → *Projekt* → *Erstellen*
- Namen vergeben (z. B. `immomanager`), Ordner wählen (z. B. `/docker/immomanager`)
- Den Inhalt der Datei [`docker-compose.yml`](docker-compose.yml) aus diesem Projekt komplett dort
  einfügen (Feld vorher leeren, bevor man einfügt — sonst gibt's YAML-Fehler durch doppelte
  Einträge!)
- Erstellen klicken — die NAS lädt das Paket herunter und startet die App automatisch. Danach
  erreichbar unter `http://<NAS-IP>:8080`.

### Neue Version einspielen (das wiederholt sich bei jedem Update)

Nachdem eine neue Version gepusht wurde und der Workflow bei GitHub grün ist (siehe oben):

1. **SSH auf der NAS aktivieren**, falls noch nicht geschehen (einmalig): *Systemsteuerung* →
   *Terminal & SNMP* → „SSH-Dienst aktivieren"
2. Auf dem eigenen PC: eine Konsole öffnen (unter Windows z. B. *PowerShell*, Start-Menü →
   "PowerShell" eintippen)
3. Verbinden: `ssh <NAS-Benutzername>@<NAS-IP>`, dann Passwort eingeben (bleibt beim Tippen
   unsichtbar, das ist normal)
4. Zum Projektordner wechseln, z. B.: `cd /volume1/docker/immomanager`
5. Ausführen:
   ```bash
   sudo docker compose pull && sudo docker compose up -d
   ```
   Das lädt das neueste Paket herunter und startet den Container damit neu — die Datenbank und
   alle Uploads bleiben dabei unangetastet (die liegen ja im separat gemounteten `./data`-Ordner).
6. Fertig. Mit `exit` das Konsolen-Fenster schließen.

### Woher weiß ich, ob ein Update überhaupt nötig ist?

Die App sagt selbst Bescheid: Auf dem Dashboard erscheint automatisch ein blauer Hinweis "Eine
neuere Version ist verfügbar", sobald auf `main` bei GitHub ein neuerer Stand liegt als der gerade
laufende — inklusive Link direkt zu dieser Anleitung. Dahinter steckt ein kleiner Hintergrund-Check
(alle 6 Stunden), der einfach den öffentlich einsehbaren Commit-Stand des Repos mit der eigenen,
beim Bauen "eingebrannten" Version vergleicht — dafür muss das Repo öffentlich sein (das
`ghcr.io`-Paket mit dem eigentlichen Programm bleibt trotzdem privat).

### Optional: Update per Knopfdruck (ganz ohne Tippen)

Wer sich Schritt 2–4 oben ersparen möchte:

1. *Systemsteuerung* → *Aufgabenplaner* → *Erstellen* → *Ausgelöste Aufgabe* →
   *Benutzerdefiniertes Skript*
2. Benutzer: `root`
3. Skript-Feld:
   ```bash
   cd /volume1/docker/immomanager
   docker compose pull
   docker compose up -d
   ```
4. Speichern. Ab jetzt reicht: *Aufgabenplaner* öffnen → Aufgabe anklicken → **Ausführen**.

### Falls beim Erstellen/Updaten ein Fehler kommt

- **„denied" beim Herunterladen des Pakets**: entweder ist der Workflow bei GitHub (noch) nicht
  erfolgreich durchgelaufen (unter *Actions* im Repo nachsehen), oder die Anmeldedaten aus Schritt
  2 oben stimmen nicht mehr (Token abgelaufen? Neues Token erzeugen und Registry-Eintrag im
  Container Manager erneuern).
- **YAML-Fehler wie „Map keys must be unique"**: das Textfeld beim Projekt-Erstellen enthielt schon
  eine Vorlage, bevor man den `docker-compose.yml`-Inhalt eingefügt hat. Feld komplett leeren
  (`Strg+A`, *Entf*) und den Inhalt frisch einfügen.
- **Genereller technischer Hinweis für später**: Windows behandelt Groß-/Kleinschreibung bei
  Ordnernamen nicht als Unterschied. Deswegen sind die Ausschluss-Regeln für den Datenordner in
  [`.gitignore`](.gitignore) und [`.dockerignore`](.dockerignore) bewusst mit einem führenden `/`
  auf das Projekt-Hauptverzeichnis festgelegt (`/data/`) — sonst würde die Regel versehentlich auch
  den (großgeschriebenen) Quellcode-Ordner `Data/` mit den Datenbank-Migrationen treffen und
  komplett aus Git/Docker verschwinden lassen.

Die App selbst kann diese Anleitung übrigens auch direkt anzeigen: Menüpunkt „Anleitung / README"
in der Navigation zeigt genau diese Datei formatiert an.

## Einstellungen (Anthropic API-Key)

Menüpunkt „Einstellungen" (`/einstellungen`,
[`Einstellungen.razor`](src/Immomanager.Web/Components/Pages/Einstellungen.razor)):

- **Warum eine eigene Seite statt appsettings.json**: Im Docker-Betrieb liegt `appsettings.json` im
  Image selbst - weder im NAS-Dateisystem auffindbar noch übersteht eine manuelle Änderung ein
  Container-Update. Der Key wird stattdessen in einer eigenen `anthropic-settings.json` im
  Datenverzeichnis gespeichert ([`AnthropicSettingsService.cs`](src/Immomanager.Web/Services/AnthropicSettingsService.cs))
  - übersteht damit Updates, ist im Repository/Docker-Image nie enthalten und bei Bedarf auch direkt
  im NAS-Dateisystem auffindbar (unter `App_Data/anthropic-settings.json` bzw. im gemounteten
  Docker-Volume).
- **Wirkt sofort ohne Neustart**: Die Datei wird als zusätzliche, zuletzt hinzugefügte
  Konfigurationsquelle eingebunden (`reloadOnChange: true` in `Program.cs`) und überschreibt damit
  `appsettings.json`/Umgebungsvariablen, falls über die UI gespeichert. Da Datei-Watcher auf
  gemounteten Docker-Volumes nicht immer zuverlässig auslösen, erzwingt der Service nach jedem
  Speichern zusätzlich explizit ein `IConfigurationRoot.Reload()`. Alle fünf Anthropic-Verbraucher
  (Armin-Asset-Chat sowie die vier PDF-Analyse-Services) nutzen bewusst `IOptionsMonitor<T>` statt
  `IOptions<T>` und lesen den aktuellen Wert bei jedem Aufruf frisch, statt ihn einmalig beim Start
  zu cachen.
- **Kein Secret-Redisplay**: Ein bereits gespeicherter Key wird nie wieder im Klartext angezeigt -
  das Eingabefeld bleibt beim Öffnen der Seite leer. Lässt man es beim Speichern leer, bleibt der
  aktuell hinterlegte Key unangetastet (z. B. um nur das Modell zu ändern), statt versehentlich
  überschrieben zu werden.

## Fotos

Immobilien können beliebig viele Fotos haben (Tab „Fotos“ auf der Detailseite):

- Dateien (JPG/PNG/WEBP/GIF, max. 15 MB) werden im Dateisystem unter
  `{Storage:DataDirectory}/uploads/<PropertyId>/<Guid>.<ext>` gespeichert — lokal also
  `App_Data/uploads/...`, im Docker-Container `/app/data/uploads/...` (und damit im gemounteten
  `./data/uploads/`).
- In der SQLite-Datenbank (`PropertyImages`-Tabelle) wird nur der relative Pfad
  (z. B. `uploads/3/ab12....jpg`) sowie Dateiname/-größe/-typ abgelegt, nie die Bilddaten selbst.
- Ausgeliefert werden die Dateien über eine zweite `UseStaticFiles`-Middleware in `Program.cs`,
  die direkt auf das Datenverzeichnis zeigt (URL-Präfix `/data-files`).
- Löschen einer Immobilie entfernt auch deren Bild-Ordner vom Dateisystem
  ([`PropertyService.DeleteAsync`](src/Immomanager.Web/Services/PropertyService.cs)).
- Da Uploads über die Blazor-Server-SignalR-Verbindung laufen, ist die maximale Nachrichtengröße
  in `Program.cs` entsprechend der 15-MB-Grenze angehoben (`HubOptions.MaximumReceiveMessageSize`).

## Renovierungs- & Sanierungs-Tracker

Jede Immobilie kann beliebig viele Renovierungsprojekte haben (Tab „Renovierungen“), jedes Projekt
wiederum beliebig viele Gewerk-Positionen (eigene Detailseite pro Projekt):

- **Projekt** (`RenovationProject`): Bezeichnung, Kategorie (z. B. Badsanierung), Status
  (Geplant/In Umsetzung/Abgeschlossen), Start-/Enddatum, betroffene Fläche, geplante Gesamtkosten.
  Die **Ist-Gesamtkosten werden nicht manuell gepflegt**, sondern automatisch als Summe der
  Gewerk-Positionen berechnet (`RenovationProject.ActualTotalCost`) — damit Kalkulator und
  Detailauswertung immer konsistent auf denselben Zahlen basieren.
- **Gewerk-Position** (`RenovationLineItem`): Gewerk (Enum, z. B. Bad/Sanitär, Bodenbelag,
  Elektrik...), Beschreibung, Menge + Einheit (z. B. 45 m² oder 3 Stück), Material- und
  Lohnkosten getrennt, optionale Eigenleistungsstunden.

**Lernwerte & Kalkulator** (Seite „Renovierungs-Kalkulator“, [`RenovationAnalyticsService`](src/Immomanager.Web/Services/RenovationAnalyticsService.cs)):

- Werden ausschließlich aus **abgeschlossenen** Projekten gebildet (laufende/geplante Projekte
  verfälschen sonst die Erfahrungswerte, da ihre Kosten noch nicht final sind).
- Zwei Aggregationsebenen, passend zu den beiden Beispielen aus der Anforderung:
  - **Je Gewerk** (mengengewichteter Ø aus allen Positionen, z. B. "Bodenbelag: 85 €/m²") —
    deckt einzelne Maßnahmen ab.
  - **Je Projektkategorie** (Ø aus den Ist-Gesamtkosten/Fläche aller Projekte dieser Kategorie,
    z. B. "Badsanierung: 1.085 €/m²") — deckt ganze Vorhaben über mehrere Gewerke hinweg ab.
- Der Rechner nutzt diese Werte, um für eine neu eingegebene Menge/Fläche eine Kostenschätzung
  inkl. Min/Max-Bandbreite auszugeben, und weist bei weniger als 3 Datenpunkten auf die geringe
  Datenbasis hin.

## Ankaufsprüfung & Szenarien-Kalkulation

Eigenständiges Modul (Menüpunkt „Ankaufsprüfung“, `/deals`) zur Vorab-Kalkulation potenzieller
Käufe, unabhängig vom Portfolio, optional aber mit einer echten Immobilie verknüpfbar (Plan/Ist-Abgleich):

- **Sandbox vs. historische Kalkulation**: beide Modi teilen sich denselben Editor
  ([`DealEditor.razor`](src/Immomanager.Web/Components/Pages/Deals/DealEditor.razor)) und dasselbe
  Modell (`DealCalculation`) — „Sandbox“ ist einfach der Zustand vor dem ersten Speichern
  (`/deals/new`, rein im Arbeitsspeicher, keine DB-Schreibzugriffe bis "Speichern"). Gespeicherte
  Kalkulationen erscheinen unter „Historische Kalkulationen“ (`/deals`).
- **Versionierung**: „Duplizieren“ erzeugt eine Kopie mit neuer Id, gleicher `VersionGroupId` und
  hochgezählter `Version` — so bleiben alle Stände derselben Prüfung nachvollziehbar.
- **Berechnungs-Engine** ([`DealCalculationEngine.cs`](src/Immomanager.Web/Services/DealCalculationEngine.cs),
  reine Rechenlogik ohne DB-Zugriff, daher für Live-Neuberechnung bei jeder Eingabe geeignet):
  - Kaufnebenkosten, Gesamtinvestition, Eigenkapitalbedarf.
  - **15%-Grenze** für anschaffungsnahe Herstellungskosten (§ 6 Abs. 1 Nr. 1a EStG) mit Warnhinweis;
    bei Überschreitung wird die Sanierung automatisch der reguläre AfA-Basis zugeschlagen statt
    sofort abgezogen zu werden.
  - **Denkmal-AfA** (§ 7i EStG, optional): Sanierungskosten werden stattdessen über 8 Jahre mit 9 %
    und weitere 4 Jahre mit 7 % abgeschrieben.
  - Annuitätischer **Zins-/Tilgungsplan** je Darlehen ([`LoanAmortizationCalculator.cs`](src/Immomanager.Web/Services/LoanAmortizationCalculator.cs),
    monatliche Simulation inkl. jährlicher Sondertilgung).
  - Jahrgenaue **30-50-Jahres-Prognose**: Mietentwicklung, AfA, Steuerlast, Cashflow vor/nach
    Steuern, Netto-Vermögensaufbau, Break-Even-Jahre, Volltilgungsjahr.
  - **Zinsänderungsrisiko** per Bisektion: maximal verkraftbare Zinserhöhung, bevor der Cashflow
    nach Steuern in Jahr 1 negativ wird.
  - **Sensitivität/Szenarien**: `CloneForScenario(...)` erzeugt eine In-Memory-Kopie mit
    angepasstem Kaufpreis/Zins/Miete — dieselbe Methode treibt sowohl die Live-Regler im
    Sensitivitäts-Tab als auch bis zu 3 gespeicherte, dauerhaft vergleichbare Szenarien
    (`CalculationScenario`).
  - Zwei Mietmodi: global (ein Betrag) oder MFH-Einheiten-Tabelle (`UnitCalculation`) mit
    individueller Ist-/Ziel-Miete und Erhöhungsjahr je Einheit.
- **Bankgespräch-Export** (`/deals/{id}/export`): druckoptimierte Zusammenfassung ohne App-Navigation
  (eigenes `PrintLayout`), Kapitalbedarf, Darlehensstruktur, Einnahmen-/Ausgaben-Aufstellung,
  Kennzahlen — Export erfolgt über die Drucken-/"Als PDF speichern"-Funktion des Browsers, es wird
  keine zusätzliche PDF-Bibliothek eingebunden.

**Bekannte Lücke:** Abschnitt 6 des Bankgesprächs-Exports ("Haushaltsrechnung & Vermögensübersicht
des Investors") erfordert persönliche Einkommens-/Vermögensdaten, die kein Bestandteil der in der
Anforderung genannten Eingabeparameter (Abschnitt 1) sind — die Exportseite weist an dieser Stelle
entsprechend darauf hin, statt Daten zu erfinden.

## KI-Exposé-Analyse

Auf der Ankaufsprüfungs-Seite (`/deals/new` bzw. `/deals/{id}`) kann ganz oben ein PDF-Exposé per
Drag-and-Drop hochgeladen werden, das automatisch ausgelesen und zur Formular-Vorausfüllung genutzt
wird:

- **PDF-Textextraktion**: [`ExposeParserService.cs`](src/Immomanager.Web/Services/ExposeParserService.cs)
  nutzt [PdfPig](https://github.com/UglyToad/PdfPig) (NuGet `UglyToad.PdfPig`, aktuell nur als
  Prerelease-Version `1.7.0-custom-5` verfügbar — legitimes, vielfach heruntergeladenes Paket der
  offiziellen Maintainer, keine Sicherheitswarnung, nur ohne sauberen "stable"-Versionstag). Rein
  bildbasierte (gescannte) PDFs ohne Text-Layer liefern keinen Text; die UI weist dann darauf hin.
- **KI-Analyse**: [`AnthropicExposeAnalysisService.cs`](src/Immomanager.Web/Services/AnthropicExposeAnalysisService.cs)
  nutzt das offizielle [Anthropic .NET SDK](https://github.com/anthropics/anthropic-sdk-csharp)
  (NuGet `Anthropic`) mit **Structured Outputs** (JSON-Schema-Zwang) statt Prompt-Disziplin zu
  vertrauen — die Antwort ist damit garantiert valides JSON passend zu `ExposeAnalysisResult`.
  Modell und API-Key sind über `Anthropic:ApiKey` / `Anthropic:Model` in `appsettings.json` (oder
  Umgebungsvariablen `Anthropic__ApiKey` / `Anthropic__Model`) konfigurierbar; Standardmodell ist
  `claude-opus-4-8` (das im Feature-Wunsch genannte `claude-3-5-sonnet` ist inzwischen
  abgekündigt/retired). **Bequemer**: über die Seite „Einstellungen" in der Navigation direkt in der
  App hinterlegbar (siehe eigener Abschnitt weiter unten) - das ist die empfohlene Variante für den
  Docker-Betrieb, da `appsettings.json` dort im Image liegt und weder im NAS-Dateisystem auffindbar
  ist noch Container-Updates übersteht.
- **Fehlt der API-Key**, zeigt die Seite statt der Upload-Box einen Hinweis mit Link zu
  „Einstellungen", statt einen API-Fehler zu riskieren (`IExposeAnalysisService.IsConfigured`).
- **Vorschau vor Übernahme**: erkannte Felder erscheinen in einem Dialog
  ([`ExposeAnalysisPreviewDialog.razor`](src/Immomanager.Web/Components/Pages/Deals/ExposeAnalysisPreviewDialog.razor))
  samt KI-Zusammenfassung; erst nach Bestätigung werden sie ins Formular übernommen und die
  betroffenen Felder kurz grün hervorgehoben (`.ai-filled-highlight` in `app.css`).
- **Mapping-Besonderheiten** (`DealEditor.ApplyExposeAnalysis`): Adresse/PLZ/Ort werden an die
  Notizen angehängt, da `DealCalculation` (anders als `Property`) kein Adressfeld hat; bei >1
  erkannter Einheit wird automatisch auf den MFH-Einheiten-Modus umgeschaltet und die erkannte
  Gesamtmiete gleichmäßig auf Platzhalter-Einheiten verteilt; ist im erkannten Hausgeld eine
  Instandhaltungsrücklage enthalten, wird sie herausgerechnet, um sie nicht doppelt in "nicht
  umlegbare Kosten" und "Instandhaltungsrücklage" zu erfassen.
- End-to-End getestet (Upload → PdfPig-Extraktion → echter API-Call → Fehlerbehandlung) mit einem
  programmatisch erzeugten Test-PDF; die KI-Antwort selbst wurde mangels echtem API-Key nicht
  live verifiziert (401-Fehlerpfad lief aber korrekt durch).

## Armin Asset (KI-Chat-Assistent)

Ein globaler Chat-Assistent, erreichbar über den Floating-Button unten rechts auf jeder Seite
(persistiert für die Dauer der Browser-Sitzung, da die Komponente in `MainLayout.razor` einmalig
eingebunden ist statt auf einzelnen Seiten):

- **Agenten-Schleife** ([`ArminAssetAgentService.cs`](src/Immomanager.Web/Services/ArminAssetAgentService.cs)):
  klassische manuelle Tool-Calling-Schleife mit der Claude Messages API (kein Beta-`ToolRunner`,
  um bei jedem Tool-Aufruf gezielt einen Live-Status ("Armin führt Werkzeug X aus...") an die UI
  zu melden). Nutzt denselben `Anthropic:ApiKey` / `Anthropic:Model` wie die Exposé-Analyse.
- **Fünf Tools**: `get_portfolio_summary`, `get_property_details`, `query_database_custom`,
  `generate_expose_pdf`, `generate_power_point`.
- **Bewusste Abweichung von der Anforderung:** `query_database_custom` lässt die KI **nicht**
  beliebiges SQL generieren und ausführen — das wäre ein "Confused Deputy"/SQL-Injection-Risiko,
  insbesondere da Objektnamen/Notizen aus der Datenbank potenziell von außen beeinflussbaren Text
  enthalten könnten (Prompt-Injection-Kette). Stattdessen liefert das Tool einen sicheren,
  schreibgeschützten JSON-Snapshot über EF Core (Immobilien + Ankaufsprüfungen kompakt
  zusammengefasst), aus dem die KI selbst die passende Information herausliest — funktional
  gleichwertig für Leseanfragen, ohne das Sicherheitsrisiko.
- **PDF-Exposé** ([`ExposePdfGenerator.cs`](src/Immomanager.Web/Services/ExposePdfGenerator.cs)):
  via [QuestPDF](https://www.questpdf.com/) (Fotos, Objektdaten, Kennzahlen, Beschreibung).
  **Lizenzhinweis:** QuestPDF ist ab Version 2023 nur für die Community-Lizenz kostenlos
  (Unternehmen/Privatpersonen mit < 1 Mio. USD Jahresumsatz) — bei Überschreiten ist eine
  kostenpflichtige Lizenz nötig, siehe [questpdf.com/pricing](https://www.questpdf.com/pricing.html).
  Da die App selbst als "privat/geschäftlich" beschrieben wurde, bitte die eigene Situation prüfen.
- **PowerPoint** ([`PropertyPowerPointGenerator.cs`](src/Immomanager.Web/Services/PropertyPowerPointGenerator.cs)):
  bewusst über `DocumentFormat.OpenXml` (offizielles, MIT-lizenziertes Microsoft-SDK) statt einer
  Drittanbieter-Bibliothek erzeugt, um jede Lizenzfrage zu vermeiden. Drei einfache Textfolien
  (Titel, Objektdaten/Kennzahlen, Finanzierung) - isoliert mit `OpenXmlValidator` gegen das
  OOXML-Schema geprüft (0 Validierungsfehler).
- Beide Generatoren speichern unter `{DataDirectory}/exports/` und sind automatisch über die
  bestehende `/data-files`-Middleware herunterladbar; der Chat zeigt nach Erstellung einen
  anklickbaren Download-Link an.
- End-to-End getestet (Chat öffnen, Nachricht senden, Tool-Aufruf, Fehlerbehandlung bei
  ungültigem Key) - die eigentliche KI-Antwort wurde mangels echtem API-Key nicht live verifiziert.

## Soll-Miete pro m² & Quartal

Jede Immobilie kann quartalsweise eine Soll-Nettokaltmiete pro m² hinterlegen (Tab „Soll-Miete“ auf
der Detailseite) und diese der aktuellen Ist-Miete gegenüberstellen:

- **Datenmodell** (`RentTarget`): pro Immobilie beliebig viele Einträge, eindeutig identifiziert
  durch (Immobilie, Jahr, Quartal) — sowohl per DB-Unique-Index als auch per Service-seitigem
  Duplikatscheck abgesichert ([`RentTargetService.cs`](src/Immomanager.Web/Services/RentTargetService.cs)),
  mit freundlicher deutscher Fehlermeldung statt DB-Exception bei Kollision.
- **Soll-Ist-Vergleich** ([`RentTargetAnalyticsService.cs`](src/Immomanager.Web/Services/RentTargetAnalyticsService.cs)):
  **bewusste Vereinfachung**, da die App keine historische Ist-Miete je Quartal führt, sondern nur
  den aktuellen Stand auf der Immobilie (`Property.CurrentColdRentMonthly`) — dieser aktuelle
  Ist-Wert pro m² wird als Vergleichsgröße für alle Quartale herangezogen, statt eine komplette
  neue Historisierung der Ist-Miete einzuführen, die so nicht angefragt war.
- **Fehlende Werte fallen auf**: fehlt für das laufende Kalenderquartal ein Soll-Wert, erscheint
  sowohl auf der Objektdetailseite als auch **portfolioweit auf dem Dashboard**
  ([`Home.razor`](src/Immomanager.Web/Components/Pages/Home.razor)) ein Warnhinweis mit Direktlink
  zur betroffenen Immobilie.
- Tabellendarstellung zeigt je Quartal Soll-/Ist-Wert, absolute und prozentuale Abweichung
  (rot bei Miete unter Soll, grün darüber) und markiert das laufende Quartal.
- **Einheiten mit abweichender Nutzung ausschließbar**: Garagen/Stellplätze werden zwar (wie jede
  Einheit) mit Fläche und Kaltmiete erfasst, unterliegen aber keinem Wohnflächen-Mietspiegel und
  würden den €/m²-Ist-Wert sonst verzerren. Über `PropertyUnit.CountsTowardRentTarget` (Checkbox
  im Einheiten-Dialog, Standard aktiviert) lässt sich das je Einheit ausschalten - ausgeschlossene
  Einheiten tragen dann weder zur Fläche noch zur Kaltmiete bei, die
  [`RentTargetAnalyticsService.BuildComparison`](src/Immomanager.Web/Services/RentTargetAnalyticsService.cs)
  für den Ist-€/m²-Wert heranzieht (sichtbar markiert in der Einheiten-Tabelle sowie als
  Berechnungsbasis-Hinweis im Soll-Miete-Tab). Bewusst **nicht** angetastet: `Property.LivingAreaSqm`
  und alle darauf basierenden Kennzahlen (KPI-Bruttorendite, Nebenkosten-€/m², Versicherungs-
  Benchmark) - dort bleiben Garagen weiterhin Teil der Gesamtfläche, da nur die Soll-Miete-
  Berechnung fachlich betroffen war.

## Einheiten (Wohnungen/Gewerbe)

Jede Immobilie besteht aus mindestens einer Einheit (Tab „Einheiten" auf der Objektdetailseite,
[`PropertyUnit.cs`](src/Immomanager.Web/Models/PropertyUnit.cs)):

- **Bewusster Architekturumbau**: Wohn-/Nutzfläche, Kaltmiete und nicht umlegbare Kosten sind keine
  eigenen Property-Spalten mehr, sondern berechnete C#-Properties, die aus der Summe der Einheiten
  entstehen (`Property.LivingAreaSqm` etc.) - dadurch können Aggregat- und Einzelwerte nie
  auseinanderlaufen. Alle bestehenden Verbraucher (KPI-Berechnung, Soll-Miete, Dashboard, Armin
  Asset, Exporte) funktionieren unverändert weiter, da sie nur diese Properties lesen.
- **Migration**: Bestehende Immobilien wurden beim Schema-Umbau automatisch in eine erste Einheit
  "Einheit 1" mit den bisherigen Summenwerten überführt (Datenmigration direkt in der
  EF-Core-Migration, per Raw-SQL vor dem Löschen der alten Spalten).
- **Neue Immobilien** starten automatisch mit einer leeren Einheit (`PropertyService.CreateAsync`),
  damit nie der Zustand "0 Einheiten" entsteht - befüllt wird sie danach im Tab „Einheiten".

## Versicherungs-Cockpit & Policen-Prüfung

Tab „Versicherungen" auf der Objektdetailseite, je Immobilie zwei Kategorien (Gebäudeversicherung,
Haus-/Grundbesitzerhaftpflicht):

- **Datenmodell**: [`InsurancePolicy.cs`](src/Immomanager.Web/Models/InsurancePolicy.cs) trägt die
  Vertragsfakten (Anbieter, Scheinnummer, Jahresbeitrag, Laufzeit, PDF-Pfad) - eindeutig je
  Immobilie+Kategorie. Die Prüf-Checkliste ([`InsuranceCheckItem.cs`](src/Immomanager.Web/Models/InsuranceCheckItem.cs))
  hängt bewusst **direkt** an der Immobilie, nicht an der Police, damit sie unabhängig von einer
  konkreten Police gepflegt werden kann.
- **Fester Prüfkatalog** ([`InsuranceCheckCatalog.cs`](src/Immomanager.Web/Services/InsuranceCheckCatalog.cs)):
  11 Prüfpunkte (Grundgefahren, Elementarschutz, WEG-Prüfung, Klauseln/Kleingedrucktes, Anpassung an
  den Neuwertfaktor für die Gebäudeversicherung; Deckungssumme, Verkehrssicherungspflichten,
  Bauarbeiten für die Haftpflicht) mit stabilem Key je Position - wird beim ersten Öffnen des Tabs
  automatisch pro Immobilie angelegt (auch nachträglich für bereits bestehende Objekte) und ist
  idempotent, falls der Katalog künftig erweitert wird.
- **Benchmark-Ampel** ([`InsuranceService.CalculateBenchmark`](src/Immomanager.Web/Services/InsuranceService.cs)):
  Jahresbeitrag ÷ Anzahl Einheiten (€/WE) sowie ÷ Wohnfläche (€/m²), verglichen mit Richtwerten
  (Gebäude 120-250 €/WE p.a., Haftpflicht 15-35 €/WE p.a.) - grün im Rahmen, rot ungewöhnlich
  günstig (Gefahr von Deckungslücken), gelb über dem Richtwert.
- **PDF-Ablage**: Policen-PDFs landen unter `{DataDirectory}/policies/<PropertyId>/...` und sind
  über `/data-files` abrufbar - damit kann Armin Asset (siehe unten) jederzeit erneut darauf
  zugreifen, ohne dass erneut hochgeladen werden muss.
- **Armin-Asset-Tool** `analyze_insurance_policy_pdf` ([`ArminAssetAgentService.cs`](src/Immomanager.Web/Services/ArminAssetAgentService.cs)):
  liest die hinterlegte PDF bei jedem Aufruf frisch ein (PdfPig, wiederverwendet über
  `IExposeParserService`) und lässt sie per Claude Structured Outputs
  ([`AnthropicInsurancePolicyAnalysisService.cs`](src/Immomanager.Web/Services/AnthropicInsurancePolicyAnalysisService.cs))
  auswerten: Vertragsfakten extrahieren, jeden Prüfpunkt der jeweiligen Kategorie mit
  abgedeckt/nicht abgedeckt/unklar bewerten, Ergebnisse in der Datenbank speichern. Bewusst nimmt
  das Tool nur `propertyId` + Kategorie entgegen, keinen vom Modell frei wählbaren Dateipfad - der
  tatsächliche PDF-Pfad wird serverseitig aus der Datenbank aufgelöst. Ein Teil des extrahierten
  Rohtexts geht zusätzlich mit der Tool-Antwort zurück, damit Armin auch offene Detailfragen zum
  Vertragstext (z. B. Selbstbeteiligung) beantworten kann, ohne dass dafür jeder denkbare Punkt im
  festen Auswerteschema stehen müsste. KI-Werte überschreiben dabei nie bereits vorhandene manuelle
  Angaben mit "nicht gefunden" (null), sondern ergänzen nur, was neu erkannt wurde.
- End-to-End getestet (Einheiten/Police/Checkliste im Browser angelegt, Benchmark-Berechnung
  verifiziert, Armin-Tool-Aufruf bis zum Anthropic-Request durchlaufen) - die eigentliche
  KI-Antwort wurde mangels echtem API-Key nicht live verifiziert (401-Fehlerpfad lief aber korrekt
  durch und wurde geloggt).

## Nebenkosten- & Betriebskosten-Analytiker

Tab „Nebenkosten" auf der Objektdetailseite, eine Abrechnung je Immobilie und Kalenderjahr:

- **Datenmodell**: [`UtilityStatement.cs`](src/Immomanager.Web/Models/UtilityStatement.cs) (Jahr,
  Gesamtsumme, PDF-Pfad, Geprüft-Flag, eindeutig je Immobilie+Jahr) mit 1:N
  [`UtilityCostItem.cs`](src/Immomanager.Web/Models/UtilityCostItem.cs) (Kategorie gemäß
  Betriebskostenverordnung, Beschreibung, Betrag). Bewusst bleibt „TotalCosts" ein eigenständig
  gepflegtes Feld statt einer aus den Positionen berechneten Summe - es bildet den auf der echten
  Abrechnung ausgewiesenen Gesamtbetrag ab, der nicht zwingend exakt der Summe der (ggf.
  unvollständig erfassten) Einzelpositionen entsprechen muss.
- **Drill-Down** ([`UtilityCostCatalog.cs`](src/Immomanager.Web/Services/UtilityCostCatalog.cs)):
  10 BetrKV-Kategorien werden zu 4 Hauptgruppen zusammengefasst (Warmkosten/Heizung, Kommunale
  Abgaben, Betrieb & Pflege, Versicherungen) und als aufklappbare Abschnitte mit Gruppensumme,
  Anteil am Gesamtvolumen sowie den Einzelpositionen dargestellt.
- **Kennzahlen** ([`UtilityService.CalculateKpi`](src/Immomanager.Web/Services/UtilityService.cs)):
  Gesamtkosten Haus, Ø pro Einheit/Jahr (nutzt die Einheiten-Anzahl aus dem Umbau oben), €/m² p.a.
  und €/m²/Monat - letzteres explizit zum Abgleich mit den Betriebskostenvorauszahlungen der Mieter.
- **Dashboard-Erinnerung**: Warnbanner, sobald für das Vorjahr (aktuelles Jahr − 1) für mindestens
  eine Immobilie noch keine Abrechnung vorliegt, mit Direkt-Link je betroffenem Objekt.
- **Portfolio-Vergleich** (Dashboard, unterhalb „Objekte im Überblick"): sortierbare Tabelle aller
  Immobilien mit Abrechnung im gewählten Jahr (Fläche, Einheiten, Kosten, €/m²/Jahr, €/m²/Monat) plus
  ein selbst gezeichnetes SVG-Balkendiagramm
  ([`UtilityBenchmarkChart.razor`](src/Immomanager.Web/Components/Pages/UtilityBenchmarkChart.razor))
  mit gestrichelter Referenzlinie (Standard: 2,80 €/m²/Monat) und roter Einfärbung für Objekte über
  dem Schwellenwert (Standard: 3,50 €/m²). Bewusst kein `MudChart`: das färbt Balken nur pro Series
  einheitlich statt pro Objekt abhängig vom Wert und bietet keine Referenzlinie mit Label.
  **Gefundener und behobener Bug:** Die App-weite de-DE-Kultur ließ SVG-Koordinaten anfangs mit
  Komma statt Punkt als Dezimaltrennzeichen rendern (`58,7` statt `58.7`) - laut SVG/XML-Spezifikation
  ungültig, wodurch der Browser die Balken gar nicht zeichnete. Alle numerischen SVG-Attribute werden
  daher jetzt explizit mit `CultureInfo.InvariantCulture` formatiert.
- **Armin-Asset-Tool** `analyze_utility_statement_pdf`: liest die hinterlegte Abrechnungs-PDF bei
  jedem Aufruf frisch ein und lässt sie per Claude Structured Outputs
  ([`AnthropicUtilityStatementAnalysisService.cs`](src/Immomanager.Web/Services/AnthropicUtilityStatementAnalysisService.cs))
  auswerten: Gesamtsumme und alle Kostenpositionen samt BetrKV-Kategorie extrahieren, in der
  Datenbank speichern (bei erneuter Analyse werden vorherige Positionen ersetzt statt dupliziert,
  da es hier - anders als bei der festen Versicherungs-Checkliste - keinen stabilen Schlüssel für
  einzelne Positionen gibt) und die Dashboard-Erinnerung für das Jahr damit auflösen. Das von der KI
  im Dokument erkannte Jahr wird nur informativ zurückgegeben, nicht für die Datenbank-Zuordnung
  verwendet (sonst könnte ein KI-Lesefehler versehentlich die falsche Jahres-Abrechnung überschreiben).
- End-to-End getestet (Abrechnung/Positionen im Browser angelegt, Kennzahlen und Drill-Down-Prozente
  verifiziert, Dashboard-Vergleich inkl. Sortierung und Balkendiagramm mit echtem Ausreißer-Fall
  geprüft, Armin-Tool-Aufruf bis zum Anthropic-Request durchlaufen) - die eigentliche KI-Antwort
  wurde mangels echtem API-Key nicht live verifiziert (401-Fehlerpfad lief aber korrekt durch und
  wurde geloggt).

## Mietverhältnisse

Jede Einheit hat eine eigene Detailseite (`/properties/{id}/units/{unitId}`, erreichbar über die
Bezeichnung im Tab „Einheiten"), auf der Mietverhältnisse verwaltet werden:

- **Datenmodell** ([`Tenancy.cs`](src/Immomanager.Web/Models/Tenancy.cs)): 1:N an `PropertyUnit`, da
  eine Einheit über die Zeit mehrere Mietverhältnisse hat (aktuelles + historische). „IsCurrent" wird
  bewusst aus dem Auszugsdatum abgeleitet statt separat gepflegt. Die vertragliche Kaltmiete/
  Nebenkostenvorauszahlung wird bewusst unabhängig von `PropertyUnit.ColdRentMonthly` gespeichert,
  damit historische Mietverhältnisse ihren damaligen Stand behalten, auch wenn die Einheit später auf
  einen neuen Mieter mit anderer Miete aktualisiert wird.
- **Einheiten-Übersicht**: zeigt je Einheit den aktuellen Mieter oder „Leerstand" auf einen Blick.
- **Mietvertrags-Upload**: PDFs landen unter `{DataDirectory}/leases/<UnitId>/...`. Ein Upload legt
  immer ein neues Mietverhältnis mit Platzhalter-Mieterdaten an (anders als bei Police/Abrechnung gibt
  es hier keinen natürlichen Schlüssel, über den sich ein Upload einem vorhandenen Mietverhältnis
  eindeutig zuordnen ließe - ein neuer Vertrag ist inhaltlich ohnehin ein neues Mietverhältnis).
- **Armin-Asset-Tool** `analyze_lease_pdf`: liest den hinterlegten Mietvertrag bei jedem Aufruf frisch
  ein (wiederverwendet `IExposeParserService`) und lässt ihn per Claude Structured Outputs
  ([`AnthropicLeaseAnalysisService.cs`](src/Immomanager.Web/Services/AnthropicLeaseAnalysisService.cs))
  auswerten: Mieter, Kontaktdaten, Mietbeginn/-ende, Kaltmiete, Nebenkostenvorauszahlung und Kaution
  extrahieren und das Mietverhältnis damit automatisch vervollständigen. `get_property_details` wurde
  um eine Einheiten-Liste (Id, Bezeichnung, aktueller Mieter) erweitert, damit Armin eine
  natürlichsprachliche Einheiten-Referenz (z. B. "die Wohnung im EG") auf die passende `unitId`
  auflösen kann, ohne dass der Nutzer eine rohe Datenbank-Id nennen müsste. Ein Teil des extrahierten
  Rohtexts geht mit der Tool-Antwort zurück, damit Armin auch offene Detailfragen zum Vertragstext
  (z. B. Kündigungsfristen) beantworten kann.
- **Armin-Asset-Tool** `generate_tenant_letter` ([`TenantLetterPdfGenerator.cs`](src/Immomanager.Web/Services/TenantLetterPdfGenerator.cs)):
  erstellt Mahnungen, einfache Anschreiben oder Kündigungsentwürfe als PDF. Bewusste Aufgabenteilung:
  Claude formuliert Betreff und Brieftext selbst (er kennt den konkreten Anlass aus dem
  Gesprächsverlauf - offene Beträge, Fristen, Kündigungsgrund), das Tool übernimmt nur den korrekten
  Absender-/Empfänger-/Objektbezug aus der Datenbank und die Formatierung als Brief mit Fußzeilen-
  Hinweis. **Bewusste Sicherheitsgrenze:** Das Tool erzeugt ausschließlich einen Entwurf zum Download -
  es verschickt nichts selbst (weder postalisch noch per E-Mail); der System-Prompt weist Armin an,
  besonders bei Kündigungen auf eine rechtliche Prüfung vor Versand hinzuweisen (im deutschen
  Mietrecht gelten strenge Form- und Fristvorschriften).
- End-to-End getestet (Mietverhältnis manuell angelegt, Mietvertrag hochgeladen und Download
  verifiziert, Belegungsstatus in der Einheiten-Übersicht geprüft, beide Armin-Tool-Aufrufe bis zum
  Anthropic-Request durchlaufen) - die eigentliche KI-Antwort wurde mangels echtem API-Key nicht live
  verifiziert (401-Fehlerpfad lief aber korrekt durch und wurde geloggt).

## Dokumente & zentrales Aufgaben-Dashboard

Zwei zusammenhängende Ergänzungen: eine freie Dokumentenablage auf Objekt- und Einheiten-Ebene,
und eine zentrale Aufgabenliste auf dem Dashboard, die alle im Laufe der App entstandenen
Vollständigkeits-Prüfungen (Soll-Miete, Nebenkostenabrechnung, jetzt auch Dokumente) bündelt.

- **Datenmodell**: bewusst zwei getrennte Entitäten statt einer polymorphen Tabelle mit doppelter
  Fremdschlüssel-Option, passend zum Muster der übrigen 1:N-Kindentitäten in der App:
  [`PropertyDocument.cs`](src/Immomanager.Web/Models/PropertyDocument.cs) (1:N an `Property`, Typen
  Energieausweis/Grundbuchauszug/Teilungserklärung/Baugenehmigung/Sonstiges) und
  [`UnitDocument.cs`](src/Immomanager.Web/Models/UnitDocument.cs) (1:N an `PropertyUnit`, Typen
  Grundriss/Übergabeprotokoll/Sonstiges). Anders als bei Police/Abrechnung gibt es bewusst **keine**
  Eindeutigkeits-Beschränkung je Typ - ein Energieausweis wird z. B. im Zeitverlauf erneuert, ohne
  dass die alte Fassung zwingend gelöscht werden muss; die "Sonstiges"-Kategorie fängt alles ab, was
  über die explizit genannten Beispiele hinausgeht.
  [`DocumentService.cs`](src/Immomanager.Web/Services/DocumentService.cs) akzeptiert PDF, JPG, PNG
  oder WEBP (max. 20 MB), da Grundrisse in der Praxis oft als Bild statt als PDF vorliegen.
- **Ablage**: unter `{DataDirectory}/documents/properties/<PropertyId>/...` bzw.
  `.../documents/units/<UnitId>/...`, über `/data-files` abrufbar wie alle übrigen Uploads.
- **Zentrales Aufgaben-Widget** ([`DashboardTodoService.cs`](src/Immomanager.Web/Services/DashboardTodoService.cs)):
  fasst vier Prüfungen zu einer Liste zusammen (fehlender Soll-Mietwert fürs laufende Quartal,
  fehlende Nebenkostenabrechnung fürs Vorjahr, fehlender Energieausweis je Objekt, fehlender
  Grundriss je Einheit) und ersetzt die bisherigen, verstreuten Einzel-Banner auf dem Dashboard
  ([`Home.razor`](src/Immomanager.Web/Components/Pages/Home.razor)). Jeder Eintrag ist klickbar und
  führt direkt zur Stelle, an der er behoben werden kann. Neue Prüfungen für künftige Module werden
  als weiterer Block in `GetOpenItemsAsync()` ergänzt, statt wieder ein eigenes Banner zu bauen.
- **Direktnavigation zu Tabs**: die Objektdetailseite unterstützt `?tab=<schlüssel>` als
  Query-Parameter (`dokumente`, `einheiten`, `finanzierungen`, `fotos`, `renovierungen`,
  `versicherungen`, `nebenkosten`, `sollmiete`, `kpi`) über `[SupplyParameterFromQuery]` und aktiviert
  beim Laden automatisch den passenden Tab (`PropertyDetail.razor`) - darüber springen die
  TODO-Einträge für Soll-Miete/Nebenkosten/Energieausweis direkt in den richtigen Tab. Grundriss-
  Einträge verlinken auf die (nicht tab-basierte) Einheiten-Detailseite, deren neue
  „Dokumente"-Sektion direkt sichtbar ist.
- **Bewusst nicht per-Einheit für Soll-Miete**: die Anforderung nannte als Beispiel "Soll-Miete für
  Einheit xy fehlt", das bestehende `RentTarget`-Modell führt Soll-Werte aber weiterhin nur je
  Immobilie (nicht je Einheit) - eine Umstellung der Granularität wäre ein größerer, hier nicht
  angefragter Umbau. Das TODO-Widget zeigt den fehlenden Soll-Wert daher weiterhin auf Objektebene.
- End-to-End getestet (Dokument je Ebene hochgeladen und wieder gelöscht inkl. Dateisystem-Check,
  TODO-Liste vor/nach Upload verglichen, Klick-Navigation zu Tab und Einheiten-Seite verifiziert).

## Backup & Wiederherstellung

Menüpunkt „Backup & Wiederherstellung" (`/backup`, [`BackupService.cs`](src/Immomanager.Web/Services/BackupService.cs)):

- **Sicherungsmechanik**: nutzt bewusst die SQLite-eigene Online-Backup-API
  (`SqliteConnection.BackupDatabase`) statt eines rohen Datei-Kopierens, damit ein Backup auch bei
  laufendem Betrieb (offene Verbindungen) konsistent ist. Gesichert wird nur die SQLite-Datenbank,
  keine hochgeladenen Fotos oder generierten Exporte.
- **Ablage**: Sicherungsdateien landen unter `{Storage:DataDirectory}/backups/` (damit im
  Docker/Synology-Volume enthalten) und sind über die bestehende `/data-files`-Middleware
  herunterladbar.
- **Automatisch vs. manuell**: wird rein über das Dateinamens-Präfix (`auto_`/`manual_`)
  unterschieden - es gibt bewusst keine Metadaten-Tabelle in der (damit gesicherten) Datenbank
  selbst. Beim Bereinigen alter Sicherungen werden dadurch garantiert **nur automatische
  Sicherungen** gelöscht, nie manuell erstellte.
- **Automatisches Backup bei Programmstart**: per Checkbox aktivierbar, mit Dropdown "Behalte die
  letzten X Sicherungen" (1-20). Läuft in `Program.cs` direkt nach der DB-Migration; ein Fehler
  dabei verhindert nicht den App-Start, wird aber geloggt.
  Einstellung wird in einer eigenen `backup-settings.json` im Datenverzeichnis gespeichert (bewusst
  nicht in `appsettings.json`, das Deployment-Konfiguration ist, und nicht in der SQLite-DB selbst,
  da ein Restore auf einen alten Stand sonst die Backup-Einstellung mit zurücksetzen würde).
- **Wiederherstellung**: entweder aus einer vorhandenen Sicherung in der Liste oder durch Hochladen
  einer `.db`-Datei. Vor jedem Restore wird automatisch eine Sicherheitskopie des aktuellen Stands
  angelegt (zählt selbst als automatische Sicherung). Die hochgeladene/gewählte Datei wird per
  SQLite-Header-Prüfung validiert, bevor irgendetwas überschrieben wird.
  Da Microsoft.Data.Sqlite Verbindungen poolt, wird vor dem Überschreiben `SqliteConnection.ClearAllPools()`
  aufgerufen, damit die Zieldatei nicht durch eine noch offene gepoolte Verbindung gesperrt ist;
  danach werden eventuelle `-wal`/`-shm`-Restdateien entfernt.
  Nach erfolgreichem Restore erzwingt die Seite einen vollständigen Browser-Reload
  (`NavigationManager.NavigateTo(..., forceLoad: true)`), damit alle Seiten wieder frische Daten aus
  der wiederhergestellten Datenbank laden.
- **Warnhinweis**: Restore läuft ausschließlich über einen eigenen Bestätigungsdialog
  ([`RestoreConfirmDialog.razor`](src/Immomanager.Web/Components/Pages/Backup/RestoreConfirmDialog.razor))
  mit rot hervorgehobenem Warnhinweis und einer Pflicht-Checkbox ("Ich verstehe, dass alle
  aktuellen Daten unwiderruflich überschrieben werden"), die den Wiederherstellen-Button erst
  freischaltet.

## Darlehen: Darlehensart & Restschuld-Berechnung

Tab „Finanzierungen" auf der Objektdetailseite:

- **Darlehensart** (`LoanType`: Annuität/Endfällig/Sonstiges) klassifiziert jedes Darlehen und wird
  als Chip neben dem Banknamen angezeigt.
- **„Restschuld berechnen"**: aus „Beginn Berechnung" (i. d. R. Auszahlungsdatum), Sollzins und
  Monatsrate projiziert [`LoanAmortizationCalculator.ProjectRemainingDebt`](src/Immomanager.Web/Services/LoanAmortizationCalculator.cs)
  die heutige Restschuld - bei Annuität über eine monatliche Tilgungsplan-Simulation, bei Endfällig
  bleibt sie konstant beim Ursprungsbetrag (nur Zinszahlung, keine Tilgung bis zur Fälligkeit).
  Gegen eine echte Bank-Restschuldangabe verifiziert (80.000 €, 3,71 % p.a., 380,67 €/Monat über 119
  Monate): 60.856,02 € berechnet vs. 60.651,62 € Bankangabe, Abweichung 0,3 % (Rundungs-/
  Zinstageffekte).
- **Bewusst nur Vorschlag, kein Zwang**: das Ergebnis füllt lediglich das ohnehin frei editierbare
  Feld „Aktuelle Restschuld" - reale Sondertilgungen o. Ä. werden nicht nachgebildet, das Feld bleibt
  jederzeit manuell überschreibbar.

## Fachliche Kennzahlen

Berechnungslogik in [`Services/KpiCalculationService.cs`](src/Immomanager.Web/Services/KpiCalculationService.cs):

- Bruttomietrendite = Jahreskaltmiete / Kaufpreis
- Nettomietrendite = (Jahreskaltmiete − nicht umlegbare Kosten) / Gesamtkaufkosten
- Cashflow vor Steuern = Kaltmiete − nicht umlegbare Kosten − Kapitaldienst
- Cash-on-Cash Return = Jahres-Cashflow / eingesetztes Eigenkapital
- ROI = (aktueller Eigenkapitalwert − eingesetztes Eigenkapital) / eingesetztes Eigenkapital
- LTV = Restschuld / aktueller Marktwert
- Eigenkapital-Spiegel = Σ Marktwerte − Σ Restschulden (jeweils unter Berücksichtigung der
  Beteiligungsquote im Modus „Mein Anteil“)
