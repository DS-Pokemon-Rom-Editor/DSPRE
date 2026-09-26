using System;
using System.Linq;
using System.Collections.Generic;

namespace DSPRE.Models
{
    /// <summary>Software rasterizer for tile thumbnails.</summary>
    public static class TileThumbnail
    {
        public delegate bool Picture(string name, string colours, out byte[] rgba, out int width, out int height);

        public static byte[] Draw(MapTileset.Tile tile, int size, Picture pictures,
                                  float yawDegrees = 35f, float pitchDegrees = 30f)
        {
            var rgba = new byte[size * size * 4];
            if (tile == null || tile.Corners.Count == 0 || size <= 0) return rgba;

            double yaw = yawDegrees * Math.PI / 180.0, pitch = pitchDegrees * Math.PI / 180.0;
            double cy = Math.Cos(yaw), sy = Math.Sin(yaw);
            double cp = Math.Cos(pitch), sp = Math.Sin(pitch);

            var at = new (float x, float y, float depth)[tile.Corners.Count];
            float lowX = float.MaxValue, highX = float.MinValue;
            float lowY = float.MaxValue, highY = float.MinValue;

            for (int i = 0; i < tile.Corners.Count; i++)
            {
                var c = tile.Corners[i];
                double rx = c.X * cy - c.Z * sy;
                double rz = c.X * sy + c.Z * cy;
                double ry = c.Y * cp - rz * sp;
                double depth = c.Y * sp + rz * cp;

                at[i] = ((float)rx, (float)-ry, (float)depth);
                lowX = Math.Min(lowX, at[i].x); highX = Math.Max(highX, at[i].x);
                lowY = Math.Min(lowY, at[i].y); highY = Math.Max(highY, at[i].y);
            }

            float wide = Math.Max(1e-5f, Math.Max(highX - lowX, highY - lowY));
            float scale = (size - 4) / wide;
            float middleX = (lowX + highX) / 2f, middleY = (lowY + highY) / 2f;

            for (int i = 0; i < at.Length; i++)
                at[i] = (size / 2f + (at[i].x - middleX) * scale,
                         size / 2f + (at[i].y - middleY) * scale,
                         at[i].depth);

            var nearest = new float[size * size];
            for (int i = 0; i < nearest.Length; i++) nearest[i] = float.MinValue;

            var known = new Dictionary<string, (byte[] rgba, int w, int h)>();

            foreach (var face in tile.Faces)
            {
                if (face.Corners == null || face.Corners.Length < 3) continue;

                if (!known.TryGetValue((face.Picture ?? "") + "|" + face.Palette, out var picture))
                {
                    picture = pictures != null && pictures(face.Picture, face.Palette, out var dots, out int pw, out int ph)
                        ? (dots, pw, ph) : (null, 0, 0);
                    known[(face.Picture ?? "") + "|" + face.Palette] = picture;
                }

                for (int i = 2; i < face.Corners.Length; i++)
                    Triangle(rgba, nearest, size,
                             at[face.Corners[0]], at[face.Corners[i - 1]], at[face.Corners[i]],
                             tile.Corners[face.Corners[0]], tile.Corners[face.Corners[i - 1]],
                             tile.Corners[face.Corners[i]], picture, (face.Look?.Alpha ?? 31) / 31f);
            }

            return rgba;
        }

        public static (byte[] rgba, int wide, int tall) DrawFromAbove(MapTileset.Tile tile, int turn,
                                                                       int perSquare, Picture pictures)
        {
            var (across, down) = TileGrid.Footprint(tile?.Wide ?? 1, tile?.Deep ?? 1, (byte)(turn & 3));
            var drawn = DrawFromAbove(tile, turn, perSquare, pictures, 0, 0, across, down);
            return (drawn.rgba, drawn.wide, drawn.tall);
        }

        public static (byte[] rgba, int wide, int tall, int fromX, int fromZ, int across, int down) DrawWholeFromAbove(
            MapTileset.Tile tile, int turn, int perSquare, Picture pictures)
        {
            if (tile == null || tile.Corners.Count == 0) return (Array.Empty<byte>(), 0, 0, 0, 0, 0, 0);
            float w = tile.Wide * MapTileset.TileWidth, d = tile.Deep * MapTileset.TileWidth;
            float lowX = float.MaxValue, lowZ = float.MaxValue, highX = float.MinValue, highZ = float.MinValue;
            foreach (var c in tile.Corners)
            {
                var (x, z) = Turn(c.X, c.Z, turn, w, d);
                lowX = Math.Min(lowX, x); highX = Math.Max(highX, x);
                lowZ = Math.Min(lowZ, z); highZ = Math.Max(highZ, z);
            }
            int fromX = (int)Math.Floor(lowX / MapTileset.TileWidth + 1e-3f);
            int fromZ = (int)Math.Floor(lowZ / MapTileset.TileWidth + 1e-3f);
            int toX = Math.Max(fromX + 1, (int)Math.Ceiling(highX / MapTileset.TileWidth - 1e-3f));
            int toZ = Math.Max(fromZ + 1, (int)Math.Ceiling(highZ / MapTileset.TileWidth - 1e-3f));
            int span = Math.Max(toX - fromX, toZ - fromZ);
            int per = span > 8 ? Math.Max(2, perSquare * 8 / span) : perSquare;
            var drawn = DrawFromAbove(tile, turn, per, pictures, fromX, fromZ, toX - fromX, toZ - fromZ);
            return (drawn.rgba, drawn.wide, drawn.tall, fromX, fromZ, toX - fromX, toZ - fromZ);
        }

        private static (float x, float z) Turn(float x, float z, int turn, float w, float d) => (turn & 3) switch
        {
            1 => (d - z, x),
            2 => (w - x, d - z),
            3 => (z, w - x),
            _ => (x, z),
        };

        private static (byte[] rgba, int wide, int tall) DrawFromAbove(MapTileset.Tile tile, int turn,
            int perSquare, Picture pictures, int fromX, int fromZ, int across, int down)
        {
            if (tile == null || perSquare <= 0) return (Array.Empty<byte>(), 0, 0);
            int wide = across * perSquare, tall = down * perSquare;
            var rgba = new byte[wide * tall * 4];
            var nearest = new float[wide * tall];
            for (int i = 0; i < nearest.Length; i++) nearest[i] = float.MinValue;

            float w = tile.Wide * MapTileset.TileWidth, d = tile.Deep * MapTileset.TileWidth;
            float dotsPer = perSquare / MapTileset.TileWidth;

            var at = new (float x, float y, float depth)[tile.Corners.Count];
            for (int i = 0; i < at.Length; i++)
            {
                var c = tile.Corners[i];
                var (x, z) = (turn & 3) switch
                {
                    1 => (d - c.Z, c.X),
                    2 => (w - c.X, d - c.Z),
                    3 => (c.Z, w - c.X),
                    _ => (c.X, c.Z),
                };
                at[i] = ((x - fromX * MapTileset.TileWidth) * dotsPer, (z - fromZ * MapTileset.TileWidth) * dotsPer, c.Y);
            }

            // Shadow faces would hide the tile, so they are skipped unless the tile is only a shadow.
            static bool Shadow(MapTileset.Face f) => (f.Picture ?? "").IndexOf("kage", StringComparison.OrdinalIgnoreCase) >= 0
                                                  || (f.Picture ?? "").IndexOf("shadow", StringComparison.OrdinalIgnoreCase) >= 0;
            bool onlyShadow = tile.Faces.All(Shadow);
            var known = new Dictionary<string, (byte[] rgba, int w, int h)>();
            foreach (var face in tile.Faces)
            {
                if (face.Corners == null || face.Corners.Length < 3) continue;
                if (!onlyShadow && Shadow(face)) continue;
                if (!known.TryGetValue((face.Picture ?? "") + "|" + face.Palette, out var picture))
                {
                    picture = pictures != null && pictures(face.Picture, face.Palette, out var dots, out int pw, out int ph)
                        ? (dots, pw, ph) : (null, 0, 0);
                    known[(face.Picture ?? "") + "|" + face.Palette] = picture;
                }
                for (int i = 2; i < face.Corners.Length; i++)
                    Triangle(rgba, nearest, wide, tall,
                             at[face.Corners[0]], at[face.Corners[i - 1]], at[face.Corners[i]],
                             tile.Corners[face.Corners[0]], tile.Corners[face.Corners[i - 1]],
                             tile.Corners[face.Corners[i]], picture, (face.Look?.Alpha ?? 31) / 31f);
            }
            return (rgba, wide, tall);
        }

        private static void Triangle(byte[] rgba, float[] nearest, int size,
                                     (float x, float y, float depth) a,
                                     (float x, float y, float depth) b,
                                     (float x, float y, float depth) c,
                                     MapTileset.Corner ca, MapTileset.Corner cb, MapTileset.Corner cc,
                                     (byte[] rgba, int w, int h) picture, float opacity = 1f)
            => Triangle(rgba, nearest, size, size, a, b, c, ca, cb, cc, picture, opacity);

        private static void Triangle(byte[] rgba, float[] nearest, int size, int tall,
                                     (float x, float y, float depth) a,
                                     (float x, float y, float depth) b,
                                     (float x, float y, float depth) c,
                                     MapTileset.Corner ca, MapTileset.Corner cb, MapTileset.Corner cc,
                                     (byte[] rgba, int w, int h) picture, float opacity = 1f)
        {
            float area = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
            if (Math.Abs(area) < 1e-6f) return;

            int left = Math.Max(0, (int)Math.Floor(Math.Min(a.x, Math.Min(b.x, c.x))));
            int right = Math.Min(size - 1, (int)Math.Ceiling(Math.Max(a.x, Math.Max(b.x, c.x))));
            int top = Math.Max(0, (int)Math.Floor(Math.Min(a.y, Math.Min(b.y, c.y))));
            int bottom = Math.Min(tall - 1, (int)Math.Ceiling(Math.Max(a.y, Math.Max(b.y, c.y))));

            for (int y = top; y <= bottom; y++)
                for (int x = left; x <= right; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f;

                    float w0 = ((b.x - px) * (c.y - py) - (b.y - py) * (c.x - px)) / area;
                    float w1 = ((c.x - px) * (a.y - py) - (c.y - py) * (a.x - px)) / area;
                    float w2 = 1f - w0 - w1;
                    if (w0 < -1e-4f || w1 < -1e-4f || w2 < -1e-4f) continue;

                    float depth = w0 * a.depth + w1 * b.depth + w2 * c.depth;
                    int spot = y * size + x;
                    if (depth <= nearest[spot]) continue;

                    byte r = 170, g = 170, bl = 170, alpha = 255;
                    if (picture.rgba != null && picture.w > 0 && picture.h > 0)
                    {
                        float s = w0 * ca.S + w1 * cb.S + w2 * cc.S;
                        float t = w0 * ca.T + w1 * cb.T + w2 * cc.T;

                        int sx = Wrap((int)Math.Floor(s), picture.w);
                        int sy = Wrap((int)Math.Floor(t), picture.h);
                        int from = (sy * picture.w + sx) * 4;
                        if (from + 3 < picture.rgba.Length)
                        {
                            r = picture.rgba[from]; g = picture.rgba[from + 1];
                            bl = picture.rgba[from + 2]; alpha = picture.rgba[from + 3];
                        }
                    }

                    float solid = alpha / 255f * opacity;
                    if (solid < 0.03f) continue;
                    if (solid >= 0.97f)
                    {
                        nearest[spot] = depth;
                        rgba[spot * 4] = r; rgba[spot * 4 + 1] = g;
                        rgba[spot * 4 + 2] = bl; rgba[spot * 4 + 3] = 255;
                        continue;
                    }
                    float under = rgba[spot * 4 + 3] / 255f;
                    float outA = solid + under * (1f - solid);
                    if (outA <= 0f) continue;
                    byte Mix(byte src, byte dst) => (byte)Math.Round((src * solid + dst * under * (1f - solid)) / outA);
                    rgba[spot * 4] = Mix(r, rgba[spot * 4]);
                    rgba[spot * 4 + 1] = Mix(g, rgba[spot * 4 + 1]);
                    rgba[spot * 4 + 2] = Mix(bl, rgba[spot * 4 + 2]);
                    rgba[spot * 4 + 3] = (byte)Math.Round(outA * 255f);
                }
        }

        private static int Wrap(int at, int of)
        {
            if (of <= 0) return 0;
            at %= of;
            return at < 0 ? at + of : at;
        }
    }
}
