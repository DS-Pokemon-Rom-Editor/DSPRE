@ DSPRE VS intro timing add-on (HeartGold, with the trainer class metadata patch).
@ Position independent except ClassVar, which DSPRE fills with the address of the patch's active-class word.
@ Table: 256 classes x 8 bytes; a 0 keeps the game's own value.
@ Fields: 0 flash count, 1 gym slide, 2 VS shrink, 3 VS gap, 4 emblem flight, 5 doors close, 6 doors hold, 7 doors open.
@ Every hook replaces exactly four bytes reached by BL and keeps the registers the code after it reads.

    .syntax unified
    .thumb

    .global Start
Start:
    .ascii "DSVT"
    .short 2                    @ version
    .short Table - Start        @ table offset
    .word ClassVar - Start      @ where DSPRE writes the active-class word's address
    .word 0, 0, 0               @ reserved

    .align 2
    .thumb_func
@ r0 = field, r1 = default. Returns r0. Clobbers r1-r3.
GetTiming:
    ldr r2, ClassVar
    ldr r2, [r2]
    cmp r2, #255
    bhi 1f
    lsls r2, r2, #3
    adds r2, r2, r0
    adr r3, Table
    ldrb r0, [r3, r2]
    cmp r0, #0
    bne 2f
1:  movs r0, r1
2:  bx lr

    .align 2
ClassVar:
    .word 0

@ ---- ov117, grunt emblems: field 4, default 8 ----

    .align 2
    .thumb_func
HookEmblemMove:                 @ movs r1, #8; str r1, [sp]
    push {r0, r2, r3, lr}
    movs r0, #4
    movs r1, #8
    bl GetTiming
    movs r1, r0
    str r1, [sp, #16]
    pop {r0, r2, r3, pc}

    .align 2
    .thumb_func
HookEmblemScale:                @ movs r0, #0xf2; str r1, [sp]   (r1 = 8 also seeds the start scale)
    push {r1, r2, r3, lr}
    movs r0, #4
    movs r1, #8
    bl GetTiming
    str r0, [sp, #16]
    pop {r1, r2, r3}
    movs r0, #0xf2
    pop {pc}

    .align 2
    .thumb_func
HookEmblemTurn:                 @ movs r1, #0; movs r3, #8
    push {r0, r2, lr}
    movs r0, #4
    movs r1, #8
    bl GetTiming
    movs r3, r0
    movs r1, #0
    pop {r0, r2, pc}

@ ---- ov115, gym and league VS mark and face ----

    .align 2
    .thumb_func
HookShrinkCopies:               @ lsrs r2, r1, #1; movs r3, #6
    push {r0, r1, lr}
    movs r0, #2
    movs r1, #6
    bl GetTiming
    movs r3, r0
    pop {r0, r1}
    lsrs r2, r1, #1
    pop {pc}

    .align 2
    .thumb_func
HookShrinkLetters:              @ adds r2, r1, #0; movs r3, #6
    push {r0, r1, lr}
    movs r0, #2
    movs r1, #6
    bl GetTiming
    movs r3, r0
    pop {r0, r1}
    adds r2, r1, #0
    pop {pc}

    .align 2
    .thumb_func
HookVsGap:                      @ movs r2, #3; strh r2, [r1]
    push {r0, r1, lr}
    movs r0, #3
    movs r1, #3
    bl GetTiming
    movs r2, r0
    pop {r0, r1}
    strh r2, [r1]
    pop {pc}

    .align 2
    .thumb_func
HookFaceSlide:                  @ movs r0, #4; str r0, [sp]
    push {lr}
    movs r0, #1
    movs r1, #4
    bl GetTiming
    str r0, [sp, #4]
    pop {pc}

@ ---- ov118, Kimono doors ----

    .align 2
    .thumb_func
HookDoorsClose:                 @ movs r0, #0x12; movs r1, #2
    push {lr}
    movs r0, #5
    movs r1, #18
    bl GetTiming
    movs r1, #2
    pop {pc}

    .align 2
    .thumb_func
HookDoorsOpen:                  @ movs r0, #0x12; movs r2, #2
    push {lr}
    movs r0, #7
    movs r1, #18
    bl GetTiming
    movs r2, #2
    pop {pc}

    .align 2
    .thumb_func
@ adds r0, r2, #1; str r0, [r5, #0x18], before "cmp r2, #5; bls wait". r2 becomes 0 (still holding) or 6 (done).
HookDoorsHold:
    adds r0, r2, #1
    str r0, [r5, #0x18]
    push {r1, r2, lr}
    movs r0, #6
    movs r1, #5
    bl GetTiming
    pop {r1, r2}
    cmp r2, r0
    bls 1f
    movs r2, #6
    b 2f
1:  movs r2, #0
2:  ldr r0, [r5, #0x18]
    pop {pc}

@ ---- opening flash count: field 0 ----

    .align 2
    .thumb_func
HookFlash2:                     @ movs r0, #2; str r0, [sp]   (grunt, Kimono, three ball intros)
    push {lr}
    movs r0, #0
    movs r1, #2
    bl GetTiming
    str r0, [sp, #4]
    pop {pc}

    .align 2
    .thumb_func
HookFlashBalls:                 @ adds r2, r1, #0; str r0, [sp]   (r0 = 2; keep r1 and r3)
    push {r1, r3, lr}
    movs r0, #0
    movs r1, #2
    bl GetTiming
    str r0, [sp, #12]
    pop {r1, r3}
    adds r2, r1, #0
    pop {pc}

    .align 2
    .thumb_func
HookFlashAdmin:                 @ adds r2, r1, #0; str r0, [sp]   (r0 = 1 stays)
    push {r0, r1, lr}
    movs r0, #0
    movs r1, #1
    bl GetTiming
    str r0, [sp, #12]
    pop {r0, r1}
    adds r2, r1, #0
    pop {pc}

    .align 2
    .thumb_func
HookFlashGym:                   @ movs r1, #0x10; str r0, [sp]   (r0 = 1 stays)
    push {r0, lr}
    movs r0, #0
    movs r1, #1
    bl GetTiming
    str r0, [sp, #8]
    pop {r0}
    movs r1, #0x10
    pop {pc}

    .align 2
    .thumb_func
HookFlashLeague:                @ adds r3, r6, #4; str r0, [sp]   (r0, r1, r2 stay)
    push {r0, r1, r2, lr}
    movs r0, #0
    movs r1, #1
    bl GetTiming
    str r0, [sp, #16]
    pop {r0, r1, r2}
    adds r3, r6, #4
    pop {pc}

    .align 2
Table:
    .fill 256 * 8, 1, 0
End:
