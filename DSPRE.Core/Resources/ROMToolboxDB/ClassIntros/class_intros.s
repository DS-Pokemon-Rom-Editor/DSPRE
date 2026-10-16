@ DSPRE class intros for Diamond, Pearl and Platinum: replaces the class-to-intro switch (EncEffects_TrainerClassEffect
@ in pokeplatinum src/enc_effects.c, sub_020475C0 in pokediamond) with one 32-byte row per class:
@   +0 style (0 ordinary, 1 gym leader, 2 Elite Four/Champion, 3 Galactic grunt, 4 Galactic boss,
@        5 Frontier Brain or Tower Tycoon, 6 double battle), +1 unused, +2 battle music,
@   +4 gym leader record (20 bytes in Platinum, 8 in Diamond and Pearl), +24 Elite Four record (8 bytes).
@ Each style borrows an effect pair: the routine writes the class's music into it and copies the record into the
@ buffer the borrowed gym leader's or Aaron's intro reads. DSPRE fills PairTable and StylePairs per game (the values
@ below are Platinum's).
@ Build: arm-none-eabi-as -mcpu=arm946e-s -mthumb, then objcopy -O binary.

    .syntax unified
    .thumb

    .global Start
Start:
    .ascii "DSCI"
    .short 1                    @ version
    .short Rows - Start         @ class rows
    .word PairTable - Start     @ where DSPRE writes the pair table's address
    .word GymBuffer - Start
    .word E4Buffer - Start
    .word ClassEffect - Start   @ the hook's target

    .align 2
    .thumb_func
@ r0 = trainer class. Returns the effect pair in r0. Clobbers r1-r3, as the switch it replaces did.
ClassEffect:
    push {r4, lr}
    cmp r0, #255
    bhi Ordinary
    lsls r4, r0, #5
    adr r1, Rows
    adds r4, r4, r1
    ldrb r1, [r4]
    cmp r1, #6
    bls 1f
    movs r1, #0
1:  adr r2, StylePairs
    ldrb r0, [r2, r1]
    ldr r2, PairTable
    lsls r3, r0, #2
    adds r2, r2, r3
    ldrh r3, [r4, #2]
    strh r3, [r2, #2]
    cmp r1, #1
    bne 2f
    adr r2, GymBuffer
    ldr r3, [r4, #4]
    str r3, [r2, #0]
    ldr r3, [r4, #8]
    str r3, [r2, #4]
    ldr r3, [r4, #12]
    str r3, [r2, #8]
    ldr r3, [r4, #16]
    str r3, [r2, #12]
    ldr r3, [r4, #20]
    str r3, [r2, #16]
    b 3f
2:  cmp r1, #2
    bne 3f
    adr r2, E4Buffer
    ldr r3, [r4, #24]
    str r3, [r2, #0]
    ldr r3, [r4, #28]
    str r3, [r2, #4]
3:  pop {r4, pc}
Ordinary:
    adr r2, StylePairs
    ldrb r0, [r2]
    pop {r4, pc}

    .align 2
PairTable:
    .word 0
@ ordinary, gym (Volkner's pair), Elite Four (Aaron's), Galactic grunt, Galactic commander, Frontier Brain, double (the rival's)
StylePairs:
    .byte 33, 7, 8, 24, 25, 31, 13, 33
GymBuffer:
    .space 20
E4Buffer:
    .space 8
    .align 2
Rows:
