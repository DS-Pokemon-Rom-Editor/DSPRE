[Research](../../../ResearchNotes.md) / [Pokemon Resear…](../../PokemonResearch.md) / [Sprites Resear…](../SpritesResearch.md) / [Sprite Animati…](SpriteAnimationResearch.md) / Frame Animatio…

# Frame Animation Logic, HeartGold/SoulSilver

Source: [pokeheartgold decomp](https://github.com/pret/pokeheartgold). This was structured into a document with AI.

This covers the generic engine that steps a sprite through its NANR/NCER animation frames. It applies to every sprite in the game, not just Pokemon, and is separate from the idle animation data table covered in `IdleAnimationLogic.md`.

## The Sprite struct

`include/sprite.h`, `struct Sprite`:

```c
typedef struct Sprite {
    VecFx32 matrix;
    VecFx32 affineMatrix;
    VecFx32 scale;
    u16 rotation;
    u8 affine;
    u8 flip;
    u8 overwrite;
    u8 palIndex;
    u8 palOffset;
    BOOL mosaic;
    GXOamMode mode;
    u8 drawFlag;
    u8 animActive;
    fx32 speed;
    SpriteList *spriteList;
    u32 animationData[SPRITE_ANIMATION_DATA_WORD_COUNT];
    NNSG2dImageProxy imageProxy;
    NNSG2dImagePaletteProxy paletteProxy;
    SpriteAnimType flag;
    u16 animationNo;
    u8 priority;
    u16 drawPriority;
    NNS_G2D_VRAM_TYPE type;
    struct Sprite *prev;
    struct Sprite *next;
} Sprite;
```

`animationData` holds one of two layouts depending on `flag` (`SpriteAnimType`, also in `sprite.h`):

```c
typedef struct SpriteAnimationData {
    const NNSG2dCellDataBank *cellBank;
    const NNSG2dCellAnimBankData *animBankData;
    NNSG2dCellAnimation animation;
} SpriteAnimationData;

typedef struct SpriteMultiAnimationData {
    const NNSG2dCellDataBank *cellBank;
    const NNSG2dCellAnimBankData *animBankData;
    NNSG2dMultiCellAnimation animation;
    const NNSG2dMultiCellDataBank *multiCellBank;
    const NNSG2dMultiCellAnimBankData *multiAnimBankData;
    NNSG2dNode *node;
    NNSG2dCellAnimation *cellAnim;
} SpriteMultiAnimationData;
```

`SPRITE_ANIM_TYPE_CELL`/`SPRITE_ANIM_TYPE_CELL_TRANSFER` use `SpriteAnimationData`, `SPRITE_ANIM_TYPE_MULTICELL` uses `SpriteMultiAnimationData`.

## Frame stepping

All of the frame-stepping functions in `src/sprite.c` branch on `sprite->flag` and forward straight into the Nitro SDK's g2d library:

```c
void Sprite_UpdateAnim(Sprite *sprite, fx32 frames) {
    if (sprite->flag == SPRITE_ANIM_TYPE_CELL || sprite->flag == SPRITE_ANIM_TYPE_CELL_TRANSFER) {
        SpriteAnimationData *animData = (SpriteAnimationData *)sprite->animationData;
        NNS_G2dTickCellAnimation(&animData->animation, frames);
    } else {
        SpriteMultiAnimationData *animData = (SpriteMultiAnimationData *)sprite->animationData;
        NNS_G2dTickMCAnimation(&animData->animation, frames);
    }
}

void Sprite_SetAnimationFrame(Sprite *sprite, u16 frameIndex) {
    if (sprite->flag == SPRITE_ANIM_TYPE_CELL || sprite->flag == SPRITE_ANIM_TYPE_CELL_TRANSFER) {
        SpriteAnimationData *animData = (SpriteAnimationData *)sprite->animationData;
        NNS_G2dSetCellAnimationCurrentFrame(&animData->animation, frameIndex);
    } else {
        SpriteMultiAnimationData *animData = (SpriteMultiAnimationData *)sprite->animationData;
        NNS_G2dSetMCAnimationCurrentFrame(&animData->animation, frameIndex);
    }
}

u16 Sprite_GetAnimationFrame(Sprite *sprite) {
    SpriteAnimationData *animData = (SpriteAnimationData *)sprite->animationData;
    return NNS_G2dGetAnimCtrlCurrentFrame(&animData->animation.animCtrl);
}
```

Switching which animation sequence plays goes through `Sprite_SetAnimCtrlSeq` (`src/sprite.c:295`), which pulls the sequence by index out of `animBankData` (`NNS_G2dGetAnimSequenceByIdx`), assigns it to the live `animation` (`NNS_G2dSetCellAnimationSequence`), and starts it (`NNS_G2dStartAnimCtrl`). `Sprite_TryChangeAnimSeq` (`:311`) is a no-op guard that only calls `Sprite_SetAnimCtrlSeq` if `sprite->animationNo` actually changed. `Sprite_ResetAnimCtrlState` (`:317`) resets the control state and forces the frame back to 0.

## What DSPRE already does

DSPRE has its own reimplementation of this same cell/frame model, built directly from the NANR/NCER binary layout rather than from source. `DS_Map/Avalonia/Data/CellAnim.cs` defines `CFrame` (cell index, duration, position, rotation, scale per frame), `CellSequence` (an array of `CFrame`), and `CellActor` (owns the timeline/playback state and advances it). This mirrors the same shape as `SpriteAnimationData`/`NNSG2dCellAnimation`: a cell bank, an animation bank, and a live per-instance playback cursor, just parsed straight from the ROM's NANR/NCER files instead of loaded through the g2d library.

## Not decompiled yet

The actual per-frame delay countdown and looping logic live inside `NNS_G2dTickCellAnimation`/`NNS_G2dTickMCAnimation` and the rest of the `NNS_G2d*` functions. None of the g2d library's own source is part of `pokeheartgold`; only the calls into it are decompiled. `include/` has no `g2d`-named header, so the `NNSG2dCellAnimation`/`NNSG2dAnimSequenceData`/`NNSG2dCellDataBank` struct layouts are not visible in this project either.
