using System;
using System.Collections.Generic;

namespace DSPRE.Avalonia.Data
{
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
        /// <summary>Markers already in screen pixels (HGSS's blocks), not grid cells.</summary>
        public bool Absolute;
        public int Priority;
        /// <summary>Blend weights in sixteenths for translucent markers (HGSS): the marker, then what is under it; 0 for solid.</summary>
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

}
