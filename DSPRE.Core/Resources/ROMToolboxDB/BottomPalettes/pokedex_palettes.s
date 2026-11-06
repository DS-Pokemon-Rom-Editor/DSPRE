@ DSPRE bottom screen palettes, for Platinum's Pokédex.
@ Every page loads its bottom graphics through PokedexGraphics_LoadGraphicNarcCharacterData and
@ PokedexGraphics_LoadGraphicNarcPaletteData (pokeplatinum src/applications/pokedex/pokedex_graphics.c). Their calls
@ come here.
@ Tile: a drawing loaded into BG layer 6 (sub BG2) is swapped for its 256-colour copy from Tiles, then the game's
@ loader runs as before.
@ Palette: the game's load runs as before. A bottom palette (location 4) with a twin in Palettes then also loads the
@ twin into BG2's extended palettes (slot 2, 0x4000) at 16 times the offset and size, through
@ Graphics_LoadPaletteFromOpenNARC, which maps bank H for the write. Tables are member pairs ending with -1.
@ DSPRE fills the addresses, the archive handle's offset and the tables per ROM.
@ Build: arm-none-eabi-as -mcpu=arm946e-s -mthumb, then objcopy -O binary.

    .syntax unified
    .thumb

Tile:
    cmp r3, #6
    bne 3f
    push {r4, r5}
    ldr r4, TilesAt
1:
    ldr r5, [r4]
    adds r5, #1
    beq 2f
    subs r5, #1
    cmp r5, r1
    beq 4f
    adds r4, #8
    b 1b
4:
    ldr r1, [r4, #4]
2:
    pop {r4, r5}
3:
    push {r4}
    ldr r4, TileLoader
    mov r12, r4
    pop {r4}
    bx r12

Palette:
    push {r4-r7, lr}
    sub sp, #8
    adds r4, r0, #0
    adds r5, r1, #0
    adds r6, r2, #0
    adds r7, r3, #0
    ldr r0, [sp, #28]
    str r0, [sp]
    ldr r0, [sp, #32]
    str r0, [sp, #4]
    ldr r0, PaletteLoader
    mov r12, r0
    adds r0, r4, #0
    blx r12
    cmp r6, #4
    bne 3f
    ldr r1, PalettesAt
1:
    ldr r2, [r1]
    adds r2, #1
    beq 3f
    subs r2, #1
    cmp r2, r5
    beq 2f
    adds r1, #8
    b 1b
2:
    ldr r1, [r1, #4]
    ldr r0, ArchiveAt
    ldr r0, [r4, r0]
    movs r2, #6
    lsls r3, r7, #4
    movs r6, #1
    lsls r6, r6, #14
    adds r3, r3, r6
    ldr r6, [sp, #28]
    lsls r6, r6, #4
    str r6, [sp]
    ldr r6, [sp, #32]
    str r6, [sp, #4]
    ldr r6, GameLoader
    blx r6
3:
    add sp, #8
    pop {r4-r7, pc}

    .align 2
TileLoader:
    .word 0
PaletteLoader:
    .word 0
GameLoader:
    .word 0
ArchiveAt:
    .word 0
TilesAt:
    .word 0
PalettesAt:
    .word 0
