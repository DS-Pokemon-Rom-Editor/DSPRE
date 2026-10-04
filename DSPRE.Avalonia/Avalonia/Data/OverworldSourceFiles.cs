using System;
using System.IO;
using System.Linq;
using DSPRE.HgEngine;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// An overworld texture's hg-engine source files. The build reads only the PNG's pixel indices and takes every
    /// colour from the JASC palettes the JSON names, so a texture saves as both; a raw member saves as its .bin.
    /// </summary>
    internal static class OverworldSourceFiles
    {
        /// <summary>Writes a/0/8/1 member <paramref name="member"/>'s texture into its sources. Null on success, or when no folder is open.</summary>
        public static string Write(int member, byte[] btx)
        {
            if (!HgEngineProject.IsActive) return null;
            var members = HgEngineOverworlds.Members(out string error);
            if (members == null) return error;
            if (member < 0 || member >= members.Count) return $"The checkout has no source for overworld texture {member}.";
            return Write(members[member], btx);
        }

        public static string Write(HgEngineOverworlds.Member member, byte[] btx)
        {
            try
            {
                if (member.Bin != null)
                {
                    if (!File.Exists(member.Bin) || !File.ReadAllBytes(member.Bin).AsSpan().SequenceEqual(btx)) File.WriteAllBytes(member.Bin, btx);
                    return null;
                }
                string problem = Check(member, btx, out byte[] indices, out var palettes, out byte[] oldIndices, out uint[] oldColours);
                if (problem != null) return problem;

                // The build ignores the PNG's own colours, so they only follow a slot this save actually recoloured.
                int[] oldPalette = member.Palettes.Count > 0 ? HgEngineOverworlds.ReadJasc(member.Palettes[0]) : null;
                int length = Math.Max(oldColours.Length, indices.Max() + 1);
                var colours = new uint[length];
                for (int i = 0; i < length; i++)
                {
                    uint before = i < oldColours.Length ? oldColours[i] : 0xFF000000u | (uint)palettes[0][Math.Min(i, 15)];
                    bool recoloured = i < 16 && (oldPalette == null || i >= oldPalette.Length || (oldPalette[i] & 0xF8F8F8) != (palettes[0][i] & 0xF8F8F8));
                    colours[i] = recoloured ? 0xFF000000u | (uint)palettes[0][i] : before;
                }
                if (!oldIndices.AsSpan().SequenceEqual(indices) || !oldColours.AsSpan().SequenceEqual(colours))
                {
                    IndexedPng.TryRead(File.ReadAllBytes(member.Png), out _, out _, out int w, out int h);
                    File.WriteAllBytes(member.Png, IndexedPng.Write(indices, colours, w, h, 4));
                }
                for (int p = 0; p < member.Palettes.Count; p++) HgEngineOverworlds.WriteJasc(member.Palettes[p], palettes[p]);
                return null;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return ex.Message;
            }
        }

        /// <summary>Why <paramref name="btx"/> can't be saved into this member's PNG and palettes, or null.</summary>
        public static string Check(HgEngineOverworlds.Member member, byte[] btx) => member.Bin != null ? null : Check(member, btx, out _, out _, out _, out _);

        private static string Check(HgEngineOverworlds.Member member, byte[] btx, out byte[] indices, out System.Collections.Generic.List<int[]> palettes,
            out byte[] oldIndices, out uint[] oldColours)
        {
            oldIndices = null;
            oldColours = null;
            if (!HgEngineOverworlds.TryDecodeBtx(btx, out int width, out int height, out indices, out palettes))
                return "This texture isn't a 16-colour overworld, which is all hg-engine builds from a PNG.";
            string png = Rel(member.Png);
            if (!File.Exists(member.Png) || !IndexedPng.TryRead(File.ReadAllBytes(member.Png), out oldIndices, out oldColours, out int pw, out int ph))
                return $"{png} is missing or isn't an indexed PNG.";
            if (pw != width || ph != height)
                return $"{png} is {pw}×{ph} but its JSON lays out {width}×{height}, so hg-engine builds it wrong. Fix the checkout's PNG or JSON first.";
            if (member.Palettes.Count > palettes.Count)
                return $"{Rel(member.Json)} names {member.Palettes.Count} palettes but the texture has {palettes.Count}.";
            return null;
        }

        /// <summary>
        /// Puts a follower sheet into the species' sprites/&lt;mon&gt;/overworld.png. Its colours become the normal palette,
        /// and an optional shiny sheet with the same pixels gives the shiny one. A species with no sheet yet takes its
        /// JSON and palettes from <paramref name="templateSpecies"/>. Null on success; <paramref name="warning"/> says when
        /// the kept shiny palette may no longer suit the new art.
        /// </summary>
        public static string ImportFollower(int species, string normalPng, string shinyPng, int? templateSpecies, out string warning)
        {
            warning = null;
            try
            {
                if (!HgEngineOverworldSprite.TryGetSpritePngPath(species, out string target, mustExist: false))
                    return "pokegra.mk has no overworld for this species. It is generated from species.h by scripts/reformat_sprite_data.py.";
                string json = Path.ChangeExtension(target, ".json");
                string stem = Path.GetFileNameWithoutExtension(target);
                string dir = Path.GetDirectoryName(target);

                if (!IndexedPng.TryRead(File.ReadAllBytes(normalPng), out byte[] indices, out uint[] colours, out int w, out int h))
                    return $"{Path.GetFileName(normalPng)} isn't an indexed PNG. Save it with a palette of at most 16 colours.";
                if (indices.Length > 0 && indices.Max() > 15)
                    return $"{Path.GetFileName(normalPng)} uses more than 16 colours; overworlds have 16.";

                byte[] oldIndices = null;
                string layoutPng = target;
                if (!File.Exists(json))
                {
                    if (templateSpecies is not int from || !HgEngineOverworldSprite.TryGetSpritePngPath(from, out string templatePng))
                        return "This species has no overworld yet. Pick a template species to copy its layout and palettes from.";
                    if (!File.Exists(Path.ChangeExtension(templatePng, ".json"))) return "The template species has no overworld layout to copy.";
                    layoutPng = templatePng;
                }
                else if (File.Exists(target)) IndexedPng.TryRead(File.ReadAllBytes(target), out oldIndices, out _, out _, out _);

                if (!IndexedPng.TryRead(File.ReadAllBytes(layoutPng), out _, out _, out int lw, out int lh))
                    return $"{Rel(layoutPng)} isn't an indexed PNG.";
                if (w != lw || h != lh)
                    return $"The sheet is {w}×{h} but this overworld's layout is {lw}×{lh}.";

                byte[] shinyIndices = null;
                uint[] shinyColours = null;
                if (shinyPng != null)
                {
                    if (!IndexedPng.TryRead(File.ReadAllBytes(shinyPng), out shinyIndices, out shinyColours, out int sw, out int sh))
                        return $"{Path.GetFileName(shinyPng)} isn't an indexed PNG.";
                    if (sw != w || sh != h || !shinyIndices.AsSpan().SequenceEqual(indices))
                        return "The shiny sheet has to be the same drawing as the normal one, only recoloured.";
                }

                // Layout and palettes come with the template; the copies are named for this sheet.
                if (layoutPng != target)
                {
                    Directory.CreateDirectory(dir);
                    string from = Path.GetFileNameWithoutExtension(layoutPng);
                    HgEngineOverworlds.CopyAsNew(Path.ChangeExtension(layoutPng, ".json"), json);
                    foreach (string pal in Directory.GetFiles(Path.GetDirectoryName(layoutPng), from + "-*.pal"))
                        HgEngineOverworlds.CopyAsNew(pal, Path.Combine(dir, stem + Path.GetFileName(pal).Substring(from.Length)));
                }

                var member = new HgEngineOverworlds.Member(Path.GetFileName(target), target, json, PalettesOf(json, dir, stem), null);
                if (member.Palettes.Count == 0) return $"{Rel(json)} names no palettes.";

                File.WriteAllBytes(target, IndexedPng.Write(indices, colours.Take(16).ToArray(), w, h, 4));
                HgEngineOverworlds.WriteJasc(member.Palettes[0], Sixteen(colours));
                if (shinyColours != null && member.Palettes.Count > 1)
                    HgEngineOverworlds.WriteJasc(member.Palettes[1], Sixteen(shinyColours));
                else if (member.Palettes.Count > 1)
                {
                    // Slots the old drawing never used have no designed shiny colour.
                    var used = indices.Distinct().ToHashSet();
                    var before = oldIndices?.Distinct().ToHashSet();
                    var fresh = before == null ? used : used.Where(i => !before.Contains(i)).ToHashSet();
                    if (layoutPng != target)
                        warning = $"The shiny colours were copied from the template species. Import a shiny sheet or edit {Path.GetFileName(member.Palettes[1])} to match.";
                    else if (fresh.Count > 0)
                        warning = $"The new drawing uses colour slots {string.Join(", ", fresh.OrderBy(i => i))} that the old one didn't, so the shiny version may look wrong there.";
                }
                return null;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return ex.Message;
            }
        }

        private static int[] Sixteen(uint[] colours) =>
            Enumerable.Range(0, 16).Select(i => i < colours.Length ? (int)(colours[i] & 0xFFFFFF) : 0).ToArray();

        private static System.Collections.Generic.List<string> PalettesOf(string json, string dir, string stem)
        {
            var list = new System.Collections.Generic.List<string>();
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(json));
            if (doc.RootElement.TryGetProperty("palettes", out var pals))
                foreach (var pal in pals.EnumerateObject())
                    if (pal.Value.TryGetProperty("fileName", out var file))
                        list.Add(Path.Combine(dir, stem + "-" + file.GetString()));
            return list;
        }

        private static string Rel(string path) =>
            path == null ? "" : Path.GetRelativePath(HgEngineProject.RepoPathUnc, path).Replace('\\', '/');
    }
}
