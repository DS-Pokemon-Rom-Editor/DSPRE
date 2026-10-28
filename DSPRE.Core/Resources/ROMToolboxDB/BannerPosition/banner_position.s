@ DSPRE place-name banner position patch (Platinum overlay 5, HeartGold and SoulSilver overlay 1).
@ Rewrites the 14 instructions before the window function's call to Window_Add without moving anything.
@ The game passes x and y as the same zero register, so neither can change. Storing y with width and height with palette
@ frees an instruction to load x.
@ DSPRE fills Y, X, H and PAL from the banner layout, and K so the load reaches the base tile literal
@ the game already has (k = (literal - ((Window + 0x0E + 4) & ~3)) / 4).

    .syntax unified
    .thumb

    .equ Y, 0                   @ row, in tiles
    .equ X, 0                   @ column, in tiles
    .equ H, 5                   @ window height, in tiles
    .equ PAL, 7                 @ Platinum 7, HGSS 11
    .equ K, 5

Window:
    movs r2, #Y                 @ sp+0: y
    movs r3, #(32 - X)          @ sp+4: width, the rest of the row
    add r1, sp, #0
    stmia r1!, {r2, r3}
    movs r2, #H                 @ sp+8: height
    movs r3, #PAL               @ sp+12: palette
    stmia r1!, {r2, r3}
    ldr r2, [pc, #(K * 4)]      @ sp+16: base tile
    str r2, [r1]
    movs r3, #X                 @ x
    adds r1, r0, #0
    adds r1, #32                @ the window
    ldr r0, [r0, #28]           @ the BG config
    movs r2, #3                 @ BG layer 3
    @ bl Window_Add follows, unchanged
