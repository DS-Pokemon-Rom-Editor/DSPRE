[Research](../../ResearchNotes.md) / [Pokemon Research](../PokemonResearch.md) / Growth Curves Logic

# Growth Curves Logic, Diamond/Pearl, Platinum and HeartGold/SoulSilver

Source: the [pokeplatinum](https://github.com/pret/pokeplatinum), [pokeheartgold](https://github.com/pret/pokeheartgold) and [pokediamond](https://github.com/pret/pokediamond) decomps, cited by file and function, and the tables read from the US retail ROMs (Diamond v05, Platinum revision 1, HeartGold). This was structured into a document with AI.

A growth curve is a list of the total EXP a Pokémon needs to be each level. Every species names one curve by its growth rate, and the game works a Pokémon's level out from its EXP by walking that list. Editing a curve changes both how fast new Pokémon level and what level old ones are.

## The table

The curves are a NARC of eight members, one per growth rate, each 101 little-endian u32 totals for levels 0 to 100 (404 bytes). All three games measure exactly that, and the member is picked by the species' growth rate byte (`expRate`, byte `0x13` of the personal data, `include/struct_defs/species.h` in pokeplatinum).

| Member | Growth rate | Species using it (HeartGold, Platinum) | Diamond |
|---:|---|---:|---:|
| 0 | Medium Fast | 191 | 186 |
| 1 | Erratic | 22 | 22 |
| 2 | Fluctuating | 14 | 14 |
| 3 | Medium Slow | 125 | 124 |
| 4 | Fast | 46 | 46 |
| 5 | Slow | 110 | 109 |
| 6 | unused | 0 | 0 |
| 7 | unused | 0 | 0 |

The order and the two unused rates are pokeplatinum's `generated/exp_rates.txt` (`EXP_RATE_6_UNUSED`, `EXP_RATE_7_UNUSED`); the counts were measured from the personal archives. Members 6 and 7 are byte-for-byte copies of Medium Fast in all three games.

| Game | Archive |
|---|---|
| Diamond and Pearl | `poketool/personal/growtbl.narc` (pokediamond `arm9/src/filesystem.c`) |
| Platinum | `poketool/personal/pl_growtbl.narc` (pokeplatinum `src/pokemon.c`) |
| HeartGold and SoulSilver | `a/0/0/3` (`NARC_poketool_personal_growtbl` in pokeheartgold `include/filesystem_files_def.h`) |

Platinum still ships the Diamond-style `growtbl.narc` beside its own. No code reads it, and it is byte-identical to `pl_growtbl.narc`.

The loader asserts the rate is below 8 and, in HeartGold, copies the whole member into a 101-entry buffer, so a member longer than 404 bytes would overflow it.

## How the game uses it

The same code is in all three decomps (pokeplatinum `src/pokemon.c`, pokeheartgold `src/pokemon.c`, pokediamond `arm9/src/pokemon.c`).

**Level from EXP.** The game scans levels 1 to 100 and stops at the first total greater than the Pokémon's EXP; the level is one below that, or 100 if the EXP reaches the level 100 total. So:

- A Pokémon whose EXP is below the level 1 total comes out as level 0.
- Two equal totals make the lower level unreachable.
- A total lower than the one before stops the scan early, so the level the party stores and the level worked out again from EXP (when a Pokémon is deposited, or in the Day Care) disagree.

**Levelling up.** `Pokemon_ShouldLevelUp` in Platinum and `Pokemon_TryLevelUp` in HeartGold first clamp EXP to the level 100 total, then raise the level by one while it is below 100 and the EXP reaches the next total. Level 100 is the cap whatever the table says.

**Rare Candy** (pokeheartgold `use_item_on_mon.c`) is only allowed below 100. It adds the next level's total minus the current EXP, where the current level is worked out from EXP, then recalculates. With a list that does not keep rising this can lower EXP.

**Day Care** (pokeheartgold `get_egg.c`) adds EXP step by step, takes the new level from the scan, and charges (levels gained + 1) × 100, so a bad list can give a negative number of levels gained.

**Level 0.** The level 0 entry is there and is 0. No normal-play read of it was found; that is located, not traced through every caller.

Every creator of a new Pokémon sets its EXP to the total for its level, so a level 1 total above 0 only hurts Pokémon whose EXP is already below it, such as ones in an existing save or traded in.

### What an edit does to an existing save

These follow from the code above.

- Lowering the level 100 total clamps the EXP of existing Pokémon the next time they gain any.
- Raising the level 100 total makes a Pokémon stored at the old maximum EXP come out as level 99 wherever its level is worked out from EXP, such as in the box.
- Any change to a curve changes the level worked out for Pokémon already in a save.

## Formulas against the data

Every total for levels 2 to 100 matches the standard formulas, with levels 0 and 1 at 0: no mismatches in any of the six curves in any of the three games. HeartGold, Platinum's two archives and Diamond are byte-identical to each other. All divisions round down.

| Curve | Total EXP for level n | Level 2 | Level 100 |
|---|---|---:|---:|
| Medium Fast | n³ | 8 | 1,000,000 |
| Fast | 4n³ / 5 | 6 | 800,000 |
| Slow | 5n³ / 4 | 10 | 1,250,000 |
| Medium Slow | 6n³ / 5 − 15n² + 100n − 140 | 9 | 1,059,860 |
| Erratic | n ≤ 50: n³(100 − n) / 50; n ≤ 68: n³(150 − n) / 100; n ≤ 98: n³ ⌊(1911 − 10n) / 3⌋ / 500; above: n³(160 − n) / 100 | 15 | 600,000 |
| Fluctuating | n ≤ 15: n³(⌊(n + 1) / 3⌋ + 24) / 50; n ≤ 36: n³(n + 14) / 50; above: n³(⌊n / 2⌋ + 32) / 50 | 4 | 1,640,000 |

## What DSPRE does

The curves are edited in the Growth Curves editor (`GrowthCurveEditorViewModel`, through `GrowthTable` in `DSPRE.Core/ROMFiles`), under the Pokémon menu behind the beta gate, for all three games.

| What | Edited in DSPRE | Written |
|---|---|---|
| Totals | one curve at a time, a row per level with its total and the EXP to the next level | the curve's own member, in place, keeping the file's length |
| Level 1 | read-only at 0 | |

DSPRE refuses to save a curve whose level 1 total is not 0 or whose totals do not keep rising. That is a policy, stricter than the game: as described above, equal totals only skip a level and a level 1 total above 0 only affects Pokémon already below it. The editor warns when the level 100 total is lowered, and its ? tour says that any edit changes the levels of Pokémon already in a save and that raising the level 100 total turns old level 100s into 99s.
