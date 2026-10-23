@ DSPRE summary screen fonts, for HeartGold and SoulSilver.
@ The summary screen prints every text box through AddTextPrinterParameterizedWithColor and measures centred and
@ right-aligned text with FontID_String_GetWidth and FontID_String_GetCenterAlignmentX (pokeheartgold
@ asm/unk_0208C3E4.s). Each of those calls goes through an entry here, which finds the box's window in Table by its
@ template (the eight bytes from bgId to baseTile) and swaps in the font listed for it. Boxes not listed keep the
@ game's font. The screen's FontID_Alloc(4) and FontID_Release(4) go through LoadFonts and ReleaseFonts, which also
@ load fonts 2 and 5, so every font can be picked. DSPRE fills the five addresses per ROM and writes Table.
@ Build: arm-none-eabi-as -mcpu=arm946e-s -mthumb, then objcopy -O binary.

    .syntax unified
    .thumb

@ r6 = the window. Sets \dest to its font when Table lists it. Uses r4 to r6 and ip.
    .macro FIND dest
    ldr r5, [r6, #4]            @ bgId, left, top, width
    ldr r6, [r6, #8]            @ height, palette, baseTile and the colour mode bit
    lsls r6, r6, #1
    lsrs r6, r6, #1
    mov ip, r6
    ldr r4, TableAt
1:  ldr r6, [r4, #0]
    cmp r6, #0
    beq 3f
    cmp r6, r5
    bne 2f
    ldr r6, [r4, #4]
    cmp r6, ip
    bne 2f
    ldr \dest, [r4, #8]
    b 3f
2:  adds r4, #12
    b 1b
3:
    .endm

@ Jumps to the function at \target with r4 to r7 and the stack as the caller left them, so stack arguments and lr
@ still belong to the caller.
    .macro JUMP target
    ldr r4, \target
    str r4, [sp, #12]
    pop {r4-r6}
    pop {pc}
    .endm

    .align 2
    .thumb_func
Print:                          @ r0 = window, r1 = font
    push {r4-r7}
    adds r6, r0, #0
    FIND r1
    JUMP PrintAt

    .thumb_func
WidthR6:                        @ the shared print routine: r6 = window, r0 = font
    push {r4-r7}
    FIND r0
    JUMP WidthAt

    .thumb_func
WidthR5R7:                      @ r5 + r7 = window
    push {r4-r7}
    adds r6, r5, r7
    FIND r0
    JUMP WidthAt

    .thumb_func
WidthR6R7:                      @ r6 + r7 = window
    push {r4-r7}
    adds r6, r6, r7
    FIND r0
    JUMP WidthAt

    .thumb_func
CentreName:                     @ r4 = the screen's work; its page windows start at [r4 + 0x224]
    push {r4-r7}
    movs r6, #0x89
    lsls r6, r6, #2
    ldr r6, [r4, r6]
    adds r6, #0x10
    FIND r0
    JUMP CentreAt

    .thumb_func
CentreTrainer:
    push {r4-r7}
    movs r6, #0x89
    lsls r6, r6, #2
    ldr r6, [r4, r6]
    adds r6, #0x20
    FIND r0
    JUMP CentreAt

    .thumb_func
LoadFonts:                      @ r0 = 4, r1 = heap
    push {r4, lr}
    adds r4, r1, #0
    ldr r3, AllocAt
    blx r3
    movs r0, #2
    adds r1, r4, #0
    ldr r3, AllocAt
    blx r3
    movs r0, #5
    adds r1, r4, #0
    ldr r3, AllocAt
    blx r3
    pop {r4, pc}

    .thumb_func
ReleaseFonts:                   @ r0 = 4
    push {r3, lr}
    ldr r3, ReleaseAt
    blx r3
    movs r0, #2
    ldr r3, ReleaseAt
    blx r3
    movs r0, #5
    ldr r3, ReleaseAt
    blx r3
    pop {r3, pc}

    .align 2
PrintAt:     .word 0            @ AddTextPrinterParameterizedWithColor | 1
WidthAt:     .word 0            @ FontID_String_GetWidth | 1
CentreAt:    .word 0            @ FontID_String_GetCenterAlignmentX | 1
AllocAt:     .word 0            @ FontID_Alloc | 1
ReleaseAt:   .word 0            @ FontID_Release | 1
TableAt:     .word 0            @ the address of Table
@ Table follows: three words a box (the template's first and second words, the font), ended by a zero word.
