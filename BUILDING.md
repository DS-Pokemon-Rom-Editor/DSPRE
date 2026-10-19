# Building DSPRE

## Projects

| Project | Target | What it is |
|---|---|---|
| `DSPRE.Core` | `net8.0` | Cross-platform ROM core: formats, ROM data, and scripts |
| `DSPRE.Avalonia` | `net8.0` | The app, for Windows and Linux |
| `DSPRE.Tests` | `net8.0` | xUnit tests |

## Prerequisites

- .NET SDK 10 (9.0.300 or newer works; `global.json` refuses older), and the .NET 8 runtime to run the app and
  tests. Avalonia 12 needs the newer compiler; the projects still target .NET 8.
- On Linux, install both using the instructions for your distribution at
  https://learn.microsoft.com/dotnet/core/install/linux.

## Windows

```powershell
dotnet build DSPRE.sln
dotnet test DSPRE.Tests/DSPRE.Tests.csproj
```

Run profiles in the `DSPRE.Avalonia` project: **DSPRE (Avalonia)**, and **DSPRE (Avalonia, beta editors on)**,
which passes `--beta`. The WSL profiles run the same build under WSL.

## Linux

```bash
dotnet build DSPRE.sln
dotnet build DSPRE.Avalonia/DSPRE.Avalonia.csproj
dotnet run --project DSPRE.Avalonia/DSPRE.Avalonia.csproj
```

Solution configurations `Debug-Linux` and `Release-Linux` build the cross-platform projects, with
`DSPRE.Avalonia` targeting `linux-x64`:

```bash
dotnet build DSPRE.sln -c Release-Linux
```

### System libraries

The app needs ICU and the X11 and OpenGL libraries Avalonia loads. On Debian or Ubuntu, including WSL:

```bash
sudo apt install libicu-dev libice6 libsm6 libx11-6 libx11-xcb1 libxext6 libxrandr2 libxi6 libxcursor1 \
  libfontconfig1 libgl1 libegl1
```

`libicu-dev` pulls in whichever `libicuNN` the release ships. Under WSL, start DSPRE with
`MESA_D3D12_DEFAULT_ADAPTER_NAME=NVIDIA` (or the GPU's name) so it renders on the dedicated GPU.

### Distributable output

```bash
dotnet publish DSPRE.Avalonia/DSPRE.Avalonia.csproj -p:PublishProfile=linux-x64
dotnet publish DSPRE.Avalonia/DSPRE.Avalonia.csproj -p:PublishProfile=win-x64
```

Windows releases rename the published `DSPRE.Avalonia.exe` to `DSPRE.exe`, the name Velopack updates installed
copies through. The launcher still finds `DSPRE.Avalonia.dll` by name, so only the exe is renamed.

### Runtime pieces beside the executable

- `Tools/` contains the Windows helpers and the native helpers currently bundled for Linux.
  Extensionless `chatot`, `dsrom`, `rotom`, and `rotom-lsp` binaries are present, together with
  `libnitroarc_ffi.so`. `ndstool` and `blz` are currently `.exe`-only and require WSL interop or Wine
  outside Windows. `DSUtils.ToolPath()` resolves this directory from `AppContext.BaseDirectory` and
  prefers a native binary off Windows. CI builds `dsrom` from
  https://github.com/DS-Pokemon-Rom-Editor/ds-rom and builds the 0BSD-licensed `apicula` for Windows
  and Linux from https://github.com/scurest/apicula at pinned revision
  `3d4e91e14045392a49c89e86dab8cb936225588c`. It also builds `rotom`, `rotom-lsp` and the
  `nitroarc_ffi` library they load for Windows and Linux from the `Tools/rotom-src` submodule,
  DSPRE's fork of rotom at https://github.com/DS-Pokemon-Rom-Editor/rotom (clone with
  `--recurse-submodules`, or run `git submodule update --init`). The Windows build uses MSYS2 MinGW
  and the `x86_64-pc-windows-gnu` target, as rotom's own CI does. `chatot` is built with plain cargo on
  both platforms from the `Tools/chatot-src` submodule, DSPRE's fork of chatot at
  https://github.com/DS-Pokemon-Rom-Editor/chatot. Both submodules track their fork's `dspre` branch,
  where DSPRE's fixes land ahead of upstream: release workflows build its latest commit, and
  `git submodule update --remote` brings a local checkout up to it. The checked-in `Tools/apicula.exe`, rotom and
  chatot binaries remain available for ordinary local builds; release workflows replace them with
  builds from source. Each rotom and chatot binary set has a `Tools/<tool>.<os>.stamp` naming the
  submodule commit it was built from. A local DSPRE build warns (DSPRE001) when a submodule has moved
  past its stamp or has local edits, and fails (DSPRE002) when a binary is missing. The bundled
  binaries are always used as they are; `dotnet build -p:RebuildNativeTools=true` rebuilds them from the
  submodules and updates the stamps, and fails (DSPRE003) naming anything missing from the toolchain:
  cargo, the `x86_64-pc-windows-gnu` Rust target, MinGW `gcc` and `make`, or a POSIX `sh` on Windows.
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
