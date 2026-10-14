# Synthypia

Windows-Anwendung (C#, .NET Framework 4.8) mit VST2/VST3-Plugin-Hosting.

- `Synthypia/` – WinForms-Anwendung
- `SynthypiaVst3/` – natives VST3-Host-Modul (C++, `build.cmd`)
- `third_party/vst3sdk/` – VST3 SDK (MIT-Lizenz, siehe jeweilige `LICENSE.txt`)

## Bauen

1. `SynthypiaVst3\build.cmd [Debug|Release]` (MSVC erforderlich; ggf. Pfad zu `vcvars64.bat` anpassen)
2. `Synthypia.slnx` in Visual Studio öffnen und bauen.
