using System;
using System.IO;
using System.Linq;
using DSPRE.HgEngine;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// Keeps the PNGs hg-engine builds battle sprites and icons from in step with their archive members. A drawing
    /// saved into a member goes into its PNG's pixels and a palette into its PNG's colours, so the next build makes
    /// what was saved instead of putting the old picture back.
    /// </summary>
    internal static class BuiltPngSources
    {
        /// <summary>Why a picture can't be saved into this member's PNG, or null when it can or the member has none.</summary>
        public static string CannotWrite(DirNames dir, string archive, int member, int memberCount)
        {
            HgEngineBuiltPngs.Source source = HgEngineBuiltPngs.For(archive, member, memberCount);
            if (source?.Part != HgEngineBuiltPngs.Part.Pixels) return null;
            GraphicAssets.Archive a = GraphicAssets.All.FirstOrDefault(x => x.Dir == dir);
            GraphicAssets.Indexed ix = a == null ? null : GraphicAssets.ReadIndexed(a, member, out _);
            if (ix == null) return null;
            if (!File.Exists(source.Path) || !IndexedPng.TryRead(File.ReadAllBytes(source.Path), out _, out _, out int w, out int h))
                return $"hg-engine builds this from {Rel(source.Path)}, which is missing or isn't an indexed PNG.";
            return w != ix.Width || h != ix.Height
                ? $"hg-engine builds this from {Rel(source.Path)}, which is {w}×{h} while the drawing is {ix.Width}×{ix.Height}, so it can't be saved there."
                : null;
        }

        /// <summary>Called once <paramref name="data"/> is on disk as member <paramref name="member"/>, which held <paramref name="before"/>.</summary>
        public static void Write(DirNames dir, string archive, int member, int memberCount, byte[] before, byte[] data)
        {
            HgEngineBuiltPngs.Source source = HgEngineBuiltPngs.For(archive, member, memberCount);
            if (source == null) return;
            switch (source.Part)
            {
                case HgEngineBuiltPngs.Part.Copy:
                    if (!File.Exists(source.Path) || !File.ReadAllBytes(source.Path).AsSpan().SequenceEqual(data)) File.WriteAllBytes(source.Path, data);
                    break;
                case HgEngineBuiltPngs.Part.Pixels:
                    WritePixels(dir, member, source.Path);
                    break;
                case HgEngineBuiltPngs.Part.Colours:
                    WriteColours(before, data, source.Path);
                    break;
                case HgEngineBuiltPngs.Part.JascColours:
                    WriteJasc(data, source.Path);
                    break;
            }
        }

        private static void WritePixels(DirNames dir, int member, string png)
        {
            GraphicAssets.Archive a = GraphicAssets.All.FirstOrDefault(x => x.Dir == dir);
            GraphicAssets.Indexed ix = a == null ? null : GraphicAssets.ReadIndexed(a, member, out _);
            if (ix == null) return;
            byte[] file = File.Exists(png) ? File.ReadAllBytes(png) : null;
            if (file == null || !IndexedPng.TryRead(file, out byte[] oldIndices, out uint[] colours, out int w, out int h)) return;
            if (w != ix.Width || h != ix.Height)
            {
                AppLogger.Error($"BuiltPngSources: {Rel(png)} is {w}×{h}, the drawing {ix.Width}×{ix.Height}; not written.");
                return;
            }
            if (oldIndices.AsSpan().SequenceEqual(ix.Indices)) return;

            // The build takes only the numbers from this PNG, so its colours stay unless the drawing reaches past them.
            int needed = ix.Indices.Length == 0 ? 0 : ix.Indices.Max() + 1;
            if (needed > colours.Length)
                colours = colours.Concat(Enumerable.Range(colours.Length, needed - colours.Length)
                    .Select(i => i < ix.Palette.Length ? ix.Palette[i] : 0xFF000000u)).ToArray();
            File.WriteAllBytes(png, IndexedPng.Write(ix.Indices, colours, w, h, BitDepth(file, needed)));
        }

        private static void WriteColours(byte[] previous, byte[] nclr, string png)
        {
            byte[] file = File.Exists(png) ? File.ReadAllBytes(png) : null;
            if (file == null || file.Length == 0 || !IndexedPng.TryRead(file, out byte[] indices, out uint[] old, out int w, out int h)) return;
            (byte r, byte g, byte b)[] stored = NitroBgCodec.ReadPalette(GraphicAssets.Unsqueeze(nclr), out int count);
            // Only colours this save changed go in: the build leaves values past the drawing's own that the PNG doesn't hold.
            (byte r, byte g, byte b)[] was = previous == null ? null : NitroBgCodec.ReadPalette(GraphicAssets.Unsqueeze(previous), out _);
            int depth = file.Length > 24 ? file[24] : 8;
            int length = Math.Max(old.Length, Math.Min(count, 1 << Math.Min(depth, 8)));

            uint[] colours = new uint[length];
            int used = old.Length;
            for (int i = 0; i < length; i++)
            {
                uint before = i < old.Length ? old[i] : 0xFF000000u;
                colours[i] = before;
                if (i >= count || (was != null && was[i] == stored[i])) continue;
                (byte r, byte g, byte b) = stored[i];
                uint now = ((uint)r << 16) | ((uint)g << 8) | b;
                // A colour the game would show the same keeps the PNG's own value.
                if ((before & 0xF8F8F8) == now) continue;
                colours[i] = (before & 0xFF000000u) | now;
                used = Math.Max(used, i + 1);
            }
            if (used == old.Length && colours.Take(used).SequenceEqual(old)) return;
            Array.Resize(ref colours, used);
            File.WriteAllBytes(png, IndexedPng.Write(indices, colours, w, h, BitDepth(file, used)));
        }

        // A JASC palette the build converts into this member keeps its own length.
        private static void WriteJasc(byte[] nclr, string path)
        {
            (byte r, byte g, byte b)[] stored = NitroBgCodec.ReadPalette(GraphicAssets.Unsqueeze(nclr), out int count);
            int[] old = File.Exists(path) ? HgEngineOverworlds.ReadJasc(path) : null;
            int length = old?.Length ?? count;
            int[] colours = new int[length];
            for (int i = 0; i < length; i++)
            {
                if (i >= count) { colours[i] = old?[i] ?? 0; continue; }
                (byte r, byte g, byte b) = stored[i];
                colours[i] = (r << 16) | (g << 8) | b;
            }
            HgEngineOverworlds.WriteJasc(path, colours);
        }

        // Rewrites keep the PNG's own bit depth when its colours still fit in it.
        private static int BitDepth(byte[] png, int colours) => png.Length > 24 && png[24] == 4 && colours <= 16 ? 4 : 8;

        private static string Rel(string path) =>
            Path.GetRelativePath(HgEngineProject.RepoPathUnc, path).Replace('\\', '/');
    }
}
