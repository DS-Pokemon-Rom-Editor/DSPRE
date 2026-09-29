[Research](../../ResearchNotes.md) / [Field Research](../FieldResearch.md) / Encounter Slots Logic

# Encounter Slots Logic, Diamond/Pearl, Platinum and HeartGold/SoulSilver

Source: the [pokeplatinum](https://github.com/pret/pokeplatinum), [pokeheartgold](https://github.com/pret/pokeheartgold) and [pokediamond](https://github.com/pret/pokediamond) decomps, cited by file and function, and the wild files and overlays read from the US retail ROMs (Diamond v05, Platinum revision 1, HeartGold). This was structured into a document with AI.

A wild file lists the Pokémon for each way of meeting one; which of them appears is decided by a roll of 0 to 99 against fixed slot chances. The chances are not data: they are compiled into the code as compare instructions. What changes the list during play, by time of day, swarm or sound, is covered here too; the swarm table itself is on its own page, [Swarms Logic](SwarmsLogic.md).

## The slot chances

Every roll is 0 to 99 (`LCRandRange(100)` in Platinum, `LCRNG_RandMod(100)` in HeartGold, `LCRandom() / 0x290` in Diamond).

| Method | Diamond, Pearl, Platinum | HeartGold |
|---|---|---|
| Walking, 12 slots | 20, 20, 10, 10, 10, 10, 5, 5, 4, 4, 1, 1 | same |
| Surfing, 5 | 60, 30, 5, 4, 1 | same |
| Old Rod, 5 | 60, 30, 5, 4, 1 | 40, 30, 15, 10, 5 |
| Good Rod, 5 | 40, 40, 15, 4, 1 | 40, 30, 15, 10, 5 |
| Super Rod, 5 | 40, 40, 15, 4, 1 | 40, 30, 15, 10, 5 |
| Rock Smash, 2 | none | 80, 20 |
| Headbutt, 6 | none | 50, 15, 15, 10, 5, 5 |
| Honey tree, 6 | 40, 20, 20, 10, 5, 5 | none |
| Safari Zone, 10 | see [Special Encounters Logic](SpecialEncountersLogic.md) | equal, 10 each |

HeartGold uses one fishing table for all three rods: its rod selector takes the rod type and never reads it.

The selectors are `GetGroundEncounterSlot`, `GetWaterEncounterSlot` and `GetRodEncounterSlot` in pokeplatinum `src/overlay006/wild_encounters.c`; the `EncounterSlot_WildMonSlotRoll_*` functions in pokeheartgold `src/field/encounter_check.c`; and `ov06_0223C5AC` and its neighbours in pokediamond's overlay 6. The honey tree chances are `GetTreeEncounterSlot` in pokeplatinum `src/overlay005/honey_tree.c`, written differently and not edited by DSPRE.

## Where the chances live

Each selector compares the roll with the running totals, `cmp r0, #total`, most of them twice (once for the lower bound of a slot, once for the upper). Walking's eleventh slot is different: the roll is compared with 98 and the branch is `bne`, an equality test, and everything else falls to the twelfth slot.

| Game | Overlay | First compare (decompressed) |
|---|---|---|
| Diamond | 6 | `0x2F2A` |
| Platinum | 6 | `0x37E2` |
| HeartGold | 2 | `0x1B20` (walking), `0x1BB4`, `0x1BF8`, `0x1C30`, `0x1C54` |

Every compare byte in all three games is the total shown above, followed by the `cmp r0, #imm` opcode byte `0x28`, and the eleventh walking slot's branch is 98 followed by `0xD1` (`bne`). Giving that slot a width other than one needs the branch rewritten as `bcs` (`0xD2`), so it covers a range instead of one value.

The same selectors are reused elsewhere, so a change reaches them too:

- A Poké Radar encounter without a chain uses the walking selector (`CreateWildMon_FromRadarNoChain`).
- Magnet Pull and Static can override the roll on land, water, rods, Rock Smash and Headbutt. In Diamond, Pearl and Platinum, on water, Static's check overwrites Magnet Pull's (commented as a bug in pokeplatinum).
- Hustle, Vital Spirit and Pressure can move a walking roll to a higher level slot (`TryFindHigherLevelSlot` in Platinum, `ApplyAbilityEffectToSlotLevel` in HeartGold).

## The wild files

**Diamond, Pearl and Platinum: 424 bytes** (all 183 files in Diamond and in Platinum). The layout is `WildEncounters` in pokeplatinum `include/overlay006/wild_encounters.h`.

| Offset | Content |
|---|---|
| `0x00` | walking rate (u32), then 12 slots of s8 level, 3 bytes padding, u32 species |
| `0x64` | swarm species, 2 × u32 |
| `0x6C` | day species, 2 |
| `0x74` | night species, 2 |
| `0x7C` | Poké Radar species, 4 |
| `0x8C` | form rates, 5 × u32: only the first two are read, Shellos and Gastrodon, nonzero meaning the East Sea form |
| `0xA0` | Unown table: 0 for none, 1 to 8 for table 0 to 7 |
| `0xA4` | Game Boy Advance slot species: Ruby, Sapphire, Emerald, FireRed, LeafGreen, 2 each |
| `0xCC` | surfing rate, then 5 slots of s8 max level, s8 min level, 2 bytes padding, u32 species |
| `0xF8` | 44 bytes the code never reads; zero in every retail file |
| `0x124` | Old Rod, same layout as surfing |
| `0x150` | Good Rod |
| `0x17C` | Super Rod |

In water slots the maximum level comes first: in every retail water slot the first level is the larger. In retail, 115 Platinum files hold 100 in both form rates and 68 hold 0.

**HeartGold: 196 bytes** (all 142 files). The layout is `EncounterData` in pokeheartgold `include/wild_encounter.h`.

| Offset | Content |
|---|---|
| `0x00` | six u8 rates: walking, surfing, Rock Smash, Old Rod, Good Rod, Super Rod |
| `0x08` | 12 walking levels, u8, shared by all three times of day |
| `0x14` | morning species, 12 × u16 |
| `0x2C` | day species, 12 |
| `0x44` | night species, 12 |
| `0x5C` | Hoenn Sound species, 2 |
| `0x60` | Sinnoh Sound species, 2 |
| `0x64` | surfing, 5 slots of u8 min level, u8 max level, u16 species |
| `0x78` | Rock Smash, 2 slots |
| `0x80` | Old Rod, 5 slots |
| `0x94` | Good Rod |
| `0xA8` | Super Rod |
| `0xBC` | swarm species for walking, surfing, night fishing and fishing swarms, u16 each |

Here the minimum level comes first, the opposite of the other games.

## What changes the list during play

**Time of day.** In Diamond, Pearl and Platinum the walking slots are the morning list; during the day and twilight slots 2 and 3 are replaced by the two day species, and at night by the two night species (`WildEncounters_ReplaceTimedEncounters`). In HeartGold the whole walking list switches: morning uses the morning species, day and evening the day species, night and late night the night species, and the levels stay the same.

**Diamond, Pearl and Platinum walking replacements**, applied in this order (time, swarm, Trophy Garden, Game Boy Advance, Great Marsh):

| Replacement | Slots | When |
|---|---|---|
| Swarm | 0, 1 | see [Swarms Logic](SwarmsLogic.md) |
| Poké Radar | 4, 5, 10, 11 | only on a patch shaking hard |
| Trophy Garden | 6, 7 | after the National Pokédex |
| Game Boy Advance cartridge | 8, 9 | after the National Pokédex |
| Great Marsh daily | 6, 7 | during a Safari Game |

**HeartGold replacements.** With the Hoenn or the Sinnoh Sound on the radio, walking slots 2 and 3 take the first sound species and slots 4 and 5 the second, after the swarm. Night fishing replaces Good Rod slot 3 or Super Rod slot 1, before a fishing swarm.

The Diamond slot numbers for the Poké Radar and the Great Marsh are located from the call site, not traced.

## What DSPRE does

The wild files are edited in the Wild Pokémon Editor. The slot chances are edited in the Encounter Slot Odds editor (`EncounterSlotOddsView`, through `EncounterSlotOdds` in `DSPRE.Core/ROMFiles`), under the Pokémon menu behind the beta gate, for all three games.

| What | Edited in DSPRE | Written |
|---|---|---|
| Slot chances | each method's slots as percentages that add up to 100 | the compare bytes, at the sites in `RomInfo`, and the eleventh walking slot's branch, `bne` for a width of one and `bcs` otherwise |
| Wild files | species and levels per slot and time, in the Wild Pokémon Editor | the wild file |

The walking chances also move the Poké Radar, as the editor notes.
