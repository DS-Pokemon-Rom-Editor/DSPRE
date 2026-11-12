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
        /// <summary>Animates the chosen picture in game with these frames, converted to fit it.</summary>
        public string AnimateChosen(IReadOnlyList<string> framePaths)
        {
            if (!CanAnimate) return "No picture chosen.";
            string name = _lookPicture;
            try
            {
                byte[] pack = ArchiveMembers.Read(DirNames.MapTextures, new AreaData(_areaId).MapTileset);
                NSBTXLoader.LoadNsbtx(new MemoryStream(pack), out List<NSBMDTexture> textures, out List<NSBMDPalette> palettes);
                NSBMDTexture target = textures?.FirstOrDefault(t => t.Texname == name);
                if (target == null) return Fail($"{name} isn't in this area's textures. Add textures to ROM first.");

                MapTileset.Face face = _set.Tiles.SelectMany(t => t.Faces).FirstOrDefault(f => f.Picture == name && !string.IsNullOrEmpty(f.Palette));
                NSBMDPalette palette = palettes?.FirstOrDefault(pl => pl.Palname == (face?.Palette ?? name))
                              ?? palettes?.FirstOrDefault(pl => pl.Palname == name + "_pl") ?? palettes?.FirstOrDefault(pl => pl.Palname == name);
                ushort[] colours = palette?.Paldata?.Select(c => (ushort)((c.R >> 3) | ((c.G >> 3) << 5) | ((c.B >> 3) << 10))).ToArray()
                              ?? Array.Empty<ushort>();

                List<DsTexture> frames = new List<DsTexture>();
                for (int i = 0; i < framePaths.Count; i++)
                {
                    if (!AnyPng.TryReadRgbaFile(framePaths[i], out byte[] rgba, out int w, out int h, out _))
                        return Fail($"{Path.GetFileName(framePaths[i])} couldn't be read.");
                    if (w != target.Width || h != target.Height)
                        return Fail($"{Path.GetFileName(framePaths[i])} is {w}x{h}; {name} is {target.Width}x{target.Height}.");
                    byte[] texels = DsTexture.TexelsLike(rgba, w, h, target.Format, colours, target.Color0 != 0, out string whynot);
                    if (texels == null) return Fail(whynot);
                    frames.Add(new DsTexture { Name = $"{name}.{i + 1}", Width = w, Height = h, Format = (DsTexture.Kind)target.Format,
                                               Pixels = texels, Colours = colours, FirstColourIsClear = target.Color0 != 0 });
                }
                if (frames.Count == 0 || frames.Count > FieldTextureAnimations.MostFrames)
                    return Fail($"Give 1 to {FieldTextureAnimations.MostFrames} frames.");

                NsbtxWriter.Result built = NsbtxWriter.Build(frames);
                if (built.Whynot != null) return Fail(built.Whynot);

                FieldTextureAnimations list = MatrixSceneBuilder.FieldAnimations(reload: true);
                FieldTextureAnimations.Entry entry = list.For(name);
                if (entry == null) { entry = new FieldTextureAnimations.Entry { Name = name }; list.Entries.Add(entry); }
                entry.FramePack = built.Bytes;
                entry.Frames = Enumerable.Range(0, frames.Count).Select(i => ((byte)i, (byte)AnimationStep)).ToList();
                list.Save();
                MatrixSceneBuilder.FieldAnimations(reload: true);

                int inPack = textures.Count(t => list.For(t.Texname) != null);
                Warning = inPack > 16 ? $"{inPack} of this area's textures are animated; the game animates the first 16." : null;
                Note = $"{name}: {frames.Count} frames";
            }
            catch (Exception ex) when (AppLogger.Noted("MapTilesViewModel.AnimateChosen", ex)) { return Fail(ex.Message); }

            Rebuild();
            Raise(nameof(AnimationNote)); Raise(nameof(IsAnimated));
            return null;
        }

        public void StopAnimatingChosen()
        {
            FieldTextureAnimations list = MatrixSceneBuilder.FieldAnimations(reload: true);
            FieldTextureAnimations.Entry entry = list?.For(_lookPicture);
            if (entry == null) return;
            list.Entries.Remove(entry);
            list.Save();
            MatrixSceneBuilder.FieldAnimations(reload: true);
            Note = $"{_lookPicture} no longer animates.";
            Rebuild();
            Raise(nameof(AnimationNote)); Raise(nameof(IsAnimated));
        }

        private string Fail(string why) { Warning = why; return why; }

        public bool HasChosen => Chosen != null;

        public string TileName
        {
            get => Chosen?.Name ?? "";
            set
            {
                if (Chosen == null || string.IsNullOrWhiteSpace(value) || Chosen.Name == value) return;
                Chosen.Name = value.Trim();
                SetChanged(redraw: false);
            }
        }

        public int TileWide
        {
            get => Chosen?.Wide ?? 1;
            set => Resize(Math.Clamp(value, 1, MapTileset.MostSquares), Chosen?.Deep ?? 1);
        }

        public int TileDeep
        {
            get => Chosen?.Deep ?? 1;
            set => Resize(Chosen?.Wide ?? 1, Math.Clamp(value, 1, MapTileset.MostSquares));
        }

        private void Resize(int wide, int deep)
        {
            MapTileset.Tile tile = Chosen;
            if (tile == null || (tile.Wide == wide && tile.Deep == deep)) return;
            tile.Wide = wide; tile.Deep = deep;
            int dropped = 0;
            foreach (TileGrid g in _painter.AllGrids) dropped += g.Resized(Brush, wide, deep);
            if (dropped > 0) Warning = "";
            SetChanged(redraw: true);
        }

        public void NudgeTile(int dx, int dy, int dz)
        {
            if (Chosen == null) return;
            float step = MapTileset.TileWidth / 8f;
            MapTileset.Nudge(Chosen, dx * step, dy * step, dz * step);
            SetChanged(redraw: true);
        }

        public void TurnTileShape()
        {
            if (Chosen == null) return;
            MapTileset.TurnShape(Chosen);
            foreach (TileGrid g in _painter.AllGrids) g.Resized(Brush, Chosen.Wide, Chosen.Deep);
            SetChanged(redraw: true);
        }

        public void MirrorTile()
        {
            if (Chosen == null) return;
            MapTileset.Mirror(Chosen);
            SetChanged(redraw: true);
        }

        public void DuplicateTile()
        {
            if (_set == null) return;
            int made = _set.DuplicateTile(Brush);
            if (made < 0) return;
            SetChanged(redraw: false);
            Brush = made;
        }

        public void RemoveTile()
        {
            if (_set == null || Chosen == null) return;
            int gone = Brush;
            _set.RemoveTile(gone, _painter.AllGrids.ToArray());
            _fromAbove.Clear();
            _thumbs.Clear();
            SetChanged(redraw: true);
            Brush = Math.Min(gone, _set.Tiles.Count - 1);
        }

        public void MoveTile(int by)
        {
            if (_set == null || Chosen == null) return;
            int to = Brush + by;
            if (to < 0 || to >= _set.Tiles.Count) return;
            _set.SwapTiles(Brush, to, _painter.AllGrids.ToArray());
            _fromAbove.Clear();
            _thumbs.Clear();
            int now = to;
            SetChanged(redraw: true);
            Brush = now;
        }

        public void AddTilesFrom(string path)
        {
            if (_set == null) return;
            bool incoming = Path.GetExtension(path).Equals(".pdsts", StringComparison.OrdinalIgnoreCase);
            MapTileset other = incoming ? PdstsFile.Read(path, out string whynot) : MapTileset.FromObj(path, out whynot);
            if (other == null) { Warning = whynot; return; }
            if (incoming) TileCollisions.ReadMeta(path, other);
            SizePlaces(other);
            Dictionary<MapTileset.Tile, int> places = incoming ? TileCollisions.ListedPlaces(other) : null;
            int first = _set.Append(other);
            if (incoming)
                for (int i = first; i < _set.Tiles.Count; i++)
                    if (places.TryGetValue(other.Tiles[i - first], out int listed)) _tileSource[_set.Tiles[i]] = (path, listed);
            if (other.PictureFiles.Count > 0)
                for (int i = first; i < _set.Tiles.Count; i++) _ownTiles.Add(i);
            foreach (KeyValuePair<string, (byte[] rgba, int w, int h)> kv in PicturesOfTheSet(other)) _pictures.TryAdd(kv.Key, kv.Value);
            SetChanged(redraw: false);
            FillSmart();
            Brush = first;
            Note = $"Appended {other.Tiles.Count} tiles.";
            Warning = MissingPictures(_set);
            Raise(nameof(HasOwnPictures));
            Raise(nameof(SetHasMovement));
        }

        public bool TileJoinsAcross { get => Chosen?.AcrossTileable ?? false; set => Flag(t => t.AcrossTileable = value); }
        public bool TileJoinsDown { get => Chosen?.DownTileable ?? false; set => Flag(t => t.DownTileable = value); }
        public bool TileRepeatsAcross { get => Chosen?.PictureRepeatsAcross ?? false; set => Flag(t => t.PictureRepeatsAcross = value); }
        public bool TileRepeatsDown { get => Chosen?.PictureRepeatsDown ?? false; set => Flag(t => t.PictureRepeatsDown = value); }
        public bool TileAcrossTheMap { get => Chosen?.PictureAcrossTheMap ?? false; set => Flag(t => t.PictureAcrossTheMap = value); }

        private void Flag(Action<MapTileset.Tile> change)
        {
            if (Chosen == null) return;
            change(Chosen);
            SetChanged(redraw: false);
        }

        public string TileGroundKind { get => Hex(Chosen?.WholeCollision(TileCollisions.TypeLayer)); set => SetMovement(TileCollisions.TypeLayer, value); }
        public string TileWalk { get => Hex(Chosen?.WholeCollision(TileCollisions.CollisionLayer)); set => SetMovement(TileCollisions.CollisionLayer, value); }
    }
}
