@ DSPRE place-name banner motion patch (Platinum overlay 5, HeartGold and SoulSilver overlay 1).
@ Both slide steps call Motion instead of "bl Bg_SetOffset(bgConfig, 3, SET_Y, yOffset)". The game still steps
@ yOffset by its speeds; Motion swaps it for Scroll[yOffset] and sets BG3's fade or mosaic to Level[yOffset].
@ Level 0 puts the blend registers back, and DSPRE writes 0 at the shown and hidden offsets.
@ Hand-assembled; DSPRE fills Data when it writes the style.

    .syntax unified
    .thumb

    .thumb_func
Motion:                         @ r0 bgConfig, r1 layer, r2 mode: kept for the call. r3 yOffset.
    push {r4-r7, lr}
    lsls r4, r3, #24
    lsrs r4, r4, #24            @ r4 = yOffset & 0xFF
    adr r5, Data
    adr r6, Scroll
    lsls r7, r4, #1
    ldrsh r3, [r6, r7]          @ the scroll to set
    adds r7, r5, r4
    ldrb r6, [r7, #16]          @ Level[yOffset]
    ldrb r7, [r5, #9]           @ effect: 1 fade, 2 pixelate
    cmp r7, #1
    beq Fade
    cmp r7, #2
    beq Mosaic
    b Call

Fade:
    movs r7, #0x80
    lsls r7, r7, #19
    adds r7, #0x50              @ BLDCNT
    cmp r6, #0
    bne FadeOn
    ldrb r4, [r5, #8]           @ restore what was saved, once
    cmp r4, #0
    beq Call
    ldrh r4, [r5, #4]
    strh r4, [r7, #0]
    ldrh r4, [r5, #6]
    strh r4, [r7, #2]
    movs r4, #0
    strb r4, [r5, #8]
    b Call
FadeOn:
    ldrb r4, [r5, #8]           @ save BLDCNT and BLDALPHA the first time
    cmp r4, #0
    bne Set
    ldrh r4, [r7, #0]
    strh r4, [r5, #4]
    ldrh r4, [r7, #2]
    strh r4, [r5, #6]
    movs r4, #1
    strb r4, [r5, #8]
Set:
    ldrh r4, [r5, #10]          @ BG3 over everything else, alpha
    strh r4, [r7, #0]
    movs r4, #16
    subs r4, r4, r6             @ EVA = 16 - level
    lsls r6, r6, #8             @ EVB = level
    orrs r4, r6
    strh r4, [r7, #2]
    b Call

Mosaic:
    movs r7, #0x80
    lsls r7, r7, #19
    ldrh r4, [r7, #14]          @ BG3CNT
    movs r5, #64                @ its mosaic bit
    bics r4, r5
    cmp r6, #0
    beq 1f
    orrs r4, r5
1:  strh r4, [r7, #14]
    lsls r4, r6, #4
    orrs r4, r6
    adds r7, #0x4C              @ MOSAIC: the background size, both ways
    strb r4, [r7, #0]

Call:
    adr r5, Data
    ldr r7, [r5, #0]
    blx r7                      @ Bg_SetOffset with the new scroll
    pop {r4-r7, pc}

    .align 2
Data:
    .word 0                     @ Bg_SetOffset | 1
    .hword 0, 0                 @ saved BLDCNT, BLDALPHA
    .byte 0                     @ saved flag
    .byte 0                     @ effect
    .hword 0x3748               @ blend control
    .byte 0, 0, 0, 0            @ direction, motion, effect, unused; DSPRE reads these back
    .space 256                  @ Level
Scroll:
    .space 512                  @ s16 Scroll
