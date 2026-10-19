using System;
using System.Collections.Generic;
using System.Linq;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// The Pokédex pages as the loaded ROM's language shows them. The recipes describe the English games; a Japanese
    /// game's other-language page offers English to Spanish, with Japanese on the first button.
    /// </summary>
    public static class DexPageLanguages
    {
        private static readonly string[] EnglishTabs = { "French", "German", "Italian", "Spanish", "Japanese" };
        private static readonly string[] JapaneseTabs = { "English", "French", "German", "Italian", "Spanish" };

        public static IReadOnlyList<ScreenPage> ForLoadedRom()
        {
            IReadOnlyList<ScreenPage> pages = DexPageRecipes.For(gameFamily);
            if (gameLanguage != GameLanguages.Japanese) return pages;
            foreach (ScreenPage page in pages.Where(p => p.Id == "info_foreign")) ToJapanese(page);
            return pages;
        }

        private static void ToJapanese(ScreenPage page)
        {
            page.Variants = (string[])JapaneseTabs.Clone();
            foreach (ScreenLayout screen in new[] { page.Main, page.Sub })
            {
                foreach (ScreenText text in screen.Texts)
                {
                    int tab = Array.IndexOf(EnglishTabs, text.When);
                    if (tab < 0) continue;
                    text.When = JapaneseTabs[tab];
                    // The tab's title names the language: lines 22 (English) to 26 (Spanish) of the Pokédex messages.
                    if (text.Kind == ScreenTextKind.Message) text.Line = 22 + tab;
                }

                int first = screen.Sprites.FindIndex(s => s.Name == "Language");
                if (first < 0) continue;
                ScreenSprite model = screen.Sprites[first];
                screen.Sprites.RemoveAll(s => s.Name == "Language");
                // Six buttons 40 pixels apart. Each language has an off and an on sequence, Japanese 0 and 1, English 2
                // and 3, and so on to Spanish 10 and 11; the first button is the game's own language and stays off.
                List<ScreenSprite> buttons = new List<ScreenSprite>();
                for (int tab = 0; tab < JapaneseTabs.Length; tab++)
                    for (int slot = 0; slot < 6; slot++)
                        buttons.Add(Button(model, slot == 0 ? 0 : 2 * slot + (slot - 1 == tab ? 1 : 0), model.X + 40 * slot, JapaneseTabs[tab]));
                screen.Sprites.InsertRange(first, buttons);
            }
        }

        private static ScreenSprite Button(ScreenSprite model, int seq, int x, string when) => new ScreenSprite
        {
            Name = model.Name, Cells = model.Cells, Drawing = model.Drawing, Anim = model.Anim, Seq = seq, Frame = model.Frame,
            Colours = model.Colours, Width = model.Width, Height = model.Height, Row = model.Row, X = x, Y = model.Y,
            Priority = model.Priority, TypeSlot = model.TypeSlot, TypeDrawing = model.TypeDrawing, Size = model.Size,
            Readout = model.Readout, Step = model.Step, Place = model.Place, When = when,
        };
    }
}
