# Glass Taskbar Studio

A premium customization app for the **real Windows taskbar** - Liquid Glass by default, every aspect adjustable, fully reversible.
(WPF / .NET 8, no admin rights, Windows 11 primarily, Windows 10 where Windows allows it.)

> This repo also contains **GlassBar** (`GlassBar/`), an older, separate project that *replaces* the taskbar with its own dock window. Glass Taskbar Studio does not replace anything: it restyles the taskbar Windows already runs.

## Download
Every push to `master` is built by GitHub Actions and published as a release containing `GlassTaskbarStudio.exe` (single file, no .NET install needed) - see **Releases**. It is unsigned, so SmartScreen may ask you to confirm.

## What you get
- **Default look out of the box:** centered icons, floating rounded glass, blur, gloss, rim light, soft shadow.
- **Dashboard** with a large live preview, preset cards, active theme, Apply / Reset / Restore buttons.
- **Customization:** Appearance, Transparency & Blur, Colors, Position & Layout, Animations, Presets, Advanced.
- **Presets:** Liquid Glass, Windows 11 Pro, Midnight, Frosted Glass, Minimal, Neon - one click, applied instantly with a smooth morph.
- **Persistence:** settings in `%AppData%\GlassTaskbarStudio\settings.json`; starts with Windows silently in the tray and re-applies your look.
- **Safety:** original Explorer values are backed up; *Restore Windows taskbar* in the app and tray; `GlassTaskbarStudio.exe --reset` as an emergency exit; quitting the app always hands the taskbar back to Windows.

## How each feature really works (nothing is faked)
| Badge | Technique | Features |
|---|---|---|
| **NATIVE** | Accent policy on the real taskbar window (`SetWindowCompositionAttribute`, the same mechanism Windows' own UI uses; works on Win10/11) | Material (Clear / Blur / Acrylic / Opaque), transparency, tint color, all monitors |
| **REGION** | The taskbar window is clipped to a custom shape (`SetWindowRgn`); the taskbar stays fully functional | Corner radius, floating gaps, compact "island" mode, fixed/auto width |
| **OVERLAY** | A click-through, never-focused layered window drawn on top of the taskbar | Gloss, rim light, border, shadow/glow, shimmer |
| **REGISTRY** | `HKCU\...\Explorer\Advanced` (`TaskbarAl`, `TaskbarSi`) - Windows 11 | Icon alignment (left/center), size (small/medium/large; needs an Explorer restart) |
| **APP** | Inside this app | Transitions, autostart, refresh interval, UI animations |

**Auto-fit** compact mode measures the real icon positions with UI Automation and follows them as apps open/close.

### Limits imposed by Windows (explained in-app, controls disabled when unsupported)
- No adjustable **blur radius** - Windows exposes only Blur and Acrylic, so there is deliberately no blur slider.
- On some Windows 11 builds the taskbar keeps a faint **system tint** of its own on top of the glass.
- Windows 11 supports only a **bottom** taskbar; top/left/right docking and arbitrary heights are not possible on the real taskbar.
- Windows 10 cannot center icons or use compact mode.
- Windows' own icon hover/launch animations cannot be hooked.
- Effects are active **while the app runs** (it lives in the tray). Overlay shadows may slightly touch the Start menu edge when it opens.

## Build & run
```
dotnet publish GlassTaskbarStudio/GlassTaskbarStudio.csproj -c Release -o publish
publish\GlassTaskbarStudio.exe
```
Requires Windows with the .NET 8 SDK. Tests (pure logic: geometry, settings, presets, colors, persistence): `dotnet test GlassTaskbarStudio.Tests`.

## Project layout
```
GlassTaskbarStudio/
  Core/   TaskbarSettings, Presets, LayoutCalculator (pure geometry, unit-tested), TaskbarEngine (apply/restore),
          TaskbarMeasure (UI Automation), OverlayWindow, SystemTweaks (registry/autostart), AppConfig, Native (P/Invoke)
  UI/     MainWindow + MainViewModel, Theme.xaml (glass design system), Controls (live preview, color picker, setting rows),
          Pages (Dashboard, Appearance, Transparency, Colors, Layout, Animations, Presets, Advanced), TrayService
GlassTaskbarStudio.Tests/   xunit tests
GlassBar/                   older standalone dock project
```
The live preview and the real engine share the same `LayoutCalculator` and `GlassPillVisual`, so what you see is what the taskbar gets (the frosted backdrop in the preview is a simulation).
