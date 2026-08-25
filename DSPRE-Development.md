# DSPRE Development Guide
## DS Pokémon ROM Editor Reloaded — Architecture, Codebase & Avalonia Port

> **Branch:** `feature/avalonia`
> **Target framework:** `net8.0-windows`
> **Solution:** `DS_Map.sln`

---

## Table of Contents

1. [Project Overview](#1-project-overview)
2. [Solution Structure](#2-solution-structure)
3. [Core Architecture](#3-core-architecture)
4. [Key Subsystems](#4-key-subsystems)
5. [ROM Data & File Formats](#5-rom-data--file-formats)
6. [Editors Reference](#6-editors-reference)
7. [Working With the Codebase](#7-working-with-the-codebase)
8. [Avalonia Port — Status & Architecture](#8-avalonia-port--status--architecture)
9. [How to Add a New Avalonia Editor](#9-how-to-add-a-new-avalonia-editor)
10. [Patterns & Conventions Reference](#10-patterns--conventions-reference)
11. [Build & Dependencies](#11-build--dependencies)

---

## 1. Project Overview

DSPRE is a comprehensive ROM editor for Generation IV Pokémon games running on the Nintendo DS:

| Game | ROM ID (US) | Family |
|------|-------------|--------|
| Diamond | `ADAE` | `DP` |
| Pearl | `APAE` | `DP` |
| Platinum | `CPUE` | `Plat` |
| HeartGold | `IPKE` | `HGSS` |
| SoulSilver | `IPGE` | `HGSS` |

Gen V (Black/White, Black2/White2) is not supported. `GameFamilies.BW`/`BW2` exist as enum values but
are never reachable from ROM loading — the only place that checks them (`Main Window.cs`) treats them as
an explicitly unsupported family, not a working code path.

The tool unpacks a `.nds` ROM image into a working directory using `ndstool.exe`, extracts NARC archives on demand, allows editing via a rich tabbed UI, and repacks the ROM on save.

---

## 2. Solution Structure

```
DS_Map.sln
├── DS_Map/DSPRE.csproj                   ← Windows host app (net8.0-windows, WinForms)
├── DSPRE.Core/DSPRE.Core.csproj          ← Cross-platform ROM core (net8.0)
├── DSPRE.Avalonia/DSPRE.Avalonia.csproj  ← Cross-platform Avalonia UI + app (net8.0)
├── DSPRE.Tests/DSPRE.Tests.csproj        ← xUnit tests (net8.0, +net8.0-windows on Windows hosts)
├── Ekona/Ekona.csproj                    ← NDS sprite/image library (NCLR, NCGR, NCER)
└── Images/Images/Images.csproj           ← Nintendo image format plugin
```

Partway into the Avalonia port, the projects are split **logically**, not yet **physically**: two
root-level MSBuild `.props` files (`CoreFiles.props`, `AvaloniaFiles.props`) are each a single source
of truth listing which `.cs`/`.axaml` files under `DS_Map\` belong to which cross-platform project.
`DSPRE.Core.csproj` and `DSPRE.Avalonia.csproj` **compile** those file sets; `DS_Map\DSPRE.csproj`
**removes** the same sets from its own compile and gets the types back via a project reference. The
files themselves still physically live under `DS_Map\` (e.g. `DS_Map\ROMFiles\`, `DS_Map\DSUtils\`,
`DS_Map\RomInfo.cs`, `DS_Map\Avalonia\**`) — moving them into `DSPRE.Core\`/`DSPRE.Avalonia\` on disk
is tracked future cleanup, not required for the split to work.

| Project | TFM | Role |
|---|---|---|
| `DS_Map/DSPRE.csproj` | `net8.0-windows`, WinForms | The Windows executable (`DSPRE.exe`). Hosts the legacy WinForms `MainProgram` (`Main Window.cs`) as the default shell, **and** references `DSPRE.Avalonia.csproj` in-process so the same `.exe` can also show the pure-Avalonia `MainWindowView` shell (via `DSPRE_AVALONIA_SHELL=1`, or a WinForms Tools-menu item). Owns the WinForms-only editors, bundled native tools (`Tools\*.exe`), and the Windows-only bridge packages (ScintillaNET, WindowsAPICodePack). |
| `DSPRE.Core/DSPRE.Core.csproj` | `net8.0` | Cross-platform ROM core: file formats, ROM data models (`ROMFiles`), binary I/O (`DSUtils`), the script/text systems, and the game databases. No WinForms or Avalonia references — `System.Drawing.Common` compiles here but its GDI+ paths only actually run from the WinForms shell. Referenced by every other project. |
| `DSPRE.Avalonia/DSPRE.Avalonia.csproj` | `net8.0`, `WinExe` | The cross-platform Avalonia application: the entire `Avalonia\` UI layer (views, view models, GL renderer) plus a thin `Main`. Builds and runs standalone on Windows or Linux (`dotnet publish` with a `-Linux` configuration) as its own app, independent of WinForms. |
| `DSPRE.Tests/DSPRE.Tests.csproj` | `net8.0` (+`net8.0-windows` on Windows hosts) | xUnit test project. Always references `DSPRE.Core` and `DSPRE.Avalonia`; references `DS_Map/DSPRE.csproj` only when targeting `net8.0-windows`, so Windows-only tests (e.g. the GDI/`System.Drawing` bridge tests) compile alongside the cross-platform ones. |
| `Ekona/Ekona.csproj` | — | NDS sprite/image library (NCLR, NCGR, NCER). |
| `Images/Images/Images.csproj` | — | Nintendo image format plugin (depends on Ekona). |

`InternalsVisibleTo` wires the projects together (`DSPRE.Core` → `DSPRE`, `DSPRE.Avalonia`,
`DSPRE.Tests`; `DSPRE.Avalonia` → `DSPRE`, `DSPRE.Tests`) so UI code can still reach `internal`
core/UI types left over from the one-project era.

### DS_Map project layout

```
DS_Map/
├── Avalonia/                  ← NEW: Avalonia UI layer (see §8)
│   ├── App.axaml / App.axaml.cs
│   ├── DialogHelper.cs        ← Async message-box + file-dialog wrapper
│   ├── ImageConverter.cs      ← GDI+ → Avalonia bitmap bridge
│   ├── ViewModels/            ← INotifyPropertyChanged ViewModels
│   └── Views/                 ← .axaml + .axaml.cs pairs
│
├── DSUtils/                   ← Low-level NDS binary utilities
│   ├── ARM9.cs                ← ARM9 read/write + compression
│   ├── DSUtils.cs             ← ROM pack/unpack, NARC, EasyReader/Writer
│   ├── OverlayUtils.cs        ← Overlay decompression
│   ├── TextConverter.cs       ← DS character encoding (charmap.json)
│   └── YamlUtils.cs
│
├── Editors/                   ← WinForms editor UserControls (legacy)
│   ├── IEditorWithUnsavedChanges.cs
│   ├── OpenEditorsRegistry.cs
│   ├── Utils/
│   │   ├── NarcReader.cs      ← Streaming NARC reader
│   │   ├── SpriteSet.cs       ← Battle sprite container (4 bitmaps + 2 palettes)
│   │   ├── IndexedBitmapHandler.cs
│   │   └── UniqueList.cs
│   └── [EditorName].cs / .Designer.cs
│
├── ROMFiles/                  ← Data model classes (game binary ↔ C# objects)
│   ├── RomFile.cs             ← Abstract base with ToByteArray() + Save helpers
│   └── [DataType].cs
│
├── Resources/
│   ├── ScriptDatabase.cs      ← Script command definitions (from JSON)
│   ├── PokeDatabase.cs        ← Game constants (music, weather, cameras, etc.)
│   └── CharMaps/
│
├── LibNDSFormats/             ← 3D model loaders (NSBMD, NSBTX, NSBCA)
├── ScintillaUtils/            ← Script editor helpers
├── Script/                    ← Script parameter/label types
├── FlyEditor/                 ← Fly destination editor + data models
├── Tools/                     ← Bundled binaries (ndstool, blz, apicula, chatot, dsrom)
│
├── RomInfo.cs                 ← Static ROM context (ALL metadata & paths)
├── Helpers.cs                 ← UI utilities, status bar, handler state
├── Filesystem.cs              ← Typed path accessors for every NARC directory
├── EditorPanels.cs            ← Tab management + pop-out registry
├── SettingsManager.cs         ← JSON settings persistence
├── AppLogger.cs               ← Application-wide levelled logger
└── Main Window.cs             ← Main form; launches all editors
```

---

## 3. Core Architecture

### Static ROM context — `RomInfo`

All information about the currently loaded ROM is stored as **static properties** on `RomInfo`. This is the single source of truth used everywhere.

```csharp
RomInfo.workDir           // Extracted ROM folder
RomInfo.romID             // 4-char game code, e.g. "IPKE"
RomInfo.gameVersion       // GameVersions enum
RomInfo.gameFamily        // DP | Plat | HGSS
RomInfo.gameLanguage      // English | Japanese | ...
RomInfo.gameDirs          // Dictionary<DirNames, NarcDirectory>
RomInfo.isHGE             // true when hg-engine mod detected
```

**Critical:** always branch on `RomInfo.gameFamily` (and sometimes `gameLanguage`) before accessing offsets or structures. Many addresses differ per version/language.

### NARC archive system

Game data lives in NARC (Nintendo ARChive) files. They are unpacked on demand to numbered files (`0000`, `0001`, …) in a temp working directory.

```csharp
// Ensure unpacked before use
DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.scripts });

// Then access files
string dir = RomInfo.gameDirs[DirNames.scripts].unpackedDir;
string filePath = Filesystem.GetScriptPath(42);   // convenience wrapper
```

### `RomFile` base class

Every ROM data structure is a `RomFile`:

```csharp
public abstract class RomFile {
	public abstract byte[] ToByteArray();

	// Save back to NARC slot by ID
	protected bool SaveToFileDefaultDir(DirNames dir, int IDtoReplace, ...);

	// Save with a file browser dialog
	protected void SaveToFileExplorePath(...);
}
```

### Handler state guard — `Helpers.DisableHandlers()`

WinForms event handlers run immediately when combo boxes and NumericUpDowns are populated during loading. Use the global disable flag to suppress them:

```csharp
Helpers.DisableHandlers();
try {
	comboBox.SelectedIndex = newValue;
} finally {
	Helpers.EnableHandlers();
}

// In every handler:
if (Helpers.HandlersDisabled) return;
```

This pattern is **mandatory** in every WinForms editor and is also used inside Avalonia ViewModels for the same reason.

---

## 4. Key Subsystems

### ARM9 binary

```csharp
byte[] data = ARM9.ReadBytes(offset, length);
ARM9.WriteBytes(data, offset);
if (ARM9.CheckCompressionMark()) ARM9.Decompress(RomInfo.arm9Path);
```

### Overlay system

```csharp
string ovPath  = OverlayUtils.GetPath(overlayNumber);
if (OverlayUtils.IsCompressed(overlayNumber)) OverlayUtils.Decompress(overlayNumber);
uint ramAddr   = OverlayUtils.OverlayTable.GetRAMAddress(overlayNumber);
```

### Binary I/O helpers

```csharp
using (var reader = new DSUtils.EasyReader(path, offset)) {
	ushort v = reader.ReadUInt16();
	byte[] b = reader.ReadBytes(count);
}
using (var writer = new DSUtils.EasyWriter(path, offset)) {
	writer.Write(value);
}
```

### NARC streaming reader (`NarcReader`)

```csharp
var narc = new NarcReader(packedPath);  // not IDisposable — do NOT use 'using var'
narc.OpenEntry(idx);                    // opens FileStream, seeks to entry start
// ... read narc.fs ...
narc.Close();                           // closes the FileStream
```

### Text system (dual format)

Text archives exist in two forms simultaneously:
- **Binary** `.bin` in unpacked NARC directory (authoritative for the game)
- **Plaintext** `.txt` in an `expanded/` directory (human-editable)

`TextArchive` constructor tries `.txt` first if it is newer. The Avalonia text editor must respect this priority.

### Script system (dual format)

Same dual-format approach:
- **Binary** `.bin` in unpacked scripts NARC
- **Plaintext** `.script` in `expanded/scripts/`

Script commands are defined in a JSON database loaded by `ScriptDatabase.cs` at startup.

---

## 5. ROM Data & File Formats

### Battle sprites (pokemonBattleSprites NARC)

Layout: **6 entries per Pokémon** at `baseOffset = speciesIndex * 6`:

| Offset | Content | Size |
|--------|---------|------|
| +0 | Female back sprite | 6 448 bytes |
| +1 | Male back sprite | 6 448 bytes |
| +2 | Female front sprite | 6 448 bytes |
| +3 | Male front sprite | 6 448 bytes |
| +4 | Normal palette | 72 bytes |
| +5 | Shiny palette | 72 bytes |

**Sprite decode** (`MakeImage`):
1. Seek 48 bytes into the entry (skip header)
2. Read 3 200 `ushort` words
3. Decrypt with a linear congruential XOR pass:
   - Plat/HGSS: forward pass starting from `arr[0]`
   - DP: backward pass starting from `arr[3199]`
4. Unpack 4-bit nibbles → 160×80 8bpp indexed `Bitmap`

**Palette decode** (`ReadPalette`):
1. Seek 40 bytes into the entry (skip header)
2. Read 16 `ushort` words in BGR555 format
3. Convert: `R = (v & 0x1F) << 3`, `G = ((v>>5) & 0x1F) << 3`, `B = ((v>>10) & 0x1F) << 3`

### Map headers

Three concrete types depending on game family:

```csharp
MapHeader h = MapHeader.LoadFromARM9(headerNumber);
// or via dynamic-headers patch:
MapHeader h = MapHeader.GetMapHeader(headerNumber);
```

Common properties: `areaDataID`, `matrixID`, `eventFileID`, `scriptFileID`, `textArchiveID`, `wildPokemon`, `musicDayID`, `weatherID`, `cameraAngleID`.

### Event file structure

`EventFile` contains four typed lists:

| Type | Description |
|------|-------------|
| `Spawnable` | Signs, hidden items, misc interactables |
| `Overworld` | NPCs, trainers, item pickups |
| `Warp` | Map transitions (header + anchor) |
| `Trigger` | Script triggers on tile step |

All events store both map-relative (`xMapPosition`, `yMapPosition`) and matrix-relative (`xMatrixPosition`, `yMatrixPosition`) coordinates.

### Evolution file

7 evolution slots per Pokémon (`EvolutionFile.numEvolutions = 7`), each slot is `EvolutionData { EvolutionMethod method, ushort param, ushort target }`.

### Learnset

`LearnsetData` uses `UniqueList<(byte level, ushort move)>`. Vanilla limit is 20 entries. Entries beyond 20 are supported but flagged with a UI warning.

---

## 6. Editors Reference

### WinForms editors (legacy — still in use)

All editors below are `UserControl` subclasses in `DS_Map/Editors/`. Each has a `.Designer.cs` companion. They are embedded in the main window tab strip or opened in pop-out windows via `EditorPanels`.

> **Verified against the current tree:** four editors that used to have a WinForms file here have since
> been **fully retired from WinForms** — `Editors/EggMoveEditor.cs`, `Editors/TradeEditor.cs`,
> `Editors/OverlayEditor.cs` and `FlyEditor/FlyEditor.cs` no longer exist on disk; only their Avalonia
> ports remain (see §8's migration table). Every other row below still has its WinForms file.

| Editor | File | NARC sources |
|--------|------|-------------|
| Header Editor | `HeaderEditor.cs` | ARM9 header table |
| Matrix Editor | `MatrixEditor.cs` | `matrices` |
| Map Editor | `MapEditor.cs` | `maps`, `buildingTextures` |
| Event Editor | `EventEditor.cs` | `eventFiles` |
| Script Editor | `ScriptEditor.cs` | `scripts` |
| Level Script Editor | `LevelScriptEditor.cs` | `scripts` |
| Text Editor | `TextEditor.cs` | `textArchives` |
| Trainer Editor | `TrainerEditor/TrainerEditor.cs` | `trainerProperties`, `trainerParty` |
| Wild Encounters (DPPt) | `WildEditorDPPt.cs` | `encounters` |
| Wild Encounters (HGSS) | `WildEditorHGSS.cs` | `encounters` |
| Pokémon Editor | `PokemonEditor.cs` | `personalPokeData` |
| Personal Data | `PersonalDataEditor.cs` | `personalPokeData` |
| Learnset Editor | `LearnsetEditor.cs` | `learnsets` |
| Evolutions Editor | `EvolutionsEditor.cs` | `evolutions` |
| Sprite Editor | `PokemonSpriteEditor.cs` | `pokemonBattleSprites` |
| Move Data Editor | `MoveDataEditor.cs` | `moveData` |
| Item Editor | `ItemEditor.cs` | `itemData` |
| TM Editor | `TMEditor.cs` | ARM9 TM table |
| ~~Egg Move Editor~~ | *(retired — Avalonia-only, `EggMoveEditorView`)* | `eggMoves` |
| ~~Trade Editor~~ | *(retired — Avalonia-only, `TradeEditorView`)* | `trades` |
| Building Editor | `BuildingEditor.cs` | `exteriorBuildingModels` |
| NSBTX Editor | `NsbtxEditor.cs` | `buildingTextures` |
| ~~Overlay Editor~~ | *(retired — Avalonia-only, `OverlayEditorView`)* | overlays |
| Spawn Editor | `SpawnEditor.cs` | ARM9 spawn table |
| Camera Editor | `CameraEditor.cs` | overlay camera data |
| ~~Fly Editor~~ | *(retired — Avalonia-only, `FlyEditorView`)* | ARM9 fly table |
| Char Map Manager | `Resources/CharMaps/CharMapManager.cs` *(file still present; `Main Window.cs` now opens the Avalonia `CharMapManagerView` instead)* | `charmap.json` |

### Editors with unsaved changes tracking

Editors that write data implement `IEditorWithUnsavedChanges`:

```csharp
public interface IEditorWithUnsavedChanges {
	bool HasUnsavedChanges { get; }
	string UnsavedChangesDescription { get; }
	void SaveChanges();
	void DiscardChanges();
}
```

They register/unregister with `OpenEditorsRegistry` via `SetDirty()` / `SetClean()`. The main window checks the registry before closing or switching ROMs.

---

## 7. Working With the Codebase

### Loading sequence

1. `Program.cs` — Velopack init, creates `MainProgram` (main form)
2. User opens a `.nds` ROM → `DSUtils.UnpackRom()` extracts to `workDir`
3. `RomInfo` is populated (game ID, family, language, offsets, NARC directory paths)
4. Each editor is lazily initialized when its tab is first selected

### Adding a new WinForms editor

1. Create `Editors/MyEditor.cs` + `MyEditor.Designer.cs` (UserControl)
2. Register a tab page in `Main Window.Designer.cs`
3. Wire `mainTabControl_SelectedIndexChanged` in `Main Window.cs`
4. If it saves data: implement `IEditorWithUnsavedChanges`, call `SetDirty()` / `SetClean()`
5. Add a `Reset()` method that clears state when a new ROM is loaded

### Accessing a NARC file

```csharp
// 1. Unpack on demand
DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.yourDir });

// 2. Use Filesystem helpers for paths
string filePath = Filesystem.GetYourTypePath(id);   // e.g. GetScriptPath(id)
int count       = Filesystem.GetYourTypeCount();    // e.g. GetScriptCount()

// 3. Or build path manually
string dir  = RomInfo.gameDirs[DirNames.yourDir].unpackedDir;
string path = Path.Combine(dir, $"{id:D4}");
```

### Game-version branching

```csharp
switch (RomInfo.gameFamily) {
	case GameFamilies.DP:   /* Diamond/Pearl */   break;
	case GameFamilies.Plat: /* Platinum */         break;
	case GameFamilies.HGSS: /* HG/SS */            break;
}
// Also check: RomInfo.gameLanguage, RomInfo.isHGE
```

### Logging

```csharp
AppLogger.Debug("verbose detail");
AppLogger.Info("normal info");
AppLogger.Warn("potential problem");
AppLogger.Error("non-fatal error");
AppLogger.Fatal("crash-level problem");
string recent = AppLogger.GetRecentLogs(); // for crash reports
```

### Settings

```csharp
bool autoUpdate = SettingsManager.Settings.automaticallyCheckForUpdates;
SettingsManager.Settings.useDecompNames = true;
SettingsManager.Save();
```

---

## 8. Avalonia Port — Status & Architecture

### Why Avalonia?

DSPRE targets `net8.0-windows`. WinForms works but has limitations:
- No cross-platform future path
- Poor high-DPI scaling and theming
- Limited data-binding support

Avalonia 12 gives us MVVM data binding, compiled bindings (type-safe at compile time), and a modern UI with Fluent theming, while still running on Windows via the same `.exe`.

### Dual-stack coexistence

The project currently runs **both** WinForms and Avalonia simultaneously:

```
Program.cs
 ├─ Velopack init
 ├─ Avalonia App bootstrapped (App.axaml)     ← new
 └─ MainProgram (WinForms main window)         ← legacy
	 └─ Opens Avalonia windows for migrated editors
		(passed as regular Window objects, shown with .Show())
```

WinForms editors that have been migrated are opened as Avalonia `Window` objects from `Main Window.cs`. Legacy editors still open as WinForms forms/user controls. Both coexist until migration is complete.

**Packages still kept for the bridge phase:**
- `jacobslusser.ScintillaNET` — script editor (WinForms only, not yet migrated)
- `Microsoft.WindowsAPICodePack-Shell` — used for legacy file dialogs in unmigrated code

### Avalonia folder structure

```
DS_Map/Avalonia/
├── App.axaml                  ← FluentTheme, DataGrid styles, app resources
├── App.axaml.cs               ← Avalonia Application entry
├── DialogHelper.cs            ← Static async message-box + file pickers
├── ImageConverter.cs          ← System.Drawing.Image → Avalonia.Media.Imaging.Bitmap
│
├── ViewModels/
│   ├── [Editor]ViewModel.cs   ← INotifyPropertyChanged, no WinForms deps
│   └── BoolToGridLengthConverter.cs
│
└── Views/
	├── [Editor]View.axaml     ← Compiled-binding AXAML markup
	└── [Editor]View.axaml.cs  ← Code-behind (handlers only, no logic)
```

### MVVM pattern used

All Avalonia editors follow **direct `INotifyPropertyChanged`** — no MVVM framework (no ReactiveUI, no CommunityToolkit). This keeps the code portable and easy to understand.

```csharp
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

	// Design-time constructor (parameterless) — always guarded with Design.IsDesignMode
	public MyEditorViewModel()
	{
		if (!Design.IsDesignMode) return;
		// populate dummy data for the Avalonia designer
	}

	// Runtime constructor (takes real data)
	public MyEditorViewModel(string[] names, ...) { ... }
}
```

### Designer safety rule

**Every ViewModel with a parameterless constructor must guard its design-time branch with `Design.IsDesignMode`.**

Without this, the designer tries to call `RomInfo` properties before a ROM is loaded and throws `NullReferenceException`, crashing the preview.

```csharp
public MyViewModel()
{
	if (!Design.IsDesignMode) return;  // ← ALWAYS first line
	// safe dummy data below
	Names = new ObservableCollection<string> { "Item A", "Item B" };
}
```

### Compiled bindings rule

The project sets `<AvaloniaUseCompiledBindingsByDefault>true</AvaloniaUseCompiledBindingsByDefault>`.

Every `.axaml` file must declare `x:DataType`:

```xml
<UserControl ...
	x:DataType="vm:MyEditorViewModel">
```

Bindings are then type-checked at compile time. If you see `CS0xxx` binding errors, check that `x:DataType` matches the ViewModel and that the bound property exists.

### `IEditorWithUnsavedChanges` in Avalonia

Avalonia ViewModels implement the same interface as WinForms editors:

```csharp
public bool HasUnsavedChanges => _dirty;
public string UnsavedChangesDescription => $"My Editor (item {_currentId})";
public void SaveChanges()   { /* write to NARC */ SetClean(); }
public void DiscardChanges(){ _dirty = false; OnPropertyChanged(nameof(HasUnsavedChanges)); }
```

Dirty tracking helpers:

```csharp
private void SetDirty() {
	if (_dirty) return;
	_dirty = true;
	OnPropertyChanged(nameof(HasUnsavedChanges));
}
private void SetClean() {
	if (!_dirty) return;
	_dirty = false;
	OnPropertyChanged(nameof(HasUnsavedChanges));
}
```

**Guard every event handler** against false-dirty on initial load:

```csharp
private void SomeControl_ValueChanged(object sender, ...) {
	if (_isLoading) return;   // or a 'HandlersDisabled' equivalent flag
	SetDirty();
	...
}
```

### Composite editors — parent/child ViewModels

The Pokémon Editor demonstrates the recommended pattern for editors with multiple tabs:

```
PokemonEditorViewModel          ← parent: owns selector, icon, Save All
├── PersonalDataEditorViewModel ← child sub-VM
├── LearnsetEditorViewModel     ← child sub-VM
├── EvolutionsEditorViewModel   ← child sub-VM
└── PokemonSpriteEditorViewModel← child sub-VM
```

The parent:
- Holds `SelectedMonIndex` and calls `LoadMon(id)` on all children
- Aggregates `HasUnsavedChanges` from all children
- Subscribes to children's `PropertyChanged` to re-raise `HasUnsavedChanges`
- `SaveAll()` / `DiscardChanges()` delegate to each child

The child view is embedded as a `UserControl` inside the parent window's tab:

```xml
<!-- PokemonEditorView.axaml -->
<TabItem Header="Personal Data">
	<views:PersonalDataEditorView DataContext="{Binding PersonalVM}"/>
</TabItem>
```

The child view does **not** own its own Pokémon selector — that belongs to the parent.

### `DialogHelper` — all dialogs go through here

Never use `System.Windows.Forms.MessageBox` or `OpenFileDialog` in Avalonia code.

```csharp
// Message boxes
await DialogHelper.ShowInfo("Saved successfully.");
await DialogHelper.ShowError("Could not read file.");
bool yes = await DialogHelper.AskYesNo("Discard changes?");

// File dialogs (requires the owning Window)
string path = await DialogHelper.OpenFile(owner, "Open PNG",
	new[] { DialogHelper.PngFilter, DialogHelper.AllFilter });

string savePath = await DialogHelper.SaveFile(owner, "Save CSV",
	new[] { DialogHelper.CsvFilter }, "export.csv");
```

### `ImageConverter` — GDI+ to Avalonia

GDI+ (`System.Drawing`) is still used for sprite decode and icon rendering because `DSUtils.GetPokePic()` returns a `System.Drawing.Image`. Convert before binding:

```csharp
using GdiBitmap = System.Drawing.Bitmap;
using AvaBitmap = Avalonia.Media.Imaging.Bitmap;

AvaBitmap avaBmp = ImageConverter.ToAvaloniaBitmap(gdiBitmap);
```

### Migration status

As of this refresh, every editor that was tracked in the old §6 WinForms list has a real Avalonia
port (confirmed by matching `DS_Map/Avalonia/Views/*.axaml` against `DS_Map/Avalonia/ViewModels/*.cs`,
80 view files / 78 real view models at last count — the migration is far along; what's left is
mostly the WinForms shell itself, not individual editors. Editors are grouped below the way the
Avalonia main menu groups them (see the **Main shell** entry), with composite editors' sub-editors
indented via →. "UserControl, `EditorHostWindow`" means the view is a plain `UserControl` that is
embedded directly where it's reused (e.g. a Maps-workspace tab) and wrapped in the generic
`EditorHostWindow` (`Avalonia/Views/EditorHostWindow.axaml.cs`) when launched standalone from a menu
— see the note below the tables.

#### ✅ Fully migrated to Avalonia

##### Main shell & Maps workspace

| Editor | Avalonia View | ViewModel |
|--------|--------------|-----------|
| **Main Window shell** | `MainWindowView.axaml` (Window) | `MainWindowViewModel` |
| Maps workspace *(Header/Map/Events/Matrix/Area Data/Encounters/Scripts/Level Scripts/Text tabs)* | `MapsWorkspaceView.axaml` (UserControl) | `HeaderEditorViewModel` (shared) |
| → Header sidebar | `HeaderSidebarView.axaml` (UserControl) | `HeaderEditorViewModel` (shared) |
| → Header fields tab | `HeaderFieldsView.axaml` (UserControl) | `HeaderEditorViewModel` (shared) |
| Header Editor *(standalone launch)* | `HeaderEditorView.axaml` (UserControl, `EditorHostWindow`) | `HeaderEditorViewModel` |
| Advanced Header Search | `HeaderSearchView.axaml` (Window) | `HeaderSearchViewModel` |

##### World / Map domain

| Editor | Avalonia View | ViewModel |
|--------|--------------|-----------|
| Camera Editor | `CameraEditorView.axaml` (UserControl, `EditorHostWindow`) | `CameraEditorViewModel` |
| Map Editor | `MapEditorView.axaml` (UserControl; embedded in Maps workspace + standalone) | `MapEditorViewModel` |
| Matrix Editor *(2-D map/header/height grids; the 3-D "full matrix" fly-around lives in the Map Editor)* | `MatrixEditorView.axaml` (UserControl; embedded + standalone) | `MatrixEditorViewModel` (+ `MatrixGridControl`) |
| Event Editor | `EventEditorView.axaml` (UserControl; embedded + standalone) | `EventEditorViewModel` |
| → Ground Item Scripts dialog | `GroundItemScriptsView.axaml` (Window) | `GroundItemScriptsViewModel` |
| Building Editor | `BuildingEditorView.axaml` (Window) | `BuildingEditorViewModel` |
| Area Data Editor | `AreaDataEditorView.axaml` (UserControl; embedded + standalone) | `AreaDataEditorViewModel` |
| Spawn Point Editor | `SpawnEditorView.axaml` (Window) | `SpawnEditorViewModel` |
| Fly / Warp Editor | `FlyEditorView.axaml` (Window) | `FlyEditorViewModel` |
| Overlay Editor | `OverlayEditorView.axaml` (Window) | `OverlayEditorViewModel` |

##### Text & Scripts

| Editor | Avalonia View | ViewModel |
|--------|--------------|-----------|
| Text Editor | `TextEditorView.axaml` (UserControl; embedded + standalone) | `TextEditorViewModel` |
| Script Editor *(AvaloniaEdit + TextMate, real "rotom" syntax-highlighting grammar and an OneDark theme — no longer plain-text)* | `ScriptEditorView.axaml` (UserControl; embedded + standalone) | `ScriptEditorViewModel` |
| Level Script Editor | `LevelScriptEditorView.axaml` (UserControl; embedded + standalone) | `LevelScriptEditorViewModel` |

##### Pokémon domain

| Editor | Avalonia View | ViewModel |
|--------|--------------|-----------|
| **Pokémon Editor** (unified) | `PokemonEditorView.axaml` (Window) | `PokemonEditorViewModel` |
| → Personal Data | `PersonalDataEditorView.axaml` (UserControl) | `PersonalDataEditorViewModel` |
| → Learnset | *(inline in PokemonEditorView)* | `LearnsetEditorViewModel` |
| → → Bulk Learnset dialog | `BulkLearnsetEditorView.axaml` (Window) | `BulkLearnsetEditorViewModel` |
| → Evolutions | *(inline in PokemonEditorView)* | `EvolutionsEditorViewModel` |
| → Sprites | `PokemonSpriteEditorView.axaml` (UserControl) | `PokemonSpriteEditorViewModel` |
| Move Data Editor | `MoveDataEditorView.axaml` (Window) | `MoveDataEditorViewModel` |
| Move Animations & Battle Scripts | `BattleScriptEditorView.axaml` (UserControl, `EditorHostWindow`) | `BattleScriptEditorViewModel` |
| → Battle scene preview *(shared control — also used by the Battle Display Editor)* | `BattleSceneControl.axaml` (UserControl) | bound to the hosting VM |
| → Script Command Guide dialog | `ScriptCommandGuideView.axaml` (Window) | `ScriptCommandGuideViewModel` |
| TM Editor | `TMEditorView.axaml` (Window) | `TMEditorViewModel` |
| → TM/HM Bulk Editor | `TmHmBulkEditorView.axaml` (UserControl, `EditorHostWindow`) | `TmHmBulkEditorViewModel` |
| → → Copy Machines dialog | `CopyMachinesDialogView.axaml` (Window) | `CopyMachinesDialogViewModel` |
| Egg Move Editor | `EggMoveEditorView.axaml` (Window) | `EggMoveEditorViewModel` |
| Trade Editor | `TradeEditorView.axaml` (Window) | `TradeEditorViewModel` |
| Starter Pokémon Editor | `StarterEditorView.axaml` (Window) | `StarterEditorViewModel` |
| Form Editor (hg-engine) | `HgEngineFormEditorView.axaml` (Window) | `HgEngineFormEditorViewModel` |
| Trophy Garden Editor (DP/Plat) | `TrophyGardenEditorView.axaml` (UserControl, `EditorHostWindow`) | `TrophyGardenEditorViewModel` |

##### Encounters

| Editor | Avalonia View | ViewModel |
|--------|--------------|-----------|
| Wild Encounters (DPPt) | `WildEditorDPPtView.axaml` (UserControl) | `WildEditorDPPtViewModel` |
| Wild Encounters (HGSS) | `WildEditorHGSSView.axaml` (UserControl) | `WildEditorHGSSViewModel` |
| **Special Encounters Editor** (composite) | `EncountersEditorView.axaml` (Window) | `EncountersEditorViewModel` |
| → Honey Tree (DPPt) | `HoneyTreeEncounterView.axaml` (UserControl) | `HoneyTreeEncounterViewModel` |
| → Great Marsh (DPPt) | `GreatMarshEncounterView.axaml` (UserControl) | `GreatMarshEncounterViewModel` |
| → Bug Contest (HGSS) | `BugContestEncounterView.axaml` (UserControl) | `BugContestEncounterViewModel` |
| → Safari Zone (HGSS) | `SafariZoneEncounterView.axaml` (UserControl) | `SafariZoneEncounterViewModel` |
| → → Safari Zone group | `SafariZoneGroupView.axaml` (UserControl) | `SafariZoneGroupViewModel` |
| Headbutt Editor (HGSS) *(now its own standalone editor — see note)* | `HeadbuttEncounterView.axaml` (Window) | `HeadbuttEncounterViewModel` |

##### Trainers

| Editor | Avalonia View | ViewModel |
|--------|--------------|-----------|
| Trainer Editor | `TrainerEditorView.axaml` (Window) | `TrainerEditorViewModel` (+ `TrainerPartyMonViewModel`) |
| → Trainer Classes tab | `TrainerClassesView.axaml` (UserControl) | `TrainerClassesViewModel` |
| → → Add Trainer Class dialog | `AddTrainerClassView.axaml` (Window) | `AddTrainerClassViewModel` |
| → Mon Reorder dialog | `MonReorderView.axaml` (Window) | `MonReorderViewModel` |
| → Trainer Search dialog | `TrainerSearchView.axaml` (Window) | `TrainerSearchViewModel` |
| → DV Calculator | `DVCalcView.axaml` (+ `DVCalcNatureViewerView.axaml`) (Window) | `DVCalcViewModel` (wraps static `DVCalculator`) + `DVCalcNatureViewerViewModel` |
| → Battle Messages | `BattleMessageEditorView.axaml` (Window) | `BattleMessageEditorViewModel` |
| Trainer Sprite Editor | `TrainerSpriteEditorView.axaml` (Window) | `TrainerSpriteEditorViewModel` |
| Vs. Seeker Rematch Editor (DP/Plat English) | `VsSeekerRematchView.axaml` (UserControl, `EditorHostWindow`) | `VsSeekerRematchViewModel` |
| Trainer Flag Bulk Editor | `TrainerFlagBulkEditorView.axaml` (UserControl, `EditorHostWindow`) | `TrainerFlagBulkEditorViewModel` |
| Battle Tower Editor | `BattleTowerEditorView.axaml` (UserControl, `EditorHostWindow`) | `BattleTowerEditorViewModel` |

##### Items

| Editor | Avalonia View | ViewModel |
|--------|--------------|-----------|
| Item Editor | `ItemEditorView.axaml` (Window) | `ItemEditorViewModel` |
| Item Tables (Pickup / Hidden / Rock Smash) | `ItemTableEditorView.axaml` (Window) | `ItemTableEditorViewModel` |

##### Graphics

| Editor | Avalonia View | ViewModel |
|--------|--------------|-----------|
| Title Screen Editor | `TitleScreenEditorView.axaml` (Window) | `TitleScreenEditorViewModel` |
| Dungeon Cutin Editor | `DungeonCutinEditorView.axaml` (Window) | `DungeonCutinEditorViewModel` |
| Trainer Card Editor | `TrainerCardEditorView.axaml` (Window) | `TrainerCardEditorViewModel` |
| Overworld (BTX) Editor | `BtxEditorView.axaml` (Window) | `BtxEditorViewModel` |
| → Add Overworld Entry dialog | `AddOverworldEntryView.axaml` (Window) | `AddOverworldEntryViewModel` |
| NSBTX Texture Editor | `NsbtxEditorView.axaml` (Window) | `NsbtxEditorViewModel` |
| Battle Display Editor | `BattleDisplayEditorView.axaml` (UserControl, `EditorHostWindow`) | `BattleDisplayEditorViewModel` |

##### hg-engine integration

| Editor | Avalonia View | ViewModel |
|--------|--------------|-----------|
| Link hg-engine checkout | `HgEngineLinkView.axaml` (Window) | `HgEngineLinkViewModel` |
| Compile ROM | `CompileRomView.axaml` (Window) | `CompileRomViewModel` |
| Custom Script Command Manager | `CustomScrcmdManagerView.axaml` (Window) | `CustomScrcmdManagerViewModel` |

##### Tools & utility windows

| Editor | Avalonia View | ViewModel |
|--------|--------------|-----------|
| Command Palette ("Go to…", Ctrl+P) | `CommandPaletteView.axaml` (Window) | `CommandPaletteViewModel` |
| Address Helper | `AddressHelperView.axaml` (Window) | `AddressHelperViewModel` |
| Research Helper | `ResearchHelperView.axaml` (Window) | `ResearchHelperViewModel` |
| Char Map Manager | `CharMapManagerView.axaml` (Window) | `CharMapManagerViewModel` |
| Validation & Where-Used (Project Checks) | `ProjectChecksView.axaml` (Window) | `ProjectChecksViewModel` |
| Edit Dropdown Labels | `LabelEditorView.axaml` (Window) | `LabelEditorViewModel` |
| ROM Patch Toolbox | `PatchToolboxView.axaml` (Window) | `PatchToolboxViewModel` |
| Music & Battle Tables (Table Editor) | `TableEditorView.axaml` (Window) | `TableEditorViewModel` |
| Game Icon & Banner Editor | `BannerEditorView.axaml` (Window) | `BannerEditorViewModel` |
| Welcome & Tutorial | `WelcomeView.axaml` (Window) | `WelcomeViewModel` |
| Settings | `SettingsWindowView.axaml` (Window) | `SettingsWindowViewModel` |
| 3D Model Viewer (GL Test) | `GlTestView.axaml` (Window) | *(code-behind only — no dedicated ViewModel)* |

> **`EditorHostWindow`** (`Avalonia/Views/EditorHostWindow.axaml.cs`) is the generic window that hosts
> a `UserControl`-based editor when it's launched standalone from a menu (as opposed to being embedded
> in a tab, e.g. inside the Maps workspace). Editors are authored as `UserControl` — not `Window` —
> specifically so they can be reused both ways; Avalonia windows cannot be re-parented as a child
> control, so a `UserControl` editor that's meant to be embeddable must never be changed back to a
> `Window` base type (this is called out directly in `EventEditorView`'s class doc comment).

> **Camera Editor note:** Ported as an Avalonia `UserControl` (not Window). It is **no longer embedded
> as a MainWindow tab** — that only held true for an earlier slice of the shell milestone; today it
> launches standalone (`AvaloniaEditorLauncher.OpenCameraEditor` wraps it in an `EditorHostWindow`,
> same as most other `UserControl` editors). `SetupAsync` runs from the `Loaded` handler based on the
> control's `DataContext`, so it works whether the VM is supplied via the `(vm)` constructor or via
> `DataContext` binding. HGSS shows 13 columns (3 offset cols); for DP/Plat the X/Y/Z offset columns
> are hidden in code-behind by matching their **Header text** (Avalonia's compiled-XAML name generator
> does not emit fields for `DataGridColumn`, so `x:Name` cannot be used on columns).

> **Main Window shell note:** `MainWindowView` has grown from an early preview into a complete shell:
> a full menu bar (File/Pokémon/Trainers/Items/Text/World/Graphics/Tools) matching or exceeding the
> WinForms menu, plus the embedded **Maps workspace** (`MapsWorkspaceView`, see above) as its main
> surface — a Porymap-style header sidebar + context strip + tabbed Map/Events/Matrix/Area
> Data/Encounters/Scripts/Level Scripts/Text view that follows whichever header is selected. Every
> other migrated editor opens from its menu via `AvaloniaEditorLauncher` (mostly as standalone
> windows). It is the default shell for the standalone `DSPRE.Avalonia` cross-platform exe, and for
> the Windows exe when `DSPRE_AVALONIA_SHELL=1` (see `AvaloniaApp.OnFrameworkInitializationCompleted`
> in `Avalonia/App.axaml.cs`); it is also still reachable *from* the legacy WinForms main window via
> **Tools → Avalonia Main Window (Preview)** (`avaloniaMainWindowToolStripMenuItem_Click` in
> `Main Window.cs`), which remains the default entry point for the Windows exe otherwise
> (`DS_Map/WinFormsShellHost.cs` installs the hook Program.cs uses to pick a shell).

> **`AvaloniaEditorLauncher`** (`Avalonia/AvaloniaEditorLauncher.cs`) is a static class centralising the NARC-unpack + data-sourcing + launch logic (`.Show()` for `Window`s, `EditorHostWindow` + `.ShowManaged()` for `UserControl`s) for each migrated editor. The new shell delegates to it; the WinForms handlers can be refactored onto it later to remove the current duplication. Every launcher is a no-op when `IsRomLoaded` is false, and `BlockedForHge(...)` additionally blocks editors whose data an active hg-engine link now owns.

> **Encounters Editor note:** Composite Window following the parent/child sub-VM pattern (like the Pokémon Editor). `EncountersEditorViewModel` gates sub-editor tabs by game family and aggregates their dirty state. **DPPt side is complete** (Honey Tree + Great Marsh). **HGSS side: Bug Contest + Safari Zone are complete.** **Headbutt now has its own dedicated standalone editor** (`HeadbuttEncounterView`/`HeadbuttEncounterViewModel`, reachable from the Pokémon menu's "Headbutt Editor…") rather than living inside this composite — `EncountersEditorViewModel` still carries a stale `PendingNote` string calling it "not yet ported," left over from before that editor existed; the composite itself never grew a Headbutt tab. The Safari Zone sub-editor is itself a mini-composite: `SafariZoneEncounterViewModel` owns five `SafariZoneGroupViewModel`s (Grass/Surf/Old Rod/Good Rod/Super Rod), each with Morning/Day/Night normal lists plus shared object-encounter slots carrying item requirements. Pokémon icons are rendered via `DSUtils.GetPokePic` → `ImageConverter.ToAvaloniaBitmap`. Add an HGSS sub-editor by creating its `UserControl`+VM, adding a gated `TabItem`, and wiring it into the parent's dirty aggregation + `SetupAsync`.

> **Header Editor note:** Full field port — all common fields plus per-family ones (DP/Plat location specifier; Plat/HGSS area icon; HGSS world-map coords, follow mode, Kanto flag, location type). Camera/weather/music use a small `MappedCombo` helper (parallel name list + raw-ID list) to keep a ComboBox synced with a NumericUpDown; weather/camera/area-icon show preview images loaded from `DSPRE.Properties.Resources` via `ImageConverter`. The VM edits the live `MapHeader` and relies on `MapHeader.ToByteArray()` for the per-family bit-packing on save (ARM9 table or dynamic-headers file). The Advanced Header Search sub-form is now also ported (`HeaderSearchView`/`HeaderSearchViewModel`, World menu) — `HeaderEditorViewModel`'s own class doc comment still lists it as "not yet ported," which is stale. **Genuinely still missing** (cross-editor / peripheral, per that same comment): the "create associated Text/Script/Level-Script/Event files" prompt on add-header, and the open-wild/script/level-script/area-data navigation buttons (the Maps workspace's tabbed layout covers most of the same need by following the selected header directly).

> **Trainer Editor note:** Core port — trainer selection + properties (name, class, AI flags, 4 held items, double/custom-moves/held-items flags, party size) and the full 6-Pokémon party (species/form/level/4 moves/held item/gender/ability/IV/ball seals), each party slot a `TrainerPartyMonViewModel` whose ability list rebuilds per species. The trainer-class sprite uses the shared `TrainerClassSpriteRenderer` (see below). Saves `trp.ToByteArray()` + `party.ToByteArray()` back to the NARC dirs and the trainer name to its text archive. **Sub-forms (now ported):** Mon Reorder (`MonReorderView`, returns a slot permutation the editor applies), Trainer Search (`TrainerSearchView`, name filter → go-to), DV Calculator (`DVCalcView` + `DVCalcNatureViewerView`, wraps the existing static `DVCalculator` engine and writes DV/gender/ability back to the party), and Battle Messages (`BattleMessageEditorView`, the trainer-text table editor — Scintilla swapped for a plain TextBox; Save rewrites the whole table + offset file + message archive). A **Trainer Classes tab** (`TrainerClassesView`/`TrainerClassesViewModel`) has also been added since, with its own **Add Trainer Class dialog** (`AddTrainerClassView`/`AddTrainerClassViewModel`). **Remaining minor fidelity items:** the per-species "more than one gender" gate isn't applied (gender selector editable whenever the game supports it — HGSS / AI-backport), and form is edited as a raw numeric ID (no per-species form-name enumeration).

> **`TrainerClassSpriteRenderer`** (`Avalonia/TrainerClassSpriteRenderer.cs`) is the reusable trainer-class sprite renderer extracted from the WinForms `LoadTrainerClassPic`/`UpdateTrainerClassPic` (Ekona NCLR/NCGR/NCER → `Get_Image` → Avalonia bitmap). Shared by the Trainer Editor and the Table Editor. DP classes have no NCER, so `FrameCount` is 0 and `Render` returns null there.

> **Table Editor note:** Ported data-only. Parity points: (1) the animated **trainer-class sprite preview** on the VS-Trainer tab — **now restored** via `TrainerClassSpriteRenderer` (frame selector included); (2) the **VS Pokémon** table is read-only, matching the original (its WinForms Save handler was an empty no-op). Sections are gated by game family: Conditional Music (HGSS), Effect Combos (HGSS + Plat), VS Trainer/Pokémon (HGSS). The VM mirrors the original ARM9 addressing, including the synth-overlay repoint flags on `PatchToolboxDialog`.

> **3D renderer rebuild note:** The OpenGL layer was **already stripped to stubs** on this branch to drop the Tao dependency — `NSBMDGlRenderer`'s draw methods are no-ops (only its matrix math works) and `SimpleOpenGlControl2` is an empty `Panel`. 3D views in the current WinForms build never rendered anything real either. The original ~3019-line immediate-mode (FFP: `glBegin`/`glVertex`/`glLightfv`/`glMatrixMode`) renderer survives in git history at `cdc228d~1`. **Chosen rebuild path:** modern GL (VBO + GLSL shader) on Avalonia `OpenGlControlBase` (`Avalonia/Gl/NsbmdGlControl.cs`), recovering the NDS display-list→geometry logic and replacing FFP draw/lighting with a shader. Built as runnable, visually-verified slices (GL can't be validated by compile alone).
>
> **Slice 1 (done):** `NsbmdGlControl`, `GlFunctions` (GL bound via `GetProcAddress` delegates), `Mat4` math, an orbit camera, a self-test cube.
> **Slice 2 (done):** `NsbmdGeometry.BuildMesh` — a self-contained NDS GE display-list interpreter (joint matrix stack, `NSBMDPolygon.PolyData` decode, tri/quad/strip tessellation) emitting a `[pos,color]` mesh.
> **Slice 3 (done — textures):** `NsbmdTextureDecoder` decodes all 7 NDS texture formats → RGBA8; `NsbmdGeometry.BuildModel` returns an `NsbmdRenderModel` of per-material textured parts; `NsbmdGlControl` uploads one VBO+texture per part with alpha-test discard.
> **Slice 4 (still not started):** normals + diffuse lighting — `NsbmdGeometry.cs` still skips the NORMAL (`0x21`) display-list command outright (`case 0x21: idx += 4; break; // NORMAL (lighting deferred)`), and `NsbmdGlControl`'s doc comment still says "Normals/lighting are a later slice." Models render with flat per-material/vertex colour only; there is no real-time lighting yet.
>
> **What the old note framed as future work — "wire it into real editors" — is now done, well past slice 4's original scope**, and is the renderer's main growth since the last refresh of this doc:
> - **Map Editor** (`MapEditorViewModel`) has three view modes — single map, this-header (stitched), and full-matrix fly-around (render-only) — switches textured-geometry rendering via `NsbmdGlControl`, paints the two 32×32 movement-permission grids (`PermissionGridControl`, with a live GL tint overlay), and supports **moving buildings with a translate gizmo** (drag or arrow-key nudge) alongside add/remove. Building placement-by-picking (adding a *new* building by clicking a 3D location) and tileset texture binding for the preview are still the deferred parts.
> - **Building Editor** (`BuildingEditorView`) and **Event Editor** (`EventEditorView`) both drive the same gizmo/pick machinery via the shared `Gl3DPointerNavigation` helper (`Avalonia/Gl/Gl3DPointerNavigation.cs`), which centralises mouse-drag camera + pick/gizmo wiring so button mapping stays consistent across every 3D viewport (Map/Event/Building/Headbutt). The Event Editor additionally renders **event markers** as an overlay mesh (`NsbmdGlControl.SetMarkers`), supports **click-to-pick the nearest event**, and drags/nudges the selected event's position with the same gizmo — none of that was true when the old note called markers/click-place "deferred" (that class's own doc comment is stale on this point; the actual code, wired in `EventEditorView.axaml.cs`, has all three).
> - **Matrix Editor** itself stays a 2-D grid tool (`MatrixGridControl`, no GL view) for editing the map/header/height grids directly; the **3-D "whole matrix" view lives in the Map Editor's full-matrix mode**, built by `MatrixSceneBuilder` (`Avalonia/Gl/MatrixSceneBuilder.cs`), which stitches every non-VOID map of a `GameMatrix` (resolving each cell's texture pack through the real per-cell header linkage) into one `NsbmdRenderModel` positioned by the matrix grid — shared by the Map Editor (full-matrix fly-around, or just "this header"'s own maps) and the Event Editor (show every map an event's file spans).
> - **Headbutt Editor** (`HeadbuttEncounterView`) now exists as its own editor with an on-map 3D tree-marker view (see the Encounters Editor note above for why it's not inside the `EncountersEditorViewModel` composite).
> - **NSBTX Editor** and **Matrix Editor** are ported (see the migration table) but don't themselves embed `NsbmdGlControl` — NSBTX edits texture data directly, and Matrix Editor is the 2-D grid tool described above.

#### ⏳ Still WinForms

Per-editor porting is essentially complete: every editor tracked in this doc's old "pending migration"
table (Map, Matrix, NSBTX, Event, Script, Level Script, Headbutt, Building Editor) now has a real
Avalonia port — see the migration table above. What's left is mostly one architectural item and a
handful of small, already-noted peripheral gaps, not whole editors:

| Item | Notes |
|--------|-------|
| **WinForms shell (`Main Window.cs` / `MainProgram`)** | Not retired. It's still the *default* process shell for the Windows exe (see the Main Window shell note above) and still owns its own copies of every legacy WinForms editor (`DS_Map/Editors/*.cs`, e.g. `Editors/BuildingEditor.cs`) — those stay reachable from the old shell's own tab strip/toolbar even though an Avalonia port of the same editor now exists and is reachable from `MainWindowView`. Retiring the WinForms shell (and deleting the legacy `Editors/` code it depends on) is the actual remaining "migration," not any single editor. |
| A few peripheral cross-editor gaps | Called out inline on the relevant editor: Header Editor's "create associated files" prompt on add-header and its open-wild/script/level-script/area-data navigation buttons (see the Header Editor note); Map Editor's building placement-by-picking and preview tileset texture binding, and the renderer's normals/lighting slice 4 (see the 3D renderer note). |

#### 🏗 Architecture decisions made

- **Main window approach:** Avalonia `MainWindowView` with a full menu bar over an embedded Maps-workspace tab strip (`MapsWorkspaceView`) as the primary surface; every other migrated editor opens from the menu, mostly as a standalone window (`UserControl` editors go through `EditorHostWindow`). The legacy WinForms `MainProgram` (`Main Window.cs`) is still the default shell for the Windows exe and hasn't been retired — see the Main Window shell note above for how the two coexist and how to reach each one.
- **Dialog helper:** `DSPRE.Avalonia.DialogHelper` static class wraps all Avalonia async dialogs (MessageBox, OpenFile, SaveFile). Use this everywhere instead of `System.Windows.Forms.MessageBox`.
- **Build warnings suppressed in csproj:** `CA1416` (Windows-only), `AVLN3001` (views without parameterless ctor), `NU1701` (transitional packages), `CS0649/414/169/168/197/109/8981`, `CA2200`, `GenerateResourceWarnOnBinaryFormatterUse=false`.
- **Obsolete:** `TextBox.Watermark` → use `PlaceholderText` instead. `CanUserAddRows`/`CanUserDeleteRows` don't exist on Avalonia DataGrid — use `IsReadOnly`.

---

## 9. How to Add a New Avalonia Editor

### Step 1 — Create the ViewModel

File: `DS_Map/Avalonia/ViewModels/MyEditorViewModel.cs`

```csharp
using Avalonia.Controls;
using DSPRE.Editors;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DSPRE.Avalonia.ViewModels
{
	public class MyEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges
	{
		public event PropertyChangedEventHandler PropertyChanged;
		private void OnPropertyChanged([CallerMemberName] string n = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
		private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
		{ if (EqualityComparer<T>.Default.Equals(f, v)) return false; f=v; OnPropertyChanged(n); return true; }

		// ── Design-time constructor ──────────────────────────────────────
		public MyEditorViewModel()
		{
			if (!Design.IsDesignMode) return;
			Items = new ObservableCollection<string> { "Sample A", "Sample B" };
		}

		// ── Runtime constructor ──────────────────────────────────────────
		public MyEditorViewModel(string[] items)
		{
			Items = new ObservableCollection<string>(items);
		}

		// ── Properties ──────────────────────────────────────────────────
		public ObservableCollection<string> Items { get; }

		private int _selectedIndex = -1;
		public int SelectedIndex
		{
			get => _selectedIndex;
			set { if (Set(ref _selectedIndex, value) && value >= 0) LoadItem(value); }
		}

		// ── IEditorWithUnsavedChanges ────────────────────────────────────
		private bool _dirty;
		public bool HasUnsavedChanges => _dirty;
		public string UnsavedChangesDescription => $"My Editor (item {_selectedIndex})";

		public void SaveChanges()   { /* write data */ SetClean(); }
		public void DiscardChanges(){ _dirty = false; OnPropertyChanged(nameof(HasUnsavedChanges)); }

		private void SetDirty()
		{ if (_dirty) return; _dirty = true; OnPropertyChanged(nameof(HasUnsavedChanges)); }
		private void SetClean()
		{ if (!_dirty) return; _dirty = false; OnPropertyChanged(nameof(HasUnsavedChanges)); }

		private void LoadItem(int id) { /* read from NARC */ }
	}
}
```

### Step 2 — Create the View

File: `DS_Map/Avalonia/Views/MyEditorView.axaml`

```xml
<Window xmlns="https://github.com/avaloniaui"
		xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
		xmlns:vm="clr-namespace:DSPRE.Avalonia.ViewModels"
		x:Class="DSPRE.Avalonia.Views.MyEditorView"
		x:DataType="vm:MyEditorViewModel"
		Title="{Binding Title}"
		Width="600" Height="400">

	<DockPanel>
		<!-- Your layout here -->
		<ListBox ItemsSource="{Binding Items}"
				 SelectedIndex="{Binding SelectedIndex}"/>
	</DockPanel>
</Window>
```

### Step 3 — Create the code-behind

File: `DS_Map/Avalonia/Views/MyEditorView.axaml.cs`

```csharp
using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views
{
	public partial class MyEditorView : Window
	{
		private MyEditorViewModel VM => (MyEditorViewModel)DataContext;

		public MyEditorView(MyEditorViewModel vm)
		{
			DataContext = vm;
			InitializeComponent();
		}

		// Warn before closing with unsaved changes
		protected override async void OnClosing(WindowClosingEventArgs e)
		{
			if (VM.HasUnsavedChanges)
			{
				e.Cancel = true;
				bool discard = await DialogHelper.AskYesNo(
					$"Discard changes to {VM.UnsavedChangesDescription}?", "Unsaved Changes");
				if (discard) { VM.DiscardChanges(); Close(); }
			}
			base.OnClosing(e);
		}
	}
}
```

### Step 4 — Wire into Main Window

In `Main Window.cs`, find the menu item or button for the editor and add:

```csharp
private void OpenMyEditor_Click(object sender, EventArgs e)
{
	// Build data from ROM
	var names = /* read from ROM */;

	var vm   = new MyEditorViewModel(names);
	var view = new MyEditorView(vm);
	view.Show();
}
```

### Step 5 — If it's a sub-editor (UserControl, not Window)

Use `UserControl` instead of `Window` in AXAML and code-behind, then embed it inside a parent window's tab:

```xml
<TabItem Header="My Sub-Editor">
	<views:MyEditorView DataContext="{Binding MySubVM}"/>
</TabItem>
```

The parent ViewModel owns `MySubVM`, constructs it, calls `LoadItem(id)` on it when selection changes, and aggregates `HasUnsavedChanges`.

---

## 10. Patterns & Conventions Reference

### Naming

| Thing | Convention | Example |
|-------|-----------|---------|
| AXAML view | `[Name]View.axaml` | `TMEditorView.axaml` |
| View code-behind | `[Name]View.axaml.cs` | `TMEditorView.axaml.cs` |
| ViewModel | `[Name]ViewModel.cs` | `TMEditorViewModel.cs` |
| Avalonia `Window` | Suffix `View` | `PokemonEditorView` |
| Avalonia `UserControl` | Suffix `View` | `PersonalDataEditorView` |
| ROM data model | No suffix | `PokemonPersonalData`, `EvolutionFile` |
| NARC directory | `DirNames` enum value | `DirNames.pokemonBattleSprites` |
| NARC entry files | Zero-padded 4-digit | `0000`, `0001`, `0042` |

### `Bitmap` type aliasing

When a file needs both GDI+ and Avalonia bitmaps, always alias them to avoid CS0104:

```csharp
using AvaBitmap = Avalonia.Media.Imaging.Bitmap;
using GdiBitmap = System.Drawing.Bitmap;
```

### Error handling

- Use `AppLogger` for all logging.
- Show user-visible errors with `DialogHelper.ShowError(...)` in Avalonia code.
- In WinForms legacy code, `MessageBox.Show(...)` is still acceptable.
- Never silently swallow exceptions that indicate data corruption.

### Extension methods

Key helpers in `Extensions.cs`:

```csharp
progressBar.SetProgressNoAnimation(value);
control.UIThread(() => { /* thread-safe UI update */ });
bitmap.Resize(width, height);
int.ToByteArrayChooseSize(1 | 2 | 4);   // returns byte[], ushort[], or int[]
```

---

## 11. Build & Dependencies

### Build

```powershell
# Full build
msbuild DS_Map.sln /p:Configuration=Release

# Or open DS_Map.sln in Visual Studio 2022+ and press F5/Ctrl+Shift+B
```

### NuGet packages (DS_Map/DSPRE.csproj)

Per the current `<PackageReference>` list in `DS_Map/DSPRE.csproj`. `DSPRE.Core.csproj` and
`DSPRE.Avalonia.csproj` reference an overlapping subset of these directly (they don't get packages
transitively from `DS_Map/DSPRE.csproj` — it's the other way around, `DS_Map` references *them*), so
the versions below are also the ones that matter for the cross-platform build.

| Package | Version | Purpose |
|---------|---------|---------|
| `Avalonia` | 12.1.0 | Core UI framework |
| `Avalonia.AvaloniaEdit` | 12.0.0 | Text-editor control (Script/Level Script editors) |
| `Avalonia.Controls.DataGrid` | 12.1.0 | DataGrid control |
| `Avalonia.Desktop` | 12.1.0 | Win/Linux/Mac desktop target |
| `Avalonia.Fonts.Inter` | 12.1.0 | Inter font |
| `Avalonia.Themes.Fluent` | 12.1.0 | Fluent dark/light theme |
| `Avalonia.Themes.Simple` | 12.1.0 | Simple theme (alternate to Fluent) |
| `AvaloniaEdit.TextMate` | 12.0.0 | TextMate grammar/theme support for AvaloniaEdit (real syntax highlighting in the Script Editor, e.g. `rotom.tmLanguage.json`) |
| `jacobslusser.ScintillaNET` | 3.6.3 | Script editor (WinForms-only legacy code; the Avalonia Script Editor uses AvaloniaEdit instead — see above) |
| `Microsoft.WindowsAPICodePack-Core` | 1.1.0.2 | Legacy Windows shell interop (bridge phase) |
| `Microsoft.WindowsAPICodePack-Shell` | 1.1.0 | Legacy file dialogs (bridge phase) |
| `Newtonsoft.Json` | 13.0.3 | Settings serialization |
| `System.Collections.Specialized` | 4.3.0 | Legacy collection types |
| `System.Text.Json` | 9.0.7 | JSON (de)serialization |
| `Velopack` | 0.0.1298 | Auto-updater |
| `LibGit2Sharp` | 0.31.0 | Script DB Git updates |
| `YamlDotNet` | 16.2.0 | YAML utilities |

`DSPRE.Avalonia.csproj` additionally pulls in `Avalonia.Labs.Gif` (12.0.2, animated GIF playback) and
`NAudio` (2.2.1, real sound-effect playback via `NAudioOutput`, no-op on non-Windows). `DSPRE.Core.csproj`
additionally pulls in `System.Drawing.Common` (8.0.0) and `System.Resources.Extensions` (8.0.0).

### Bundled tools (DS_Map/Tools/)

| Tool | Purpose |
|------|---------|
| `ndstool.exe` | NDS ROM pack/unpack |
| `blz.exe` | BLZ compression/decompression (ARM9, overlays) |
| `apicula.exe` | NSBMD → OBJ 3D model conversion |
| `chatot.exe` | Audio utilities |
| `dsrom.exe` | ROM header tools |
| `rotom.exe` / `rotom-lsp.exe` | Script-editor language server (`ScriptEditorView`'s AvaloniaEdit integration) |
| `nitroarc_ffi.dll` | Native NARC FFI helper |
| `pokefatcs.txt` / `charmap.json` | Data files (fat-cat/version table, DS text-encoding character map) |

### Project references

```
DS_Map/DSPRE.csproj
  └─ references: DSPRE.Core.csproj (cross-platform ROM core)
  └─ references: DSPRE.Avalonia.csproj (cross-platform Avalonia UI)
  └─ references: Ekona.csproj (sprite/image library)
  └─ references: Images.csproj (Nintendo format plugin)

DSPRE.Avalonia.csproj
  └─ references: DSPRE.Core.csproj

DSPRE.Core.csproj
  └─ references: Ekona.csproj
  └─ references: Images.csproj

DSPRE.Tests.csproj
  └─ references: DSPRE.Core.csproj, DSPRE.Avalonia.csproj
  └─ references: DS_Map/DSPRE.csproj (net8.0-windows target only)
```

---

## Appendix — Quick Look-Up

### `DirNames` enum → NARC content

```csharp
DirNames.personalPokeData       // Pokémon base stats
DirNames.pokemonBattleSprites   // Battle sprites + palettes
DirNames.textArchives           // In-game text banks
DirNames.matrices               // Map connection matrices
DirNames.maps                   // 3D map NSBMD models
DirNames.exteriorBuildingModels // Building NSBMD models
DirNames.buildingTextures       // Building NSBTX textures
DirNames.eventFiles             // NPCs, warps, triggers, spawnables
DirNames.OWSprites              // Overworld (field) sprites
DirNames.scripts                // Game scripts
DirNames.encounters             // Wild Pokémon encounters
DirNames.trainerProperties      // Trainer header data
DirNames.trainerParty           // Trainer party data
DirNames.moveData               // Move definitions
DirNames.learnsets              // Pokémon learnsets
DirNames.evolutions             // Evolution data
DirNames.itemData               // Item definitions
DirNames.eggMoves               // Egg move lists
DirNames.trades                 // In-game trade data
DirNames.otherPokemonBattleSprites // Alternate-form sprites
```

### `GameFamilies` quick reference

```csharp
GameFamilies.DP   // Diamond & Pearl
GameFamilies.Plat // Platinum
GameFamilies.HGSS // HeartGold & SoulSilver
GameFamilies.BW   // Black & White (unsupported — see note above)
GameFamilies.BW2  // Black 2 & White 2 (unsupported — see note above)
GameFamilies.NULL // No ROM loaded
```

### Related repositories

- **DSPRE source:** https://github.com/DS-Pokemon-Rom-Editor/DSPRE
- **Script command database:** https://github.com/DS-Pokemon-Rom-Editor/scrcmd-database
