[Research](../../ResearchNotes.md) / [Pokemon Research](../PokemonResearch.md) / Friendship Logic

# Friendship Logic, Diamond/Pearl, Platinum and HeartGold/SoulSilver

Source: the [pokeplatinum](https://github.com/pret/pokeplatinum), [pokeheartgold](https://github.com/pret/pokeheartgold) and [pokediamond](https://github.com/pret/pokediamond) decomps, cited by file and function, and the table read from the US retail ROMs. This was structured into a document with AI.

Most friendship changes come from one small table: ten events, each with a change for three bands of friendship. The table decides the base change; a few bonuses on top of it are applied in code, in a signed byte, which is where editing it can go wrong.

## The table

A table of 10 × 3 signed bytes in the ARM9, identical in all three games: `sFriendshipChangeTable` in pokeplatinum `src/pokemon.c`, `sFriendshipModTable` in pokeheartgold `src/pokemon.c`, and the same table in pokediamond `arm9/src/pokemon.c`. The columns are friendship below 100, 100 to 199, and 200 or more.

| Game | ARM9 offset (decompressed, US) |
|---|---|
| Diamond | `0xF7ED4` |
| Platinum | `0xF05A0` |
| HeartGold | `0xFF524` |

| Row | Event | Below 100 | 100 to 199 | 200 up | Who applies it |
|---:|---|---:|---:|---:|---|
| 0 | Level up | 5 | 3 | 2 | battle level up, in all three; in HeartGold also a Pokémon coming back from the Pokéwalker |
| 1 | Vitamin | 5 | 3 | 2 | nothing: items use the friendship values in their own item data |
| 2 | Battle item | 1 | 1 | 0 | nothing, as row 1 |
| 3 | Gym Leader, Elite Four or Champion battle | 3 | 2 | 1 | a trainer battle against a fixed list of classes (`TrainerIsGymLeaderE4OrChampion` in pokeplatinum `battle_main.c`) |
| 4 | Learning a TM or HM | 1 | 1 | 0 | the party menu |
| 5 | Walking | 1 | 1 | 1 | every 128 steps, then skipped on a 50% roll (`field_control.c` in both decomps) |
| 6 | Fainting | −1 | −1 | −1 | fainting in battle |
| 7 | Surviving poison in the field | −5 | −5 | −10 | poison bringing a Pokémon to 1 HP outside battle |
| 8 | Fainting to a much stronger foe | −5 | −5 | −10 | fainting to a foe 30 or more levels higher (pokeplatinum `battle_script.c`) |
| 9 | Winning a Contest | 3 | 2 | 1 | Diamond, Pearl and Platinum only; HeartGold has no caller |

The callers are statically verified in Platinum and HeartGold. In Diamond they are located in the assembly, and the kinds seen match.

## Bonuses and limits

`Pokemon_UpdateFriendship` in Platinum, `MonApplyFriendshipMod` in HeartGold and the Diamond equivalent all do the same:

1. Eggs and empty slots are skipped. An egg's friendship byte is its hatch counter.
2. Only while the change is positive: +1 if the Pokémon is in a Luxury Ball, +1 if its location field matches the current place name, then × 150 / 100 if it holds a Soothe Bell.
3. The result is kept in a signed byte, added to the friendship and clamped to 0 to 255.

The location compared is the field the decomps call the egg location. For a caught Pokémon the game writes the place it was caught there, so the bonus is for being where it was caught; for a hatched Pokémon it is where the egg was received, not where it hatched. The id compared is a place-name id, not a map header.

Because the change is a signed byte (HeartGold and Diamond cast it explicitly, Platinum truncates), the largest safe table value is 83: 83 + 2 is 85, and 85 × 1.5 is 127. At 84 the result is 129, which wraps to −127, a loss. A value of 126 or 127 wraps on the +1s alone and then misses the later bonuses.

Other friendship changes do not use the table and do not overflow: items (vitamins, Rare Candy, battle items and berries) use their own item data, the script command that adds friendship applies its own Soothe Bell (before the +1s in Platinum, after them in HeartGold), a Poffin adds 1, and a Friend Ball sets 200.

## Where friendship is used

- **Evolution** at 220 or more, with plain, day and night forms, in all three games.
- **Return and Frustration**: power is friendship × 10 / 25 and (255 − friendship) × 10 / 25 (the effect scripts of both games; 121 and 123 in pokeheartgold `files/battledata/script/effect_script`).
- **HeartGold walking Pokémon**: while one is out, the lead's friendship adds to the chance of a bite when fishing (pokeheartgold `field/encounter_check.c`). The walking Pokémon's mood is a separate value.

| Friendship | Bite bonus |
|---|---:|
| 99 or less | 0 |
| 100 to 149 | +20 |
| 150 to 199 | +30 |
| 200 to 249 | +40 |
| 250 up | +50 |

The Platinum Pokétch friendship checker and the scripts that read friendship are located but not traced here.

## What DSPRE does

The table is edited in the Friendship Changes editor (`FriendshipChangesViewModel`, through `FriendshipTable` in `DSPRE.Core/ROMFiles`), under the Pokémon menu behind the beta gate, for all three games.

| What | Edited in DSPRE | Written |
|---|---|---|
| Changes | the ten events by the three bands | the 30 bytes in the ARM9, at the offset in `RomInfo` |
| Unused rows | rows 1 and 2, and row 9 in HeartGold, are shown as unused | |
| Overflow | values above 83 are flagged as able to overflow into a loss | |

The editor's warning calls the location bonus the "met location"; as described above, for a hatched Pokémon it is where the egg was received.
