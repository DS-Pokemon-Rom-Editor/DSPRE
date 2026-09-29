[Research](../../ResearchNotes.md) / [Pokemon Research](../PokemonResearch.md) / Breeding Logic

# Breeding Logic, Diamond/Pearl, Platinum and HeartGold/SoulSilver

Source: the [pokeplatinum](https://github.com/pret/pokeplatinum), [pokeheartgold](https://github.com/pret/pokeheartgold) and [pokediamond](https://github.com/pret/pokediamond) decomps, cited by file and function, and the tables read from the US retail ROMs. This was structured into a document with AI.

Most of the Day Care's rules are code; one small table, the incense babies, and one archive, the egg moves, are data. This page covers both and where each of the other rules sits.

## Incense babies

Nine rows of u16 baby, u16 item, u16 fallback, the same in all three games:

| Baby | Item a parent must hold | Otherwise hatches as |
|---|---|---|
| Wynaut | Lax Incense | Wobbuffet |
| Azurill | Sea Incense | Marill |
| Mime Jr. | Odd Incense | Mr. Mime |
| Bonsly | Rock Incense | Sudowoodo |
| Munchlax | Full Incense | Snorlax |
| Mantyke | Wave Incense | Mantine |
| Budew | Rose Incense | Roselia |
| Happiny | Luck Incense | Chansey |
| Chingling | Pure Incense | Chimecho |

The first row whose baby is the egg's species decides; if neither parent holds its item the fallback hatches instead (`Daycare_AlterEggSpeciesWithIncenseItem` in pokeplatinum `src/overlay005/daycare.c`, `Daycare_BreedingIncenseCheck` in pokeheartgold `src/get_egg.c`, `ov05_021ECD78` in pokediamond). A row with item 0 hatches the baby whenever a parent holds nothing. The row count is compiled in (Diamond has `cmp r3, #9` twice), and the table is reached through three literals, at its start, `+2` and `+4`:

| Game | Table address | Literals |
|---|---|---|
| Diamond | `0x021F7B12` | overlay 5 `0x15908`, `0x1590C`, `0x15910` |
| Platinum | `0x021F9F6C` | overlay 5 `0x15E44`, `0x15E48`, `0x15E4C` |
| HeartGold | `0x020FF4AE` | ARM9 `0x6C78C`, `0x6C790`, `0x6C794` |

## The other rules

| Rule | Diamond, Pearl | Platinum | HeartGold, SoulSilver |
|---|---|---|---|
| Everstone | only the mother's, or Ditto's, counts: a 50% chance to pass her nature | same | either parent's; if both hold one, one is picked at random; 50% |
| Power items | none | none | one IV comes from the holder's matching stat (Power Weight HP, Bracer Attack, Belt Defense, Anklet Speed, Lens Special Attack, Band Special Defense); if both hold one, one is picked |
| IVs | three different stats, each from a random parent | same | same, after the Power item's stat |
| Egg moves | a table in overlay 5, each species' list marked by species + 20,000, at most 16 moves | same | `a/2/2/9`, the same format |
| Moves the egg knows | the father's egg moves, the father's TMs the baby can learn, and the level-up moves both parents know | same | same |
| Egg species | the mother's, or the non-Ditto parent's, baby form from the personal data; Nidoran and Volbeat or Illumise split by personality value, Manaphy lays Phione, Pichu learns Volt Tackle when a parent holds a Light Ball | same | same |
| Compatibility | an Undiscovered parent or two Dittos cannot breed; with Ditto the chance is low or medium; otherwise opposite genders and a shared egg group, highest for the same species from different trainers | same | same |
| Egg cycles | Flame Body or Magma Armor in the party counts two per step | same | same |

The Everstone rules are `Daycare_GetParentToInheritNature` in Platinum and `Daycare_EverstoneCheck` in HeartGold; the Power items `Daycare_TryGetForcedInheritedIV`; the egg moves `LoadSpeciesEggMoves` and `LoadEggMoves`; the moves the egg knows `InheritMoves`; compatibility `BoxMon_GetPairDaycareCompatibilityScore`.

## What DSPRE does

The incense table is edited in the Breeding Items editor (`BreedingItemsView`, through `IncenseBreedingTable` in `DSPRE.Core/ROMFiles`), under the Pokémon menu behind the beta gate, for all three games: each row's baby, item and fallback, written in place, with the row count fixed. Egg moves are edited in the Egg Move editor (`EggMoveEditorViewModel`, through `EggMoveData`).
