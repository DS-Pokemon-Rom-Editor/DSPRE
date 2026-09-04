# Building DSPRE

## Projects

| Project | Target | What it is |
|---|---|---|
| `DSPRE.Core` | `net8.0` | Cross-platform ROM core: formats, ROM data, and scripts |
| `Ekona`, `Images` | `net8.0` | Pixel and Nintendo DS image-format libraries |
| `DSPRE.Avalonia` | `net8.0` | Cross-platform Avalonia app for Windows and Linux |
| `DSPRE` | `net8.0-windows` | Windows host executable; Avalonia by default, with the legacy WinForms shell retained |
| `DSPRE.Tests` | `net8.0`, plus `net8.0-windows` on Windows | xUnit tests; the Windows target adds tests that need GDI+ or the Windows host |

## Prerequisites

- .NET SDK 8.0 (`dotnet --version`)
- On Linux, install the .NET 8 SDK using the instructions for your distribution at
  https://learn.microsoft.com/dotnet/core/install/linux.

## Windows

```powershell
dotnet build DS_Map.sln
dotnet test DSPRE.Tests/DSPRE.Tests.csproj -f net8.0
```

Run profiles in the `DSPRE` project:

- **DSPRE** starts the Avalonia shell and is the default.
- **DSPRE (beta editors on)** passes `--beta`.
- **DSPRE (old WinForms shell)** sets `DSPRE_WINFORMS_SHELL=1`.
- `DSPRE.Avalonia` starts the same Avalonia UI without loading WinForms into the process.

Omit `-f net8.0` when the Windows-only test target is relevant. On Windows that runs both configured
targets; on other platforms the project selects the cross-platform target.

## Linux

The solution builds on Linux. Windows-only projects set `EnableWindowsTargeting` automatically, so
they compile but cannot run there.

```bash
dotnet build DS_Map.sln
dotnet build DSPRE.Avalonia/DSPRE.Avalonia.csproj
dotnet run --project DSPRE.Avalonia/DSPRE.Avalonia.csproj
```

Solution configurations `Debug-Linux` and `Release-Linux` build the cross-platform projects, with
`DSPRE.Avalonia` targeting `linux-x64`:

```bash
dotnet build DS_Map.sln -c Release-Linux
```

### Distributable output

```bash
dotnet publish DSPRE.Avalonia/DSPRE.Avalonia.csproj -p:PublishProfile=linux-x64
dotnet publish DSPRE.Avalonia/DSPRE.Avalonia.csproj -p:PublishProfile=win-x64
```

### Runtime pieces beside the executable

- `Tools/` contains the Windows helpers and the native helpers currently bundled for Linux.
  Extensionless `chatot`, `dsrom`, `rotom`, and `rotom-lsp` binaries are present, together with
  `libnitroarc_ffi.so`. `ndstool` and `blz` are currently `.exe`-only and require WSL interop or Wine
  outside Windows. `DSUtils.ToolPath()` resolves this directory from `AppContext.BaseDirectory` and
  prefers a native binary off Windows. CI builds `dsrom` from
  https://github.com/DS-Pokemon-Rom-Editor/ds-rom and builds the 0BSD-licensed `apicula` for Windows
  and Linux from https://github.com/scurest/apicula at pinned revision
  `3d4e91e14045392a49c89e86dab8cb936225588c`. The checked-in `Tools/apicula.exe` remains available
  for ordinary local builds; release workflows replace it with the pinned source build.
- `databases/` is a clone of https://github.com/DS-Pokemon-Rom-Editor/scrcmd-database. It is copied to
  the per-user data directory on first run. Without it and without network access, ROM loading still
  works but script editing is limited.

## CI

- `.github/workflows/base-build-nightly.yaml` builds canary Windows and Linux artifacts from `main`.
- `.github/workflows/avalonia-canary-build.yaml` builds only the Avalonia app on Windows and Linux from
  `feature/avalonia` or by manual dispatch.
- `.github/workflows/beta-build-nightly.yml` builds canary artifacts from `dev` and `beta`.
- `.github/workflows/update-releases.yaml` builds stable Velopack packages for the Windows and Linux
  channels from `main` or by manual dispatch.
