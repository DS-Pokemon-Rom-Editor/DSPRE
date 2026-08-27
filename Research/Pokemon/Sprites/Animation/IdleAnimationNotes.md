[Research](../../../ResearchNotes.md) / [Pokemon Resear…](../../PokemonResearch.md) / [Sprites Resear…](../SpritesResearch.md) / [Sprite Animati…](SpriteAnimationResearch.md) / Idle Animation…

Notes for [IdleAnimationLogic.md](IdleAnimationLogic.md).

a/1/1/1 - exists in files/ tree, no symbolic name
a/0/9/0 - exists in files/ tree, no symbolic name

a/1/1/1 = single NARC, 1 file, 13832 bytes
species.h: SPECIES_ARCEUS = 493, 494 species total (0-493)
13832 / 494 = 28 exact -> 28 byte record per species

a/0/9/0 = NARC, 143 files, sizes 44-768 bytes, not fixed length

pokemon_types_def.h:
struct PokeanmSub { s8 unk0; u8 unk1; }                       2 bytes
struct Pokeanm { struct PokeanmSub unk0[4]; u8 unk8[20]; }    4*2+20 = 28 bytes
struct UnkStruct_02069038 { u16 unk0; u16 unk2; u8 unk4; u8 padding; struct Pokeanm anim; }

no function in src/ references Pokeanm, PokeanmSub, or UnkStruct_02069038

pokemon.h: NATURE_HARDY=0 ... NATURE_QUIRKY=24, NATURE_NUM=25, fully named

not decompiled:
- loader/consumer function for Pokeanm
- archive name/constant for a/1/1/1, a/0/9/0
- format of the 143 files in a/0/9/0
- nature -> unk0[4] link

possible loader locations (address-named / partial, battle overlay):
- src/battle/overlay_12_0224E4FC.c (6719 lines, mixed named + ov12_ stubs)
- src/battle/battle_022378C0.c
- src/battle/battle_02261FD4.c
- src/battle/overlay_12_0226BEC4.c
