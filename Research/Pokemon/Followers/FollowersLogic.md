[Research](../../ResearchNotes.md) / [Pokemon Research](../PokemonResearch.md) / Walking Pokémon Logic

# Walking Pokémon Logic, HeartGold and SoulSilver

Source: the [pokeheartgold](https://github.com/pret/pokeheartgold) decomp, cited by file and function, and the tables read from the US retail ROM. This was structured into a document with AI.

A walking Pokémon has a model, picked from the species, form and gender; a row in the overworld sprite table that decides its size and shadow; and a four-byte entry that decides where it may walk. The tables are only described here for the US game.

## Which model a Pokémon uses

`FollowMon_GetSpriteID` (`src/follow_mon.c`) gives sprite 428 plus the species' first model (`sModelIndexLUT`), plus 1 for a female model if the species has one (`sFemaleFlagLUT`), or else plus the form, up to the species' form count (`sFormMaxLUT`). The result is capped at 993, Arceus's last form. The three tables in the retail ARM9 (`0xFF088`, `0xFE8D4`, `0xFECAE`) are the decomp's.

There are 566 models: 493 species, 12 female models and 61 forms, contiguous in species order with the base model first.

- **Female models**: Venusaur, Pikachu, Meganium, Wobbuffet, Steelix, Heracross, Combee, Gible, Gabite, Garchomp, Hippopotas and Hippowdon.
- **Forms**: Pichu 1, Unown 27, Deoxys 3, Burmy 2, Wormadam 2, Shellos 1, Gastrodon 1, Rotom 5, Giratina 1, Shaymin 1 and Arceus 17.

No species has both.

## Size and shadow: the sprite table row

Overlay 1 holds a table of 6-byte rows, u16 sprite id, u16 model file in `a/0/8/1`, u16 bits, ended by sprite id `0xFFFF` and searched from the top (`ObjectEvent_GetGraphicsInfo`; the table is `ov01_022074A8`, at `0x21BA8` in the decompressed US overlay 1, 901 rows). The bits hold three indexes:

| Bits | Picks |
|---|---|
| 0 to 4 | one of ten u16 words at `ov01_02206D00`, passed to the per-step ground effects |
| 5 to 9 | a set of callbacks at `ov01_02209A38` |
| 10 to 15 | an 8-byte entry at `ov01_02207318` |

The walking Pokémon use three combinations:

| Value | Indexes | Used by |
|---|---|---|
| `0x4E27` | 7, 17, 19 | 533 models |
| `0x4E26` | 6, 17, 19 | Diglett and Dugtrio |
| `0x5208` | 8, 16, 20 | 31 models: Steelix (both), Lugia, Ho-Oh, Wailord, Kyogre, Groudon, Rayquaza, Dialga, Palkia, Regigigas, Giratina (both forms) and Arceus's 18 forms |

Every model using the first two has 32 × 32 textures and every one using the third has 64 × 64. The first index differs between them only in the ground effect word (`0x0811`, `0x0801` and so on differ by `0x10`), which fits 6 being no shadow, 7 a normal shadow and 8 a large one, but the effect has not been traced. Entries 19 and 20 of the 8-byte table point at the same animation table and differ in one byte, whose meaning is not known.

Other rows use the same kinds of values: the generic follower sprites 415 to 420, and the "static" follower sprites that stand still in scenes (`SPRITE_FOLLOWER_MON_STATIC_*`), have rows of their own.

## Where it may walk: `a/1/4/1`

`fielddata/tsurepoke/tp_param`, 566 files of 4 bytes, one per model.

| Byte | Content |
|---|---|
| 0 | 0 in every retail entry |
| 1 | set for a large Pokémon: 1 in exactly the 31 large models above |
| 2 | 0, `0x10`, `0x11` or `0x01`: see below |
| 3 | 0 in every retail entry |

**Byte 1.** `FollowMon_GetPermissionBySpeciesAndMap` refuses a follower where the map header's follow mode is 0, and where it is 1 refuses one whose byte 1 is set (`FollowMon_GetSizeParamBySpecies`). The follow mode is two bits, 18 and 19, of the header's u32 at `+0x14` (`include/map_header.h`). The check reads the byte of the species' base model only, with no form or gender. The same byte also chooses the large sprite resources and a 64 × 64 texture in the Hall of Fame, the certificates and the friendship-room statues.

**Byte 2** goes into the walking Pokémon's object as `(byte 1 << 8) | byte 2` (`FollowMon_SetObjectForm`), with the form added to the model but a female model never added, so female models' entries are never read. What reads it there is in assembly and has not been located. Retail uses 0 for 384 models, `0x10` for 103 (floaters and swimmers such as Magnemite, Tentacool, Unown, Koffing, Diglett, Gyarados and Porygon), `0x11` for 76 (fliers such as Zubat, Butterfree, Pidgeot, Lugia, Ho-Oh, Rayquaza and Arceus) and `0x01` for Steelix, female Steelix and Tropius. Within a species it only changes for Rotom (its base form `0x10`, the others 0), Giratina (Altered 0, Origin `0x11`) and Shaymin (Land 0, Sky `0x11`).

## What DSPRE does

The walking Pokémon are edited in the Personal Data editor for HeartGold (`HgssFollowers` in `DSPRE.Core/ROMFiles`, US only), per model: the size preset (small, small with no shadow, large), whether it is too tall for restricted maps (byte 1) and its motion (byte 2). DSPRE's model order, first sprite 428 and table offsets match the game's. The motion choices include `0x01`. Byte 1 is greyed out on female models and forms, and byte 2 on female models, since the game reads the base model's there. Choosing a size on the base model sets byte 1 to match, large meaning too tall, and it can still be changed by hand. A size is also written to every other row on the same model file, which covers the standing-still sprites.
