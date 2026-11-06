@ DSPRE bottom screen palettes.
@ A screen loads its bottom palette with Graphics_LoadPaletteFromOpenNARC(narc, member, PAL_LOAD_SUB_BG, offset, size, heap),
@ as the summary screen's LoadBgGraphics does (pokeplatinum src/applications/pokemon_summary_screen/main.c). That call
@ comes here: Load makes it as before through Loader, then loads Member into the bottom screen's extended palettes at
@ Offset, through Game. Offset is the slot of a layer the patch turned 256-colour. Layers sharing one palette load get one routine
@ each, chained through Loader, so the game's load runs once. DSPRE fills the words per ROM.
@ Build: arm-none-eabi-as -mcpu=arm946e-s -mthumb, then objcopy -O binary.

    .syntax unified
    .thumb

Load:
    push {r4-r7, lr}
    sub sp, #8
    adds r4, r0, #0
    ldr r5, [sp, #28]
    ldr r6, [sp, #32]
    str r5, [sp]
    str r6, [sp, #4]
    ldr r7, Loader
    blx r7
    adds r0, r4, #0
    ldr r1, Member
    movs r2, #6
    ldr r3, Offset
    movs r5, #0
    str r5, [sp]
    str r6, [sp, #4]
    ldr r7, Game
    blx r7
    add sp, #8
    pop {r4-r7, pc}

    .align 2
Loader:
    .word 0
Member:
    .word 0
Offset:
    .word 0
Game:
    .word 0
