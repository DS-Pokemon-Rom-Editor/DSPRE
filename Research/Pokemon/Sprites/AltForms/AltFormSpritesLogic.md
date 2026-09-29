[Research](../../../ResearchNotes.md) / [Pokemon Resear…](../../PokemonResearch.md) / [Sprites Resear…](../SpritesResearch.md) / [Alt Form Sprit…](AltFormSpritesResearch.md) / Alt Form Sprit…

# Alt Form Sprites Logic, HeartGold/SoulSilver

Source: [pokeheartgold decomp](https://github.com/pret/pokeheartgold). This was structured into a document with AI.

This covers battle/box sprite selection for species with alternate forms. Icon sprite selection for the same species is a separate system, covered in `Pokemon/Icons/IconsLogic.md`.

## GetMonSpriteCharAndPlttNarcIdsEx

`src/pokemon.c`. Given a species, gender, facing, shininess, form, and personality value, this fills in which NARC and which character/palette index inside it to load:

```c
void GetMonSpriteCharAndPlttNarcIdsEx(PokepicTemplate *pokepicTemplate, u16 species, u8 gender, u8 whichFacing, u8 shiny, u8 form, u32 personality) {
    pokepicTemplate->species = SPECIES_NONE;
    pokepicTemplate->isAnimated = FALSE;
    pokepicTemplate->personality = 0;
    form = sub_02070438(species, form);
    switch (species) {
    case SPECIES_BURMY:
        pokepicTemplate->narcID = NARC_poketool_pokegra_otherpoke;
        pokepicTemplate->charDataID = (u16)(whichFacing / 2 + 0x48 + form * 2);
        pokepicTemplate->palDataID = (u16)(shiny + 0xAA + form * 2);
        break;
    ...
    default:
        pokepicTemplate->narcID = NARC_poketool_pokegra_pokegra;
        pokepicTemplate->charDataID = (u16)(species * 6 + whichFacing + (gender == MON_FEMALE ? 0 : 1));
        pokepicTemplate->palDataID = (u16)(shiny + (species * 6 + 4));
        break;
    }
}
```

13 species redirect to `NARC_poketool_pokegra_otherpoke` instead of the default `NARC_poketool_pokegra_pokegra`: Burmy, Wormadam, Shellos, Gastrodon, Cherrim, Arceus, Castform, Deoxys, Unown, Shaymin, Rotom, Giratina, Pichu. Two more entries in the same switch also point at `otherpoke`: `SPECIES_EGG`, which covers the egg and, by form, the Manaphy egg, and `SPECIES_MANAPHY_EGG`, which pokeheartgold marks as the bad egg. Every other species falls through to the `default` case and reads `pokegra.narc` directly by `species * 6 + whichFacing + gender` arithmetic.

Each of the 13 species has its own fixed `charDataID`/`palDataID` base offset plus `form * 2` or `form` added on top, so forms of the same species sit at consecutive indices inside `otherpoke.narc`. Spinda is called out as a special case in the `default` branch, for its front picture only: it sets `pokepicTemplate->isAnimated = FALSE` and carries the personality value through, for its personality-seeded spot pattern.

## Form clamping

`sub_02070438` (`src/pokemon.c`) runs before the switch above and normalizes the form value per species:

```c
u8 sub_02070438(u16 species, u8 form) {
    switch (species) {
    case SPECIES_BURMY:
        if (form > BURMY_FORM_MAX - 1) {
            form = 0;
        }
        break;
    case SPECIES_WORMADAM:
        if (form > WORMADAM_FORM_MAX - 1) {
            form = 0;
        }
        break;
    ...
    }
}
```

Each of the 13 species has its own `<SPECIES>_FORM_MAX` constant; the clamp also handles `SPECIES_EGG`. A form value past that constant is silently reset to 0 (the base form) rather than reading out of bounds.

## What DSPRE already does

DSPRE's Sprite Editor already implements the same otherpoke redirect independently. `AlternateFormSprites.Form` in `DSPRE.Avalonia/Avalonia/Data/AlternateFormSprites.cs` holds one entry per alternate form: a name, back/front sprite indices and normal/shiny palette indices.

`IsAlternateForms`, `VariantNames` and `SelectedVariantIndex` in `PokemonSpriteEditorViewModel` drive the form picker, and `AlternateFormSprites.WhoOwns` answers which form owns a given file of the archive.

The `otherPokemonBattleSprites` entry of `SetNarcDirs` in `DSPRE.Core/RomInfo.cs` points at `poketool\pokegra\otherpoke.narc` for Diamond and Pearl, `poketool\pokegra\pl_otherpoke.narc` for Platinum and `a\1\1\4` for HeartGold and SoulSilver.

## Form height lookup

`GetMonPicHeightBySpeciesGenderForm` (`src/pokemon.c`) is the height-table equivalent of `GetMonSpriteCharAndPlttNarcIdsEx`, and reuses the same `sub_02070438` form clamp and the same 13-species list. It reads a single byte out of a NARC member instead of filling in a template:

```c
u8 GetMonPicHeightBySpeciesGenderForm(u16 species, u8 gender, u8 whichFacing, u8 form, u32 pid) {
    NarcId narcId;
    s32 fileId;
    u8 ret;

    form = sub_02070438(species, form);
    switch (species) {
    case SPECIES_BURMY:
        narcId = NARC_poketool_pokegra_height_o;
        fileId = 0x48 + whichFacing / 2 + form * 2;
        break;
    ...
    default:
        narcId = NARC_poketool_pokegra_height;
        fileId = species * 4 + whichFacing + (gender != MON_FEMALE ? 1 : 0);
        break;
    }
    ReadWholeNarcMemberByIdPair(&ret, narcId, fileId);
    return ret;
}
```

`NARC_poketool_pokegra_height_o` is the height-table counterpart to `otherpoke.narc`, holding one byte per facing/form for the same 13 alt-form species (plus the two egg entries). Every other species reads `NARC_poketool_pokegra_height` (the default per-species table) at `species * 4 + whichFacing + gender`.

`GetMonPicHeightBySpeciesGenderForm_PBR` is the Pokemon Battle Revolution equivalent, reading `NARC_pbr_dp_height_o`/`NARC_pbr_dp_height` instead. For Shaymin, Rotom, and Giratina it reads the DS `height_o` table, `NARC_poketool_pokegra_height_o`, when `form != 0`; at form 0 it falls back to `NARC_pbr_dp_height` with the plain `species * 4 + whichFacing + gender` formula, the same shape as the `default` case. Pichu's spiky-ear form is present as a commented-out case in this function, `narcId = NARC_pbr_dp_height_o; fileId = 0x9C + whichFacing / 2 + form * 2;`, disabled rather than removed.
