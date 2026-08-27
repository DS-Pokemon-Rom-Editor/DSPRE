[Research](../../../ResearchNotes.md) / [Pokemon Resear…](../../PokemonResearch.md) / [Sprites Resear…](../SpritesResearch.md) / [Sprite Animati…](SpriteAnimationResearch.md) / Idle Animation…

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

## A separate, already-consumed animation system: Pokepic

`Pokeanm` above has no confirmed consumer. A different, unrelated struct and archive, both fully wired up in `src/`, cover a Pokemon sprite bouncing/wiggling on screen (the Hall of Fame list and the "give a nickname?" screen after a capture).

`include/pokepic.h`:

```c
typedef struct PokepicAnimScript {
    s8 next;
    u8 duration;
    s8 xOffset;
    s8 unk_3;
} PokepicAnimScript;
```

`include/unk_02016EDC.h`:

```c
struct UnkStruct_02072914_sub {
    u8 unk_0;
    u8 unk_1;
    u8 unk_2;
    PokepicAnimScript unk_3[10];
};

struct UnkStruct_02072914 {
    struct UnkStruct_02072914_sub unk0[2];
    s8 unk_56;
    s8 unk_57;
    u8 unk_58;
};
```

`sizeof(struct UnkStruct_02072914)` is 89 bytes (2 * (3 + 10*4) + 3). `NARC_a_1_8_0` is a single-file NARC whose one member is 43966 bytes, confirmed by reading its own FATB header directly: 43966 / 89 = 494 exactly, one record per species, same species count as `a/1/1/1`.

`NARC_ReadPokepicAnimScript` (`src/pokemon.c:2188`) reads one species' record out of `NARC_a_1_8_0` and copies out one of the two `unk0[]` sub-entries (front or back, picked by a facing argument) into a `PokepicAnimScript[10]` buffer. `register_hall_of_fame.c:2023` calls it for both the front and back sprite of each Hall of Fame party member.

`Pokepic_RunAnimInternal` (`src/pokepic.c:997`) is the real per-frame stepper:

```c
static void Pokepic_RunAnimInternal(Pokepic *pokepic) {
    if (pokepic->animActive != 0) {
        if (pokepic->animStepDelay == 0) {
            ++pokepic->whichAnim;
            while (pokepic->animScript[pokepic->whichAnim].next < -1) {
                ++pokepic->animLoopTimers[pokepic->whichAnim];
                if (pokepic->animScript[pokepic->whichAnim].duration == pokepic->animLoopTimers[pokepic->whichAnim] || pokepic->animScript[pokepic->whichAnim].duration == 0) {
                    pokepic->animLoopTimers[pokepic->whichAnim] = 0;
                    ++pokepic->whichAnim;
                } else {
                    pokepic->whichAnim = -2 - pokepic->animScript[pokepic->whichAnim].next;
                }
            }
            if (pokepic->whichAnim >= 10 || pokepic->animScript[pokepic->whichAnim].next == -1) {
                pokepic->whichAnimStep = 0;
                pokepic->animActive = 0;
                pokepic->drawParam.xOffset = 0;
            } else {
                pokepic->whichAnimStep = pokepic->animScript[pokepic->whichAnim].next;
                pokepic->animStepDelay = pokepic->animScript[pokepic->whichAnim].duration;
                pokepic->drawParam.xOffset = pokepic->animScript[pokepic->whichAnim].xOffset;
            }
        } else {
            --pokepic->animStepDelay;
        }
    }
}
```

Each of the 10 steps in a `PokepicAnimScript[10]` holds a `duration` (frames to hold), an `xOffset` (horizontal pixel shift applied while that step is active), and `next`, which is not just "go to the next step": `next == -1` ends the animation, `next >= 0` is a genuine next-step index, and `next < -1` means "loop": the engine bumps a per-step loop counter and either repeats from step `-2 - next` or falls through once the counter reaches `duration` (or `duration == 0`, an infinite-until-reset loop marker).

`src/battle/battle_command.c` also uses `Pokepic`/`PokepicManager_CreatePokepic`, inside `Task_GetPokemon` (the post-catch "send to a PC box or give a nickname" flow), sliding a `Pokepic` to the center of the screen. This confirms `Pokepic` runs inside a battle context too, though this particular call site is the post-capture screen, not the normal standing-in-battle sprite.

## Not decompiled yet

No function in `src/` reads or writes `Pokeanm`, `PokeanmSub`, or `UnkStruct_02069038`. Whatever purpose `a/1/1/1` serves is still unconfirmed; it is not the Pokepic system above, which is a separate archive (`NARC_a_1_8_0`) with its own struct and its own real consumer.

No symbolic archive name exists for `a/1/1/1`, `a/0/9/0`, or `a/1/8/0`.

The format of the 143 files in `a/0/9/0` is unknown.

No table linking nature to the `unk0[4]` array has been found.

Address-named or partially matched battle files that could hold the missing loader:

- `src/battle/overlay_12_0224E4FC.c` (6719 lines, mix of named functions and `ov12_` address stubs)
- `src/battle/battle_022378C0.c`
- `src/battle/battle_02261FD4.c`
- `src/battle/overlay_12_0226BEC4.c`
