[Research](../../ResearchNotes.md) / [Move Research](../MoveResearch.md) / Type Chart Logic

# Type Chart Logic, Diamond/Pearl, Platinum and HeartGold/SoulSilver

Source: the [pokeplatinum](https://github.com/pret/pokeplatinum), [pokeheartgold](https://github.com/pret/pokeheartgold) and [pokediamond](https://github.com/pret/pokediamond) decomps, cited by file and function. This was structured into a document with AI.

The type chart is one table of three byte records compiled into the battle overlay, and it is byte for byte the same in all three games. What makes it interesting to edit is not the table but the handful of places that read it, each of which treats it a little differently, and the fixed size it sits in.

## The table

Each record is attacker type, defender type and the multiplier times ten: 0 immune, 5 half, 20 double. A pair that is not listed is neutral. Two sentinels live in the first column: `0xFE`, which divides the chart into the matchups Foresight and Scrappy can remove and the ones they cannot, and `0xFF`, which ends it (`TYPE_FORESIGHT` and `TYPE_ENDTABLE` in HeartGold `include/constants/pokemon.h`).

Retail has 112 records: 108 matchups, then `FE FE 00`, then Normal against Ghost and Fighting against Ghost at 0, then `FF FF 00`. That is 57 halves, 46 doubles and 7 immunities, with no spare records. The type numbers are Normal 0, Fighting 1, Flying 2, Poison 3, Ground 4, Rock 5, Bug 6, Ghost 7, Steel 8, ??? 9, Fire 10, Water 11, Grass 12, Electric 13, Psychic 14, Ice 15, Dragon 16, Dark 17; ??? has no rows.

| Game | Overlay | File offset | RAM |
|---|---|---|---|
| Diamond | 11 | `0x30DB8` | `0x0225E378` |
| Platinum | 16 | `0x33B94` | `0x0226ECD4` |
| HeartGold | 12 | `0x353BC` | `0x0226CC7C` |

Offsets are in the decompressed overlay of the US release; HeartGold's overlay 12 is compressed on the cartridge. The table is `sTypeMatchupMultipliers` in Platinum's `src/battle/battle_lib.c` and `sTypeEffectiveness` in HeartGold's `src/battle/overlay_12_0224E4FC.c`.

## How the battle reads it

Platinum's four readers, all in `src/battle/battle_lib.c` (HeartGold's are the same code in `overlay_12_0224E4FC.c`):

**Damage, `BattleSystem_ApplyTypeChart`.** Walks the table until the first column is `0xFF`. At the `0xFE` row it stops early if the defender is under Foresight or the attacker has Scrappy, which drops the Ghost immunities after it; otherwise it steps over the separator. Each matching row is applied through `ApplyTypeMultiplier` for each of the defender's types, the second only if it differs from the first. Before a row applies, `BasicTypeMulApplies` can veto it: an Iron Ball, Ingrain or Gravity cancels a Flying immunity, Roost cancels every row whose defender is Flying, and Miracle Eye cancels a Dark immunity. Levitate and Magnet Rise are handled before the walk, and Struggle skips it.

**Effectiveness, `BattleSystem_CalcEffectiveness`.** The same walk for the AI and for predicting effectiveness, stopping at `0xFE` only for Scrappy.

**Stealth Rock and switching, `BattleSystem_TypeMatchupMultiplier`.** Starts from 40, walks the whole table with no separator handling, so the Ghost immunities always count, and returns the product. The Stealth Rock damage in `battle_script.c` switches on exactly 160, 80, 40, 20, 10 or 0; any other result, from a Rock row with a custom multiplier, lands on an assertion and divides by a stale value.

**Conversion 2, `BattleSystem_TypeMatchup`.** Picks records at random, up to 1000 tries, then falls back to a linear scan, and accepts a record whose attacker matches and whose multiplier is 5 or less. This is the only reader that uses the table's size rather than its terminator: the count is compiled in as an immediate, compared and used as the modulus.

Only 0, 5 and 20 set the flags the rest of the battle keys off. `ApplyTypeMultiplier` sets "super effective", "not very effective" and "no effect" for exactly those values, and Wonder Guard, Filter, Solid Rock, Expert Belt and Tinted Lens all read those flags. A custom multiplier such as 15 changes the damage but shows no message and triggers none of those effects.

## Changing it by hand

**One matchup.** Fire against Grass is record 4, `0A 0C 14`. To make it half, change the third byte to `05`: Platinum overlay 16 `0x33BA2`, HeartGold overlay 12 `0x353CA`, Diamond overlay 11 `0x30DC6`.

Diamond and Platinum keep a second copy for the Pokétch's move tester, an 18 by 18 grid of signed bytes, 1 for double, -1 for half, -10 for immune and 0 for neutral (`sMoveTesterTypeChart`, Platinum `src/applications/poketch/move_tester/main.c`). Platinum's is in overlay 43 at `0x8F0`, Diamond's in overlay 38 at `0x8F4`, and the cell for attacker `a` and defender `d` is at `18 * a + d` into it; for Fire against Grass that is `0x9B0` in Platinum, changed from `01` to `FF`. HeartGold has no Pokétch and no copy.

**Removing one.** Shift the later records up and fill the freed record with `FF FF 00`. Overwriting it with a neutral `xx yy 0A` also works, since the game multiplies by 10 and divides by 10.

**Adding one.** The table cannot grow where it is: the next object is the move side effect subscript table (`sSideEffectSubscripts` in Platinum), and writing past the end corrupts it. Adding Bug against Ice at double, for example, means a 113 record, 339 byte table of records 0 to 107, `06 0F 14`, then `FE FE 00 00 07 00 01 07 00 FF FF 00`, placed somewhere free and pointed at:

1. Rewrite the four literal words holding the table's address, the three holding address plus 1 and the three holding address plus 2. In Platinum overlay 16 they are at `0x1A18C`, `0x1A2B4`, `0x1A780` and `0x1A7D4`; `0x19E60`, `0x1A300` and `0x1A784`; and `0x19E64`, `0x1A304` and `0x1A788`. HeartGold overlay 12 has them at `0x1A78C`, `0x1A8B4`, `0x1AD80`, `0x1ADD4`; `0x1A460`, `0x1A900`, `0x1AD84`; `0x1A464`, `0x1A904`, `0x1AD88`. Diamond overlay 11 at `0x18FD8`, `0x19100`, `0x195D0`, `0x19624`; `0x18CAC`, `0x1914C`, `0x195D4`; `0x18CB0`, `0x19150`, `0x195D8`.
2. Set the record count, separator and terminator included, in the `cmp` at Platinum `0x1A754`, HeartGold `0x1AD54` or Diamond `0x195A4` (`70 29` or `70 2E` retail), and in the `movs r1` eight bytes after it (`70 21`), which is the modulus Conversion 2 uses.

Both counts are 8 bit immediates, so a table can have at most 255 records. Every reader but Conversion 2 stops at the terminator, so a count that is too small only hides rows from Conversion 2's random picks, and any padding up to the count has to be terminator records.

## Adding a type

The table takes any type number but `0xFE` and `0xFF`. Everything around it is sized for eighteen:

- The type icon tables hold 23 entries, eighteen types and then the five contest conditions, so a nineteenth type draws the Cool icon and types 19 to 22 draw Beauty, Cute, Smart and Tough (`src/type_icon.c`). See [Type Icons](../../Graphics/TypeIconsLogic.md).
- The move button colours in battle are a table of eighteen palettes, and a nineteenth type reads past it (`sMovePaletteTable` in Platinum `src/overlay011/move_palettes.c`).
- The Pokédex's type icon lookup has no default case (`PokedexGraphics_GetAnimIDfromType` in Platinum's Pokédex `infomain.c`).
- The Pokétch grid is fixed at 18 by 18.
- Hidden Power's type formula skips ??? and assumes eighteen types.

hg-engine reuses slot 9 for Fairy and keeps its own chart elsewhere; DSPRE does not treat that case specially.

## What DSPRE does

The chart is edited in the Type Chart editor (`TypeChartEditorViewModel`, through `TypeChart` in `DSPRE.Core/ROMFiles`), behind the beta gate, for US Diamond, Platinum revision 1 and HeartGold.

| What | Edited in DSPRE | Written | Left untouched |
|---|---|---|---|
| Matchups | a grid of every type pair: click to cycle neutral, double, half and immune, or type any multiplier up to 25.5; a custom multiplier warns that Stealth Rock only handles 0, 1/2, 1, 2 and 4 times and that the effectiveness abilities and items treat it as neutral | the table, in place wherever it lives, found through its ten address words | |
| Foresight rows | a flag on immune rows | the rows after the `FE FE 00` separator | |
| Pokétch copy, Diamond and Platinum | follows the chart | the move tester grid in overlay 43 or 38 | |
| Room for more rows | "Make room" (`TypeChart.MoveToExpansion`) | a copy of the chart in the ARM9 expansion with room for 255 records, the ten address words, the record count compare and the `movs r1` modulus after it | the original copy in the overlay |
| Type names | shown from the game's text | | |

The chart is read as the game reads it, stopping at the first record whose attacker byte is `0xFF` and treating one whose attacker byte is `0xFE` as the separator. A chart is saved with terminator records up to its capacity, so its size in the ROM never changes; one that would need more room than it has is refused until it is moved. The compare and the modulus sites are in `RomInfo.TypeChartSites`. A chart that an older DSPRE moved without the modulus has it set to match the compare the next time it is saved.
