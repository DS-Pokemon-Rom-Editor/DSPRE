# Extending Decomp Project Support in DSPRE

> **Scope**: Pokémon Platinum English (ROM ID `CPUE`) only.  
> All paths below are relative to the decomp repo root unless stated otherwise.

---

## BAseline Behavior

When DSPRE loads a decomp project it:

1. **Unpacks the built ROM** (`platinum.us.nds` being thedefault build target) into a temporary dsrom folder, exactly like a normal ROM open.
2. **Parses the xMAP** to find any symbols whose file offsets may differ from vanilla.
3. **Applies the parsed offsets** over the vanilla defaults that `RomInfo` would normally set.
4. **Enables only a subset of editors**, currently Header, Map, and Event, because the other editors depend on additional offsets or NARCs that are not yet wired up for decomp overrides.

All NARC paths are read verbatim from the extracted folder; a decomp build that moves a NARC to a different sub-path needs its new path communicated to DSPRE through `DecompProjectInfo`.  
All binary offsets (ARM9 or overlay) that a decomp build relocates likewise need to be exposed via the xMAP and parsed by `DecompProjectInfo.ParseXMAP()`.

---

## How Offsets and NARCs Are Registered

### `DecompProjectInfo.cs`

This is the only file you need to touch for most extensions.

```
DS_Map/DecompProjectInfo.cs
```

It contains:
- **Properties**: one per offset/path that can change.
- **`ParseXMAP()`**: reads the xmap and fills those properties from symbol names.
- **`ApplyToRomInfo()`**: writes the parsed values into the live `RomInfo` static state.

### `RomInfo.cs`

Contains the methods that set vanilla values:

| Method | What it sets |
|--------|-------------|
| `SetHeaderTableOffset()` | `RomInfo.headerTableOffset` byte offset in `arm9.bin` of the map-header array |
| `SetOWtable()` | `RomInfo.OWTableOffset` + `RomInfo.OWtablePath` byte offset in `overlay_0005.bin` of the OW sprite-config table |
| `SetupSpawnSettings()` | `RomInfo.arm9spawnOffset` byte offset in `arm9.bin` of the player-spawn record |
| `SetItemTableOffset()` | `RomInfo.itemTableOffset` byte offset in `arm9.bin` of the item-data table |
| `SetPickupTableOffsets()` | Several offsets in `overlay_0016.bin` for the Pickup ability loot tables |
| `PrepareCameraData()` | `RomInfo.cameraTblOverlayNumber` / `cameraTblOffsetsToRAMaddress` camera angle table in `overlay_0005.bin` |

Vanilla Platinum EN values for the two currently supported offsets:

| Offset | Vanilla value | Lives in |
|--------|---------------|----------|
| `headerTableOffset` | `0xE601C` | `arm9.bin` |
| `OWTableOffset` | `0x2BC34` | `overlay_0005.bin` |

---

## Currently Supported Editors and Their Requirements

### Header Editor
- **NARCs used**: `synthOverlay`, `textArchives`, `dynamicHeaders`
  - Platinum paths: `data/data/weather_sys.narc`, `data/msgdata/pl_msg.narc`, `data/debug/cb_edit/d_test.narc`
- **ARM9 offset**: `headerTableOffset` (`sMapHeaders`, `arm9.bin`)
  - xMAP symbol: `sMapHeaders`

### Map Editor
- **NARCs used**: `maps`, `exteriorBuildingModels`, `buildingConfigFiles`, `buildingTextures`, `mapTextures`, `areaData`, `matrices`
  - All under `data/fielddata/` standard decomp layout, no known relocations.
- **No binary offsets** beyond what the Header Editor needs.

### Event Editor
- **NARCs used**: `eventFiles`, `OWSprites`
  - `data/fielddata/eventdata/zone_event.narc`, `data/data/mmodel/mmodel.narc`
- **Overlay offset**: `OWTableOffset` in `overlay_0005.bin` (`sOvTable` / `gOvTable` / …)
  - xMAP symbol: `sOvTable` (or any of the aliases in `OW_TABLE_SYMBOLS`)

---

## Editors Not Yet Enabled

For each editor below, the table lists:
- The **DSPRE internal name** for every NARC it unpacks (enum value from `DirNames`).
- The **vanilla packed path** inside the extracted ROM (relative to `data/`).
- Any **binary offset** it reads from ARM9 or an overlay, the vanilla value, and the **xMAP symbol name** you need to look up in your decomp fork to provide an override.

---

### Script Editor (`DirNames.scripts`)

| Item | Detail |
|------|--------|
| NARC `scripts` | `data/fielddata/script/scr_seq.narc` |
| Binary offsets | None |

**How to enable**: No new offsets needed. The NARC path rarely changes in decomp forks. If your fork keeps `scr_seq.narc` at its vanilla location, enabling the Script Editor tab is purely a UI change in `ReadROMInitDataDecomp()`.

If your fork **moved** the NARC, add a `ScriptsNarcPath` property to `DecompProjectInfo`, look up the file in `ParseXMAP()` (or hard-code a relative path), and override `gameDirs[DirNames.scripts].packedDir` in `ApplyToRomInfo()`.

---

### Text Editor (`DirNames.textArchives`)

| Item | Detail |
|------|--------|
| NARC `textArchives` | `data/msgdata/pl_msg.narc` |
| Binary offsets | None |

**Already unpacked** by the Header Editor. If you enable the Text Editor tab the NARC is guaranteed to be present.

---

### Wild Encounter Editor (`DirNames.encounters`, `DirNames.encounterExtended`)

| Item | Detail |
|------|--------|
| NARC `encounters` | `data/fielddata/encountdata/pl_enc_data.narc` |
| NARC `encounterExtended` | `data/arc/encdata_ex.narc` (extended honey-tree data) |
| Binary offsets | None |

**How to enable**: No binary offsets; just re-enable the tab in `ReadROMInitDataDecomp()`. If your fork renamed the encounter NARC, add a `EncountersNarcPath` property and override it the same way as described for the Script Editor.

---

### Trainer Editor (`DirNames.trainerProperties`, `DirNames.trainerParty`, `DirNames.trainerGraphics`, `DirNames.trainerTextOffset`, `DirNames.trainerTextTable`)

| Item | Detail |
|------|--------|
| NARC `trainerProperties` | `data/poketool/trainer/trdata.narc` |
| NARC `trainerParty` | `data/poketool/trainer/trpoke.narc` |
| NARC `trainerGraphics` | `data/poketool/trgra/trfgra.narc` |
| NARC `trainerTextOffset` | `data/poketool/trmsg/trtblofs.narc` |
| NARC `trainerTextTable` | `data/poketool/trmsg/trtbl.narc` |
| Binary offsets | None |

---

### Pokémon Editor — Personal Data (`DirNames.personalPokeData`, `DirNames.pokemonBattleSprites`, etc.)

| Item | Detail |
|------|--------|
| NARC `personalPokeData` | `data/poketool/personal/pl_personal.narc` |
| NARC `pokemonBattleSprites` | `data/poketool/pokegra/pl_pokegra.narc` |
| NARC `otherPokemonBattleSprites` | `data/poketool/pokegra/pl_otherpoke.narc` |
| NARC `monIcons` | `data/poketool/icongra/pl_poke_icon.narc` |
| Binary offsets | None |

---

### Move Data Editor (`DirNames.moveData`)

| Item | Detail |
|------|--------|
| NARC `moveData` | `data/poketool/waza/pl_waza_tbl.narc` |
| Binary offsets | None |

---

### Item Editor (`DirNames.itemData`, `DirNames.itemIcons`)

| Item | Detail |
|------|--------|
| NARC `itemData` | `data/itemtool/itemdata/pl_item_data.narc` |
| NARC `itemIcons` | `data/itemtool/itemdata/item_icon.narc` |
| **ARM9 offset** `itemTableOffset` | Vanilla `0xF0CC4` in `arm9.bin`; array of item attribute structs |
| xMAP symbol to find | Look for the item-data pointer table symbol in your fork I think it is `sItemDataTable` or `gItemDataTable` |

**How to add**:
1. In `DecompProjectInfo`, add:
   ```csharp
   public uint ItemTableOffset { get; set; }
   ```
2. In `ParseXMAP()`, add a symbol match (e.g. `"sItemDataTable"`) and compute:
   ```
   ItemTableOffset = ramAddr - ARM9_RAM_BASE;  // 0x02000000
   ```
3. In `ApplyToRomInfo()`:
   ```csharp
   if (ItemTableOffset != 0)
	   RomInfo.itemTableOffset = ItemTableOffset;
   ```

---

### ROM Toolbox (`romToolboxToolStripButton`)

The Toolbox patches overlay and ARM9 bytes directly.  Offsets are hardcoded per-language in `ToolboxDB.cs` and each patch entry stores its own offset, so no `DecompProjectInfo` override is needed.  However, **applying any patch to a decomp build that has already relocated the patched region will corrupt it**, only use patches you have verified are still at their vanilla locations in your fork.

---

### Spawn Editor (reads ARM9 directly)

| Item | Detail |
|------|--------|
| **ARM9 offset** `arm9spawnOffset` | Vanilla `0xEA12C` in `arm9.bin`; the player's default spawn record (map header ID, X, Y, facing) |
| xMAP symbol to find | No idea, maybe `sInitialPlayerData`, `gPlayerSpawnData`, or similar ? |

**How to add**:
1. Add `public uint SpawnOffset { get; set; }` to `DecompProjectInfo`.
2. Parse symbol in `ParseXMAP()`:
   ```
   SpawnOffset = ramAddr - ARM9_RAM_BASE;
   ```
3. In `ApplyToRomInfo()`:
   ```csharp
   if (SpawnOffset != 0)
	   RomInfo.arm9spawnOffset = SpawnOffset;
   ```

---

### Camera Editor (overlay_0005)

| Item | Detail |
|------|--------|
| **Overlay offset** `cameraTblOffsetsToRAMaddress` | Vanilla offset `0x4E24` inside `overlay_0005.bin`; a pointer that leads to the camera-angle table |
| xMAP symbol to find | Maybe `sCameraTable` or `gCameraAngleData` ? |

Computing the file offset from a RAM address for an overlay:
```
fileOffset = ramAddr - OverlayUtils.OverlayTable.GetRAMAddress(5)
```

---

### Pickup Table Editor (overlay_0016)

| Item | Detail |
|------|--------|
| Overlay | `overlay_0016.bin` |
| Offsets set by | `SetPickupTableOffsets()` in `RomInfo.cs` |
| Vanilla EN values | `pickupCommonItemsOffset = 0x3352C`, `pickupRareItemsOffset = 0x33450`, `pickupActivationDivisorOffset = 0xC62A`, `pickupWeightTableOffset = 0x33968` |
| xMAP symbols to find | I beleive it is `sPickupItems`, `sPickupItems2`, and `sPickupWeights` but please verify|

---

## Step-by-Step: Adding a New Offset Override

Here is the complete recipe using `arm9spawnOffset` as an example.

### 1. Find the symbol in your decomp fork

Open your fork's xMAP file and search for the symbol that owns the data. For the spawn record that might look like:

```
0202EA12C  00000010  .data  sInitialPlayerData  (src/field/field_player.c.o)
```

The RAM address is `0202EA12C`; the file offset into `arm9.bin` is `RAM - 0x02000000 = 0xEA12C`.

If your decomp moved the struct, the RAM address will be different and you need to feed the new value to DSPRE.

### 2. Add a property to `DecompProjectInfo`

```csharp
// DS_Map/DecompProjectInfo.cs

/// <summary>
/// Byte offset inside arm9.bin of the player spawn record.
/// 0 = not found; vanilla 0xEA12C will be kept.
/// </summary>
public uint SpawnOffset { get; set; }
```

### 3. Parse the symbol in `ParseXMAP()`

Inside the per-line loop in `ParseXMAP()`, add:

```csharp
private const string SPAWN_SYMBOL = "sInitialPlayerData"; // adjust based on its name in xMap

	// inside the foreach loop:
	if (info.SpawnOffset == 0 && symbol == SPAWN_SYMBOL)
	{
		info.SpawnOffset = ramAddr - ARM9_RAM_BASE;
		AppLogger.Info($"[Decomp] xMAP: '{SPAWN_SYMBOL}' at RAM 0x{ramAddr:X8}");
	}
```

Add it to the early-exit condition so the parser stops once everything is found:

```csharp
if (headerTableRAM != null && owTableRAM != null && info.SpawnOffset != 0)
	break;
```

### 4. Apply the override in `ApplyToRomInfo()`

```csharp
// DS_Map/DecompProjectInfo.cs — ApplyToRomInfo()

if (SpawnOffset != 0)
{
	RomInfo.arm9spawnOffset = SpawnOffset;
	AppLogger.Info($"[Decomp] arm9spawnOffset overridden with 0x{SpawnOffset:X}");
}
else
{
	AppLogger.Info("[Decomp] arm9spawnOffset not overridden; vanilla value 0xEA12C kept.");
}
```

### 5. Enable the editor tab in `ReadROMInitDataDecomp()`

In `DS_Map/Main Window.cs`, inside `ReadROMInitDataDecomp()`, add the relevant tab to `allowedTabs`:

```csharp
TabPage[] allowedTabs =
{
	EditorPanels.headerEditorTabPage,
	EditorPanels.mapEditorTabPage,
	EditorPanels.eventEditorTabPage,
	EditorPanels.spawnEditorTabPage,   // Spawn Eidtor is not a tab but the logic is the same, just add it to the list of enabled editors
};
```

And enable any toolbar button or menu item that was left disabled:

```csharp
// spawn editor is currently gated behind the "Spawn Editor" menu item instead of a tab, so enable that.
// sonme editors may require both a tab and a menu item or toolbar button to be enabled.
spawnEditorToolStripMenuItem.Enabled = true;
```

---

## Quick Reference — All Vanilla Platinum EN (CPUE) Offsets

| DSPRE field | Vanilla value | Binary file | xMAP symbol (common names) |
|---|---|---|---|
| `headerTableOffset` | `0xE601C` | `arm9.bin` | `sMapHeaders` |
| `OWTableOffset` | `0x2BC34` | `overlay_0005.bin` | `sOvTable` / `gOvTable` / `sOverworldTable` |
| `arm9spawnOffset` | `0xEA12C` | `arm9.bin` | `sInitialPlayerData` / `gPlayerSpawn` |
| `itemTableOffset` | `0xF0CC4` | `arm9.bin` | `sItemDataTable` / `gItemDataTable` |
| `cameraTblOffsetsToRAMaddress[0]` | `0x4E24` | `overlay_0005.bin` | `sCameraTable` / `gCameraAngleData` |
| `pickupCommonItemsOffset` | `0x3352C` | `overlay_0016.bin` | `sPickupCommonItems` |
| `pickupRareItemsOffset` | `0x33450` | `overlay_0016.bin` | `sPickupRareItems` |
| `pickupActivationDivisorOffset` | `0xC62A` | `overlay_0016.bin` | (divisor constant search for the `%10` that handles the "division") |
| `pickupWeightTableOffset` | `0x33968` | `overlay_0016.bin` | `sPickupWeights` |

> **Converting overlay RAM into file offset**:  
> `fileOffset = ramAddr - OverlayUtils.OverlayTable.GetRAMAddress(overlayNumber)`  
> For overlay 5 (vanilla Platinum EN RAM base `0x0221A1C0`):  
> `0x2BC34 = 0x0221A1C0 + 0x2BC34` but check against your fork's overlay table.

---

## NARC Paths

Most forks **do not** move NARCs; the standard dsrom layout is used verbatim.  
If a NARC _is_ moved, add a `string XxxNarcPath` property to `DecompProjectInfo`, set it in `ParseXMAP()`, and in `ApplyToRomInfo()` write it into `gameDirs`:

```csharp
// Example — overriding the scripts NARC path
if (!string.IsNullOrEmpty(ScriptsNarcPath))
{
	var existing = RomInfo.gameDirs[DirNames.scripts];
	RomInfo.gameDirs[DirNames.scripts] = (ScriptsNarcPath, existing.unpackedDir);
}
```

The `unpackedDir` side stays the same (it is just a local temp folder); only `packedDir` needs updating.
