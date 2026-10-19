@ DSPRE clear battle text background, for Diamond, Pearl, Platinum, HeartGold and SoulSilver.
@ The battle draws a bar's name, level and HP numbers into a buffer filled with colour 15 and copies it over the bar
@ (HealthBox_DrawBattlerName, _DrawLevelNumber, _DrawCurrentHP and _DrawMaxHP in pokeplatinum src/battle/healthbox.c;
@ ov12_0226516C, _02265354, _02265474 and _02265500 in pokeheartgold). Each of those copies goes through Merge instead,
@ which keeps the bar's own pixel wherever the buffer has colour 15. The bar's untouched tiles stay in RAM for the whole
@ battle as its tile resource; each routine's call for the bar's image proxy goes through Hook, which notes where
@ they are. DSPRE fills ProxyGetter and Copy per ROM, and OffsetAt with the address of Offset.
@ Build: arm-none-eabi-as -mcpu=arm946e-s -mthumb, then objcopy -O binary.

    .syntax unified
    .thumb

@ Replaces "ldr r0, [r0]; bl Sprite_GetImageProxy" with "bl Hook; nop". r0 = the bar's ManagedSprite.
    .align 2
    .thumb_func
Hook:
    ldr r1, [r0, #4]            @ resource header
    ldr r1, [r1, #0]            @ image proxy, 0x10 into the tile transfer task
    ldr r2, [r1, #4]            @ the bar's main OBJ VRAM offset
    subs r1, #0x10
    ldr r1, [r1, #0]            @ the task's NNSG2dCharacterData
    ldr r1, [r1, #0x14]         @ pRawData: the bar's tiles as loaded
    subs r1, r1, r2
    ldr r2, ObjVram
    subs r1, r1, r2
    ldr r2, OffsetAt
    str r1, [r2, #0]            @ untouched tile = VRAM address + Offset
    ldr r0, [r0, #0]            @ the Sprite, as the replaced load did
    ldr r1, ProxyGetter
    bx r1

@ Replaces the MI_CpuCopy16 of a text buffer onto the bar: r0 = buffer, r1 = VRAM, r2 = bytes (even).
    .thumb_func
Merge:
    push {r4-r7, lr}
    ldr r4, OffsetAt
    ldr r4, [r4, #0]
    adds r4, r4, r1             @ the untouched tiles under r1
    ldr r3, Nibbles
    mov ip, r3
    b 2f                        @ a copy of no bytes writes nothing
1:  ldrh r5, [r0, #0]
    ldrh r6, [r4, #0]
    lsrs r7, r5, #1
    ands r7, r5
    lsrs r3, r7, #2
    ands r7, r3
    mov r3, ip
    ands r7, r3                 @ bit 0 of each nibble that is 15
    lsls r3, r7, #4
    subs r7, r3, r7             @ those nibbles all set
    ands r6, r7
    bics r5, r7
    orrs r5, r6
    strh r5, [r1, #0]           @ VRAM takes halfwords only
    adds r0, #2
    adds r1, #2
    adds r4, #2
2:  subs r2, #2
    bge 1b
    pop {r4-r7, pc}

    .align 2
ObjVram:     .word 0x06400000
Nibbles:     .word 0x1111
ProxyGetter: .word 0            @ Sprite_GetImageProxy | 1
OffsetAt:    .word 0            @ the address of Offset
Copy:        .word 0            @ MI_CpuCopy16, kept so the patch can be taken out
Offset:      .word 0            @ written by Hook at run time
