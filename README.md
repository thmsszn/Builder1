# GlassBar – gläserne Taskleiste für Windows 10/11

Ersetzt die Windows-Taskleiste durch eine glänzende Acrylic-Glas-Leiste (WPF, .NET 8).

## Features
- Glossy-Acrylglas-Optik mit Glanzverlauf, wählbare Glasfarbe & Intensität
- Echte AppBar: reserviert Platz, maximierte Fenster überdecken sie nicht (unten oder oben)
- Laufende Fenster mit Icons, aktives Fenster hervorgehoben; Klick = öffnen/minimieren, Rechtsklick = Minimieren/Schließen
- Angeheftete Apps (Drag & Drop von Dateien auf die Leiste, Rechtsklick zum Lösen)
- Start, Suche, Aufgabenansicht, Benachrichtigungen, Desktop anzeigen
- CPU-, RAM- und Akku-Anzeige, Lautstärke (Buttons + Mausrad), Uhr mit Datum
- Windows-Taskleiste wird ausgeblendet (abschaltbar) und beim Beenden wiederhergestellt
- **Autostart** über `HKCU\...\Run` (standardmäßig an, in den Einstellungen schaltbar)
- Tray-Icon, nur eine Instanz

## Bauen
Auf Windows: `dotnet publish GlassBar/GlassBar.csproj -c Release -o publish` → `publish/GlassBar.exe` (Single-File, ohne .NET-Installation lauffähig).
Oder über GitHub Actions (Workflow „Build GlassBar“, Artefakt `GlassBar-win-x64`).

## Notfall
Falls die Windows-Taskleiste nach einem Absturz fehlt: `GlassBar.exe --restore` ausführen (oder explorer.exe neu starten).
Einstellungen: `%AppData%\GlassBar\settings.json`.
