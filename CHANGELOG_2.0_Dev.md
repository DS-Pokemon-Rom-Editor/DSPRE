# DSPRE 2.0 — Developer / Contributor Changelog

Changes to architecture, internal systems, and patterns that matter if you're working on the codebase. For user-facing changes see [likn to user one].

---

## New Systems

### `IEditorWithUnsavedChanges` + `OpenEditorsRegistry`
Dirty tracking interface for all tab-based editors. See `IEditorWithUnsavedChanges.cs` and `OpenEditorsRegistry.cs`.

Implement on any editor that saves data:
```csharp
public class MyEditor : UserControl, IEditorWithUnsavedChanges
{
	public bool HasUnsavedChanges => isDirty;
	public string UnsavedChangesMessage => $"My Editor (File {currentFileID})";
}
```

**Always check both guards in event handlers before calling `SetDirty()`:**
```csharp
if (Helpers.HandlersDisabled || myList.SelectedIndex < 0) return;
```
Without the index check, setup code can fire handlers after `EnableHandlers()` and mark the editor dirty on first load.

**Editors still missing this** (they have a `Reset()` but no `IEditorWithUnsavedChanges`):
- `HeaderEditor.cs`: has `Reset()`, no interface
- `BuildingEditor.cs`: has `Reset()`, no interface
- `MapEditor.cs`: no dirty tracking at all
- `NsbtxEditor.cs`: no dirty tracking at all

### `UnsavedChangesDialog`
Reusable dialog for save/discard/cancel prompts. Used in main ROM switch flow and individual editors. Lives in `Editors/UnsavedChangesDialog.cs`.

### `YamlUtils`
Thin wrapper over YamlDotNet for reading ds-rom YAML files (header, overlays, arm9 config). Lives in `DSUtils/YamlUtils.cs`.

---

## Infrastructure

### ds-rom replaces ndstool for pack/unpack
- `DSUtils.cs`: ROM extraction and repacking now shells out to `dsrom.exe`
- `OverlayUtils.cs`: Updated for the new `arm9_overlays/` path and `overlays.yaml` table
- Overlays come out decompressed; overlay compression in `OverlayEditor` is currently disabled pending stability work
- See `CHANGELOG_2.0_USER.md` for the full folder layout comparison

### chatot replaces internal text encoding
- `TextConverter.cs`: All encode/decode now delegates to `chatot.exe`
- `CharMapManager.cs` + `CharMapManagerForm.cs`: Updated for JSON charmap format
- `SettingsManager.cs`: Updated for new charmap path
- `AppLogger.cs`: Logging adjustments from chatot PR
- The chatot JSON format supports language keys; `TextConverter` selects the right key based on `RomInfo.gameLanguage`

---

## New Files (code only)

| File | What it is |
|------|-----------|
| `DSUtils/YamlUtils.cs` | YAML parsing for ds-rom outputs |
| `Editors/IEditorWithUnsavedChanges.cs` | Dirty tracking interface |
| `Editors/OpenEditorsRegistry.cs` | Tracks open standalone editor Forms |
| `Editors/UnsavedChangesDialog.cs` | Save/discard/cancel dialog |
| `Editors/StrVarHelpForm.cs` | Help dialog for STRVAR system (from Discord pin) |
| `Editors/LearnsetLimitWarningForm.cs` | Warning dialog for learnset size limit |
| `Editors/BugContestEncounterEditor.cs` | Bug Contest encounter editor |
| `Editors/GreatMarshEncounterEditor.cs` | Great Marsh encounter editor |
| `Editors/HiddenItemsEditor.cs` | Hidden items editor (HGSS) |
| `Editors/HoneyTreeEncounterEditor.cs` | Honey Tree encounter editor |
| `Editors/ItemTableEditor.cs` / `ItemTableEditorForm.cs` | Container for Pickup + Hidden Items tabs |
| `Editors/PickupTableEditor.cs` | Pickup table editor |
| `Editors/TrainerEditor/TrainerSearch.cs` | Trainer search dialog |
| `Editors/TrainerEditor/BattleMessage/BattleMessageEditor.cs` | Trainer battle message editor |
| `Editors/TrainerEditor/BattleMessage/DSTextBox.cs` | Textbox control with Pokémon DS font rendering |
| `ROMFiles/BugContestEncounterFile.cs` | Bug Contest data model |
| `ROMFiles/GreatMarshEncounterFile.cs` | Great Marsh data model |
| `ROMFiles/HoneyTreeEncounterFile.cs` | Honey Tree data model |
| `ResearchHelper.cs` | Research Helper tool |
| `ScreenshotTool.cs` | Debug screenshot capture (DEBUG builds only) |
| `Tools/chatot` / `chatot.exe` | Text encoding tool (Linux + Windows) |
| `Tools/dsrom` / `dsrom.exe` | ROM pack/unpack tool (Linux + Windows) |
| `Tools/charmap.json` | Default character map for chatot |

---

## Modified Systems

### `RomInfo.cs`
- Pickup table and Honey Tree offsets moved here from editor code
- Trainer Battle Message offsets added (HGSS)
- ARM9 expansion support for JP HGSS (#156)
- Additional language entries added

### `Filesystem.cs`
- Added path accessors for Honey Tree and Bug Contest NARC directories

### `DocTool.cs`
- `ExportAll` split into `ExportCsv` and `ExportDexExports` 
- New exporters: Spawnables, Overworld OW sprite IDs, wild held items, map headers, scripts, learnsets (valid CSV), egg moves
- Export destination is now user-selectable

### `ROMFiles/EventFile.cs`
- Added variable/flag watchers for Research Helper integration
- Added level script references

### `ROMFiles/ScriptFile.cs`
- Export support for scripts (used by DocTool and Research Helper)
- Removed a debug log statement that was destroying performance on every script load (#123)

### `ROMFiles/TextArchive.cs`
- Rebuild pipeline updated for chatot
- Fixed path handling (#130)
- Text conversion happens before ROM pack (#125)

### `ROMFiles/MoveData.cs`
- Fixed move effect index to match actual game data (#96)

### `ROMFiles/LevelScriptTrigger.cs` / `VariableValueTrigger.cs`
- Fixed hex value storage and display (#134)

### `Resources/ScriptDatabase.cs`
- Gender symbol replacements (♂/♀ now use prefixed escape form)
- String replacement fixes

### `Resources/ROMToolboxDB/ToolboxDB.cs`
- JP HGSS ARM9 expansion entry added

### `FlyEditor/Data/FlyTableRowHgss.cs`
- Fixed reading offset when decomp names are active (#9)

### `PatchToolboxDialog.cs`
- ARM9 expansion compatibility check improved (#153)
- BDHCam/ScrCmd repoint compat handling fixed (#154)
- Patch flags reset on ROM load

### `Editors/TrainerEditor/DVCalculator/DVCalc.cs`
- Fixed gender modifier calculation when Platinum AI Backport is active
- Added check specifically for Lhea's backport patch

---

## Dependencies

- **Added: YamlDotNet**: used by `YamlUtils` for ds-rom YAML files
- **Added: `pokemon-ds-font.ttf`**: used by `DSTextBox` in Battle Message Editor
- CI workflows updated to build ds-rom from fork and include it in releases

---

## CI / Workflows

- `.github/workflows/base-build-nightly.yaml` and `beta-build-nightly.yml`: Both updated to build ds-rom from fork and bundle it in the release artifact (#163)

---

## Patterns / Conventions

### Editor Reset on ROM switch
Any editor that holds state from the current ROM must implement `Reset()` and be called from the main ROM switch flow. Wrap in `DisableHandlers()`/`EnableHandlers()`. Missing from: Map Editor, NSBTX Editor.

### Dirty tracking guards (mandatory)
```csharp
private void control_ValueChanged(object sender, EventArgs e)
{
	if (Helpers.HandlersDisabled || listBox.SelectedIndex < 0) return;
	SetDirty();
}
```
Both conditions are required. The index check prevents false-dirty on initial load.

### chatot invocation
All text encode/decode goes through `TextConverter`, which shells out to `chatot.exe`. Do not add new character mapping logic elsewhere. If you need to test chatot output, check `Tools/chatot.exe --help`.

### ds-rom paths
Use `OverlayUtils.GetPath(n)` for overlays, it handles both old and new layouts. Don't hardcode `overlay/overlay_XXXX.bin` anywhere new.
