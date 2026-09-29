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

`Pokemon_GetIconNaix` calls `Boxmon_GetIconNaix`, which reads species/isEgg/form off a `BoxPokemon` and calls `GetMonIconNaixEx` (`src/pokemon_icon_idx.c`).

`BoxMonGetForm` only reads a nonzero form for Unown (its letter, via `GetBoxMonUnownLetter`), Deoxys, Shellos, Gastrodon, Burmy, Wormadam, Giratina, Shaymin, and Rotom. Every other species is forced to form 0 for icon purposes.

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

`sub_02070438` is the exact same form-clamp helper used by the battle-sprite otherpoke redirect (see `AltFormSpritesLogic.md`). A plain species with form 0 lands on `species + 7`: the archive begins with 7 files every icon shares, the palettes and the cell and animation files, before the per-species entries; the two eggs are files 501 and 502.

`GetBattleMonIconNaixEx` wraps the same function, only adding two more form-aware cases on top for the battle-only icon set:

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

The palette index is not a formula on its own, it is a lookup into a real array, `sPokemonPalNoBySpeciesAndForm` (`src/pokemon_icon_idx.c`), keyed by the same remapped species/form/egg index built above. The array is fully decompiled, 544 entries long, one byte per icon slot.

`GetBattleMonIconPaletteEx` mirrors `GetBattleMonIconNaixEx`: Castform and Cherrim index straight into `sPokemonPalNoBySpeciesAndForm` at their own offsets (`540 + form - 1` and `543 + form - 1`) when they have a nonzero form, otherwise it falls through to `GetMonIconPaletteEx`.

## What DSPRE already does

The archive is found through the `monIcons` entry of `SetNarcDirs` in `DSPRE.Core/RomInfo.cs`: `poketool\icongra\poke_icon.narc` in Diamond and Pearl, `pl_poke_icon.narc` in Platinum, and `a\0\2\0` in HeartGold and SoulSilver.

The per species palette byte is the same table as `sPokemonPalNoBySpeciesAndForm` above. DSPRE reads and writes it at `RomInfo.monIconPalTableAddress` through `GetMonIconPaletteId` and `SetMonIconPaletteId` in `DSPRE.Core/DSUtils/DSUtils.cs`, after `TryResolveMonIconPalTable` works out whether that address falls in the ARM9 or in the overlay the table was moved to.

The file numbering is carried by `PokemonIconFiles` in `DSPRE.Core/ROMFiles/PokemonIconFiles.cs`, which is the arithmetic of `GetMonIconNaixEx` written as data: seven shared files first, so a species is file `species + 7`, the egg at 501 and the Manaphy egg at 502, and one run of files per form family starting where the formulas above put it (Deoxys at 503, Unown from 506 so that B lands on 507, and so on), with Giratina, Shaymin and Rotom only from Platinum and the battle-only Castform and Cherrim frames only in HeartGold and SoulSilver. `Describe` turns a file number back into a species and form, which is how the icon editors label each file and open the Pokémon Editor entry that owns it.

The pictures themselves are read and written per file by `GetMonIconGraphicRaw`, `ValidateMonIconGraphic` and `SetMonIconGraphic` in `DSUtils.cs`, with the shared palette files read from the same unpacked archive.
