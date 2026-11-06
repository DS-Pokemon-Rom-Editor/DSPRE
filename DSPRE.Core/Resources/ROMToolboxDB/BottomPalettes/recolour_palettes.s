@ DSPRE bottom screen palettes: colours the game changes while the screen is open.
@ The HGSS bag recolours its bottom screen per pocket with GXS_LoadBGPltt into rows 0 to 3 (pokeheartgold
@ src/application/bag/bag_app_main.c, BagApp_LoadBgPaletteByPocket). Layers the patch turned 256-colour read the
@ extended palettes instead, so the last of those calls comes here: it makes the call through Recolour, then copies the
@ first Rows rows of the bottom palette into the start of extended palettes 0 to Rows - 1 of Slots slots from First.
@ Bank H is mapped to the CPU for the copy and given back to the extended palettes. DSPRE fills the words per ROM.
@ Build: arm-none-eabi-as -mcpu=arm946e-s -mthumb, then objcopy -O binary.

    .syntax unified
    .thumb

Recolour:
    push {r4-r7, lr}
    ldr r3, Load
    blx r3
    ldr r7, BankH
    movs r0, #0x80
    strb r0, [r7]
    ldr r2, First
    movs r6, #0
slot:
    ldr r1, Rows0
    movs r4, #0
row:
    lsls r5, r4, #9
    adds r5, r2, r5
    ldmia r1!, {r0, r3}
    stmia r5!, {r0, r3}
    ldmia r1!, {r0, r3}
    stmia r5!, {r0, r3}
    ldmia r1!, {r0, r3}
    stmia r5!, {r0, r3}
    ldmia r1!, {r0, r3}
    stmia r5!, {r0, r3}
    adds r4, #1
    ldr r0, Rows
    cmp r4, r0
    blt row
    movs r0, #1
    lsls r0, r0, #13
    adds r2, r2, r0
    adds r6, #1
    ldr r0, Slots
    cmp r6, r0
    blt slot
    movs r0, #0x82
    strb r0, [r7]
    pop {r4-r7, pc}

    .align 2
BankH:
    .word 0x04000248
Rows0:
    .word 0x05000400
Load:
    .word 0
First:
    .word 0
Slots:
    .word 0
Rows:
    .word 0
