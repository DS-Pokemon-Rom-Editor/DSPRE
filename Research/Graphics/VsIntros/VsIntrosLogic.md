[Research](../../ResearchNotes.md) / VS Intros Logic

# VS Intros Logic, Diamond/Pearl, Platinum and HeartGold/SoulSilver

Source: the [pokeheartgold](https://github.com/pret/pokeheartgold), [pokeplatinum](https://github.com/pret/pokeplatinum) and [pokediamond](https://github.com/pret/pokediamond) decomps, cited by file and function, and the retail US ROMs. This was structured into a document with AI.

A trainer battle opens with an intro: the plain terrain transition for most trainers, and for gym leaders, the Elite Four, the Champion, the rival and a few villains a special cut-in, the "VS" mugshot. This covers how the game picks an intro and its music, what each mugshot is drawn from, what is data and what is code, how to change them by hand, and what DSPRE's VS Intro Editor and Wild Pokémon Intro Editor edit.

## How an intro is picked

Every game picks a pair of an intro effect and a battle theme, then applies overrides for the kind of battle.

**HeartGold and SoulSilver** keep three data tables in the ARM9 (`BattleSetup_GetTransitionAndMusicParam` and the lookups around it, `asm/unk_020517A4.s`):

| Table | Place (US) | Rows | Row |
|---|---|---|---|
| Classes | `0x020FC3CA`, pointer at `0x02051890`, count `cmp r2, #0x20` at `0x02051886` | 32 | `u16`: class in bits 0 to 9, combo in bits 10 to 15 |
| Species | `0x020FC3B4`, pointer at `0x020518D4`, count at `0x020518CA` | 11 | the same, with a species |
| Combos | `0x020FC40A`, pointers at `0x020517C8` (intro) and `0x020517E4` (music, the table plus 2), counts at `0x020517AA` and `0x020517D4` | 45 | `u16` effect, `u16` sequence |

The first class row that matches wins; a class with no row gets combo 41, the ordinary trainer. Then the battle type overrides: in a Frontier battle only the Frontier Brain combo (39) survives, and anything else becomes 35, or 37 if double; Team Rocket, the Kimono Girls and Red keep their own; any other double battle takes 37 and a link battle 36. So a gym leader fought in a double battle loses the mugshot. An effect of `0xFFFF` means the terrain and time of day transition.

**Diamond, Pearl and Platinum** pick through code: a jump table in the ARM9 covers classes 62 to 102 in Platinum (`EncEffects_TrainerClassEffect`, `src/enc_effects.c`, table at `0x51C34`, 41 halfwords) and 62 to 97 in Diamond (`sub_020475C0`, `arm9/asm/unk_02047500.s`, table at `0x475D4`, 36 halfwords). Each entry jumps to a `movs r1, #pair` stub; every other class gets the ordinary pair (33 in Platinum, 29 in Diamond). The pair table is data (`sEncEffectsTable`, Platinum `0xEC208`, 35 rows; Diamond `0xF457C`, 31 rows) of `u16` intro, `u16` sequence, reached through two pointers, one of them two bytes in for the music. Doubles, link battles, Team Galactic and, in Platinum, Volkner in a double battle are decided in code (`EncEffects_GetEffectPair`).

## The intro kinds

| Kind | HeartGold effects | Platinum effects | Face |
|---|---|---|---|
| Terrain transition | 0 to 11, and `0xFFFF` | 0 to 11, and `0xFFFF` | no |
| Gym leader | 12 to 27 | 12 to 19 | from the record |
| Rival | 28 | the terrain transition, with the rival theme | HeartGold only |
| Elite Four and Champion | 29 to 33 | 20 to 24 | from the record, and the player's |
| Legendary Pokémon | 34 to 36 | 25, 26 | no |
| Poké Ball zoom, four Poké Balls | 37, 38 | 29, 30 | no |
| Team Rocket or Galactic | 39 grunt, 40 to 44 executives and Giovanni | 27 grunt, 28 commanders and Cyrus | HeartGold executives only |
| Kimono Girls' doors, Red's old-style Poké Ball | 45, 46 | | no |

The routines are picked by a function table in an overlay (HeartGold overlay 1, 47 entries; Platinum and Diamond overlay 5, 31 entries). Each gym, league and executive routine is a small wrapper holding a pointer to its own record, so which record an intro reads is fixed in code while the record itself is data.

## The mugshot records

**Gym leaders and the HeartGold rival**, 20 bytes (HeartGold overlay 115 at `0x022603B0`, 16 rows, rival at `0x02260374`; Platinum overlay 5 at `0x28FB4`, 8 rows; `EncounterEffect_GymLeader` in Platinum):

| Offset | Size | Field |
|---|---|---|
| 0x00 | 4 | where the face stops, `fx32` (214) |
| 0x04 | 4 | trainer whose name is printed |
| 0x08 | 2 | class: in Platinum the class whose palette colours the face; in HeartGold 23 prints the rival's name instead |
| 0x0A | 2 | a face column offset read by Diamond and Pearl only |
| 0x0C | 4 | face palette, tiles, cells and animation, archive members |
| 0x10 | 3 | banner palette, tiles and screen, archive members |

**Elite Four and Champion**, 8 bytes (HeartGold overlay 115 at `0x02260388`; Platinum overlay 5 at `0x28F8C`; `EncounterEffect_EliteFourChampion`): the face palette member (`u16`, the tiles, cells and animation must be the next three members), the frame palette member, the length of the clash shake in frames, the class and the trainer whose name is printed.

**HeartGold Rocket executives**, 8 bytes (overlay 117 at `0x0225FACC`, 5 rows): the four face members and the trainer whose name is printed.

**Diamond and Pearl** have no mugshot art. Their gym record (overlay 5 at `0x20458`, 8 bytes) is where the face stops, the class and a column offset: the face is cut at runtime as a 64 by 64 block from that class's battle sprite. Their league record (overlay 5 at `0x20430`) is a camera turn, its length in frames, the class and the banner palette; the intro ends by panning the field camera.

The art is in the encounter effect archive: HeartGold `a/1/0/9` (242 members), Platinum `graphic/field_encounteffect.narc` (155 members, named in the decomp's `res/trainers/classes/field_encounteffect.order`), Diamond the same path with 26 members.

## Faces, names and colours

The face is a fixed set of archive members per record; it does not follow the trainer being fought. The name under "VS" is text: the name of the trainer stored in the record, printed from message bank 189 (HeartGold) or 359 (Platinum), so a record's banner shows its own trainer's name whoever is fought. The "VS" itself is a sprite.

In Platinum the face's colours are not the face's own palette member: the game loads the palette of the record's class from the trainer battle sprites (`EncounterEffect_BlendTrainerSpritePltt`), so recolouring a face means recolouring that class's battle sprite. Checked in game: with the class left at a gym leader's own, a different leader's face drew in the first leader's colours.

## Fixed in code

The "VS" mark (HeartGold 59 to 62, Platinum 51 to 54), the name colours (HeartGold 16, Platinum 11), the Elite Four frame (HeartGold 48 to 50, Platinum 40 to 42), the player's face in the league intro (HeartGold 207 to 214, Platinum 147 to 154), the league particle files (HeartGold 151 and 152, Platinum 107 and 108, with their emitter counts), and every timing and position are immediates in the routines.

## Music

Music is the second half of each combo or pair row. HeartGold swaps the wild and trainer themes for their Kanto versions in Kanto, and the GB Sounds item swaps every theme for its Game Boy version through a table of pairs (`asm/unk_02004A44.s`). Platinum's Frontier battles set their own music.

## Changing one by hand

- **Reassign a class, HeartGold:** change its row in the class table. The Youngster, class 2, has no row in retail; replacing a row with `0x0002` (class 2, combo 0) gives Youngsters Falkner's intro. Checked in game.
- **Reassign a class, Platinum:** point its jump table entry at another entry's stub. Entry `i` is at `0x51C34 + 2i` for class `62 + i`, and jumps to `0x02051C36` plus its value; copying Roark's entry (class 62, value 80) into the rival's (class 63) gives the rival Roark's intro. Checked in game.
- **Change a mugshot:** edit the record. Setting Roark's face members to 59 to 62, the trainer to 315 and the class to 74 shows Gardenia, in her colours and with her name, in Roark's intro. Checked in game.
- **More class rows, HeartGold:** the class table cannot grow where it is. Copy it somewhere free with room for more rows, point `0x02051890` at it and raise the count byte at `0x02051886`, up to 255. Unused rows hold class 1023, which matches nothing.

A brand new intro needs code: a copy of a wrapper routine pointing at a new record, a longer routine table and, in Diamond, Pearl and Platinum, a new stub or hook for the class.

## What DSPRE does

Both editors are behind the beta gate and read one shared model (`VsIntroTables` in `DSPRE.Core/ROMFiles`), which checks every table by its contents before using it. They support US English HeartGold, Platinum and Diamond, and open SoulSilver and Pearl on the same offsets when their bytes match (not yet checked on those ROMs); Tools > Music & Battle Tables keeps the old tables for anything else.

| What | Edited in DSPRE | Written | Left untouched |
|---|---|---|---|
| Mugshot records | VS Intro Editor, Mugshots tab: banner trainer, face, face colours (Platinum), banner, where the face stops, frame colours and clash length (league), with a composed preview and an Animate approximation | only the bytes changed, in the overlay | the routines and wrappers |
| Class to intro | VS Intro Editor, Trainer intros and music tab: every class, named and numbered | HeartGold class rows; Platinum and Diamond jump table entries for classes 62 to 102 (97) | classes outside the jump table |
| More class rows | "Make room", HeartGold: moves the class table to the expanded ARM9 area with room for 255 classes | the new block, the pointer and the count | the old copy |
| Trainer intro music | the same tab, by plain name and sound archive name | the combo or pair row's sequence | |
| Wild intros and music | Wild Pokémon Intro Editor: HeartGold species rows and the music of intros only wild Pokémon use; Platinum and Diamond wild music | species rows, sequences | Platinum and Diamond wild species, which are picked in code |
| Fixed values | shown, with Open buttons for the art and the particle files | | all |

A save re-reads the files and refuses if a byte it is about to write was changed by something else since it was read, so the two editors and Tools never overwrite each other. Adding brand new intros is not supported yet.
