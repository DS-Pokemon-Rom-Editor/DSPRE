[Research](../../../ResearchNotes.md) / [Pokemon Resear…](../../PokemonResearch.md) / [Sprites Resear…](../SpritesResearch.md) / [Alt Form Sprit…](AltFormSpritesResearch.md) / Alt Form Sprit…

Notes for [AltFormSpritesLogic.md](AltFormSpritesLogic.md).

covers battle/box sprite selection for alt-form species. icon selection is separate (Pokemon/Icons/IconsLogic.md)

GetMonSpriteCharAndPlttNarcIdsEx (src/pokemon.c)
  form = sub_02070438(species, form) first
  switch(species): 13 species -> NARC_poketool_pokegra_otherpoke instead of NARC_poketool_pokegra_pokegra:
    Burmy, Wormadam, Shellos, Gastrodon, Cherrim, Arceus, Castform, Deoxys, Unown, Shaymin, Rotom, Giratina, Pichu
  + SPECIES_EGG (egg, Manaphy egg by form) and SPECIES_MANAPHY_EGG (bad egg) also -> otherpoke
  each has own charDataID/palDataID base + form*2 or form offset -> forms sit at consecutive indices in otherpoke.narc
  default case: pokegra.narc, charDataID = species*6 + whichFacing + (gender==FEMALE?0:1), palDataID = shiny + species*6+4
  SPINDA special case in default, front only: isAnimated=FALSE, personality carried through (spot pattern)

sub_02070438 (src/pokemon.c) = form clamp
  per-species <SPECIES>_FORM_MAX constant, form > MAX-1 -> reset to 0
  runs for same 13 species (minus the 2 egg entries)

DSPRE side:
  AlternateFormSprites.Form (Avalonia/Data/AlternateFormSprites.cs) { Name, BackSpriteIndex, FrontSpriteIndex, NormalPaletteIndex, ShinyPaletteIndex }
  IsAlternateForms / VariantNames / SelectedVariantIndex (PokemonSpriteEditorViewModel) drive the picker; AlternateFormSprites.WhoOwns maps a file back to its form
  RomInfo.SetNarcDirs otherPokemonBattleSprites: poketool/pokegra/otherpoke.narc (DP), poketool/pokegra/pl_otherpoke.narc (Pt), a/1/1/4 (HGSS)

GetMonPicHeightBySpeciesGenderForm (pokemon.c) = height-table equivalent of GetMonSpriteCharAndPlttNarcIdsEx
  same sub_02070438 clamp, same 13-species list, reads one byte via ReadWholeNarcMemberByIdPair
  13 species -> NARC_poketool_pokegra_height_o (otherpoke.narc's height counterpart); from Shaymin on its fileIds differ from the sprite redirect's (0x88/0x8C/0x98/0x9C against 0x86/0x8A/0x96/0x9A)
  default -> NARC_poketool_pokegra_height, fileId = species*4 + whichFacing + gender

GetMonPicHeightBySpeciesGenderForm_PBR (pokemon.c) = Pokemon Battle Revolution equivalent
  uses NARC_pbr_dp_height_o / NARC_pbr_dp_height instead
  Shaymin/Rotom/Giratina: form!=0 -> NARC_poketool_pokegra_height_o (the DS table, not a pbr one), form==0 -> NARC_pbr_dp_height default formula
  Pichu spiky-ear case present but commented out (disabled, not removed)
