# Building DSPRE

## Projects

| Project | Target | What it is |
|---|---|---|
| `DSPRE.Core` | `net8.0` | Cross-platform ROM core (formats, ROM data, script system) |
| `Ekona`, `Images` | `net8.0` | Pixel/format engine (Tinke-derived) |
| `DSPRE.Avalonia` | `net8.0` | **Cross-platform app** (Avalonia UI) — runs on Windows and Linux |
| `DSPRE` | `net8.0-windows` | Legacy Windows app (WinForms shell hosting the same Avalonia UI) |
| `DSPRE.Tests` | `net8.0-windows` | xunit tests (some exercise GDI+, so they only *run* on Windows) |

## Prerequisites

- .NET SDK **8.0** (`dotnet --version`)
  - Linux: `sudo apt install dotnet-sdk-8.0` (Ubuntu 22.04+) or see
    https://learn.microsoft.com/dotnet/core/install/linux

## Windows

```powershell
dotnet build DS_Map.sln            # everything
dotnet test  DSPRE.Tests           # run the tests
```

Run profiles (VS dropdown on the `DSPRE` project): **WinForms shell** or **Avalonia shell**
(the latter just sets `DSPRE_AVALONIA_SHELL=1`). `DSPRE.Avalonia` is always the pure Avalonia app.

## Linux

Everything builds on Linux (the Windows-only projects compile but can't run there —
they set `EnableWindowsTargeting` automatically):

```bash
dotnet build DS_Map.sln                          # whole solution
# or just the app you can actually run:
dotnet build DSPRE.Avalonia/DSPRE.Avalonia.csproj
dotnet run --project DSPRE.Avalonia
```

Solution configurations `Debug-Linux` / `Release-Linux` build only the cross-platform
projects, with `DSPRE.Avalonia` targeting the `linux-x64` runtime:

```bash
dotnet build DS_Map.sln -c Release-Linux
```

### Distributable output

```bash
dotnet publish DSPRE.Avalonia -p:PublishProfile=linux-x64   # or win-x64
```

### Runtime pieces the app expects next to the executable

- `Tools/` — native helpers, named **without** extension on Linux:
  `ndstool`, `dsrom`, `blz`, `apicula`, `rotom`, `rotom-lsp`
  (`DSUtils.ToolPath()` appends `.exe` on Windows only). CI builds `dsrom` from
  https://github.com/DS-Pokemon-Rom-Editor/ds-rom.
- `databases/` — a clone of https://github.com/DS-Pokemon-Rom-Editor/scrcmd-database
  (copied to the per-user data dir on first run; without it and without internet,
  ROM loading still works but script editing is limited).

## CI

- `.github/workflows/base-build-nightly.yaml` — canary: Windows zip + Linux tar.gz.
- `.github/workflows/avalonia-canary-build.yaml` — canary: `DSPRE.Avalonia` only (no WinForms
  build), Windows + Linux, triggered on push to `feature/avalonia`.
- `.github/workflows/beta-build-nightly.yml` — canary build off the `beta` branch.
- `.github/workflows/update-releases.yaml` — stable: Velopack packages for the
  `win` channel (installer) and `linux` channel (AppImage) on the same release;
  the in-app updater picks the channel matching its OS.
