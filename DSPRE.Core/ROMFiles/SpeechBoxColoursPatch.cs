using System;
using System.Collections.Generic;
using System.IO;
using static DSPRE.RomInfo;

namespace DSPRE.ROMFiles
{
    /// <summary>Fills the speech box's empty colours 3 and 4 with the menu row's green and pink, as RefinedPlat worked out.</summary>
    public static class SpeechBoxColoursPatch
    {
        // Entries 7 to 10 of the row are 0000 in every dump but Korean, so those colours print black.
        public const int FirstEntry = 7;
        public static readonly ushort[] Colours = { 0x0F45, 0x47B3, 0x5E5F, 0x737F };

        /// <summary>The font archive member the speech box loads its text colours from.</summary>
        public static int Member => GameFamily == GameFamilies.HGSS ? 8 : 7;

        private static byte[] Read()
        {
            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.Fonts });
            string path = ArchiveMembers.PathOf(DirNames.Fonts, Member);
            return File.Exists(path) ? File.ReadAllBytes(path) : null;
        }

        private static bool Matches(byte[] nclr, Func<int, ushort> expected)
        {
            ushort[] colours = NitroBgCodec.ReadColours(nclr);
            if (colours.Length < FirstEntry + Colours.Length) return false;
            for (int i = 0; i < Colours.Length; i++)
                if (colours[FirstEntry + i] != expected(i)) return false;
            return true;
        }

        public static bool IsApplied => Read() is byte[] nclr && Matches(nclr, i => Colours[i]);

        /// <summary>Why the patch can't go on, or null.</summary>
        public static string WhyNotApplicable()
        {
            byte[] nclr = Read();
            if (nclr == null) return "The speech box's colour file couldn't be read.";
            if (!Matches(nclr, _ => 0)) return "Colours 3 and 4 of the speech box are already set in this game.";
            return null;
        }

        public static void Apply()
        {
            byte[] nclr = Read() ?? throw new IOException("The speech box's colour file couldn't be read.");
            for (int i = 0; i < Colours.Length; i++)
                if (!NitroBgCodec.PutColour(nclr, FirstEntry + i, Colours[i]))
                    throw new InvalidDataException("The speech box's colour file is too short.");
            ArchiveMembers.Put(DirNames.Fonts, Member, nclr);
        }
    }
}
