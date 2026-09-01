using System;
using System.Collections.Generic;

namespace DSPRE.ROMFiles
{
    /// <summary>One row of the games' field camera table.</summary>
    public sealed class FieldCameraEntry
    {
        private const float FixedPointOne = 4096f;
        private const float TurnDegrees = 360f / 65536f;

        /// <summary>One tile is sixteen units in the games' own field coordinates.</summary>
        public const float GameUnitsPerTile = 16f;

        public int Id { get; }
        public string Name { get; }
        public int RawDistance { get; }
        public int RawPitch { get; }
        public bool Orthographic { get; }

        /// <summary>PerspWay. </summary>
        public int RawHalfFieldOfView { get; }

        public int NearClip { get; }
        public int FarClip { get; }

        /// <summary>Moves the whole view, camera and what it looks at together, in game units.</summary>
        public int ShiftX { get; }
        public int ShiftY { get; }
        public int ShiftZ { get; }

        internal FieldCameraEntry(int id, string name, int distance, int pitch, bool ortho,
                                  int halfFov, int near, int far, int sx, int sy, int sz)
        {
            Id = id; Name = name;
            RawDistance = distance; RawPitch = pitch; Orthographic = ortho;
            RawHalfFieldOfView = halfFov; NearClip = near; FarClip = far;
            ShiftX = sx; ShiftY = sy; ShiftZ = sz;
        }

        /// <summary>How far back the camera sits, in tiles.</summary>
        public float DistanceInTiles => RawDistance / FixedPointOne / GameUnitsPerTile;

        /// <summary>How far the camera looks down, in degrees. The stored angle is negative.</summary>
        public float PitchDegrees => -RawPitch * TurnDegrees;

        /// <summary>Half the vertical view angle, which is what the table actually stores.</summary>
        public float HalfFieldOfViewDegrees => RawHalfFieldOfView * TurnDegrees;

        /// <summary>The whole vertical view angle, which is what a renderer usually wants.</summary>
        public float FieldOfViewDegrees => HalfFieldOfViewDegrees * 2f;

        public float ShiftXInTiles => ShiftX / FixedPointOne / GameUnitsPerTile;
        public float ShiftYInTiles => ShiftY / FixedPointOne / GameUnitsPerTile;
        public float ShiftZInTiles => ShiftZ / FixedPointOne / GameUnitsPerTile;

        /// <summary>How many tiles fit on screen top to bottom at the distance the camera sits.</summary>
        public float VisibleTilesAtTarget =>
            2f * DistanceInTiles * (float)Math.Tan(HalfFieldOfViewDegrees * Math.PI / 180.0);

        /// <summary>Half the height of the flat view, for the two entries that use one. </summary>
        public float OrthoHalfHeightInTiles => VisibleTilesAtTarget / 2f;

        /// <summary>The camera distance in the units the preview's scene uses.</summary>
        public float DistanceForScene(float tileSize) => DistanceInTiles * tileSize;
    }

    /// <summary>Where the games put the camera while you walk about a map. </summary>
    public static class FieldCamera
    {
        // id, name, distance, pitch, orthographic, half view angle, near, far, shift x/y/z
        private static readonly FieldCameraEntry[] Table =
        {
            new FieldCameraEntry( 0, "Normal",                    0x29aec1, -0x229e, false, 0x5c1, 150, 1200, 0, 0, 0),
            new FieldCameraEntry( 1, "Violet Gym",                0x19465c, -0x1c7d, false, 0x981, 134, 1200, 0, 0x25000, -0xf000),
            new FieldCameraEntry( 2, "Goldenrod Gym",             0x29aec1, -0x1c3e, false, 0x5c1, 150, 1200, 0, 0, 0),
            new FieldCameraEntry( 3, "Olivine Lighthouse roof",   0x29aec1, -0x0dbe, false, 0x5c1, 150, 1200, 0, 0x1e9c5, -0xc0c9),
            new FieldCameraEntry( 4, "Indoors, flat view",        0x61b89b, -0x237e, true,  0x281, 150, 1735, 0, 0, 0),
            new FieldCameraEntry( 5, "Azalea Gym",                0x1d19f6, -0x225e, false, 0x881, 150, 1160, 0, 0, -0x18000),
            new FieldCameraEntry( 6, "Lugia",                     0x29aec1, -0x201e, false, 0x5c1, 150, 1500, 0, 0, 0),
            new FieldCameraEntry( 7, "Azalea Gym, second",        0x29aec1, -0x1e1e, false, 0x5c1, 150, 1200, 0, 0x17c5b, -0x16a1e),
            new FieldCameraEntry( 8, "Battle Frontier",           0x20374c, -0x26de, false, 0x770, 150,  900, 0, 0, 0),
            new FieldCameraEntry( 9, "Vermilion Gym",             0x29bec1, -0x2a7e, false, 0x5c1, 150, 1200, 0, 0, 0),
            new FieldCameraEntry(10, "Lugia from above",          0x13c805, -0x20be, false, 0xc81, 150, 1700, 0, 0xc5f1, -0x25c74),
            new FieldCameraEntry(11, "Ladder dungeon test",       0x215c29, -0x1c3e, false, 0x741, 150, 1200, 0, -0x8000, 0),
            new FieldCameraEntry(12, "Lugia from below",          0x29aec1, -0x201e, false, 0x5c1, 150, 1700, 0, 0, -0x20000),
            new FieldCameraEntry(13, "Bell Tower roof",           0x29aec1, -0x0dbe, false, 0x5c1, 150, 1700, 0, 0x1e9c5, -0x1e0c9),
            new FieldCameraEntry(14, "Fuchsia Gym",               0x29aec1, -0x2b3e, false, 0x5c1, 150, 1200, 0, 0, 0),
            new FieldCameraEntry(15, "Dance Theatre, flat view",  0x61b89b, -0x237e, true,  0x281, 150, 1735, 0, 0, -0x2e000),
            new FieldCameraEntry(16, "Battle Tower indoors",      0x29aec1, -0x29fe, false, 0x5c1, 150,  900, 0, 0, 0),
        };

        public static IReadOnlyList<FieldCameraEntry> Entries => Table;

        public static int Count => Table.Length;

        /// <summary>The row a header's camera number picks. Out of range falls back to the normal one.</summary>
        public static FieldCameraEntry Entry(int cameraId) =>
            cameraId >= 0 && cameraId < Table.Length ? Table[cameraId] : Table[0];

        /// <summary>The ordinary walking-about camera, which is what most maps use.</summary>
        public static FieldCameraEntry Normal => Table[0];

        /// <summary>Which way the camera faces. </summary>
        public const float YawDegrees = 0f;

        /// <summary>
        /// How many frames the camera's height lags behind the player, from FIELD_CAMERA_DELAY.
        /// </summary>
        public const int TrailFrames = 6;

        /// <summary>Only the height is delayed. </summary>
        public const bool HeightLagsBehind = true;

        // Kept so callers that only ever wanted the ordinary camera read the same as before.
        public const float GameUnitsPerTile = FieldCameraEntry.GameUnitsPerTile;
        public static float DistanceInTiles => Normal.DistanceInTiles;
        public static float PitchDegrees => Normal.PitchDegrees;
        public static float HalfFieldOfViewDegrees => Normal.HalfFieldOfViewDegrees;
        public static float FieldOfViewDegrees => Normal.FieldOfViewDegrees;
        public static float DistanceForScene(float tileSize) => Normal.DistanceForScene(tileSize);
    }
}
