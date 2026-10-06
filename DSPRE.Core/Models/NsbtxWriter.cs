using System;
using System.Collections.Generic;
using System.Linq;

namespace DSPRE.Models
{
    /// <summary>NSBTX writer.</summary>
    public static class NsbtxWriter
    {
        public const int MostPictures = 255;

        public sealed class Result
        {
            public byte[] Bytes;
            public string Whynot;
            public int Pictures;
            public List<string> Notes = new List<string>();
            // Names Extend left out because the pack already has a texture by that name.
            public HashSet<string> AlreadyThere = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            public string Summary => Whynot
                ?? $"{Pictures} picture{(Pictures == 1 ? "" : "s")}, {Bytes.Length:n0} bytes.";
        }

        /// <summary>What a pack holds: its texture names, each palette's colours, and the bytes both take in video memory.</summary>
        public sealed class Contents
        {
            public List<string> Textures = new List<string>();
            public Dictionary<string, byte[]> Palettes = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            public int TextureBytes, PaletteBytes;
        }

        public static Contents Read(byte[] nsbtx)
        {
            try
            {
                int tex = BitConverter.ToInt32(nsbtx, 16);
                if (tex <= 0 || tex + 60 > nsbtx.Length || nsbtx[tex] != 'T' || nsbtx[tex + 3] != '0') return null;
                int U16(int at) => nsbtx[tex + at] | (nsbtx[tex + at + 1] << 8);
                int U32(int at) => BitConverter.ToInt32(nsbtx, tex + at);
                Contents c = new Contents
                {
                    TextureBytes = (U16(0x0C) << 3) + (U16(0x1C) << 3) * 3 / 2,
                    PaletteBytes = U16(0x30) << 3,
                };
                c.Textures.AddRange(NitroDictionary.Read(nsbtx, tex + U16(0x0E)).Select(t => t.name));
                List<(string name, int at)> pals = NitroDictionary.Read(nsbtx, tex + U32(0x34))
                    .Select(p => (p.name, at: (p.entry[0] | (p.entry[1] << 8)) << 3)).OrderBy(p => p.at).ToList();
                int palAt = U32(0x38);
                for (int i = 0; i < pals.Count; i++)
                {
                    int end = i + 1 < pals.Count ? pals[i + 1].at : c.PaletteBytes;
                    int from = Math.Min(pals[i].at, c.PaletteBytes), length = Math.Max(0, Math.Min(end, c.PaletteBytes) - from);
                    byte[] bytes = new byte[length];
                    Array.Copy(nsbtx, tex + palAt + from, bytes, 0, length);
                    c.Palettes[pals[i].name] = bytes;
                }
                return c;
            }
            catch { return null; }
        }

        /// <summary>Adds pictures to an existing pack, keeping every texture and palette it had byte for byte.</summary>
        public static Result Extend(byte[] nsbtx, IReadOnlyList<DsTexture> more) => Extend(nsbtx, more, null);

        /// <param name="replace">Names whose texture and palettes the new ones take the place of; the old bytes stay unreferenced.</param>
        public static Result Extend(byte[] nsbtx, IReadOnlyList<DsTexture> more, ISet<string> replace)
        {
            Result r = new Result();
            try
            {
                int tex = BitConverter.ToInt32(nsbtx, 16);
                if (tex <= 0 || tex + 60 > nsbtx.Length || nsbtx[tex] != 'T' || nsbtx[tex + 3] != '0')
                { r.Whynot = "Not a texture pack."; return r; }

                int U16(int at) => nsbtx[tex + at] | (nsbtx[tex + at + 1] << 8);
                int U32(int at) => BitConverter.ToInt32(nsbtx, tex + at);
                byte[] Slice(int from, int length) { byte[] b = new byte[length]; Array.Copy(nsbtx, tex + from, b, 0, length); return b; }

                List<(string name, byte[] entry)> texDict = NitroDictionary.Read(nsbtx, tex + U16(0x0E));
                List<(string name, byte[] entry)> palDict = NitroDictionary.Read(nsbtx, tex + U32(0x34));
                byte[] texData = Slice(U32(0x14), U16(0x0C) << 3);
                int packed = U16(0x1C) << 3;
                byte[] packedData = Slice(U32(0x24), packed);
                byte[] packedIndex = Slice(U32(0x28), packed / 2);
                byte[] palData = Slice(U32(0x38), U16(0x30) << 3);

                replace ??= new HashSet<string>();
                HashSet<string> had = new HashSet<string>(texDict.Select(t => t.name), StringComparer.OrdinalIgnoreCase);
                List<DsTexture> adding = more.Where(t => !had.Contains(t.Name) || replace.Contains(t.Name)).ToList();
                r.AlreadyThere.UnionWith(more.Where(t => had.Contains(t.Name) && !replace.Contains(t.Name)).Select(t => t.Name));
                texDict = texDict.Where(t => !adding.Any(a => string.Equals(a.Name, t.name, StringComparison.OrdinalIgnoreCase))).ToList();
                HashSet<string> replacedPalettes = new HashSet<string>(adding.Where(t => replace.Contains(t.Name))
                    .SelectMany(t => t.PaletteNames != null && t.PaletteNames.Count > 0 ? t.PaletteNames : new List<string> { t.Name }));
                palDict = palDict.Where(p => !replacedPalettes.Contains(p.name)).ToList();
                if (texDict.Count + adding.Count > MostPictures)
                { r.Whynot = $"{texDict.Count + adding.Count} textures; a texture pack holds {MostPictures}."; return r; }

                List<string> texNames = texDict.Select(t => t.name).ToList();
                List<byte[]> texEntries = texDict.Select(t => t.entry).ToList();
                List<string> palNames = palDict.Select(p => p.name).ToList();
                List<byte[]> palEntries = palDict.Select(p => p.entry).ToList();
                List<byte> newTex = new List<byte>(texData);
                List<byte> newPal = new List<byte>(palData);
                foreach (DsTexture t in adding)
                {
                    while (newTex.Count % 8 != 0) newTex.Add(0);
                    while (newPal.Count % 8 != 0) newPal.Add(0);
                    byte[] e = new byte[8];
                    BitConverter.GetBytes(t.ImageParam(newTex.Count)).CopyTo(e, 0);
                    BitConverter.GetBytes((t.Width & 0x7FF) | ((t.Height & 0x7FF) << 11)).CopyTo(e, 4);
                    texNames.Add(t.Name); texEntries.Add(e);
                    newTex.AddRange(t.Pixels);

                    List<string> called = t.PaletteNames != null && t.PaletteNames.Count > 0 ? t.PaletteNames : new List<string> { t.Name };
                    foreach (string name in called)
                    {
                        if (string.IsNullOrEmpty(name) || palNames.Contains(name)) continue;
                        byte[] pe = new byte[4];
                        pe[0] = (byte)(newPal.Count >> 3); pe[1] = (byte)(newPal.Count >> 11);
                        pe[2] = 0;
                        palNames.Add(name); palEntries.Add(pe);
                    }
                    foreach (ushort c in t.Colours) { newPal.Add((byte)c); newPal.Add((byte)(c >> 8)); }
                    r.Notes.AddRange(t.Notes);
                }
                while (newTex.Count % 8 != 0) newTex.Add(0);
                while (newPal.Count % 8 != 0) newPal.Add(0);
                if (newTex.Count >> 3 > 0xFFFF || newPal.Count >> 3 > 0xFFFF)
                { r.Whynot = "The texture pack would be too large."; return r; }

                byte[] td = NitroDictionary.Write(texNames, texEntries);
                byte[] pd = NitroDictionary.Write(palNames, palEntries);
                int header = 0x3C;
                int Align8(int v) => (v + 7) & ~7;
                int texAt = Align8(header + td.Length + pd.Length);
                int packedAt = Align8(texAt + newTex.Count);
                int indexAt = Align8(packedAt + packedData.Length);
                int palAt = Align8(indexAt + packedIndex.Length);
                int size = (palAt + newPal.Count + 3) & ~3;

                byte[] o = new byte[size];
                void P16(int at, int v) { o[at] = (byte)v; o[at + 1] = (byte)(v >> 8); }
                void P32(int at, int v) => BitConverter.GetBytes(v).CopyTo(o, at);
                o[0] = (byte)'T'; o[1] = (byte)'E'; o[2] = (byte)'X'; o[3] = (byte)'0';
                P32(4, size);
                P16(0x0C, newTex.Count >> 3); P16(0x0E, header); P32(0x14, texAt);
                P16(0x1C, packedData.Length >> 3); P32(0x24, packedAt); P32(0x28, indexAt);
                P16(0x30, newPal.Count >> 3); P32(0x34, header + td.Length); P32(0x38, palAt);
                td.CopyTo(o, header); pd.CopyTo(o, header + td.Length);
                newTex.ToArray().CopyTo(o, texAt);
                packedData.CopyTo(o, packedAt); packedIndex.CopyTo(o, indexAt);
                newPal.ToArray().CopyTo(o, palAt);

                r.Bytes = NsbmdWriter.Envelope(new[] { o }, "BTX0", version: 1);
                r.Pictures = texNames.Count;
            }
            catch (Exception ex) { r.Whynot = "Texture pack build failed: " + ex.Message; }
            return r;
        }

        public static Result Build(IReadOnlyList<DsTexture> pictures)
        {
            Result r = new Result();
            if (pictures == null || pictures.Count == 0)
            { r.Whynot = "No textures."; return r; }

            if (pictures.Count > MostPictures)
            { r.Whynot = $"{pictures.Count} textures; a texture pack holds {MostPictures}."; return r; }

            HashSet<string> named = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (DsTexture picture in pictures)
            {
                if (string.IsNullOrEmpty(picture.Name))
                { r.Whynot = "Texture without a name."; return r; }

                if (!named.Add(picture.Name))
                { r.Whynot = $"Duplicate texture name {picture.Name}."; return r; }

                r.Notes.AddRange(picture.Notes);
            }

            try
            {
                // The loader rejects BTX0 files that aren't version 1.
                r.Bytes = NsbmdWriter.Envelope(new[] { NsbmdWriter.BuildTex0(pictures) }, "BTX0", version: 1);
                r.Pictures = pictures.Count;
            }
            catch (Exception ex) { r.Whynot = "Texture pack build failed: " + ex.Message; }

            return r;
        }
    }
}
