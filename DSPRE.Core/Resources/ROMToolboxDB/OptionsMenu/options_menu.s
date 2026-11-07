@ DSPRE Options menu: text speeds, locked choices and rows taken off the screen, for Platinum and HGSS.
@ Options_TextFrameDelay reads a pace from Paces by the saved speed. A pace below 0x80 is frames per letter, 0x80 + n
@ draws n letters a frame, and 0xFE a page at a time. Text_AddPrinter keeps each printer's letters by its id.
@ The Options screens read each row's slot (0xFF removed), HGSS each row's first text line, and clamp saved choices.
@ Base holds block + 0x30: Letters[8] +0, Pending +8, Count +9, Arrows +10, FirstLines[8] +0xC, Slots[8] +0x14.
@ DSPRE fills Base, the three bl targets, Platinum's row registers and its copied str instructions.
@ Build: arm-none-eabi-as -mcpu=arm946e-s -mthumb, then objcopy -O binary.

    .syntax unified
    .thumb

AddPrinter:
    push {lr}
    adds r0, r4, #0
    bl ZeroIconGfx
    ldr r2, Base
    movs r1, #1
    cmp r6, #0xff
    beq a1
    cmp r6, #0x80
    bcc a1
    movs r1, #0x7f
    ands r1, r6
    movs r6, #1
    adds r3, r4, #0
    adds r3, #0x29
    ldrb r0, [r3]
    lsrs r0, r0, #7
    lsls r0, r0, #7
    adds r0, #1
    strb r0, [r3]
a1:
    strb r1, [r2, #8]
    pop {pc}

PrinterId:
    adds r1, r4, #0
    adds r1, #0x2c
    strb r0, [r1]
    cmp r0, #8
    bcs b1
    ldr r2, Base
    ldrb r3, [r2, #8]
    adds r1, r2, r0
    strb r3, [r1, #0]
b1:
    bx lr

Render:
    push {r5, r6, lr}
    adds r0, r4, #0
    adds r0, #0x2c
    ldrb r0, [r0]
    movs r5, #1
    cmp r0, #8
    bcs c1
    ldr r1, Base
    adds r1, r1, r0
    ldrb r5, [r1, #0]
    cmp r5, #0
    bne c1
    movs r5, #1
c1:
    cmp r5, #0x7e
    bne c0
    lsls r5, r5, #4
c0:
    movs r6, #0
c2:
    adds r0, r4, #0
    bl RenderText
    cmp r0, #0
    bne c3
    movs r6, #1
    subs r5, #1
    bne c2
    pop {r5, r6, pc}
c3:
    cmp r6, #0
    beq c4
    push {r0}
    ldr r0, [r4, #4]
    bl CopyToVram
    pop {r0}
c4:
    pop {r5, r6, pc}

@ End of LoadAllEntryChoices: a saved choice past a row's list shows its last choice
PtTail:
    movs r0, #0
    adds r1, r5, #0
    adds r1, #0x5c
e1:
    ldrh r2, [r1, #0]
    ldrh r3, [r1, #2]
    cmp r2, #0
    beq e2
    cmp r3, r2
    bcc e2
    subs r3, r2, #1
    strh r3, [r1, #2]
e2:
    adds r1, #0x54
    adds r0, #1
    cmp r0, #7
    bcc e1
    add sp, #8
    pop {r3, r4, r5, r6, r7, pc}

@ End of OptionsApp_LoadMenuEntriesData, the same for HGSS's six rows
HgTail:
    movs r0, #0
    adds r1, r6, #0
    adds r1, #0x84
f1:
    ldrh r2, [r1, #0]
    ldrh r3, [r1, #2]
    cmp r2, #0
    beq f2
    cmp r3, r2
    bcc f2
    subs r3, r2, #1
    strh r3, [r1, #2]
f2:
    adds r1, #0x54
    adds r0, #1
    cmp r0, #6
    bcc f1
    add sp, #8
    pop {r3, r4, r5, r6, r7, pc}

@ cmp rX,#5; bne; movs r0,#0x54 (bne after the call); DSPRE writes rX into both cmps
PtArrows:
    movs r0, #0x54
    cmp r7, #5
    beq d1
    cmp r7, #0
    bne d1
    ldr r1, Base
    ldrb r1, [r1, #10]
    cmp r1, #1
d1:
    bx lr

@ Japanese HGSS: cmp r0,#5; bne; adds r2,r0,#0 (bne after the call)
JpArrows:
    adds r2, r0, #0
    cmp r0, #5
    beq g1
    cmp r0, #0
    bne g1
    ldr r1, Base
    ldrb r1, [r1, #10]
    cmp r1, #1
g1:
    bx lr

@ lsls r0,rX,#4; str r0,[sp,#N]: DSPRE writes rX and copies the game's str
PtLabel:
    adds r0, r4, #0
    ldr r3, Base
    adds r3, r3, r0
    ldrb r0, [r3, #0x14]
    cmp r0, #0xff
    bne h1
    movs r0, #0xc0
    b h2
h1:
    lsls r0, r0, #4
h2:
    str r0, [sp, #0]
    bx lr

PtChoice:
    adds r0, r7, #0
    ldr r3, Base
    adds r3, r3, r0
    ldrb r0, [r3, #0x14]
    cmp r0, #0xff
    bne i1
    movs r0, #0xc0
    b i2
i1:
    lsls r0, r0, #4
i2:
    str r0, [sp, #20]
    bx lr

@ lsls r3,r3,#4; adds r3,#24
PtScroll:
    ldr r0, Base
    adds r0, r0, r3
    ldrb r3, [r0, #0x14]
    lsls r3, r3, #4
    adds r3, #24
    bx lr

@ In place of the blx to the remainder: r0 is cursor + 6, r1 gets the row
CursorUp:
    movs r2, #6
    b j0

CursorDown:
    movs r2, #1
j0:
    ldr r3, Base
j1:
    cmp r0, #7
    bcc j2
    subs r0, #7
    b j1
j2:
    adds r1, r3, r0
    ldrb r1, [r1, #0x14]
    cmp r1, #0xff
    bne j3
    adds r0, r0, r2
    b j1
j3:
    adds r1, r0, #0
    bx lr

@ movs r0,#24; muls r0,r4; adds r0,r0,#5
HgLabel:
    adds r0, r4, #0
    b HgRowY

@ adds r0,r6,#0; muls r0,r1; adds r0,r0,#5
HgChoice:
    adds r0, r6, #0

@ Japanese: adds r2,r0,#0; muls r2,r1; adds r0,r2,#5
HgRowY:
    ldr r3, Base
    adds r3, r3, r0
    ldrb r0, [r3, #0x14]
    cmp r0, #0xff
    beq l1
    movs r3, #24
    muls r0, r3
    adds r0, #5
    bx lr
l1:
    movs r0, #0xc0
    bx lr

@ ldr r0,[r6,#40]; adds r1,#9: each row's names from its own first line
HgLines:
    ldr r0, [r6, #40]
    ldr r2, [sp, #4]
    ldr r3, Base
    adds r3, r3, r2
    ldrb r1, [r3, #0xc]
    adds r1, r1, r4
    bx lr
    .align 2

Base:
    .word 0

