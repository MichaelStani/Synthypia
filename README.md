# Synthypia

**Klavier lernen im Synthesia-Stil – mit eigenem Klang, eigener Wertung und Light-Guide-Unterstützung.**

Synthypia lädt MIDI-Dateien, zeigt die Noten als fallende Balken über einer Klaviatur
und begleitet das Üben mit Warte-Modus, Wertung und Abschnittswiederholung. Als Klangquelle dient
wahlweise ein Windows-MIDI-Gerät oder ein beliebiges VST2/VST3-Instrument-Plugin, das Synthypia selbst hostet.

## Funktionen

- **Fallende Noten** über einer Klaviatur (49, 61, 88 Tasten oder automatisch ans Stück angepasst)
- **Scrollende Notenschrift** (Klaviersystem) als dezente Ergänzung
- **MIDI-Dateien** laden (`.mid`, `.midi`, `.kar`), Spuren einzeln der rechten/linken Hand zuordnen oder ausblenden
- **Drei Übungsmodi**
  - *Zuschauen* – das Stück spielt sich selbst
  - *Warten* – die Wiedergabe hält an, bis du die richtige Taste drückst
  - *Wertung* – getroffene und verpasste Noten werden gezählt
- **Hände getrennt üben** (beide / nur rechts / nur links)
- **A-B-Schleife** und Tempoanpassung zum gezielten Üben schwieriger Stellen
- **Plugin-Hosting:** VST2 und VST3 (eigenes natives Hostmodul auf Basis des Steinberg-VST3-SDK), inklusive Plugin-Oberfläche; der Plugin-Zustand wird pro Plugin gespeichert
- **MIDI-Eingang:** echtes Keyboard anschließen oder per PC-Tastatur spielen (QWERTZ/QWERTY)
- **Light Guide:** zeigt die nächsten Noten auf der Tastenbeleuchtung von Native-Instruments-Keyboards (Kontrol S-Serie MK3) an; das MIDI des Keyboards bleibt dabei voll nutzbar
- **Modernes dunkles Design** im Windows-11-Stil mit eigener Titelleiste und Startbildschirm

## Aufbau

| Ordner | Inhalt |
|---|---|
| `Synthypia/` | WinForms-Anwendung (C#, .NET Framework 4.8): Oberfläche, Wiedergabe-Engine, MIDI/Audio-Ausgabe, VST2-Host, Light-Guide-Client |
| `SynthypiaVst3/` | Natives VST3-Hostmodul (C++17, `SynthypiaVst3.dll`), wird von der Anwendung per P/Invoke geladen |
| `third_party/vst3sdk/` | Steinberg VST3 SDK (MIT-Lizenz, siehe die jeweiligen `LICENSE.txt`) |

## Voraussetzungen

- Windows 10/11 (x64)
- Visual Studio mit den Workloads *.NET-Desktopentwicklung* und *Desktopentwicklung mit C++*
- .NET Framework 4.8 Developer Pack

## Bauen

1. Natives Hostmodul bauen (Konfiguration `Debug` oder `Release`):

   ```
   SynthypiaVst3\build.cmd Release
   ```

   Das Skript ruft `vcvars64.bat` auf; falls Visual Studio woanders installiert ist, den Pfad in `build.cmd` anpassen.
   Die DLL landet in `Synthypia\bin\<Konfiguration>`.

2. `Synthypia.slnx` in Visual Studio öffnen und die Anwendung bauen (Plattform **x64**).

## Start

`Synthypia.exe` starten, MIDI-Datei öffnen, Klangquelle (Windows-MIDI-Gerät oder Plugin via *Plugin laden…*) und
Eingabegerät wählen, Übungsmodus einstellen und losspielen.

## Lizenz

Noch nicht festgelegt. Das VST3 SDK unterliegt seiner eigenen Lizenz (MIT).
