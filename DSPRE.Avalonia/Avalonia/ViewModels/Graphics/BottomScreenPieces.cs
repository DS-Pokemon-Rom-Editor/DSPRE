using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using DSPRE.Avalonia.Data;
using DSPRE.Editors;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Graphics
{
    public sealed class BottomScreenSwatch
    {
        /// <summary>Index across the whole palette file (row * 16 + colour), not within the row.</summary>
        public int At { get; init; }
        public uint Argb { get; init; }
        public IBrush Fill { get; init; }
        public string Tip { get; init; }
    }

    public sealed class BottomScreenPieceRow
    {
        public BottomScreenPiece Piece { get; init; }
        public string Name => Piece.Name;
        public string Where => Piece.Where;
    }

    public sealed class BottomScreenPiece
    {
        public string Name;
        public string What;
        public DirNames Archive;
        public int Drawing = -1;        // the picture; a PNG import replaces it
        public int Arrangement = -1;    // NSCR, where its tiles go
        public int Cells = -1;          // NCER, how its pieces are grouped
        public int Animation = -1;      // NANR, the frame order
        public int Sprites = -1;        // the sheet the cells take their tiles from
        public int SharedSheet = -1;    // a sheet loaded before it; tile numbers start after it
        public int PaletteMember = -1;
        public int PaletteRow;
        public int PaletteRows = 1;     // more than one when the piece is painted from several rows

        public string SharedWith;

        public string ReadOnlyBecause;

        /// <summary>The most tiles the drawing may have, or 0 when nothing is known to limit it.</summary>
        public int TileRoom;

        /// <summary>For a touch menu icon, which of overlay 27's icon kinds it is, or -1.</summary>
        public int IconKind = -1;

        public string Warning;

        public string Where
        {
            get
            {
                List<string> bits = new List<string>();
                if (Drawing >= 0) bits.Add("drawing");
                if (Arrangement >= 0 || Cells >= 0) bits.Add("layout");
                if (Animation >= 0) bits.Add("animation");
                if (PaletteMember >= 0) bits.Add("colours");
                string where = string.Join(", ", bits);
                return where.Length == 0 ? where : char.ToUpperInvariant(where[0]) + where.Substring(1);
            }
        }
    }
}
