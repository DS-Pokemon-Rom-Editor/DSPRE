using System;
using System.Collections.Generic;
using System.Linq;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.Data
{
    /// <summary>The game's Pokédex pages, drawn with a sample Pokémon.</summary>
    public sealed class PokedexBook : ScreenBook
    {
        private readonly DexSample _sample;
        private readonly IReadOnlyList<ScreenPage> _pages;

        /// <param name="species">The Pokémon the pages are shown with; 0 keeps the usual sample.</param>
        public PokedexBook(int species = 0)
        {
            _sample = new DexSample(species);
            _pages = DexPageLanguages.ForLoadedRom();
        }

        public override string Title => "Pokédex graphics";
        public override DirNames Archive => DirNames.PokedexGraphics;
        public override IReadOnlyList<ScreenPage> Pages => _pages;

        public override ScreenPageComposer MakeComposer(Func<int, FieldFont> font) => ComposerFor(_sample, font);

        // The sample's sprite, icon and footprint come from editors of their own.
        public override DirNames[] PictureArchives => new[] { DirNames.PokemonBattleSprites, DirNames.MonIcons, DirNames.FootprintGraphics };

        public override int SpriteDrawing(ScreenSprite s) =>
            s.TypeSlot > 0 && s.TypeDrawing >= 0 ? s.TypeDrawing + Math.Max(0, _sample.TypeSlot(s.TypeSlot)) : s.Drawing;

        public override IEnumerable<(string Name, string Where, DirNames Dir, int Member)> MonParts(ScreenMon m)
        {
            int species = _sample.Species + m.Offset;
            switch (m.Kind)
            {
                case ScreenMonKind.Front:
                case ScreenMonKind.Back:
                    int first = species * 6;
                    int face = m.Kind == ScreenMonKind.Front ? first + 3 : first + 1;
                    yield return (m.Kind == ScreenMonKind.Front ? "Front" : "Back", "Battle sprites " + face, DirNames.PokemonBattleSprites, face);
                    yield return ("Colours", "Battle sprites " + (first + 4), DirNames.PokemonBattleSprites, first + 4);
                    break;
                case ScreenMonKind.Footprint:
                    int foot = GraphicAssets.FootprintEntry(species);
                    if (foot >= 0) yield return ("Footprint", "Footprints " + foot, DirNames.FootprintGraphics, foot);
                    break;
                case ScreenMonKind.Icon:
                    yield return ("Icon", "Party icons " + (species + 7), DirNames.MonIcons, species + 7);
                    break;
            }
        }

        public override void Forget() => _sample.Forget();

        /// <summary>A composer drawing the game's Pokédex pages with <paramref name="sample"/>'s pictures and values.</summary>
        internal static ScreenPageComposer ComposerFor(DexSample sample, Func<int, FieldFont> font)
        {
            ScriptNarc dex = null;
            try
            {
                DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.PokedexGraphics, DirNames.FootprintGraphics,
                                                            DirNames.PokemonBattleSprites, DirNames.MonIcons,
                                                            DirNames.PokedexAreas, DirNames.PokedexSearchSteps });
                dex = new ScriptNarc(DirNames.PokedexGraphics);
            }
            catch (Exception ex) { AppLogger.Error("Pokédex graphics: " + ex.Message); }
            ScriptNarc archive = dex;
            return new ScreenPageComposer(i => archive?.Get(i), font, sample.Text, sample.Picture, sample.TypeSlot,
                                          i => ScreenGraphicsLayouts.BankFor(DirNames.PokedexGraphics, i))
            {
                Habitat = sample.Habitat,
                Size = sample.SizeInfo,
                Digit = sample.SearchDigit,
            };
        }

        /// <summary>The game's size comparison page: DP and Platinum's height page, HGSS's size page.</summary>
        internal static ScreenPage SizePage(IReadOnlyList<ScreenPage> pages) =>
            pages.FirstOrDefault(p => p.Id == "size_height") ?? pages.FirstOrDefault(p => p.Id == "size");
    }
}
