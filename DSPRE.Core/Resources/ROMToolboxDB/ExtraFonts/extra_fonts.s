@ DSPRE extra fonts, for HeartGold and SoulSilver.
@ The block holds this loader, then the font module's tables and work area with room for 15 fonts. main.c's
@ FontID_Alloc(3, HEAP_ID_3) call goes to Load instead. Load makes that call, then loads each added font, so they stay
@ loaded all game like the system, dialogue and Unown fonts.
@ DSPRE fills the two addresses per ROM (ExtraFontsPatch.Apply) and the count as fonts are added (ExtraFontsPatch.Add).
@ Build: arm-none-eabi-as -mcpu=arm946e-s -mthumb, then objcopy -O binary.

    .syntax unified
    .thumb

    .align 2
    .thumb_func
Load:
    push {r4, r5, r6, lr}
    movs r0, #3
    movs r1, #3                 @ HEAP_ID_3
    ldr r6, AllocAt
    blx r6
    ldr r5, CountAt
    ldr r5, [r5]
    adds r5, #6                 @ the first font number past the added ones
    movs r4, #6
    b Check
Next:
    adds r0, r4, #0
    movs r1, #3
    blx r6
    adds r4, #1
Check:
    cmp r4, r5
    blt Next
    pop {r4, r5, r6, pc}
    nop

    .align 2
AllocAt: .word 0                @ FontID_Alloc | 1
CountAt: .word Count
Count:   .word 0                @ how many fonts were added, up to 9

@ Then, in order: sFontArcParam with 15 rows of 4 bytes (archive entry, fixed width), sFontInfos with 16 rows of 8 bytes,
@ and FontWork: the 0x84-byte glyph buffer, extraData[15], fontDataMan[15] and fontDataRefCount[15]. The font code's
@ fontDataMan offset becomes 192 and its fontDataRefCount offset 252, and its six FONT_NUM checks compare with 15.
