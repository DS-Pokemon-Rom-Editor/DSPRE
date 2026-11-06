@ DSPRE HGSS bag item buttons.
@ The bag's item button draw routines (pokeheartgold bag_app_main.c BagApp_DrawItemBgButtons_NormalMode and _MoveMode) call
@ here instead. Normal runs the list for the number of items shown. Move runs that list, then the picked slot's.
@ A command is 8 bytes: type (0 ends, 1 copies, 2 fills with tile 0), source x and y, destination x and y, width, height, pad.
@ Build: arm-none-eabi-as -mcpu=arm946e-s -mthumb, then objcopy -O binary.

    .syntax unified
    .thumb

Normal:
    push {r4-r7, lr}
    adds r4, r0, #0
    lsls r5, r1, #24
    lsrs r5, r5, #24
    adds r6, r2, #0
    adds r1, r5, #0
    ldr r3, GetBuffer
    blx r3
    adds r7, r0, #0
    cmp r6, #6
    bhi 1f
    lsls r6, r6, #2
    ldr r0, NormalLists
    ldr r6, [r0, r6]
    bl RunList
1:
    pop {r4-r7, pc}

Move:
    push {r3-r7, lr}
    adds r4, r0, #0
    lsls r5, r1, #24
    lsrs r5, r5, #24
    adds r6, r2, #0
    adds r1, r5, #0
    ldr r3, GetBuffer
    blx r3
    adds r7, r0, #0
    cmp r6, #6
    bhi 2f
    lsls r6, r6, #2
    ldr r0, MoveLists
    ldr r6, [r0, r6]
    bl RunList
2:
    ldr r6, [sp]
    cmp r6, #5
    bhi 3f
    lsls r6, r6, #2
    ldr r0, PickedLists
    ldr r6, [r0, r6]
    bl RunList
3:
    pop {r3-r7, pc}

@ r4 the BG config, r5 the layer, r6 the list, r7 the tilemap buffer.
RunList:
    push {lr}
    sub sp, #20
4:
    str r6, [sp, #16]
    ldrb r0, [r6]
    cmp r0, #0
    beq 6f
    ldrb r1, [r6, #4]
    str r1, [sp]
    ldrb r1, [r6, #5]
    str r1, [sp, #4]
    ldrb r1, [r6, #6]
    str r1, [sp, #8]
    ldrb r3, [r6, #3]
    cmp r0, #1
    bne 5f
    ldrb r2, [r6, #2]
    lsls r2, r2, #5
    ldrb r1, [r6, #1]
    adds r2, r2, r1
    lsls r2, r2, #1
    adds r2, r7, r2
    adds r0, r4, #0
    adds r1, r5, #0
    ldr r6, CopyRect
    blx r6
    b 7f
5:
    movs r2, #0
    str r2, [sp, #12]
    adds r0, r4, #0
    adds r1, r5, #0
    ldr r6, FillRect
    blx r6
7:
    ldr r6, [sp, #16]
    adds r6, #8
    b 4b
6:
    add sp, #20
    pop {pc}

    .align 2
GetBuffer:
    .word 0
CopyRect:
    .word 0
FillRect:
    .word 0
NormalLists:
    .word 0
MoveLists:
    .word 0
PickedLists:
    .word 0
