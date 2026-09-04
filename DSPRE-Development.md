# Working on DSPRE

DSPRE 3.0 is an Avalonia app. This is how the code is laid out, how we write it, and the traps that
have cost people a day. Build and publish instructions are in [BUILDING.md](BUILDING.md).

The old WinForms shell is still in the repo behind `--winforms`. New UI work targets Avalonia unless a
task explicitly concerns the legacy shell, and the rest of this document focuses on Avalonia.

---

## Projects

| | Target | What belongs here |
|---|---|---|
| `DSPRE.Core` | net8.0 | ROM formats, `ROMFiles` models, `DSUtils`, `RomInfo`, scripts, text, hg-engine. No UI. |
| `DSPRE.Avalonia` | net8.0 | The app. Views, view models, the GL renderer, readers only the UI needs. |
| `DSPRE.Tests` | net8.0 | xunit. |

`DS_Map` is the Windows exe that hosts the same Avalonia UI, plus the legacy WinForms editors.

**Core or Avalonia?** If the type is written back into the ROM it goes in `DSPRE.Core/ROMFiles` and
derives from `RomFile` with a `ToByteArray()`. If it only ever reads, to draw or list something, it goes
in `DSPRE.Avalonia/Avalonia/Data`: `GraphicAssets`, `SdatArchive`, `SpaArchive`, `NitroBgCodec`.

Core never shows UI. If you want a dialog from inside `ROMFiles`, raise it through `AppMessages` (in
`Ekona`) and let the shell decide what to show.

### Views and view models are filed by menu

Both trees have the same eleven folders, and each is its own namespace:

```
Shell  Pokemon  Trainers  Items  Text  World  Graphics  Battle  Audio  Tools  Controls
```

The folder is the menu the editor opens from, not what the class is called. `Controls` is the shared
pieces with no menu entry of their own: `PixelGrid`, `RomTextBlock`, `GlyphPainter`, `WaveformView`.

`MapEditorViewModel` lives in `ViewModels/World/`, namespace `DSPRE.Avalonia.ViewModels.World`. You
don't need a `using`: the csproj declares a global using per section. XAML doesn't get those, so a
view's `xmlns:vm` names its own section. Two views embed across sections and carry a second prefix:
`PokemonEditorView` embeds the battle display, `MapsWorkspaceView` embeds the text and script editors.

---

## Build and run

```powershell
dotnet build DS_Map.sln          # everything
dotnet test DSPRE.Tests/DSPRE.Tests.csproj -f net8.0
```

In Visual Studio the dropdown on `DSPRE` gives you **DSPRE** (the Avalonia shell),
**DSPRE (beta editors on)** which passes `--beta`, and **DSPRE (old WinForms shell)**.
`DSPRE.Avalonia` runs the same UI with no WinForms in the process at all, and has its own two profiles.

From a terminal the executable is `DS_Map\bin\Debug\net8.0-windows\DSPRE.exe`, or
`DSPRE.Avalonia\bin\Debug\net8.0\DSPRE.Avalonia.exe`. Helpers resolve from the output's `Tools\`
directory through `AppContext.BaseDirectory`; the process working directory does not control lookup.

Extracted test projects are configured per machine. See [Pointing tests at your games](#pointing-them-at-your-games).

**Two build traps.**

A partial build leaves a stale DLL: building `DSPRE.Avalonia.csproj` alone does not copy the DLL into
`DS_Map\bin\Debug\net8.0-windows\`, so `DSPRE.exe` keeps running your old code and the build reports
success. Build `DS_Map.sln`, and kill `DSPRE.exe` and `rotom-lsp.exe` first or you get MSB3026.

Quiet output can hide the actionable Avalonia compiler line among other build output. When a XAML
change fails, inspect medium-verbosity errors and `AVLN` diagnostics:

```powershell
dotnet build DS_Map.sln -v m --nologo 2>&1 | Select-String ": error |AVLN"
```

---

## How we write an editor

Every editor in the app follows the same shape. Copy an existing one in the same section rather than
starting from scratch; `CameraEditorViewModel` is a good small example, `MapEditorViewModel` a large one.

### The view model

Plain `INotifyPropertyChanged`, declared per class. No ReactiveUI, no CommunityToolkit, no shared view
model base class: view models declare `PropertyChanged` themselves, so there is little framework
machinery to learn before reading one.

```csharp
namespace DSPRE.Avalonia.ViewModels.World
{
    public class MyEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges
    {
        public event PropertyChangedEventHandler PropertyChanged;
        void Notify([CallerMemberName] string p = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));

        private string _name;
        public string Name { get => _name; set { _name = value; Notify(); SetDirty(); } }

        // The designer calls this one, so it must not touch RomInfo.
        public MyEditorViewModel()
        {
            if (!Design.IsDesignMode) return;
            Entries = new ObservableCollection<MyRowVM> { new MyRowVM(0) };
        }

        public MyEditorViewModel(string[] names) { ... }
    }
}
```

The `Design.IsDesignMode` guard on the first line of any parameterless constructor is not optional.
Without it the designer reads `RomInfo` with no ROM loaded and the preview dies.

For a grid, give each row its own small view model in the same file, suffixed `VM` or `Row`:
`CameraRowVM`, `WildEncounterRow`, `TrainerPartyMonViewModel`. Keep fields the format has but the UI
doesn't show, so a round trip doesn't drop them:

```csharp
// Hidden fields preserved for round-trip
internal short Unk1 { get; private set; }
```

### Unsaved changes

Anything that edits data implements `IEditorWithUnsavedChanges` and calls `SetDirty()` from its setters,
`SetClean()` after a successful save. `OpenEditors` uses it to prompt before a ROM is closed, so an
editor that skips it silently loses the user's work.

```csharp
public bool HasUnsavedChanges => _dirty;
public string UnsavedChangesDescription => $"Camera {_index}";
public void SaveChanges()   { WriteBack(); SetClean(); }
public void DiscardChanges(){ _dirty = false; Notify(nameof(HasUnsavedChanges)); }
```

### The view

`Views/<Section>/MyEditorView.axaml` with `x:Class` naming the section namespace. Compiled bindings are
on by default, so every file needs `x:DataType` or you get a build error that reads like something else:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="clr-namespace:DSPRE.Avalonia.ViewModels.World"
             x:Class="DSPRE.Avalonia.Views.World.MyEditorView"
             x:DataType="vm:MyEditorViewModel">
```

Prefer a `UserControl` to a `Window`. A UserControl can be embedded in a Maps workspace tab or inside a
bigger editor, and `EditorHostWindow` wraps it when it opens from a menu. The code-behind holds event
handlers and nothing else: no ROM logic, no file IO.

### Opening it

Add a method to `AvaloniaEditorLauncher.cs` that builds the view model, builds the view, and calls
`.ShowManaged()`.

```csharp
public static void OpenMyEditor()
    => new MyEditorView(new MyEditorViewModel(names)).ShowManaged();
```

**Always `ShowManaged()`, never `Show()`.** It is the single place that positions the window on the
right monitor, registers it with `OpenEditors`, and asks the beta gate. A plain `Show()` skips all
three, and a test fails the build if you write one.

### Wiring the menu

Add the entry to `MainWindowView.axaml` with its click handler. If the editor only applies to some
games, add a `CanUseMyEditor` property to `MainWindowViewModel` and bind `IsEnabled` to it.

Never leave a greyed control with nothing on screen saying why. Bind `ToolTip.Tip` to the reason.

### Long work on first open

If the editor unpacks a NARC the first time it opens, do it off the UI thread behind the busy overlay,
or the window appears frozen. The pattern is a pair of properties the view binds an overlay to:

```csharp
public bool IsBusy { get => _busy; set { _busy = value; Notify(); } }
public string BusyText { get => _busyText; set { _busyText = value; Notify(); } }
```

### The rest of the house furniture

| Need | Use | Never |
|---|---|---|
| Any dialog | `DialogHelper.ShowInfo/ShowError/Confirm/AskThreeWay` | `MessageBox` |
| A good/warn/bad colour | `StatusBrushes` | `Brushes.DarkGreen` |
| Tell other editors something changed | `AppEvents.RaiseNamesChanged()` and friends | reaching into another view model |
| A name the user can rename | `LabelStore` | a hardcoded string table |
| Undo | `UndoHistory` | a private stack |
| GDI bitmap into Avalonia | `ImageConverter.ToAvaloniaBitmap` | |

`AppEvents` carries `NamesChanged`, `LabelsChanged`, `RomPatchStateChanged`, `BannerChanged` and
`HgEngineLinkChanged`. Raise the right one after a write and every open editor picks it up.

---

## The beta gate

Editors that are not finished are listed in `DSPRE.Core/BetaEditors.cs`, one dictionary of window class
names, and switched off unless the app is started with `--beta`. Debug builds always have them on.
Adding or removing a line is the whole job.

Two places ask. The menu asks through `Beta[WindowName]`, so the entry greys out with the reason on
hover. `WindowPlacement.ShowManaged` asks again when the window opens, so an editor reached from the
command palette or a button inside another editor is stopped too.

The message says the editor is not available yet and nothing else. Don't mention the switch in the UI or
in the user changelog.

---

## Tests

Tests are filed by subject under `DSPRE.Tests/`.

```powershell
dotnet test DSPRE.Tests/DSPRE.Tests.csproj -f net8.0 --nologo
dotnet test DSPRE.Tests/DSPRE.Tests.csproj -f net8.0 --filter "FullyQualifiedName~BattleIcon"
```

Many tests read extracted projects. Use a class-name filter for a focused iteration, then run the
broader relevant target before handoff. Do not run full test processes concurrently because the ROM
fixtures and application state are shared.

### Pointing them at your games

Most of the suite reads real extracted projects. Where yours live is per machine, so put it in
`testroms.json` beside `DS_Map.sln` (git ignores it):

```json
{
  "heartGold": "<path-to-heartgold-project>",
  "platinum":  "<path-to-platinum-project>",
  "diamond":   "<path-to-diamond-project>"
}
```

`DSPRE_TEST_HEARTGOLD`, `DSPRE_TEST_PLATINUM` and `DSPRE_TEST_DIAMOND` override the file, which is how
to point one run somewhere else. `DSPRE_TEST_ROMS_CONFIG` selects an alternate JSON file. If you keep
all three under one folder in the layout expected by `TestRoms`, set `DSPRE_TEST_ROMS` instead of
naming each one.

Read them through `TestRoms.HeartGold` / `.Platinum` / `.Diamond`, never a path written into the test.
A test whose optional game is missing should use `SkippableFact` and `Skip.If` with a useful reason,
not return as a passing test. A sweep must also assert that it checked at least one record.

Two conventions cause confusing failures if you miss them:

- `[Collection("rom")]` on anything that opens a ROM. `RomInfo` is static, so two ROM tests running side
  by side read each other's game.
- `RomFiles.Settled(dir)` instead of `Directory.GetFiles` when listing an unpacked NARC. Both target
  frameworks share one unpacked project, so a sweep can catch the folder half filled.

`DSPRE.Tests/Tools/` is not tests. It is ROM builders and battle stagers that write a modified ROM so
you can look at an edit in a running game; they no-op unless an environment variable names what to build.

A test earns its place by being able to fail for a reason that matters. Source-text searches and
reflection checks usually establish only that a symbol exists, not that the application reaches it.
Assertions should consume values produced by the code under test, and a new test should be observed
failing for the intended reason when practical.

---

## ROM project formats

`RomInfo.IsDsRomProject` is set while a project is loaded from the folder type returned by
`DSUtils.GetFolderType`. That method detects ds-rom by `config.yaml` and legacy ndstool projects by
`header.bin`. Keep format detection and conversion in `DSPRE.Core/DSUtils/DSUtils.cs` and do not
reproduce it in an editor.

The current ds-rom layout uses `files/` for the ROM file system, `arm9/arm9.bin`,
`arm9_overlays/ovNNN.bin`, and YAML metadata headed by `header.yaml`. The legacy ndstool layout uses
`data/`, a root `arm9.bin`, `overlay/overlay_NNNN.bin`, and `header.bin`. New extraction uses ds-rom;
legacy projects can be converted by the existing conversion path.

Overlay callers use `OverlayUtils.GetPath` so both layouts continue to work. Saving a ROM must
write through the existing NARC and project build paths rather than moving files directly from an
editor.

## Script binary and plaintext workflow

`DSPRE.Core/ROMFiles/ScriptFile.cs` owns the dual representation. Binary script data comes from the
ROM project; optional editable text lives at `expanded/scripts/NNNN.script`.

- `TryReadPlaintextIfNewer` uses the plaintext form only when it is newer than the binary form.
- Export preserves current plaintext unless the database hash requires regeneration.
- `.database_hash` records the script database state used for the export.
- The save path rebuilds binaries that require it before packing the project.

Keep timestamp comparison, cache invalidation, parsing, and rebuild decisions in that owner. Changes
need round-trip coverage for an unchanged binary, an externally edited plaintext file, and a database
change. Search for existing script readers before adding another interpretation of the format.

---

## Gotchas

### ROM data

- Branch on `RomInfo.gameFamily` before touching any offset, and often `gameLanguage`. Most offsets
  differ per version and several per language.
- Offsets belong in `RomInfo`, not as private consts in an editor, and every `Setup*` must run at ROM
  load. `SetupSpawnSettings` was called by one editor only, so everything else read offset 0.
- `param0` is stored as `sightRange` but is the engine's general `param0`. For trainer types it is the
  sight range; `param1` is the glance and spin interval for those types; `param2` has no reader in the
  field code; and on hg-engine item overworlds `param0` carries the item ID instead.
  (`EventFile.cs:247`, `EventEditorViewModel.cs:275`)
- A block is 32 tiles, tile 0 is raw 0, and event anchors are separate from tile positions.

### Graphics

- Tile sheets often write `0xFFFF` for both dimensions, meaning unspecified, because the game gets the
  shape from a cell layout instead. Read it **signed**: that is -1, not 65535. Guessing a square from the
  tile count is right by luck often enough to fool you. A 32x32 item icon is 16 tiles and comes out
  right; a 32x16 battle icon is 8 tiles and comes out stacked.
- Background arrangements wider than 32 squares are stored in 32x32 blocks; 32 or narrower is stored
  straight across at its own width. At exactly 32 both rules agree, and most files are exactly 32, so a
  sample will not tell you which you have.
- Battle sprite palettes are per species at slot 4. Taking the nearest palette in the archive hands most
  species the previous one's shiny colours, which looks almost right.
- Battle sprite pixels are scrambled with a rolling XOR key. Read straight they look like static.

### Avalonia

- A hidden `TabItem` is still tab zero, so selecting by index lands on the wrong tab.
- A `ComboBox` clears its own `SelectedIndex` when its items change. Restore the selection after the
  collection settles, not with it.
- Populating a combo box fires its selection handler, so guard reentrancy while loading.
- Mutating an `ObservableCollection` from inside its own `CollectionChanged` handler throws. The battle
  display crashed this way on every gender switch.

### hg-engine

- Read it as source text through `ElementScanner`, never by byte offset. The layout moves between
  versions; the source does not.
- Check the hg-engine wiki before calling something an upstream bug.

---

## House style

- Comments say why, not what, and only when the why is not obvious. One line. No block comments
  narrating how you debugged something.
- No em dashes anywhere: code, UI text, changelogs, commit messages.
- If the operator asks for a commit, subjects use `type(Scope): what changed`, lowercase, no body.
  Common types are `feature`, `fix`, and `chore`.
- Changelogs live in `Changelogs/`, written for users, with numbers where there are numbers.
- Don't name leaked source files in anything that lands in the repo. Say what the games do and how you
  checked it. The pret decomp is public and fine to cite.

---

## Where the domain knowledge is

`Research/` has the worked-out material: move animation opcodes and routines, field animation, sprites
and icons, the graphics archive census. Read it before re-deriving something.
[pret/pokeheartgold](https://github.com/pret/pokeheartgold) is the public reference for HGSS behaviour.
The source-selection and privacy policy is in
[Research/ReferenceEcosystem.md](Research/ReferenceEcosystem.md).

Bundled tools live in `Tools/` at the repo root and are copied next to the exe. `ndstool` and `blz`
are Windows-only so far; `chatot`, `dsrom`, `apicula`, `rotom` and `rotom-lsp` have Linux builds.
Release workflows build the pinned apicula source through `.github/actions/build-apicula` instead of
shipping the checked-in local fallback.
