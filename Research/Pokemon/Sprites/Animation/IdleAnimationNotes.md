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

== separate, already-consumed system: Pokepic (not the same as Pokeanm above) ==
pokepic.h: struct PokepicAnimScript { s8 next; u8 duration; s8 xOffset; s8 unk_3; }   4 bytes
unk_02016EDC.h:
  struct UnkStruct_02072914_sub { u8 unk_0; u8 unk_1; u8 unk_2; PokepicAnimScript unk_3[10]; }   3+40 = 43 bytes
  struct UnkStruct_02072914 { struct UnkStruct_02072914_sub unk0[2]; s8 unk_56; s8 unk_57; u8 unk_58; }   2*43+3 = 89 bytes

NARC_a_1_8_0 = single-file NARC, member size 43966 bytes (read directly from its own FATB header)
  43966 / 89 = 494 exact -> one 89-byte record per species, same species count as a/1/1/1

NARC_ReadPokepicAnimScript (pokemon.c:2188): reads one species record, copies one of unk0[2] (front/back) into PokepicAnimScript[10]
  called from register_hall_of_fame.c:2023 for both front and back sprite per party member

Pokepic_RunAnimInternal (pokepic.c:997) = real per-frame stepper
  animStepDelay counts down to 0, then advances whichAnim
  each step: duration (hold frames), xOffset (pixel shift while active), next
    next == -1 -> animation ends
    next >= 0 -> real next-step index
    next < -1 -> loop: bump loop counter, repeat from step (-2 - next) until counter == duration (or duration==0 = infinite until reset)

Pokepic also used in src/battle/battle_command.c, inside Task_GetPokemon (post-catch nickname/PC screen), not confirmed for the normal standing-in-battle sprite

not decompiled:
- loader/consumer function for Pokeanm (a/1/1/1) - still unconfirmed, NOT the same as Pokepic above
- archive name/constant for a/1/1/1, a/0/9/0, a/1/8/0
- format of the 143 files in a/0/9/0
- nature -> unk0[4] link

possible loader locations (address-named / partial, battle overlay):
- src/battle/overlay_12_0224E4FC.c (6719 lines, mixed named + ov12_ stubs)
- src/battle/battle_022378C0.c
- src/battle/battle_02261FD4.c
- src/battle/overlay_12_0226BEC4.c
