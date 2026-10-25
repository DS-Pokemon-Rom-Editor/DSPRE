@ DSPRE HP bar fonts, for HeartGold and SoulSilver.
@ The block holds summary_fonts.s without its table, then this stub. Battle's FontID_Alloc(4) and FontID_Release(4) go
@ through LoadFonts and ReleaseFonts, so fonts 2 and 5 load and free along with font 4. The HP bar name print and the two Safari and Park ball
@ prints go through Print here, which sets the font. Each of those prints passes x = 0 in r3, so the stub clears it again
@ after borrowing it. DSPRE writes the font byte (HpBarFontPatch.WriteFont) and fills PrintAt per ROM.
@ Build: arm-none-eabi-as -mcpu=arm946e-s -mthumb, then objcopy -O binary.

    .syntax unified
    .thumb

    .align 2
    .thumb_func
Print:                          @ r0 = window, r2 = string, r1 and r3 = 0
    movs r1, #0                 @ the font
    ldr r3, PrintAt
    mov ip, r3
    movs r3, #0
    bx ip
    nop

    .align 2
PrintAt:      .word 0           @ AddTextPrinterParameterizedWithColorAndSpacing | 1
