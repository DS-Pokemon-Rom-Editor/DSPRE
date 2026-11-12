using DSPRE.Avalonia.Gl;
using DSPRE.Models;
using DSPRE.ROMFiles;
using DSPRE.Resources;
using LibNDSFormats.NSBTX;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using static DSPRE.RomInfo;
using AvBitmap = global::Avalonia.Media.Imaging.Bitmap;
using LibNDSFormats.NSBMD;
using Avalonia.Platform;
using Avalonia.Media.Imaging;

namespace DSPRE.Avalonia.ViewModels.World
{
    public partial class MapTilesViewModel
    {
        /// <summary>One texture the painted tiles need, and what adding it will do.</summary>
        public sealed class PictureChoice
        {
            public string Name { get; set; }
            public string Said { get; set; }
            public bool Clash { get; set; }
            /// <summary>For a clash: 0 keeps the area's, 1 adds this under a new name, 2 replaces.</summary>
            public int Choice { get; set; } = 1;
            public AvBitmap Ours { get; set; }
            public AvBitmap Incoming { get; set; }
            public bool HasIncoming => Incoming != null;
            internal DsTexture Texture;
        }

        /// <summary>Everything adding the textures would do, for the user to look over first.</summary>
        public sealed class TexturePlan
        {
            public List<PictureChoice> Pictures { get; } = new List<PictureChoice>();
            /// <summary>Textures to check: clashes first, then new ones.</summary>
            public List<PictureChoice> Shown => Pictures.Where(p => p.Said != Same).OrderByDescending(p => p.Clash).ThenBy(p => p.Name).ToList();
            public string AlreadyThere
            {
                get
                {
                    List<string> same = Pictures.Where(p => p.Said == Same).Select(p => p.Name).ToList();
                    return same.Count == 0 ? null : $"Already in the pack, nothing to add: {string.Join(", ", same.Take(12))}{(same.Count > 12 ? $" and {same.Count - 12} more" : "")}.";
                }
            }
            public bool HasAlreadyThere => AlreadyThere != null;
            public string Where { get; set; }
            public string Memory { get; set; }
            public bool TooBig { get; set; }
            public int Adding => Pictures.Count(p => p.Said != Same);
            internal List<(int pack, List<byte> areas)> Packs = new List<(int, List<byte>)>();
            internal List<string> Unreadable = new List<string>();
        }

        private const string Same = "Already there";

        // The field's 3D texture budget, shared by the map, buildings and people (VRAM banks A+B and E).
        private const int FieldTextureBytes = 256 * 1024, FieldPaletteBytes = 64 * 1024;

        // Tiles drawn with the tileset's pictures, not the area's.
        private readonly HashSet<int> _ownTiles = new HashSet<int>();

        public TexturePlan PlanPictures()
        {
            if (_set == null || _set.PictureFiles.Count == 0) { Warning = "This tileset has no texture files."; return null; }
            HashSet<string> used = new HashSet<string>(PicturesInUse(), StringComparer.OrdinalIgnoreCase);
            List<KeyValuePair<string, string>> wanted = _set.PictureFiles.Where(kv => used.Contains(kv.Key)).ToList();
            if (wanted.Count == 0) { Warning = "No painted tile uses the tileset's textures."; return null; }
            if (wanted.Count > NsbtxWriter.MostPictures) { Warning = $"{wanted.Count} textures are needed, but a texture pack holds at most {NsbtxWriter.MostPictures}."; return null; }

            TexturePlan plan = new TexturePlan();
            Dictionary<string, (byte[] rgba, int w, int h)> ours = ReadWantedPictures(wanted, plan);
            if (plan.Pictures.Count == 0) { Warning = "No texture could be read."; return null; }

            // Every area whose headers show this map; areas sharing a pack share the new one.
            List<byte> areas = AreasOfMap?.Invoke() ?? new List<byte>();
            if (!areas.Contains(_areaId)) areas.Insert(0, _areaId);
            foreach (IGrouping<int, byte> g in areas.GroupBy(a => (int)new AreaData(a).MapTileset)) plan.Packs.Add((g.Key, g.ToList()));

            Dictionary<int, (NsbtxWriter.Contents contents, Dictionary<string, (byte[] rgba, int w, int h)> pictures)> incoming = new Dictionary<int, (NsbtxWriter.Contents contents, Dictionary<string, (byte[] rgba, int w, int h)> pictures)>();
            foreach ((int pack, List<byte> _) in plan.Packs)
            {
                byte[] bytes = ArchiveMembers.ReadOrNull(DirNames.MapTextures, pack);
                incoming[pack] = (bytes == null ? null : NsbtxWriter.Read(bytes),
                                bytes == null ? new Dictionary<string, (byte[] rgba, int w, int h)>() : PicturesInPack(bytes, PalettesOf));
            }

            CompareWithPacks(plan, incoming, ours);
            MeasureMemory(plan, incoming);
            if (plan.Adding == 0) { Warning = null; Note = "Every texture these tiles use is already there."; return null; }
            plan.Where = plan.Packs.Count == 1
                ? $"Area {string.Join(", ", plan.Packs[0].areas)} gets a new pack: its own textures plus these."
                : $"This map shows under areas {string.Join(", ", plan.Packs.SelectMany(p => p.areas))}; each gets a new pack with its textures plus these.";
            return plan;
        }

        private Dictionary<string, (byte[] rgba, int w, int h)> ReadWantedPictures(List<KeyValuePair<string, string>> wanted, TexturePlan plan)
        {
            Dictionary<string, (byte[] rgba, int w, int h)> ours = new Dictionary<string, (byte[] rgba, int w, int h)>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, string> kv in wanted)
            {
                try
                {
                    if (!AnyPng.TryReadRgbaFile(kv.Value, out byte[] rgba, out int w, out int h, out _))
                    { plan.Unreadable.Add(Path.GetFileName(kv.Value)); continue; }
                    DsTexture texture = DsTexture.From(rgba, w, h, kv.Key);
                    texture.PaletteNames = OwnFaces().Where(f => f.Picture == kv.Key && !string.IsNullOrEmpty(f.Palette))
                        .Select(f => f.Palette).Prepend(kv.Key).Distinct().ToList();
                    ours[kv.Key] = AsTheGameHasIt(texture) ?? (rgba, w, h);
                    plan.Pictures.Add(new PictureChoice { Name = kv.Key, Texture = texture, Ours = ImageConverter.FromRgba(rgba, w, h) });
                }
                catch (Exception ex) { AppLogger.WarnOnce("MapTilesViewModel.PlanPictures", ex); plan.Unreadable.Add(Path.GetFileName(kv.Value)); }
            }
            return ours;
        }

        private static void CompareWithPacks(TexturePlan plan,
            Dictionary<int, (NsbtxWriter.Contents contents, Dictionary<string, (byte[] rgba, int w, int h)> pictures)> incoming,
            Dictionary<string, (byte[] rgba, int w, int h)> ours)
        {
            foreach (PictureChoice choice in plan.Pictures)
            {
                bool clash = false, missing = false;
                foreach ((int pack, List<byte> _) in plan.Packs)
                {
                    (NsbtxWriter.Contents contents, Dictionary<string, (byte[] rgba, int w, int h)> pictures) = incoming[pack];
                    string had = contents?.Textures.FirstOrDefault(n => string.Equals(n, NitroDictionary.Fit(choice.Name), StringComparison.OrdinalIgnoreCase));
                    if (had == null) { missing = true; continue; }
                    if (pictures.TryGetValue(had, out (byte[] rgba, int w, int h) packed) && !LooksTheSame(ours[choice.Name], packed))
                    {
                        clash = true;
                        choice.Incoming ??= ImageConverter.FromRgba(packed.rgba, packed.w, packed.h);
                    }
                }
                choice.Clash = clash;
                choice.Said = clash ? "A different picture with the same name" : missing ? "New" : Same;
            }
        }

        // Video memory the field needs afterwards, worst of the map's areas: map pack plus buildings.
        private static void MeasureMemory(TexturePlan plan,
            Dictionary<int, (NsbtxWriter.Contents contents, Dictionary<string, (byte[] rgba, int w, int h)> pictures)> incoming)
        {
            int worstTex = 0, worstPal = 0;
            foreach ((int pack, List<byte> packAreas) in plan.Packs)
            {
                NsbtxWriter.Contents contents = incoming[pack].contents;
                int tex = contents?.TextureBytes ?? 0, pal = contents?.PaletteBytes ?? 0;
                foreach (PictureChoice c in plan.Pictures.Where(c => c.Said != Same))
                {
                    tex += c.Texture.Pixels?.Length ?? 0;
                    pal += c.Texture.Colours.Length * 2;
                }
                int buildings = 0, buildingPalettes = 0;
                try
                {
                    byte[] bld = ArchiveMembers.ReadOrNull(DirNames.BuildingTextures, new AreaData(packAreas[0]).BuildingsTileset);
                    NsbtxWriter.Contents b = bld != null ? NsbtxWriter.Read(bld) : null;
                    buildings = b?.TextureBytes ?? 0; buildingPalettes = b?.PaletteBytes ?? 0;
                }
                catch (Exception caught) when (AppLogger.Noted("MapTilesViewModel.PlanPictures.2", caught)) { }   // without the building pack its size counts as zero
                worstTex = Math.Max(worstTex, tex + buildings);
                worstPal = Math.Max(worstPal, pal + buildingPalettes);
            }
            plan.TooBig = worstTex > FieldTextureBytes || worstPal > FieldPaletteBytes;
            plan.Memory = $"Map and building textures would take {worstTex / 1024} of the field's {FieldTextureBytes / 1024} KB, "
                        + $"palettes {Math.Max(1, worstPal / 1024)} of {FieldPaletteBytes / 1024} KB. People and objects on screen use the rest.";
        }

        // Two pictures match when nearly every texel has the same 15-bit colour and transparency.
        private static bool LooksTheSame((byte[] rgba, int w, int h) a, (byte[] rgba, int w, int h) b)
        {
            if (a.w != b.w || a.h != b.h || a.rgba == null || b.rgba == null) return false;
            int n = a.w * a.h, differ = 0;
            for (int i = 0; i < n; i++)
            {
                int k = i * 4;
                bool clearA = a.rgba[k + 3] < 128, clearB = b.rgba[k + 3] < 128;
                if (clearA && clearB) continue;
                if (clearA != clearB || (a.rgba[k] >> 3) != (b.rgba[k] >> 3) || (a.rgba[k + 1] >> 3) != (b.rgba[k + 1] >> 3)
                    || (a.rgba[k + 2] >> 3) != (b.rgba[k + 2] >> 3)) differ++;
            }
            return differ <= n / 50;
        }

        private IEnumerable<string> PalettesOf(string picture) =>
            _set.Tiles.SelectMany(t => t.Faces).Where(f => string.Equals(f.Picture, picture, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(f.Palette))
                .Select(f => f.Palette).Distinct();

        private IEnumerable<MapTileset.Face> OwnFaces() =>
            _ownTiles.Where(t => t >= 0 && t < _set.Tiles.Count).SelectMany(t => _set.Tiles[t].Faces);

        public string PutPicturesIntoTheRom(TexturePlan plan)
        {
            if (plan == null || _set == null) return "Nothing to add.";
            Dictionary<int, byte[]> packBytes = plan.Packs.ToDictionary(p => p.pack, p => ArchiveMembers.ReadOrNull(DirNames.MapTextures, p.pack));
            Dictionary<int, NsbtxWriter.Contents> contents = packBytes.ToDictionary(kv => kv.Key, kv => kv.Value == null ? null : NsbtxWriter.Read(kv.Value));
            HashSet<string> taken = new HashSet<string>(contents.Values.Where(c => c != null).SelectMany(c => c.Textures.Concat(c.Palettes.Keys)), StringComparer.OrdinalIgnoreCase);
            taken.UnionWith(_set.Tiles.SelectMany(t => t.Faces).SelectMany(f => new[] { f.Picture, f.Palette }).Where(n => !string.IsNullOrEmpty(n)));

            HashSet<string> replace = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<string> renamed = new List<string>();
            List<DsTexture> adding = ChooseNames(plan, contents.Values, taken, replace, renamed);
            if (adding.Count == 0) { Warning = null; Note = "Nothing added."; return null; }

            List<string> wrote = new List<string>();
            try
            {
                string whynot = WritePacks(plan, packBytes, adding, replace, wrote);
                if (whynot != null) { Warning = whynot; return whynot; }

                _pictures = PicturesOfTheArea();
                foreach (KeyValuePair<string, (byte[] rgba, int w, int h)> kv in PicturesOfTheSet(_set)) _pictures.TryAdd(kv.Key, kv.Value);
                _fromAbove.Clear();
                _thumbs.Clear();
                FillList();
                FillSmart();
                ShowTile();
                GridChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                AppLogger.Error("MapTiles.PutPictures: " + ex.Message);
                Warning = ex.Message;
                return ex.Message;
            }

            Dirty = true;
            string said = $"Added {adding.Count} textures: {string.Join("; ", wrote)}."
                        + (renamed.Count > 0 ? $" Renamed {string.Join(", ", renamed)}." : "")
                        + (replace.Count > 0 ? $" Replaced the area's {string.Join(", ", replace)}." : "");
            if (plan.Unreadable.Count > 0) said += $" Unreadable: {string.Join(", ", plan.Unreadable.Take(5))}.";
            Warning = AnimationClashes(adding);
            Note = said;
            Changed?.Invoke(this, EventArgs.Empty);
            return null;
        }

        // Names in a pack are at most 16 characters.
        private static string FreshName(string name, HashSet<string> taken)
        {
            for (int k = 2; ; k++)
            {
                string tail = "_" + k, called = (name.Length + tail.Length > 16 ? name.Substring(0, 16 - tail.Length) : name) + tail;
                if (taken.Add(called)) return called;
            }
        }

        private void RenameFaces(string picture, Func<MapTileset.Face, bool> which, Action<MapTileset.Face> change)
        {
            foreach (MapTileset.Face f in OwnFaces().Where(f => f.Picture == picture && which(f)).ToList()) change(f);
        }

        private List<DsTexture> ChooseNames(TexturePlan plan, IEnumerable<NsbtxWriter.Contents> packs, HashSet<string> taken,
                                            HashSet<string> replace, List<string> renamed)
        {
            List<DsTexture> adding = new List<DsTexture>();
            foreach (PictureChoice c in plan.Pictures)
            {
                if (c.Said == Same) continue;
                DsTexture t = c.Texture;
                if (c.Clash && c.Choice == 1)
                {
                    string old = t.Name, now = FreshName(old, taken);
                    Dictionary<string, string> palettes = new Dictionary<string, string>();
                    foreach (string pal in t.PaletteNames) palettes[pal] = FreshName(pal, taken);
                    RenameFaces(old, f => true, f => { if (f.Palette != null && palettes.TryGetValue(f.Palette, out string p2)) f.Palette = p2; f.Picture = now; });
                    if (_set.PictureFiles.TryGetValue(old, out string file)) _set.PictureFiles[now] = file;
                    t.Name = now;
                    t.PaletteNames = t.PaletteNames.Select(pal => palettes[pal]).ToList();
                    renamed.Add($"{old} as {now}");
                }
                else if (c.Clash && c.Choice == 2) replace.Add(t.Name);

                // A palette is renamed when a different one in the pack already has its name.
                if (!replace.Contains(t.Name))
                {
                    byte[] existing = t.Colours.SelectMany(v => new[] { (byte)v, (byte)(v >> 8) }).ToArray();
                    for (int i = 0; i < t.PaletteNames.Count; i++)
                    {
                        string pal = t.PaletteNames[i];
                        bool differs = packs.Any(cn => cn != null && cn.Palettes.TryGetValue(NitroDictionary.Fit(pal), out byte[] incoming)
                                                       && !incoming.Take(existing.Length).SequenceEqual(existing.Take(incoming.Length)));
                        if (!differs) continue;
                        string now = FreshName(pal, taken);
                        RenameFaces(t.Name, f => f.Palette == pal, f => f.Palette = now);
                        t.PaletteNames[i] = now;
                    }
                }
                adding.Add(t);
            }
            return adding;
        }

        private string WritePacks(TexturePlan plan, Dictionary<int, byte[]> packBytes, List<DsTexture> adding, HashSet<string> replace, List<string> wrote)
        {
            foreach ((int pack, List<byte> packAreas) in plan.Packs)
            {
                byte[] bytes = packBytes[pack];
                NsbtxWriter.Result made = bytes != null ? NsbtxWriter.Extend(bytes, adding, replace) : null;
                if (made == null || made.Whynot != null) made = NsbtxWriter.Build(adding);
                if (made.Whynot != null) return made.Whynot;
                int newPack = NarcAPI.Narc.MemberCount(GameDirs[DirNames.MapTextures].unpackedDir);
                ArchiveMembers.Write(ArchiveMembers.PathOf(DirNames.MapTextures, newPack), made.Bytes);
                foreach (byte a in packAreas)
                {
                    AreaData area = new AreaData(a);
                    area.MapTileset = (ushort)newPack;
                    area.SaveToFileDefaultDir(a, showSuccessMessage: false);
                    AppEvents.RaiseAreaDataSaved(this, a);
                }
                wrote.Add($"pack {newPack} for area{(packAreas.Count > 1 ? "s" : "")} {string.Join(", ", packAreas)}");
            }
            return null;
        }

        // The game copies its own frames over any texture with an animated name.
        private static string AnimationClashes(List<DsTexture> adding)
        {
            string[] Formats = { "none", "A3I5", "4-colour", "16-colour", "256-colour", "compressed", "A5I3", "direct" };
            FieldTextureAnimations anims = MatrixSceneBuilder.FieldAnimations();
            List<string> clashes = new List<string>();
            foreach (DsTexture t in adding)
            {
                FieldTextureAnimations.Entry entry = anims?.For(t.Name);
                if (entry?.FramePack == null) continue;
                try
                {
                    NSBTXLoader.LoadNsbtx(new MemoryStream(entry.FramePack), out List<NSBMDTexture> frames, out _);
                    NSBMDTexture first = frames?.FirstOrDefault();
                    if (first != null && (first.Width != t.Width || first.Height != t.Height || first.Format != (int)t.Format))
                        clashes.Add($"{t.Name} ({t.Width}x{t.Height} {Formats[(int)t.Format & 7]} here, frames {first.Width}x{first.Height} {Formats[first.Format & 7]})");
                }
                catch (Exception caught) when (AppLogger.Noted("MapTilesViewModel.PutPicturesIntoTheRom", caught)) { }   // a texture that can't be read isn't checked for clashes
            }
            return clashes.Count == 0 ? null
                : "The game animates these by name with frames that don't fit: " + string.Join(", ", clashes)
                  + ". Rename the texture or give it new frames in the Tile tab.";
        }

        // Pictures the painted tiles need that the tileset itself supplies.
        private IEnumerable<string> PicturesInUse()
        {
            HashSet<int> tiles = new HashSet<int>();
            foreach ((int x, int z, TileGrid.Square square) p in _painter.Grid.Placed())
                if (_ownTiles.Contains(p.square.Tile)) tiles.Add(p.square.Tile);
            return tiles.Where(t => t >= 0 && t < _set.Tiles.Count)
                        .SelectMany(t => _set.Tiles[t].Pictures)
                        .Where(p => !string.IsNullOrEmpty(p)).Distinct();
        }

        // How a picture looks once it is in the game's format, so it compares like for like with a pack's.
        private static (byte[] rgba, int w, int h)? AsTheGameHasIt(DsTexture t)
        {
            NsbtxWriter.Result made = NsbtxWriter.Build(new[] { t });
            if (made.Whynot != null) return null;
            return PicturesInPack(made.Bytes).TryGetValue(t.Name, out (byte[] rgba, int w, int h) got) ? got : null;
        }

        /// <param name="palettesFor">Palette names a texture is drawn with, best first.</param>
        private static Dictionary<string, (byte[] rgba, int w, int h)> PicturesInPack(byte[] bytes, Func<string, IEnumerable<string>> palettesFor = null)
        {
            Dictionary<string, (byte[], int, int)> known = new Dictionary<string, (byte[], int, int)>(StringComparer.OrdinalIgnoreCase);
            try
            {
                NSBMD container = new global::LibNDSFormats.NSBMD.NSBMD();
                container.Materials = NSBTXLoader.LoadNsbtx(new MemoryStream(bytes), out container.Textures, out container.Palettes);
                foreach (NSBMDMaterial mat in container.Materials ?? new List<global::LibNDSFormats.NSBMD.NSBMDMaterial>())
                {
                    if (string.IsNullOrEmpty(mat.Texname) || known.ContainsKey(mat.Texname)) continue;
                    IEnumerable<string> wanted = (palettesFor?.Invoke(mat.Texname) ?? Enumerable.Empty<string>()).Append(mat.Texname).Append(mat.Texname + "_pl");
                    NSBMDPalette pal = wanted.Select(n => container.Palettes?.FirstOrDefault(q => q.Palname == n)).FirstOrDefault(q => q != null);
                    if (pal != null) { mat.Palname = pal.Palname; mat.Paldata = pal.Paldata; }
                    NsbmdTextureData picture = NsbmdTextureDecoder.Decode(mat);
                    if (picture?.Rgba != null) known[mat.Texname] = (picture.Rgba, picture.Width, picture.Height);
                }
            }
            catch (Exception ex) { AppLogger.Error("MapTiles.PicturesInPack: " + ex.Message); }
            return known;
        }

        private (HashSet<string> pictures, HashSet<string> palettes) WhatTheAreaCarries()
        {
            HashSet<string> pictures = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> palettes = new HashSet<string>(StringComparer.Ordinal);
            try
            {
                AreaData area = new AreaData(_areaId);
                byte[] pack = ArchiveMembers.ReadOrNull(DirNames.MapTextures, area.MapTileset);
                if (pack == null) return (pictures, palettes);

                NSBTXLoader.LoadNsbtx(new MemoryStream(pack), out List<NSBMDTexture> textures, out List<NSBMDPalette> colours);
                foreach (NSBMDTexture t in textures ?? new List<global::LibNDSFormats.NSBMD.NSBMDTexture>())
                    if (!string.IsNullOrEmpty(t.Texname)) pictures.Add(t.Texname.TrimEnd('\0'));
                foreach (NSBMDPalette p in colours ?? new List<global::LibNDSFormats.NSBMD.NSBMDPalette>())
                    if (!string.IsNullOrEmpty(p.Palname)) palettes.Add(p.Palname.TrimEnd('\0'));
            }
            catch (Exception ex) { AppLogger.Error("MapTiles.AreaPictures: " + ex.Message); }
            return (pictures, palettes);
        }

        private Dictionary<string, (byte[] rgba, int w, int h)> PicturesOfTheArea()
        {
            Dictionary<string, (byte[], int, int)> known = new Dictionary<string, (byte[], int, int)>(StringComparer.Ordinal);
            try
            {
                AreaData area = new AreaData(_areaId);
                byte[] pack = ArchiveMembers.ReadOrNull(DirNames.MapTextures, area.MapTileset);
                if (pack == null) return known;

                NSBMD container = new global::LibNDSFormats.NSBMD.NSBMD();
                container.Materials = NSBTXLoader.LoadNsbtx(new MemoryStream(pack),
                                                            out container.Textures, out container.Palettes);
                if (container.Materials == null) return known;
                _areaTextures = container.Textures ?? new List<global::LibNDSFormats.NSBMD.NSBMDTexture>();
                _areaPalettes = container.Palettes ?? new List<global::LibNDSFormats.NSBMD.NSBMDPalette>();
                _paired.Clear();

                // The map's own materials say which palette each texture is drawn with.
                Dictionary<string, string> drawnWith = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (NSBMDMaterial m in _map?.MapModel?.Models?.FirstOrDefault()?.Materials ?? new List<global::LibNDSFormats.NSBMD.NSBMDMaterial>())
                    if (!string.IsNullOrEmpty(m.Texname) && !string.IsNullOrEmpty(m.Palname)) drawnWith.TryAdd(m.Texname, m.Palname);
                foreach (NSBMDMaterial mat in container.Materials)
                {
                    if (string.IsNullOrEmpty(mat.Texname) || known.ContainsKey(mat.Texname)) continue;
                    if (drawnWith.TryGetValue(mat.Texname, out string palName) && container.Palettes?.FirstOrDefault(q => q.Palname == palName) is NSBMDPalette pal)
                    { mat.Palname = pal.Palname; mat.Paldata = pal.Paldata; }
                    NsbmdTextureData picture = NsbmdTextureDecoder.Decode(mat);
                    if (picture?.Rgba != null)
                        known[mat.Texname] = (picture.Rgba, picture.Width, picture.Height);
                }
            }
            catch (Exception ex) { AppLogger.Error("MapTiles.Pictures: " + ex.Message); }
            return known;
        }

        private static Dictionary<string, (byte[] rgba, int w, int h)> PicturesOfTheSet(MapTileset set)
        {
            Dictionary<string, (byte[], int, int)> known = new Dictionary<string, (byte[], int, int)>(StringComparer.Ordinal);
            if (set == null) return known;
            foreach (KeyValuePair<string, string> kv in set.PictureFiles)
            {
                try
                {
                    if (AnyPng.TryReadRgbaFile(kv.Value, out byte[] rgba, out int w, out int h, out _))
                        known[kv.Key] = (rgba, w, h);
                }
                catch (Exception ex) { AppLogger.Error("MapTiles.SetPicture: " + ex.Message); }
            }
            return known;
        }

        private bool Picture(string name, string colours, out byte[] rgba, out int width, out int height)
        {
            if (name != null && !string.IsNullOrEmpty(colours))
            {
                string key = name + "|" + colours;
                if (!_paired.TryGetValue(key, out (byte[] rgba, int w, int h) pair))
                    _paired[key] = pair = Decode(name, colours);
                if (pair.rgba != null) { rgba = pair.rgba; width = pair.w; height = pair.h; return true; }
            }
            if (name != null && _pictures.TryGetValue(name, out (byte[] rgba, int w, int h) found))
            { rgba = found.rgba; width = found.w; height = found.h; return true; }
            rgba = null; width = height = 0; return false;
        }

        private readonly Dictionary<string, (byte[] rgba, int w, int h)> _paired = new Dictionary<string, (byte[] rgba, int w, int h)>();
        private List<global::LibNDSFormats.NSBMD.NSBMDTexture> _areaTextures = new List<global::LibNDSFormats.NSBMD.NSBMDTexture>();
        private List<global::LibNDSFormats.NSBMD.NSBMDPalette> _areaPalettes = new List<global::LibNDSFormats.NSBMD.NSBMDPalette>();

        private (byte[] rgba, int w, int h) Decode(string picture, string colours)
        {
            try
            {
                NSBMDTexture tex = _areaTextures.FirstOrDefault(t => t.Texname == picture);
                NSBMDPalette pal = _areaPalettes.FirstOrDefault(p => p.Palname == colours);
                if (tex == null || pal == null) return (null, 0, 0);
                NSBMDMaterial stand_in = new global::LibNDSFormats.NSBMD.NSBMDMaterial
                {
                    Texdata = tex.Texdata, Spdata = tex.Spdata, Texname = tex.Texname,
                    Texoffset = tex.Texoffset, Texsize = tex.Texsize,
                    Width = tex.Width, Height = tex.Height, Format = tex.Format, Color0 = tex.Color0,
                    Paldata = pal.Paldata, Palname = pal.Palname, Paloffset = pal.Paloffset, Palsize = pal.Palsize,
                };
                NsbmdTextureData data = NsbmdTextureDecoder.Decode(stand_in);
                return data?.Rgba == null ? (null, 0, 0) : (data.Rgba, data.Width, data.Height);
            }
            catch (Exception ex) { AppLogger.Error("MapTiles.Decode: " + ex.Message); return (null, 0, 0); }
        }

        private AvBitmap BigThumbnail(int tile)
        {
            const int Size = 160;
            try
            {
                if (_set != null && tile >= 0 && tile < _set.Tiles.Count)
                    return ToBitmap(DrawThumbnail(_set.Tiles[tile], Size), Size, Size);
            }
            catch (Exception ex) { AppLogger.Error("MapTiles.BigThumbnail: " + ex.Message); }
            return null;
        }

        private AvBitmap Thumbnail(int tile)
        {
            if (_thumbs.TryGetValue(tile, out AvBitmap had)) return had;
            const int Size = 44;
            AvBitmap made = null;
            try
            {
                if (_set != null && tile >= 0 && tile < _set.Tiles.Count)
                    made = ToBitmap(DrawThumbnail(_set.Tiles[tile], Size), Size, Size);
            }
            catch (Exception ex) { AppLogger.Error("MapTiles.Thumbnail: " + ex.Message); }
            _thumbs[tile] = made;
            return made;
        }

        public TileGridControl.Look? FromAbove(int tile, int turn)
        {
            if (_fromAbove.TryGetValue((tile, turn), out TileGridControl.Look? had)) return had;
            TileGridControl.Look? made = null;
            try
            {
                if (_set != null && tile >= 0 && tile < _set.Tiles.Count)
                {
                    (byte[] rgba, int wide, int tall, int fromX, int fromZ, int across, int down) drawn = TileThumbnail.DrawWholeFromAbove(_set.Tiles[tile], turn, 16, Picture);
                    if (drawn.wide > 0 && drawn.tall > 0)
                        made = new TileGridControl.Look(ToBitmap(drawn.rgba, drawn.wide, drawn.tall),
                                                        drawn.fromX, drawn.fromZ, drawn.across, drawn.down);
                }
            }
            catch (Exception ex) { AppLogger.Error("MapTiles.FromAbove: " + ex.Message); }
            _fromAbove[(tile, turn)] = made;
            return made;
        }

        private AvBitmap SmartPicture(SmartDrawing drawing)
        {
            const int Cell = 20;
            byte[] rgba = new byte[SmartDrawing.Wide * Cell * SmartDrawing.Tall * Cell * 4];
            int stride = SmartDrawing.Wide * Cell;
            for (int slot = 0; slot < SmartDrawing.Slots; slot++)
            {
                int tile = drawing[slot];
                if (_set == null || tile < 0 || tile >= _set.Tiles.Count) continue;
                (byte[] dots, int w, int h) = TileThumbnail.DrawFromAbove(_set.Tiles[tile], 0, Cell, Picture);
                int ox = (slot % SmartDrawing.Wide) * Cell, oy = (slot / SmartDrawing.Wide) * Cell;
                for (int y = 0; y < Math.Min(h, Cell); y++)
                    Array.Copy(dots, y * w * 4, rgba, ((oy + y) * stride + ox) * 4, Math.Min(w, Cell) * 4);
            }
            return ToBitmap(rgba, stride, SmartDrawing.Tall * Cell);
        }

        private static AvBitmap ToBitmap(byte[] rgba, int w, int h)
        {
            WriteableBitmap bitmap = new global::Avalonia.Media.Imaging.WriteableBitmap(
                new global::Avalonia.PixelSize(w, h), new global::Avalonia.Vector(96, 96),
                global::Avalonia.Platform.PixelFormat.Rgba8888, global::Avalonia.Platform.AlphaFormat.Unpremul);
            using (ILockedFramebuffer locked = bitmap.Lock())
                for (int y = 0; y < h; y++)
                    System.Runtime.InteropServices.Marshal.Copy(rgba, y * w * 4, locked.Address + y * locked.RowBytes, w * 4);
            return bitmap;
        }
    }
}
