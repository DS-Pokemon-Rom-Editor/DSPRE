@ DSPRE party menu fonts, for HeartGold and SoulSilver.
@ The block holds summary_fonts.s unchanged, with a table of party menu windows, then this stub. The party menu's name,
@ gender and compatibility prints go through Print, and its FontID_Release(4) through ReleaseFonts. The menu prints its
@ panels before its own FontID_Alloc(4), so fonts 2 and 5 load here, on the way into PartyMenu_AddAllWindows.
@ DSPRE fills the heap and the two addresses per ROM (PartyFontPatch.Stub).
@ Build: arm-none-eabi-as -mcpu=arm946e-s -mthumb, then objcopy -O binary.

    .syntax unified
    .thumb

    .align 2
    .thumb_func
LoadEarly:                      @ r0 = the party menu
    push {r4, lr}
    adds r4, r0, #0
    movs r0, #2
    movs r1, #0                 @ the heap, from the movs r1 before FontID_Alloc(4)
    ldr r3, AllocAt
    blx r3
    movs r0, #5
    movs r1, #0                 @ the heap again
    ldr r3, AllocAt
    blx r3
    adds r0, r4, #0
    ldr r3, AddWindowsAt
    blx r3
    pop {r4, pc}

    .align 2
AllocAt:      .word 0           @ FontID_Alloc | 1
AddWindowsAt: .word 0           @ PartyMenu_AddAllWindows | 1
