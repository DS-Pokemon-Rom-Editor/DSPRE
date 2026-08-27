# Pokémon Battle Idle Animation System, HeartGold/SoulSilver

Source: [pokeheartgold decomp](https://github.com/pret/pokeheartgold). This was structured into a document with AI.

## Archives

`a/1/1/1` and `a/0/9/0` both exist in `pokeheartgold`'s `files/` tree. Neither has a symbolic name assigned yet in this project.

`a/1/1/1` is a single NARC, one file inside, 13832 bytes.

Species count: 494 (`include/constants/species.h`, `SPECIES_ARCEUS = 493`, last main species, IDs 0-493).

13832 / 494 = 28 exact. One 28-byte record per species.

`a/0/9/0` is a NARC with 143 files inside, sizes ranging from 44 to 768 bytes each.

## The per-species record struct

`include/pokemon_types_def.h`:

```c
struct PokeanmSub {
    s8 unk0;
    u8 unk1;
};

struct Pokeanm {
    struct PokeanmSub unk0[4];
    u8 unk8[20];
};

struct UnkStruct_02069038 {
    u16 unk0;
    u16 unk2;
    u8 unk4;
    u8 padding;
    struct Pokeanm anim;
};
```

`sizeof(struct Pokeanm)` = 4*2 + 20 = 28, matches the `a/1/1/1` record size.

## Nature list

`include/constants/pokemon.h` has the full `NATURE_*` enum: `NATURE_HARDY` (0) through `NATURE_QUIRKY` (24), `NATURE_NUM` 25.

## Not decompiled yet

No function in `src/` reads or writes `Pokeanm`, `PokeanmSub`, or `UnkStruct_02069038`.

No symbolic archive name exists for `a/1/1/1` or `a/0/9/0`.

The format of the 143 files in `a/0/9/0` is unknown.

No table linking nature to the `unk0[4]` array has been found.

Address-named or partially matched battle files that could hold the missing loader:

- `src/battle/overlay_12_0224E4FC.c` (6719 lines, mix of named functions and `ov12_` address stubs)
- `src/battle/battle_022378C0.c`
- `src/battle/battle_02261FD4.c`
- `src/battle/overlay_12_0226BEC4.c`
