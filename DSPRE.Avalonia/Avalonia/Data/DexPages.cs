using System;
using System.Collections.Generic;

namespace DSPRE.Avalonia.Data
{
    /// <summary>Which of the two screens a Pokédex page piece is on.</summary>
    public enum DexSide { Main, Sub }

    /// <summary>Where a line of text sits relative to its x.</summary>
    public enum DexAlign
    {
        Left, Centre, Right,
        /// <summary>The lines as one block, each starting at the same x, the block centred on X.</summary>
        Block,
    }

    /// <summary>What a line of Pokédex text shows.</summary>
    public enum DexTextKind
    {
        /// <summary>A fixed line of the Pokédex message bank.</summary>
        Message,
        SpeciesName,
        /// <summary>The species' number, three digits.</summary>
        Number,
        Category,
        Height,
        Weight,
        Entry,
        /// <summary>A count the save file supplies, shown with a sample value.</summary>
        Count,
        /// <summary>The entry, name or category in another language; <see cref="DexText.Line"/> picks it (0 French
        /// to 4 Japanese).</summary>
        ForeignEntry,
        ForeignName,
        ForeignCategory,
        /// <summary>A row of the HGSS area page's place list; <see cref="DexText.Line"/> is the row on screen.</summary>
        Place,
        /// <summary>A step of HGSS's height or weight search slider; <see cref="DexText.Sample"/> is the step.</summary>
        SearchHeight,
        SearchWeight,
        /// <summary>The player's name, shown with the player character's own for the state's gender.</summary>
        PlayerName,
    }

    /// <summary>What a digit sprite shows: nothing, or a digit of a height or weight search step.</summary>
    public enum DexReadout { None, SearchHeight, SearchWeight }

    /// <summary>
    /// How a piece of a size page follows the sample's Pokédex data: the height check scales and lifts the Pokémon
    /// and the player; the weight check tilts the scale, moving the pans and the icons on them.
    /// </summary>
    public enum DexSizeRole { None, PokemonHeight, TrainerHeight, PokemonPan, TrainerPan, PokemonOnPan, TrainerOnPan, Beam }

    /// <summary>The sample's size data for a page state.</summary>
    public sealed class DexSizeInfo
    {
        public int PokemonOffset, PokemonScale = 256, TrainerOffset, TrainerScale = 256;
        /// <summary>The scale's tilt in DS angle units (65536 a turn); positive lowers the player's side.</summary>
        public int Tilt;
    }

    /// <summary>The sample Pokémon's own pictures a page shows.</summary>
    public enum DexMonKind { Front, Footprint, Back, Icon }

    /// <summary>One arrangement pasted into a background at a tile position, or a fill when it has no file.</summary>
    public sealed class DexPiece
    {
        public int Screen = -1;
        public int TileX, TileY;
        /// <summary>Part of the arrangement to copy, in tiles; a negative width copies all of it.</summary>
        public int FromX, FromY, Width = -1, Height = -1;
        /// <summary>For a fill, the entry written into every square.</summary>
        public int Fill = -1;
        /// <summary>Palette row forced onto the copied squares, or -1 to keep theirs.</summary>
        public int Palette = -1;
        public string When;
    }

    /// <summary>One background layer: its drawing and what is pasted into it.</summary>
    public sealed class DexBg
    {
        public int Bg;
        public int Priority;
        public int Bpp = 4;
        public int Drawing = -1;
        /// <summary>Tiles across the layer's map: 32, or 64 for a 512-wide background.</summary>
        public int MapWidth = 32;
        public int MapHeight = 32;
        public int ScrollX, ScrollY;
        public List<DexPiece> Pieces = new();
        public string When;
    }

    /// <summary>A rectangle the game fills with one colour in a background's text window (Platinum's cry line).</summary>
    public sealed class DexRect
    {
        public int Bg = 1;
        public int X, Y, Width, Height;
        /// <summary>Palette row and colour number.</summary>
        public int Row, Colour;
        public string When;
    }

    /// <summary>Colours copied into the background palette: rows of a file into rows of palette memory.</summary>
    public sealed class DexPalette
    {
        public int File;
        public int FromRow;
        public int ToRow;
        public int Rows = 16;
        public string When;
    }

    public sealed class DexSprite
    {
        public string Name;
        /// <summary>The cell layout, or -1 for a drawing shown whole as <see cref="Width"/> by <see cref="Height"/> pixels from its top left.</summary>
        public int Cells, Drawing, Anim = -1, Seq, Colours;
        public int Width, Height;
        /// <summary>Which frame of the animation is shown: buttons show their state this way.</summary>
        public int Frame;
        /// <summary>Added to each piece's own palette number to find its row in the colours file.</summary>
        public int Row;
        public int X, Y;
        /// <summary>Hardware priority 0 (front) to 3; a sprite draws over backgrounds of the same priority.</summary>
        public int Priority;
        /// <summary>
        /// 1 or 2 for the sample Pokémon's first or second type icon. The type picks the animation, or, where
        /// <see cref="TypeDrawing"/> is set (HGSS), the drawing counted from it and that drawing's colours row.
        /// </summary>
        public int TypeSlot;
        public int TypeDrawing = -1;
        public DexSizeRole Size;
        /// <summary>For a digit: the value it is part of, the slider step, and which of the five places it is.</summary>
        public DexReadout Readout;
        public int Step, Place;
        public string When;
    }

    public sealed class DexText
    {
        public DexTextKind Kind;
        /// <summary>The line in the Pokédex message bank for <see cref="DexTextKind.Message"/>.</summary>
        public int Line = -1;
        /// <summary>The layer the text is written into, which gives it that layer's priority.</summary>
        public int Bg = 1;
        public int X, Y;
        public DexAlign Align;
        /// <summary>When set, the lines are centred down a box this tall that starts at <see cref="Y"/>.</summary>
        public int BoxHeight;
        /// <summary>Palette row the colour numbers below are taken from.</summary>
        public int Row;
        public int Ink = 2, Shadow = 1;
        /// <summary>The font archive entry: 0 system, 1 message, 2 sub screen.</summary>
        public int Font;
        /// <summary>The value a <see cref="DexTextKind.Count"/> shows.</summary>
        public int Sample;
        /// <summary>For a list row, how many species after the sample one it names.</summary>
        public int Offset;
        /// <summary>
        /// Text the game draws as sprites, in the sprite colours file given here, at sprite
        /// <see cref="Priority"/>; -1 for text written into background <see cref="Bg"/>.
        /// </summary>
        public int Colours = -1;
        public int Priority;
        /// <summary>For sprite text, the sprite (by position in the list) it is drawn just in front of; -1 for in front of all.</summary>
        public int Above = -1;
        /// <summary>
        /// The text window's rectangle in tiles, which replaces the layer's own squares there; zero size for text
        /// written over the layer. A window the game fills with its background pattern keeps what is under it.
        /// </summary>
        public int WinX, WinY, WinW, WinH;
        public bool KeepUnder;
        /// <summary>Sample values for the line's placeholders, in order.</summary>
        public string[] Args;
        public string When;
    }

    public sealed class DexMon
    {
        public DexMonKind Kind;
        /// <summary>The centre of the picture.</summary>
        public int X, Y;
        public int Priority = 2;
        /// <summary>Sixteenths of the way to black, for a silhouette.</summary>
        public int Darken;
        /// <summary>How many species after the sample one it shows, for a grid of icons.</summary>
        public int Offset;
        public DexSizeRole Size;
        public string When;
    }

    /// <summary>
    /// Where the sample Pokémon lives, drawn the DP/Pt way: a 30 by 30 grid of 5-pixel stamps written into a text
    /// layer (special places first, then the time's own on top), and a sprite on each cave or building.
    /// </summary>
    public sealed class DexHabitat
    {
        public int Bg = 1, Row = 8;
        public int X = 89, Y = 30, Cell = 5;
        /// <summary>The stamp strips: one for the time's own places, one for special ones.</summary>
        public int Stamps = 30, SpecialStamps = 31;
        public int Cells = 106, Drawing = 108, Anim = 107, Colours = 14;
        public int DungeonX = 92, DungeonY = 32;
        /// <summary>Markers already in screen pixels (HGSS's blocks) rather than grid cells.</summary>
        public bool Absolute;
        public int Priority;
        /// <summary>Blend weights in sixteenths for translucent markers (HGSS's), the markers' then what is under them; 0 for solid.</summary>
        public int Blend, BlendUnder;
        /// <summary>The player's face where the player stands (HGSS); no face when Face is -1.</summary>
        public int Face = -1, FaceDrawing = -1, FaceAnim = -1, FaceColours = -1;
    }

    /// <summary>The habitat worked out for one time of day: both grids, row-major by the game's own index, and the caves.</summary>
    public sealed class DexHabitatData
    {
        public byte[] Normal = new byte[900], Special = new byte[900];
        public List<(int X, int Y, int Seq)> Dungeons = new();
        /// <summary>HGSS's place list, sorted as the page lists it; "show all" comes before it.</summary>
        public List<string> Places = new();
        /// <summary>Where the player's face goes, in screen pixels; null when the player is off this map.</summary>
        public (int X, int Y)? Player;
    }

    public sealed class DexScreen
    {
        /// <summary>The habitat map, on the AREA page only.</summary>
        public DexHabitat Habitat;
        /// <summary>Sixteenths of the way to black the whole screen is faded.</summary>
        public int Darken;
        public List<DexBg> Bgs = new();
        public List<DexPalette> Palettes = new();
        public List<DexSprite> Sprites = new();
        public List<DexText> Texts = new();
        public List<DexMon> Mons = new();
        public List<DexRect> Rects = new();
    }

    public sealed class DexPage
    {
        public string Id, Name;
        /// <summary>Alternative states of the page; the first is the default. Pieces name the one they belong to.</summary>
        public string[] Variants = Array.Empty<string>();
        public DexScreen Main = new(), Sub = new();
        /// <summary>Whether the main engine drives the top screen; HGSS's Pokédex puts it on the touch screen.</summary>
        public bool MainOnTop = true;

        public DexScreen Side(DexSide side) => side == DexSide.Main ? Main : Sub;

        /// <summary>Whether a piece tagged <paramref name="when"/> shows in <paramref name="variant"/>.</summary>
        public static bool Shows(string when, string variant)
        {
            if (string.IsNullOrEmpty(when)) return true;
            foreach (string w in when.Split('|'))
                if (string.Equals(w, variant, StringComparison.Ordinal)) return true;
            return false;
        }
    }
}
