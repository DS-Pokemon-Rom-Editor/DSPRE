# DSPRE Development Guide

How DSPRE is built and worked on today, on the `feature/avalonia` branch that becomes 3.0.

If you only want to compile and run it, read [BUILDING.md](BUILDING.md) instead. This file is about
where code goes, how a change gets checked, and the habits the project has settled on.

## Contents

1. [What DSPRE is](#1-what-dspre-is)
2. [The solution](#2-the-solution)
3. [Where a file goes](#3-where-a-file-goes)
4. [Running it while you work](#4-running-it-while-you-work)
5. [Adding an editor](#5-adding-an-editor)
6. [Editors that are not ready yet](#6-editors-that-are-not-ready-yet)
7. [The test suite](#7-the-test-suite)
8. [Checking that a change is right](#8-checking-that-a-change-is-right)
9. [Core architecture](#9-core-architecture)
10. [UI conventions](#10-ui-conventions)
11. [ROM data and file formats](#11-rom-data-and-file-formats)
12. [Writing things down](#12-writing-things-down)
13. [Release and CI](#13-release-and-ci)
14. [Appendix](#appendix)

---

## 1. What DSPRE is

A ROM editor for the Generation IV Pokemon games on the Nintendo DS.

| Game | ROM ID (US) | Family |
|------|-------------|--------|
| Diamond | `ADAE` | `DP` |
| Pearl | `APAE` | `DP` |
| Platinum | `CPUE` | `Plat` |
| HeartGold | `IPKE` | `HGSS` |
| SoulSilver | `IPGE` | `HGSS` |

Generation V is not supported. `GameFamilies.BW` and `BW2` exist as enum values but nothing in ROM
loading can reach them.

It unpacks a `.nds` image into a working folder with `ndstool`, pulls NARC archives out on demand,
edits them through the UI, and packs the ROM back up on save.

**Where the project is now.** 3.0 is the Avalonia rewrite. The Avalonia shell is the one being built
out; the WinForms shell is still the default when you launch `DSPRE.exe` and still owns its own copy
of the older editors, but nothing new is written for it. Both live in the same process during the
changeover. Forty seven of the editors that are new in 3.0 are switched off in a normal build; see
[§6](#6-editors-that-are-not-ready-yet).

---

## 2. The solution

Four projects that matter, plus two vendored ones.

```
DS_Map.sln
├── DSPRE.Core/DSPRE.Core.csproj          net8.0            ROM core, no UI at all
├── DSPRE.Avalonia/DSPRE.Avalonia.csproj  net8.0, WinExe    the Avalonia app, all the UI
├── DS_Map/DSPRE.csproj                   net8.0-windows    the Windows exe + legacy WinForms shell
├── DSPRE.Tests/DSPRE.Tests.csproj        net8.0 (+windows) xunit
├── Ekona/Ekona.csproj                                      NDS pixel formats (Tinke derived)
└── Images/Images/Images.csproj                             Nintendo image plugin
```

The split is physical. Every file lives in the project that owns it; there is no shared-source
trickery, and the `CoreFiles.props` / `AvaloniaFiles.props` arrangement older notes describe is gone.

**`DSPRE.Core`** is the ROM: file formats, the `ROMFiles` data models, `DSUtils` binary IO, the script
and text systems, `RomInfo`, the game databases, the hg-engine integration, and the 3D format readers
in `LibNDSFormats`. No WinForms and no Avalonia. `System.Drawing.Common` still compiles here, but its
GDI paths only actually run when the WinForms shell calls them.

**`DSPRE.Avalonia`** is the whole UI: views, view models, the GL renderer, the graphics and audio
readers under `Avalonia/Data`, and a thin `Main`. It builds and runs on its own on Windows or Linux.

**`DS_Map`** is the Windows executable. It hosts the legacy WinForms `MainProgram` as the default
shell, and references `DSPRE.Avalonia` in-process so the same exe can show the Avalonia shell instead.
It owns the WinForms-only editors under `DS_Map/Editors/` and the two Windows-only packages
(ScintillaNET, WindowsAPICodePack). The bundled native helpers live in `Tools/` at the repository
root and are copied next to the executable.

**`DSPRE.Tests`** always references Core and Avalonia. It references `DS_Map` only when targeting
`net8.0-windows`, so the handful of tests that need GDI compile alongside the rest.

`InternalsVisibleTo` wires Core to DSPRE, Avalonia and Tests, and Avalonia to DSPRE and Tests, so UI
code can still reach `internal` types left over from when this was one project.

### The layer rule

Core knows nothing about the UI. If you find yourself wanting a dialog from inside `ROMFiles`, that is
the signal to raise it through `AppMessages` (it lives in `Ekona`) and let the shell decide what to
show. New features go in the Avalonia shell, not WinForms.

---

## 3. Where a file goes

### Views and view models are filed by menu section

Both trees are split into the same eleven folders, and each folder is its own namespace:

```
DSPRE.Avalonia/Avalonia/
├── ViewModels/
│   ├── Shell/        MainWindow, Welcome, CommandPalette, Settings
│   ├── Pokemon/      species, moves, learnsets, encounters, forms
│   ├── Trainers/     trainers, classes, sprites, Battle Tower
│   ├── Items/        items and item tables
│   ├── Text/         text banks, scripts, level scripts, char maps
│   ├── World/        maps, matrices, headers, events, buildings, spawns
│   ├── Graphics/     the graphics and model browsers, title screen, fonts
│   ├── Battle/       battle screen, display, scenes, battle scripts
│   ├── Audio/        the audio editor
│   ├── Tools/        helpers, project checks, patch toolbox, hg-engine
│   └── Controls/     shared pieces with no menu entry of their own
└── Views/            the same eleven folders
```

The section is the menu an editor opens from, taken from `MainWindowView.axaml`, not from the file
name. `Controls` holds the pieces that are not editors: `PixelGrid`, `RomTextBlock`, `GlyphPainter`,
`WaveformView`, `NoteTrackView`, `FieldMessageBoxView`.

So a view model is `DSPRE.Avalonia.ViewModels.World`, and its view is `DSPRE.Avalonia.Views.World`.
Consumers reach them through global usings declared in `DSPRE.Avalonia.csproj`, one line per section,
rather than a using in every file. The sections are there so you can find things; inside the app all
the editors are one surface. XAML does not get global usings, so a view's `xmlns:vm` names its own
section, and the two views that embed something from another section carry a second prefix
(`PokemonEditorView` embeds the battle display, `MapsWorkspaceView` embeds the text and script
editors).

### The rest of the Avalonia project

```
DSPRE.Avalonia/Avalonia/
├── App.axaml, App.axaml.cs        theme, resources, which shell to start
├── AvaloniaEditorLauncher.cs      one method per editor the menu can open
├── WindowPlacement.cs             ShowManaged(), size and position, the beta gate
├── DialogHelper.cs                every dialog goes through here
├── ImageConverter.cs              System.Drawing to Avalonia bitmaps
├── AppEvents.cs                   cross-editor notifications
├── OpenEditors.cs                 which editors are open, for unsaved-changes prompts
├── UndoHistory.cs                 shared undo stack
├── Data/                          readers and models the UI needs but the ROM core does not
├── Gl/                            the OpenGL renderer, NSBMD geometry, matrix scenes
├── Themes/                        light and dark, as ThemeDictionaries
└── Assets/
```

`Avalonia/Data` is where a format reader lives when only the UI uses it: `GraphicAssets`,
`BattleObjects`, `SdatArchive`, `SpaArchive`, `NitroBgCodec`, `WestOpcodes` and so on. Anything the
ROM itself round-trips belongs in `DSPRE.Core/ROMFiles` instead.

### DSPRE.Core

```
DSPRE.Core/
├── RomInfo.cs           every path and offset for the loaded ROM
├── Filesystem.cs        typed path accessors per NARC directory
├── BetaEditors.cs       which editors are switched off
├── SettingsManager.cs   JSON settings
├── DSUtils/             ARM9, overlays, ROM pack and unpack, EasyReader/Writer, text encoding
├── ROMFiles/            79 data models, game binary to C# objects
├── LibNDSFormats/       NSBMD, NSBTX, NSBCA readers
├── HgEngine/            hg-engine source reading and writing
├── Resources/           script and game databases
└── Script/              script parameter and label types
```

---

## 4. Running it while you work

```powershell
dotnet build DS_Map.sln              # everything
dotnet test DSPRE.Tests              # the suite
```

The exe is `DS_Map\bin\Debug\net8.0-windows\DSPRE.exe`. Set the working directory to that bin folder,
because `Tools\*.exe` are resolved relative to it.

- No environment variable: the WinForms shell.
- `DSPRE_AVALONIA_SHELL=1`: the Avalonia shell, titled "DSPRE (Avalonia preview)".

Two traps worth knowing before you lose an hour to either.

**A partial build leaves a stale DLL.** Building `DSPRE.Avalonia.csproj` alone does not copy the new
DLL into `DS_Map\bin\Debug\net8.0-windows\`, so `DSPRE.exe` keeps running the old code after a build
that said it succeeded. Build `DS_Map.sln` when you are about to run it. Kill any running `DSPRE.exe`
and `rotom-lsp.exe` first or the build fails with MSB3026 on a locked file.

**Do not launch `DSPRE.Avalonia.exe` directly for local testing.** It throws
`FileNotFoundException: System.Drawing.Common`, because a plain `dotnet build` does not copy that
dependency app-locally. Only local framework-dependent runs are affected; CI publishes
self-contained. Use `DSPRE.exe` with the environment variable.

Test projects live under `C:\Romhacking\ROMs\NDS\`, already unpacked, one folder per game
(`HeartGold (USA)_DSPRE_contents` and so on). Open one with File, Open extracted folder.

---

## 5. Adding an editor

Six steps. The first four are the editor, the last two are how anyone reaches it.

**1. The view model**, in `Avalonia/ViewModels/<Section>/MyEditorViewModel.cs`, namespace
`DSPRE.Avalonia.ViewModels.<Section>`. Plain `INotifyPropertyChanged`; the project uses no MVVM
framework, no ReactiveUI and no CommunityToolkit.

```csharp
namespace DSPRE.Avalonia.ViewModels.World
{
    public class MyEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        private bool Set<T>(ref T field, T value, [CallerMemberName] string n = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value; OnPropertyChanged(n); return true;
        }

        // The designer calls this one, so it must not touch RomInfo.
        public MyEditorViewModel()
        {
            if (!Design.IsDesignMode) return;
            Names = new ObservableCollection<string> { "Item A", "Item B" };
        }

        public MyEditorViewModel(string[] names) { ... }
    }
}
```

The `Design.IsDesignMode` guard on the first line of any parameterless constructor is not optional.
Without it the designer reads `RomInfo` with no ROM loaded and the preview dies.

**2. The view**, in `Avalonia/Views/<Section>/MyEditorView.axaml`, with `x:Class` naming the section
namespace. Compiled bindings are on by default, so every file needs an `x:DataType`:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="clr-namespace:DSPRE.Avalonia.ViewModels.World"
             x:Class="DSPRE.Avalonia.Views.World.MyEditorView"
             x:DataType="vm:MyEditorViewModel">
```

A `Window` is for something genuinely standalone. Prefer a `UserControl`, which can be embedded (a
Maps workspace tab, a tab inside a bigger editor) and gets wrapped in `EditorHostWindow` when it is
opened from a menu.

**3. The code-behind** holds handlers and nothing else. No ROM logic.

**4. The launcher.** Add a method to `AvaloniaEditorLauncher.cs` that builds the view model, builds
the view, and calls `.ShowManaged()`. Everything goes through `ShowManaged` so that window placement,
the open-editors registry and the beta gate all apply in one place.

**5. The menu.** Add the entry to `MainWindowView.axaml` with its click handler, and a
`CanUseMyEditor` property on `MainWindowViewModel` if it only applies to some games. Never leave a
control disabled with nothing on screen saying why: bind `ToolTip.Tip` to the reason.

**6. The beta list.** A new editor goes in `BetaEditors` until it has been used in anger.

If the editor has to unpack a NARC the first time it opens, do it off the UI thread behind the busy
overlay, or the window appears frozen on a first run.

---

## 6. Editors that are not ready yet

`DSPRE.Core/BetaEditors.cs` holds one dictionary: the window class name of every editor that is still
being tried out, and what to call it in a message. It is the whole mechanism.

```csharp
BetaEditors.ReadFrom(args);          // once, in Program.cs, before any window opens
BetaEditors.Allows("FontEditorView") // may this open in this run
BetaEditors.WhyNot("FontEditorView") // "Font editor is not available yet."
```

A release build switches them on with `--beta`. A debug build always has them on, because that is a
build made for working on DSPRE.

The gate is asked in two places. The menu asks it through `Beta[WindowName]` so the entry greys out
with the reason on hover, the same shape as the hg-engine gating. `WindowPlacement.ShowManaged` asks
it again at the moment any window opens, so an editor reached from the command palette or from a
button inside another editor is stopped too.

The message says the editor is not available yet and nothing else. How to switch them on is not
advertised in the UI or in the user changelog.

Moving an editor out of beta is deleting one line.

---

## 7. The test suite

`DSPRE.Tests`, xunit, about 850 tests in eight folders that mirror what they are about:

```
DSPRE.Tests/
├── Audio/          cries, SDAT, MIDI export, SoundFont, PSG
├── Editors/        beta gate, undo, unsaved changes, hg-engine, command palette
├── Field/          the field simulator, movements, timing, collision
├── Graphics/       archives, palettes, backgrounds, round trips
├── Models/         NSBMD read and write, animations, building animations
├── MoveAnimation/  the WEST script engine, particles, previews
├── Scripts/        the script walker, values, command names
├── Tools/          not tests: gated ROM builders, see below
├── RomFiles.cs           helper for a folder still being filled
└── RomInfoCollection.cs  the xunit collection ROM tests share
```

Run it:

```powershell
dotnet test DSPRE.Tests -f net8.0 --nologo
dotnet test DSPRE.Tests -f net8.0 --filter "FullyQualifiedName~BattleIcon"
```

The whole suite takes about nine minutes because much of it reads real ROMs. When that is too long for
a foreground call, split it by class name into two or three filters rather than skipping it.

### Rules that come from real mistakes

**A test that opens a ROM needs `[Collection("rom")]`.** `RomInfo` keeps what it reads in static
fields, so two ROM tests running side by side read each other's game.

**Two target frameworks share one unpacked project.** A sweep that lists a NARC directory can catch it
half filled by the other run. Use `RomFiles.Settled(dir)`, which waits for the count to stop moving.

**Watch a new test fail before you trust it.** Break the thing it checks, confirm it goes red, put it
back. A test that cannot fail for the reason you wrote it is worse than no test, because it reads like
cover.

**Say what a skip means.** A test that returns early when a game is not unpacked has to assert that it
ran at all, or it passes while proving nothing:

```csharp
Assert.True(played > 0, "no game was unpacked here, so this proved nothing");
```

**Check the whole set, not a sample.** The archives are right there. A sweep over every file in every
game is usually seconds, and a sample has twice now agreed with a bug.

**A round trip is not evidence.** Writing a file with your writer and reading it back with your reader
proves the two halves agree with each other. It proved 4,601 background files correct while both
halves used the wrong tile layout. Check against the games' own files, or against an independent
reader.

### What is worth keeping

A test earns its place by being able to fail for a reason that matters. Restating a lookup table in
`[InlineData]` rows does not: it just says the table equals itself, and it goes red whenever anyone
edits the table on purpose. Nor does asserting that a generated document matches its generator.
Neither does a method with no assertions that prints a report; if you want the numbers, write them to
`ITestOutputHelper` inside a test that also checks something.

`DSPRE.Tests/Tools/` holds the things that are not tests: builders that write a modified ROM so an edit
can be looked at in a running game, and stagers that set up a battle. They do nothing unless an
environment variable names what to build, so a normal run walks straight past them.

---

## 8. Checking that a change is right

**Build with `-v m` and read it.** Avalonia XAML errors do not fail a `-v q` build; it reports success
while the view is broken. Build at `-v m` and grep for `AVLN`.

```powershell
dotnet build DS_Map.sln -v m --nologo 2>&1 | Select-String ": error |AVLN"
```

**A headless probe beats driving the UI.** `DSPRE.Core` is plain `net8.0`, so a scratch console app
with a project reference to it can call `new RomInfo("IPKE", folder)` and print the full stack that a
dialog would have swallowed. The same trick renders sprites to PNG without opening a window.

**Then open it.** A clean build is not a working window. Launch the Avalonia shell, open the editor
you touched, and look.

**For anything about how the games behave, cite the file and line.** Engine behaviour stated from
memory has been wrong often enough that the rule is now absolute: a claim about the games carries the
source it came from, or it is labelled as not verified.

**Grade what you report.** Proven from source, proven across all the data, consistent with a sample, or
assumed. Use the weakest one that honestly applies, and say what would have to be true for the claim to
be wrong.

---

## 9. Core architecture

### `RomInfo`

Everything about the loaded ROM sits in static properties on `RomInfo`. It is the single source of
truth.

```csharp
RomInfo.workDir       // the extracted ROM folder
RomInfo.romID         // "IPKE"
RomInfo.gameVersion   // GameVersions
RomInfo.gameFamily    // DP | Plat | HGSS
RomInfo.gameLanguage
RomInfo.gameDirs      // Dictionary<DirNames, NarcDirectory>
RomInfo.isHGE         // an hg-engine project
```

Two things follow from that.

Branch on `RomInfo.gameFamily`, and often `gameLanguage`, before touching any offset. Most of them
differ per version, and several differ per language.

Offsets belong in `RomInfo`, not as private constants in whichever editor happens to need them, and
every `Setup*` method has to run at ROM load. A `SetupSpawnSettings` that only one editor called left
everything else reading offset zero.

### NARC archives

Game data lives in NARCs, unpacked on demand into numbered files (`0000`, `0001`, ...).

```csharp
DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.scripts });

string dir  = RomInfo.gameDirs[DirNames.scripts].unpackedDir;
string path = Filesystem.GetScriptPath(42);
int count   = Filesystem.GetScriptCount();
```

Prefer the `Filesystem` accessors over building paths by hand.

### `RomFile`

Every ROM data structure derives from it:

```csharp
public abstract class RomFile
{
    public abstract byte[] ToByteArray();
    protected bool SaveToFileDefaultDir(DirNames dir, int IDtoReplace, ...);
    protected void SaveToFileExplorePath(...);
}
```

### ARM9, overlays and binary IO

```csharp
byte[] data = ARM9.ReadBytes(offset, length);
ARM9.WriteBytes(data, offset);
if (ARM9.CheckCompressionMark()) ARM9.Decompress(RomInfo.arm9Path);

string ovPath = OverlayUtils.GetPath(n);
if (OverlayUtils.IsCompressed(n)) OverlayUtils.Decompress(n);
uint ram = OverlayUtils.OverlayTable.GetRAMAddress(n);

using (var r = new DSUtils.EasyReader(path, offset)) { ushort v = r.ReadUInt16(); }
using (var w = new DSUtils.EasyWriter(path, offset)) { w.Write(v); }
```

`NarcReader` streams a packed NARC and is deliberately not `IDisposable`. Call `OpenEntry(i)`, read
`narc.fs`, then `Close()`.

### Text and scripts are dual format

Both exist as the binary the game reads and as an editable plaintext copy under `expanded/`. The
loader takes the plaintext when it is newer. Anything that writes one has to respect that.

### Logging and settings

```csharp
AppLogger.Debug / Info / Warn / Error / Fatal
AppLogger.GetRecentLogs()      // for crash reports

SettingsManager.Settings.useDecompNames = true;
SettingsManager.Save();
```

### hg-engine

`DSPRE.Core/HgEngine` reads and writes a linked hg-engine checkout as source text through
`ElementScanner`. Never read hg-engine data by byte offset: the layout moves between versions, and the
source is the thing that is stable. Check the hg-engine docs and wiki before concluding something is an
upstream bug.

---

## 10. UI conventions

**Dialogs** go through `DialogHelper`, always. No `System.Windows.Forms.MessageBox` in Avalonia code.

**Status colours** come from `StatusBrushes`, not literals like `Brushes.DarkGreen`, so they stay
readable in both themes.

**Themes** are defined as `ThemeDictionaries` so Light reskins every control rather than most of them.

**Labels** the user can rename come from `LabelStore` and refresh live through `AppEvents`.

**Unsaved changes.** A view model that edits data implements `IEditorWithUnsavedChanges`:

```csharp
public bool HasUnsavedChanges => _dirty;
public string UnsavedChangesDescription => $"My Editor (item {_currentId})";
public void SaveChanges() { /* write */ SetClean(); }
public void DiscardChanges() { _dirty = false; OnPropertyChanged(nameof(HasUnsavedChanges)); }
```

**Handler suppression.** Populating a combo box fires its selection handler. WinForms uses
`Helpers.DisableHandlers()` / `EnableHandlers()` with `if (Helpers.HandlersDisabled) return;` at the
top of every handler. Avalonia view models need the same discipline for the same reason.

**Two Avalonia traps that cost real time.** A hidden `TabItem` is still tab zero, so selecting by index
lands on the wrong tab. A `ComboBox` clears its own `SelectedIndex` when its items change, so restoring
a selection has to happen after the collection settles, not with it.

**Bitmap aliasing.** When a file needs both, alias them:

```csharp
using AvaBitmap = Avalonia.Media.Imaging.Bitmap;
using GdiBitmap = System.Drawing.Bitmap;
```

**Naming.**

| Thing | Convention |
|---|---|
| View | `[Name]View.axaml` and `.axaml.cs` |
| View model | `[Name]ViewModel.cs` |
| ROM data model | no suffix: `EvolutionFile`, `PokemonPersonalData` |
| NARC directory | a `DirNames` value |
| NARC entry file | zero padded four digits |

---

## 11. ROM data and file formats

### Battle sprites

Six entries per species at `speciesIndex * 6`:

| Offset | Content | Size |
|---|---|---|
| +0 | female back | 6448 |
| +1 | male back | 6448 |
| +2 | female front | 6448 |
| +3 | male front | 6448 |
| +4 | normal palette | 72 |
| +5 | shiny palette | 72 |

The pixels are scrambled with a rolling XOR key: forward from `arr[0]` in Platinum and HGSS, backward
from `arr[3199]` in Diamond and Pearl. Read straight they look static. Palettes are BGR555 forty bytes
in.

The palette is per species at slot 4, not the nearest one in the archive. Guessing the nearest hands
most species the previous one's shiny colours, which looks almost right.

### Nitro 2D formats

`NCLR` colours, `NCGR` tiles, `NSCR` arrangement, `NCER` cell layout, `NANR` cell animation. Magic is
the name backwards: an `NCGR` file starts `RGCN`.

Two things catch readers out.

A great many tile sheets write `0xFFFF` for both dimensions, meaning "unspecified", because the game
gets the shape from a cell layout instead. Signed, that is -1, not 65535. A reader that guesses a
square from the tile count is right by luck sometimes and scrambled the rest of the time.

An arrangement wider than 32 squares is stored in blocks of 32 by 32; one 32 wide or narrower is
stored straight across at its own width. At exactly 32 the two rules agree, and most files in the ROM
are exactly 32, so a sample will not tell them apart.

### Map headers

```csharp
MapHeader h = MapHeader.LoadFromARM9(n);
MapHeader h = MapHeader.GetMapHeader(n);   // honours the dynamic-headers patch
```

Fields: `areaDataID`, `matrixID`, `eventFileID`, `scriptFileID`, `textArchiveID`, `wildPokemon`,
`musicDayID`, `weatherID`, `cameraAngleID`.

For the dynamic-headers patch, `ContainsKey` is not the same question as "is the patch applied". Asking
the wrong one lost the place names for 1130 of 1133 headers.

### Events

`EventFile` holds four lists: `Spawnable`, `Overworld`, `Warp`, `Trigger`. Every event stores both
map-relative and matrix-relative coordinates.

An overworld's `param0` is general purpose, not sight range. What it means depends on the overworld.

### Evolutions and learnsets

Seven evolution slots per species, each `{ method, param, target }`. `LearnsetData` is a
`UniqueList<(byte level, ushort move)>`; twenty entries is the vanilla limit and more is allowed with a
warning.

### Coordinates

A block is 32 tiles. Tile 0 is raw 0. Event anchors are separate from tile positions; conflating them
puts everything half a block out.

---

## 12. Writing things down

**Code comments** say why, in ordinary words, and only when the why is not obvious. One short line, no
multi-line blocks narrating how you debugged it. The same goes for dialog text and status messages: say
what happened and what to do, plainly. No jargon reached for to sound technical.

**No em dashes** anywhere in code, UI text, changelogs or commit messages.

**Changelogs** live in `Changelogs/`, one file per release, written for users. Say what changed and what
was wrong before, with numbers where there are numbers. `CHANGELOG_3.0_User.md` is the one in progress.

**Research** goes in `Research/`, indexed from `ResearchNotes.md`. It is for findings a future reader
needs and cannot get from the code: what the games do, checked against something, with the counts and
the method. It is not a place to restate what the code already says.

**Commits** are yours to make. Nothing in the tooling should be committing for you, and the three
tooling files at the repository root that are not part of DSPRE stay untracked.

---

## 13. Release and CI

Four workflows in `.github/workflows/`:

| File | What it does |
|---|---|
| `base-build-nightly.yaml` | canary, Windows zip and Linux tar.gz |
| `avalonia-canary-build.yaml` | canary, `DSPRE.Avalonia` only, on push to `feature/avalonia` |
| `beta-build-nightly.yml` | canary off the `beta` branch |
| `update-releases.yaml` | stable Velopack packages, `win` installer and `linux` AppImage on one release |

The in-app updater picks the channel matching its OS and shows the release notes in the prompt.

Two runtime pieces have to sit next to the executable. `Tools/` holds the native helpers, named with no
extension on Linux (`DSUtils.ToolPath()` appends `.exe` on Windows only): `ndstool`, `dsrom`, `blz`,
`apicula`, `rotom`, `rotom-lsp`. `databases/` is a clone of the scrcmd-database repository, copied to
the per-user data folder on first run.

`chatot`, `dsrom`, `rotom` and `rotom-lsp` have Linux builds in the bundle. `ndstool`, `blz` and
`apicula` do not, so those paths still fail on Linux.

---

## Appendix

### `DirNames` to content

```
personalPokeData            base stats
pokemonBattleSprites        battle sprites and palettes
otherPokemonBattleSprites   alternate forms
monIcons                    party icons
textArchives                in-game text banks
matrices                    map connection matrices
maps                        3D map models
exteriorBuildingModels      building models
buildingTextures            building textures
eventFiles                  NPCs, warps, triggers, spawnables
OWSprites                   overworld sprites
scripts                     game scripts
encounters                  wild encounters
trainerProperties           trainer headers
trainerParty                trainer parties
moveData                    move definitions
learnsets                   learnsets
evolutions                  evolution data
itemData                    item definitions
itemIcons                   item icons
eggMoves                    egg move lists
trades                      in-game trades
battleObj                   HP bars, platforms, type icons, message frames
battleBg                    battle backdrops
fonts                       the letters text is drawn with
```

### `GameFamilies`

```
DP    Diamond and Pearl
Plat  Platinum
HGSS  HeartGold and SoulSilver
BW    unsupported, unreachable
BW2   unsupported, unreachable
NULL  no ROM loaded
```

### Related repositories

- DSPRE: https://github.com/DS-Pokemon-Rom-Editor/DSPRE
- Script command database: https://github.com/DS-Pokemon-Rom-Editor/scrcmd-database
- ds-rom: https://github.com/DS-Pokemon-Rom-Editor/ds-rom
