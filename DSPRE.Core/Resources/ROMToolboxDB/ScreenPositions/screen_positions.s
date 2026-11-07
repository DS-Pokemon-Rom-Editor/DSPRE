@ Screen positions: routines that set two values the game kept in one register, each on its own.
@ DSPRE writes these halfwords itself (ScreenPositionsPatch.cs); each place's two instructions become a bl to its routine.
@ Every place's function pushes lr, so the bl is safe. The immediates marked X, Y, ROW, COL and BOTTOM are the Layout spots.

.thumb

@ Platinum start menu ball count, in place of: movs r3, #1 / str r3, [sp]
BallCount:
    movs r3, #1        @ Y
    str  r3, [sp]
    movs r3, #1        @ X
    bx   lr

@ Platinum bag Berries menu, in place of: movs r3, #23 / subs r1, r3, r0
BerryActions:
    movs r1, #23       @ BOTTOM (top row = bottom - 2 per choice)
    subs r1, r1, r0
    movs r3, #23       @ X
    bx   lr

@ Platinum PC markings, in place of: movs r2, #153 / lsls r2, r2, #2  (row * 32 + column)
@ DSPRE keeps the literal pool's byte offset, 2 * (row * 32 + column), in step.
Markings:
    movs r2, #19       @ ROW
    lsls r2, r2, #5
    adds r2, #4        @ COL
    bx   lr

@ Platinum Options title and entries: mov r3, r2 becomes movs r3, #X in place (no routine).

@ HeartGold and SoulSilver trainer card Hall of Fame mark, first copy.
@ In place of: movs r3, #7 / str r3, [sp]
HallOfFameY:
    movs r3, #7        @ width
    str  r3, [sp]
    movs r3, #7        @ Y
    bx   lr

@ In place of: movs r2, #0 / str r2, [sp, #12] / str r2, [sp, #16]  (the last becomes a nop)
HallOfFameX:
    movs r2, #0
    str  r2, [sp, #12]
    str  r2, [sp, #16]
    movs r2, #0        @ X
    bx   lr

@ After the League, in place of: movs r2, #0 / str r2, [sp, #16]  (its y is its own movs r3, #14)
HallOfFameXAfterLeague:
    movs r2, #0
    str  r2, [sp, #16]
    movs r2, #0        @ X
    bx   lr

@ HeartGold and SoulSilver bag item action window, in place of: movs r1, #1 / str r1, [sp]
ItemActionY:
    movs r1, #1        @ Y
    str  r1, [sp]
    movs r1, #1        @ the base tile the code stores later
    bx   lr

@ In place of: movs r3, #2 / str r3, [sp, #8]
ItemActionX:
    movs r3, #2        @ height
    str  r3, [sp, #8]
    movs r3, #2        @ X
    bx   lr
