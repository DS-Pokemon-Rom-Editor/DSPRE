[Research](../../ResearchNotes.md) / [Field Research](../FieldResearch.md) / Rematches Logic

# Rematches Logic: the Pokégear phone and the Vs. Seeker

Source: the [pokeheartgold](https://github.com/pret/pokeheartgold), [pokeplatinum](https://github.com/pret/pokeplatinum) and [pokediamond](https://github.com/pret/pokediamond) decomps, cited by file and function. This was structured into a document with AI.

Two systems decide when a beaten trainer fights again. HeartGold and SoulSilver do it through the Pokégear phone: a registered contact calls to ask for a rematch. Diamond, Pearl and Platinum do it with the Vs. Seeker. Both end in the same kind of table, a base trainer followed by the stronger versions that replace it as the story advances, and both pick from it the same way.

Offsets below are for the US releases: HeartGold, Platinum revision 1 and Diamond. They are file offsets in the decompressed ARM9 or overlay, with the RAM address beside them.

## The shared rematch table

Each row is six `u16` trainer ids: the base trainer, then one trainer for each of five levels.

| Slot | Meaning |
|---|---|
| 0 | the base trainer, the one on the map |
| 1 to 5 | the rematch for levels 1 to 5 |

A 0 ends the row early, and `0xFFFF` means "no trainer at this level, skip it".

Both games choose the same way (HeartGold `TryGetRematchTrainerIdByBaseTrainerId` in `src/overlay_26_022598C0.c`; Platinum `VsSeeker_GetRematchTrainerID` in `src/overlay005/vs_seeker.c`):

1. Take the first row whose slot 0 is the trainer being spoken to. No row means no rematch.
2. Walk slots 1 to 5 and take the first that is not `0xFFFF` and whose trainer has not been beaten. A 0 stops the walk at the slot before it; if every slot is beaten, slot 5 is taken.
3. If that slot's level is locked, step back to the nearest earlier slot that is not `0xFFFF`, or to slot 0, without checking it again.

A level is unlocked by a system flag, and the two games unlock them in different places:

| Level | Platinum and Diamond/Pearl flag | Set at | HeartGold flag | Set at |
|---|---|---|---|---|
| 1 | `0x97F` | Route 207, when the Vs. Seeker is given | `0x97C` | nothing sets it |
| 2 | `0x980` | Celestic Town | `0x97D` | Radio Tower observation deck |
| 3 | `0x981` | Spear Pillar | `0x97E` | Hall of Fame |
| 4 | `0x982` | Hall of Fame | `0x97F` | Viridian Gym, after Blue |
| 5 | `0x983` | Stark Mountain, deepest room | `0x980` | nothing sets it |

Platinum's flags are read in `src/system_flags.c` and HeartGold's in `src/sys_flags.c`; Diamond checks the same flags in `arm9/src/unk_0205EC84.c`; where it sets them is not visible, since its scripts are binary in pokediamond. So in HeartGold slot 1 can only work as a copy of slot 0, slot 5 is never reached, and a `0xFFFF` left in a slot that is still chosen is returned as a trainer id. In Diamond, Pearl and Platinum all five levels are live.

## HeartGold and SoulSilver: the phone

### Who is in the phone book

The phone book is a plain file, `tel/pmtel_book.dat`: a `u32` count, then one 20 byte record per contact (`LoadPhoneBookEntryI` in `src/phonebook_dat.c`). Retail has 75, and the save data, the name table and the call tables are all sized for 75, so the count is effectively fixed.

| Offset | Size | Field |
|---|---|---|
| 0x00 | 1 | contact number, equal to its position |
| 0x01 | 1 | call handler |
| 0x02 | 1 | unused by the code |
| 0x03 | 1 | title |
| 0x04 | 2 | trainer id |
| 0x06 | 2 | map header they live on |
| 0x08 | 2 | gift after a rematch win |
| 0x0A | 2 | script when called on their own map |
| 0x0C | 1 | greeting row, 0 to 7, or `0xFF` for none |
| 0x0D | 1 | rematch weekday, 0 Sunday to 6 Saturday, 7 any |
| 0x0E | 1 | rematch time, 0 morning, 1 day, 2 night, 3 any |
| 0x0F | 1 | random call group, 0 to 2 |
| 0x10 | 4 | sort ranks by title, name and location, then 0 |

The call handler is 0 generic, 1 Mom, 2 Elm, 3 Oak, 4 Kurt, 5 the bike shop, 6 Kenji, 7 Bill, 8 the Day-Care Man, 9 the Day-Care Lady, 10 Buena, 11 the rival character, 12 a Gym Leader, 13 Baoba, 14 Irwin. The title is the trainer class's name, except that 200 shows none and 201 to 207 show lines 38 onward of message archive 271; values past 207 read beyond that archive.

Joey, contact 10, at offset `0xCC`: `0a 00 00 02 08 00 22 00 2d 00 1d 00 00 01 01 01 4b 28 2f 00`, a generic caller, Youngster, trainer 8 on Route 30, gift HP Up, local script 29, greeting 0, Monday daytime, group 1.

### How a rematch is asked for

A trainer's post battle script looks up the contact with its trainer id and, if the trainer is already beaten, asks whether that contact is seeking a rematch (`PhoneBookTrainerGetRematchInfo` in `src/unk_020932A4.c`); Kenji only counts during the day. "Seeking" is set by a phone call whose script is of the rematch kind, by the Gym Leader handler once the player has sixteen badges and it is the leader's day and time, and by Kenji's own handler. Contacts 47 and 52 have no rematch call and Irwin's handler never sets it, so their rows in the rematch table are never used. After a rematch win the trainer's flag is set, "seeking" is cleared and the gift is handed over; a Cheri Berry gift becomes a random berry of the first ten.

### When the phone rings

Each contact has sixteen call headers, eight for when the player calls and eight for when they call, each a condition, a chance and a script (`src/overlay_101_021F1D74.c` and the generic handler in `phone_scripts_generic.c`). Random incoming calls roll a group, below 500 of 1000 group 0, below 800 group 1, else group 2, then pick a registered contact of handler 0, 10, 11, 12 or 14 in that group who is not on the current map and has not just been used (`src/field/overlay_2_gear_phone.c`). Morning is 4:00 to 9:59, day 10:00 to 19:59 and night 20:00 to 3:59 (`src/gf_rtc.c`).

### Changing it by hand

| What | Where | RAM | Layout |
|---|---|---|---|
| Phone book | `tel/pmtel_book.dat` | | above |
| Rematch table | overlay 26, `0x20C` | `0x02259ACC` | 63 rows of six `u16` |
| Contact name archives | ARM9, `0x10847C` | `0x0210847C` | 75 `u16` message archive numbers |
| Greetings | overlay 101, `0x11EEC` | `0x021F962C` | 8 rows of 12 message numbers |
| Call headers | overlay 101, `0x11F4C` | `0x021F968C` | 75 contacts x 16 headers x 6 bytes |
| Call scripts | overlay 101, `0x1143C` | `0x021F8B7C` | 456 records of 6 bytes |
| Scripted calls | overlay 2, `0xE104` | `0x02253A84` | 13 records of 6 bytes |

The rematch table's row count is compiled into the code as `cmp r2, #0x3F` at overlay 26 offset `0x44`, and the table ends where the next data begins, so adding rows means moving the table and patching that compare. Its first row, `9e 01 9e 01 2f 01 be 01 5a 02 00 00`, is trainer 414, a level 1 copy of 414, then 303, 446, 602 and the end.

A call header is a condition, a chance out of 100, a script kind and a script number. The conditions are 0 never, 1 random, 2 after the Rocket fight, 3 after the Rocket fight on the contact's day and time, 4 on Bug Contest days, 5 during the Rocket takeover, 6 random, 7 after the Rocket fight outside the Bug Contest, 8 a gift outside the Bug Contest, and `0xFF` none. Joey's first, `07 64 00 00 22 00`, is condition 7 at 100 percent running script 34.

### What DSPRE does

Both are on the Trainers menu; the Phone Book is behind the beta gate and the Rematch Editor is not.

| What | Edited in DSPRE | Written | Left untouched |
|---|---|---|---|
| Phone book, every field of every contact | Pokégear Phone Book (`PokegearPhoneBook`) | the whole `tel/pmtel_book.dat` | anything after the last record |
| Title | picked from the class names and 200 to 207; a title past 207 shows as "(undefined)" with a warning | +0x03 | |
| Contact count | kept at what the file says; a count other than 75 warns that the save data and tables are sized for 75 | the count | |
| Kenji, contact 16 | his day and time are shown as "Day only (10:00-19:59)" and greyed, since his handler decides | | |
| Contact names | shown from the ARM9 name table (`PokegearContactArchives.Find`) | | the name table and the message archives |
| Rematch table, every slot of every row | Pokégear Rematch editor (`PokegearRematchTable`), found through its pointers in overlay 26 | the rows, in place in overlay 26 | the row count compare, and every other overlay |
| Call headers, greetings, call scripts, scripted calls | | | all of them |

Rows and contacts are rewritten in place; no table is moved or resized.

## Diamond, Pearl and Platinum: the Vs. Seeker

### What the game does

Platinum's code is in `src/overlay005/vs_seeker.c`. The battery is a variable that gains one per step while the Vs. Seeker is in the bag, up to 100; the Vs. Seeker only works at 100 and empties it. Using it scans a box of 7 tiles to either side and 7 up and 6 down around the player for trainer objects, skipping disguised ones. An unbeaten trainer gets a single exclamation mark and is left alone; a beaten one has an even chance of getting a double mark and spinning, and a double battle partner goes with it. After a rematch is set up, a flag and a step counter run for 100 steps, after which every spinning trainer goes back to looking around.

The trainer's script asks for the rematch trainer with `GetRematchTrainerID`, which picks from the table exactly as described above. The same level flags also drive the daily trainers in Pokémon Centers.

Diamond's Vs. Seeker is still assembly in its decomp (`arm9/overlays/05/asm/ov05_021E1374.s`), with the same table, the same lookup and the same level flags; its battery and scan constants have not been checked.

### Changing it by hand

The table is 240 rows of six `u16`, in overlay 5, identical in Diamond and Platinum:

| Game | File offset | RAM | Row count compare | Pointers to the table |
|---|---|---|---|---|
| Platinum | `0x280C8` | `0x021F8E48` | `cmp r2, #0xF0` at `0xB070` | `0xB078`, `0xB0C0`, `0xB11C`, `0xB134` |
| Diamond | `0x1F43C` | `0x021F691C` | `cmp r2, #0xF0` at `0xA3D0` | `0xA3D8`, `0xA420`, `0xA47C`, `0xA494` |

The count is fixed in code, and a row index of `0xFF` means "not found", so 255 rows is the ceiling even after moving the table. Row 1 in Platinum, `15 00 73 02 74 02 ff ff 75 02 00 00`, is trainer 21 with rematches 627 and 628, no level 3, then 629 and the end. Of the 240 retail rows, 160 have no rematch at all (the base trainer copied into slot 1 and nothing after), 72 skip level 1 with `0xFFFF`, and none could return `0xFFFF`.

### What DSPRE does

| What | Edited in DSPRE | Written | Left untouched |
|---|---|---|---|
| Vs. Seeker rematch table, every slot of every row | Vs. Seeker Rematch editor (`VsSeekerRematchTable`), for English Diamond, Pearl and Platinum | the rows, in place in overlay 5 | the row count compare, the level flags and the rest of the overlay |
| Where the table is | found through the four pointers above, used only when all four agree and the rows look like trainer rows; the row count is read from the compare (`RematchTable.Resolve`) | | |
| Problems | listed under the encounter trainer: a skip right before the end of a chain, a skip in the last slot with no end, and a trainer that starts two rows (the first wins) | | |

If the pointers disagree, DSPRE falls back to the known offset in `RomInfo.SetRematchTableOffsets` only when the rows there also look valid, and otherwise refuses to read or write. Each slot is a trainer, "skip this level" (`0xFFFF`) or "end of chain" (0), and the five levels are labelled by the places that unlock them in DSPRE: Route 207, Celestic Town, Spear Pillar, Hall of Fame and Stark Mountain.
