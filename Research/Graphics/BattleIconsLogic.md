[Research](../ResearchNotes.md) / Battle Icons Logic

# Battle menu icons and gauge status words

This covers the 32 by 16 labels used for move types, contest conditions, and move categories in
battle menus, plus the smaller status-condition words written onto a Pokemon's HP gauge. The menu
icons are part of the battle-object archive, not a separate type-icon archive. The gauge words are a
second system: their pixels live in a battle overlay and borrow their colours from the battle-object
archive. Member numbers and names come from pokeplatinum's `res/graphics/battle/sprites.order`, the
palette banks from pokeplatinum's `src/type_icon.c`, and the rest from the files read from the US
retail ROMs.

## Archive and members

`RomInfo.DirNames.battleObj` resolves to a different physical NARC by game family:

| game family | path below the project's `files` or `data` root | shared type-icon palette |
|---|---|---:|
| Diamond/Pearl | `battle\graphic\batt_obj.narc` | 38 |
| Platinum | `battle\graphic\pl_batt_obj.narc` | 74 |
| HeartGold/SoulSilver | `a\0\0\8` | 74 |

The member numbers are zero-based. `ScriptNarc` unpacks them as four-digit files, so member 38 is
`0038`, member 226 is `0226`, and so on. The icon drawings, the cell and the animation are
LZ-compressed in the archive; the palettes are not.

Every icon has its own NCGR drawing. The names are pokeplatinum's, under `type_icons/`:

| type | name | Diamond/Pearl | Platinum/HGSS | palette bank |
|---|---|---:|---:|---:|
| Normal | `normal` | 170 | 234 | 0 |
| Fighting | `fighting` | 161 | 225 | 0 |
| Flying | `flying` | 163 | 227 | 1 |
| Poison | `poison` | 171 | 235 | 1 |
| Ground | `ground` | 165 | 229 | 0 |
| Rock | `rock` | 173 | 237 | 0 |
| Bug | `bug` | 167 | 231 | 2 |
| Ghost | `ghost` | 164 | 228 | 1 |
| Steel | `steel` | 174 | 238 | 0 |
| ??? | `mystery` | 172 | 236 | 2 |
| Fire | `fire` | 162 | 226 | 0 |
| Water | `water` | 177 | 241 | 1 |
| Grass | `grass` | 169 | 233 | 2 |
| Electric | `electric` | 158 | 222 | 0 |
| Psychic | `psychic` | 159 | 223 | 1 |
| Ice | `ice` | 166 | 230 | 1 |
| Dragon | `dragon` | 157 | 221 | 2 |
| Dark | `dark` | 160 | 224 | 0 |

Five contest-condition drawings sit in the same run and use the same palette:

| condition | name | Diamond/Pearl | Platinum/HGSS | palette bank |
|---|---|---:|---:|---:|
| Cool | `cool` | 176 | 240 | 0 |
| Beauty | `beauty` | 155 | 219 | 1 |
| Cute | `cute` | 156 | 220 | 1 |
| Smart | `smart` | 168 | 232 | 2 |
| Tough | `tough` | 175 | 239 | 0 |

The last three drawings are move categories:

| category | name | Diamond/Pearl | Platinum/HGSS | palette bank |
|---|---|---:|---:|---:|
| Physical | `physical` | 180 | 244 | 0 |
| Status | `status` | 181 | 245 | 0 |
| Special | `special` | 182 | 246 | 1 |

That is the complete set: eighteen types, five contest conditions, and three move categories, for
twenty-six icon drawings in each game family. The palette banks are `sMoveTypeIconPaletteIndex` and
`sMoveCategoryIconPaletteIndex` in pokeplatinum `src/type_icon.c`; the Diamond column matches
pokediamond's table.

## The shared palette

All twenty-six drawings use `type_icons/shared.NCLR`. It contains at least three consecutive banks of
sixteen BGR555 colours. There is no palette per type. DSPRE reads each icon's bank from the game's own
tables in ARM9 (`BattleUiTables.ReadIconTables`) and falls back to fixed tables
(`BattleObjects.IconBank` and `BattleObjects.KindBank`) only when the ARM9 tables do not match the
archive.

For a 4bpp drawing, `GraphicAssets.ReadIndexed` calculates the first colour as `bank * 16` and exposes
only those sixteen entries to the painter. `NitroBgCodec.ReadPalette` finds the NCLR's `TTLP` block,
starts at `TTLP + 0x18`, and reads each little-endian colour as five red, five green, and five blue
bits. Palette index 0 is treated as transparent when the preview is flattened.

The sharing is important when editing. Changing a Fire colour changes bank 0 of the shared palette, so
it also changes every other type, condition, or category assigned to bank 0. Grass instead changes
bank 2. DSPRE writes the selected bank at `(bank * 16) * 2` bytes into the NCLR colour data and leaves
the other banks alone.

## The tile drawings

Each NCGR is a 32 by 16, 4bpp indexed image: 512 pixels stored in 256 bytes, or eight 8 by 8 tiles in a
four-by-two grid. Two pixel indices share each byte, with the even pixel in the low nibble and the odd
pixel in the high nibble.

The NCGR files do not record a useful width for these icons. DSPRE therefore recognizes every
type-icon member and supplies a width of 32 through `BattleObjects.WidthFor`. The generic NCGR reader
derives the height from the tile count. This special case matters: guessing the shape from eight tiles
alone can produce 16 by 32, cutting the word in half and stacking it.

The archive also contains one shared cell layout and animation for the icons:

| part | name | Diamond/Pearl | Platinum/HGSS |
|---|---|---:|---:|
| cell | `type_icons/cell.NCER` | 178 | 242 |
| animation | `misc/single_frame_anim.NANR` | 179 | 243 |

There is no separate NCER, NANR, or NSCR for each type. The Graphics Browser does not need the shared
NCER to preview or paint an individual icon: once the width is supplied, it reads the NCGR directly.

## Status-condition words in the battle overlay

The condition shown on an HP gauge is not one of the 32 by 16 type icons. The games keep six 24 by 8
strips in a decompressed battle overlay: a blank strip for no condition, followed by Paralysis,
Freeze, Sleep, Poison, and Burn. Badly poisoned has no separate picture; it uses Poison. Each strip is
three consecutive 8 by 8 tiles, or 96 bytes at 4bpp.

`BattleGaugeText` (`DSPRE.Core/ROMFiles/BattleGaugeText.cs`) addresses these strips from the start of
the overlay's gauge-picture block:

| condition | first tile | relative byte offset | byte length |
|---|---:|---:|---:|
| None, the blank strip | `0x26` (Diamond/Pearl `0x38`) | `0x4C0` (Diamond/Pearl `0x700`) | `0x60` |
| Paralysis | `0x29` | `0x520` | `0x60` |
| Freeze | `0x2C` | `0x580` | `0x60` |
| Sleep | `0x2F` | `0x5E0` | `0x60` |
| Poison, including badly poisoned | `0x32` | `0x640` | `0x60` |
| Burn | `0x35` | `0x6A0` | `0x60` |

These are raw tiled pixels, without an NCGR header. Each tile is 32 bytes; the left pixel of each
pair is the low nibble and the right pixel is the high nibble. The indices are already the palette
indices the gauge uses, rather than the three placeholders used by the number font.

The block is found by content instead of by a fixed overlay number or absolute address. Platinum's
number font is member 4 of the font archive and HeartGold/SoulSilver's is member 5. The reader takes
the first 16 bytes of number-font tile 24, the right half of `Lv`, and searches every decompressed
overlay for that sequence. In Platinum and HeartGold the matching half-tile is `0x49` tiles after the
start of the picture block, so the block base is `match - 0x49 * 0x20`. Diamond and Pearl keep the
gender mark and `Lv` as one row, so there the match is the second half of tile `0x3D`.

In the US retail ROMs that search finds the block here; the game itself holds no such constant:

| game | overlay | picture-block base | first status strip |
|---|---:|---:|---:|
| Platinum | 16 | `0x3456C` | blank at `0x34A2C`; conditions at `0x34A8C` through `0x34C0C` |
| HeartGold | 12 | `0x35E20` | blank at `0x362E0`; conditions at `0x36340` through `0x364C0` |

The colours come from bank 0 of `healthbox/primary.NCLR` in `battleObj`, member 71 in Platinum and
HeartGold/SoulSilver. `BattleGaugeTextRenderer` uses index 0 as transparency when it draws a strip on
its own. `BattleGaugeComposer` instead copies all three packed tiles into a clone of the selected
gauge NCGR and renders that gauge with the same palette and its NCER layout.

At the byte-format level these pictures are editable by replacing the appropriate three-tile strip
in the decompressed overlay without changing its length or location. The current Battle Screen UI,
however, only reads and previews that source. Its condition picker is explicitly preview-only,
`BattleGaugeText` has no write path, and **Paint this** opens the destination gauge NCGR in
`battleObj`, not the source tiles in the overlay. A dedicated status-word painter would therefore
need to write the changed 96 bytes back through `OverlayUtils.GetPath` and reset the cached gauge
text; the repository does not currently do that.

## How the Graphics Browser connects the pieces

The `battleObj` entry in `GraphicAssets.All` supplies four battle-specific hooks:

- `BattleObjects.ColoursFor` maps every icon drawing to the shared type-icon palette.
- `BattleObjects.ColourBankFor` selects one of its three sixteen-colour banks.
- `BattleObjects.WidthFor` supplies the otherwise-missing width of 32 pixels.
- `BattleObjects.NameOf` gives each member the name shown in the browser.

`BattleObjects.Units` groups members that belong to one picture and places every type icon under
**Battle icons**.

For display and painting, `GraphicAssets.ReadIndexed` reads the NCGR indices, straightens the 8 by 8
tile order into rows, selects the correct sixteen-colour bank, and returns the 32 by 16 indexed image.
Saving reverses that operation: the indices are tiled again, two are packed into each byte, and only
the selected NCGR member is replaced.

Changing a colour in the painter is a separate write. `GraphicPainterViewModel.Save` writes the NCGR
indices first and calls `GraphicAssets.WritePalette` only if a swatch changed. Replacing an icon from
an indexed PNG writes its palette numbers into the NCGR but does not import the PNG's RGB palette;
palette colours are changed through the painter's colour controls.
