[Research](../../ResearchNotes.md) / [Pokemon Resear…](../PokemonResearch.md) / Icons Logic

# Icons Logic, HeartGold/SoulSilver

Source: [pokeheartgold decomp](https://github.com/pret/pokeheartgold). This was structured into a document with AI.

This covers the party/box icon sprite sheet (`poke_icon.narc`), a separate system from the battle/box sprite otherpoke redirect covered in `Pokemon/Sprites/AltForms/AltFormSpritesLogic.md`.

## Lookup functions

`include/pokemon_icon_idx.h` declares four functions, all implemented in `src/pokemon_icon_idx.c`:

```c
u32 GetMonIconNaixEx(u32 species, BOOL isEgg, u32 form);
u32 GetBattleMonIconNaixEx(u32 species, BOOL isEgg, u32 form);
const u8 GetMonIconPaletteEx(u32 species, u32 form, u32 isEgg);
const u8 GetBattleMonIconPaletteEx(u32 species, u32 form, BOOL isEgg);
```

`Pokemon_GetIconNaix` calls `Boxmon_GetIconNaix`, which reads species/isEgg/form off a `BoxPokemon` and calls `GetMonIconNaixEx` (`src/pokemon_icon_idx.c:11-29`).

`BoxMonGetForm` (`:85`) only reads a nonzero form for Unown (its letter, via `GetBoxMonUnownLetter`), Deoxys, Shellos, Gastrodon, Burmy, Wormadam, Giratina, Shaymin, and Rotom. Every other species is forced to form 0 for icon purposes.

## GetMonIconNaixEx

```c
u32 GetMonIconNaixEx(u32 species, BOOL isEgg, u32 form) {
    if (isEgg == TRUE) {
        if (species == SPECIES_MANAPHY) {
            return 502;
        } else {
            return 501;
        }
    }

    form = sub_02070438(species, form);
    if (form != 0) {
        if (species == SPECIES_DEOXYS) {
            return form + 503 - 1;
        } else if (species == SPECIES_UNOWN) {
            return form + 507 - 1;
        } else if (species == SPECIES_BURMY) {
            return form + 534 - 1;
        } else if (species == SPECIES_WORMADAM) {
            return form + 536 - 1;
        } else if (species == SPECIES_SHELLOS) {
            return form + 538 - 1;
        } else if (species == SPECIES_GASTRODON) {
            return form + 539 - 1;
        } else if (species == SPECIES_GIRATINA) {
            return form + 540 - 1;
        } else if (species == SPECIES_SHAYMIN) {
            return form + 541 - 1;
        } else if (species == SPECIES_ROTOM) {
            return form + 542 - 1;
        }
    }
    if (species > MAX_SPECIES) {
        species = 0;
    }
    return species + 7;
}
```

`sub_02070438` is the exact same form-clamp helper used by the battle-sprite otherpoke redirect (see `AltFormSpritesLogic.md`). A plain species with form 0 lands on `species + 7`: the icon sheet reserves the first 7 slots for non-species icons (egg placeholders and similar) before the per-species entries begin.

`GetBattleMonIconNaixEx` (`:68`) wraps the same function, only adding two more form-aware cases on top for the battle-only icon set:

```c
u32 GetBattleMonIconNaixEx(u32 species, BOOL isEgg, u32 form) {
    if (!isEgg) {
        if (species == SPECIES_CASTFORM) {
            form = sub_02070438(species, form);
            if (form != 0) {
                return form + 547 - 1;
            }
        } else if (species == SPECIES_CHERRIM) {
            form = sub_02070438(species, form);
            if (form != 0) {
                return form + 550 - 1;
            }
        }
    }
    return GetMonIconNaixEx(species, isEgg, form);
}
```

Castform and Cherrim only get alternate icon frames in the battle-only set; the party/box icon set (`GetMonIconNaixEx`) does not branch on them at all.

## GetMonIconPaletteEx

```c
const u8 GetMonIconPaletteEx(u32 species, u32 form, u32 isEgg) {
    if (isEgg == TRUE) {
        if (species == SPECIES_MANAPHY) {
            species = 495;
        } else {
            species = 494;
        }
    } else if (species > MAX_SPECIES) {
        species = 0;
    } else if (form != 0) {
        if (species == SPECIES_DEOXYS) {
            species = 496 + form - 1;
        } else if (species == SPECIES_UNOWN) {
            species = 499 + form - 1;
        } else if (species == SPECIES_BURMY) {
            species = 527 + form - 1;
        } else if (species == SPECIES_WORMADAM) {
            species = 529 + form - 1;
        } else if (species == SPECIES_SHELLOS) {
            species = 531 + form - 1;
        } else if (species == SPECIES_GASTRODON) {
            species = 532 + form - 1;
        } else if (species == SPECIES_GIRATINA) {
            species = 533 + form - 1;
        } else if (species == SPECIES_SHAYMIN) {
            species = 534 + form - 1;
        } else if (species == SPECIES_ROTOM) {
            species = 535 + form - 1;
        }
    }
    return sPokemonPalNoBySpeciesAndForm[species];
}
```

The palette index is not a formula on its own, it is a lookup into a real array, `sPokemonPalNoBySpeciesAndForm` (`src/pokemon_icon_idx.c:103`), keyed by the same remapped species/form/egg index built above. The array is fully decompiled, around 546 entries long, one byte per icon slot.

`GetBattleMonIconPaletteEx` (`:683`) mirrors `GetBattleMonIconNaixEx`: Castform and Cherrim index straight into `sPokemonPalNoBySpeciesAndForm` at their own offsets (`540 + form - 1` and `543 + form - 1`) when they have a nonzero form, otherwise it falls through to `GetMonIconPaletteEx`.

## What DSPRE already does

`RomInfo.cs:2212` maps `DirNames.monIcons` to `poketool\icongra\poke_icon.narc` for HGSS; `:2327` maps it to `pl_poke_icon.narc` for Platinum; `:2406` points a DP-generation game code straight at the raw archive path `a\0\2\0`.

DSPRE already reads a real in-ROM address, `RomInfo.monIconPalTableAddress`, for the per-species icon palette table (`DS_Map/DSUtils/DSUtils.cs`, around lines 115-127 and 1295-1303), which is the same table as `sPokemonPalNoBySpeciesAndForm` above.

DSPRE does not reimplement the `GetMonIconNaixEx` index arithmetic. The unpacked `poke_icon.narc` already has one NCGR/NCER file per icon slot, and DSPRE reads those files directly by their existing filename (`DSUtils.cs:1230`, `:1279`, `:1319`, `:1324`), so the runtime index math above is only needed by the game itself, not by an editor working against the unpacked archive.

