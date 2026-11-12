using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using DSPRE.Avalonia.Data;
using DSPRE.Editors;
using DSPRE.HgEngine;
using DSPRE.ROMFiles;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Trainers
{
    public partial class TrainerSpriteEditorViewModel
    {
        // Drawing and cells are stored packed. Only the drawing is packed again on save.
        private string _packedTilesPath;
        private byte _packedTilesMarker;
        private NanrFile _namingAnimations;
        private byte[] _topBarBackground;

        private string LoadNamingIcon(int animation)
        {
            try
            {
                string dir = RomInfo.GameDirs[_set.Archive].unpackedDir;
                string File4(int i) => ArchiveMembers.PathOf(_set.Archive, i);

                int files = DSPRE.ROMFiles.NamingScreenIcons.SpriteTilesFile;
                _packedTilesPath = File4(files);
                byte[] packedTiles = Member(files);
                _packedTilesMarker = GraphicAssets.SqueezeMarker(packedTiles);
                _tilesPath = _packedTilesPath;
                _tile = ReadDrawing(GraphicAssets.Unsqueeze(packedTiles));
                int colours = DSPRE.ROMFiles.NamingScreenIcons.SpriteColoursFile;
                _palPath = File4(colours); _palEntry = colours;
                _pal = ReadPaletteRows(Member(colours));
                int cells = DSPRE.ROMFiles.NamingScreenIcons.SpriteCellsFile;
                _sprite = ReadCells(GraphicAssets.Unsqueeze(Member(cells)));
                _cellsFromSource = false;
                _sourcePngPath = null;
                _namingAnimations = NanrFile.Read(GraphicAssets.Unsqueeze(Member(DSPRE.ROMFiles.NamingScreenIcons.SpriteAnimationsFile)));
                _bankMap = FramesOfAnimation(animation);

                _parts.Clear();
                _parts.Add(new SpritePart { Entry = animation, Tile = _tile, Pal = _pal, Sprite = _sprite, TilesPath = _tilesPath, PalPath = _palPath, PalEntry = _palEntry });
                Activate(0);
                OnPropertyChanged(nameof(HasLinkedSets));
                OnPropertyChanged(nameof(SelectedClassIndex));

                ZoomFactor = 4;
                BuildFrameThumbnails();
                _activePaletteBank = -1;
                _selectedStripFrame = -1;
                SelectedFrameIndex = 0;
                OnPropertyChanged(nameof(IsFlatSheetMode));
                OnPropertyChanged(nameof(FrameCount));
                OnPropertyChanged(nameof(CanUseSheets));
                OnPropertyChanged(nameof(CanUseAnimationSheets));
                HasUnsavedChanges = false;
                _paletteDirty = false;
                ResetSteps();
                StatusText = Frames(FrameCount);
                _topBarFrame = 0; _topBarHold = 0;
                BuildTopBarBackground(dir);
                RebuildTopBar();
                StartTopBar();
                return null;
            }
            catch (Exception ex)
            {
                _tile = null; _pal = null; _sprite = null;
                StatusText = "Couldn't load: " + ex.Message;
                AppLogger.Error("TrainerSpriteEditorViewModel.LoadNamingIcon failed: " + ex.Message);
                return ex.Message;
            }
        }

        // Each cell the animation shows, once, in the order it first appears.
        private int[] FramesOfAnimation(int animation)
        {
            List<int> seen = new List<int>();
            if (_namingAnimations != null && animation < _namingAnimations.Sequences.Count)
                for (int f = 0; f < _namingAnimations.Sequences[animation].Frames.Count; f++)
                {
                    int cell = _namingAnimations.CellOf(animation, f);
                    if (cell >= 0 && cell < _sprite.Cells.Count && !seen.Contains(cell)) seen.Add(cell);
                }
            if (seen.Count == 0) seen.Add(0);
            return seen.ToArray();
        }

        public bool HasTopBar => _set.NamingScreen;
        public bool ShowsAnimationsTab => !_set.NamingScreen;

        private Bitmap _topBarPreview;
        public Bitmap TopBarPreview { get => _topBarPreview; private set => Set(ref _topBarPreview, value); }

        private string _sampleName = "Name";
        public string SampleName
        {
            get => _sampleName;
            set { if (Set(ref _sampleName, value ?? "")) RebuildTopBar(); }
        }

        private void BuildTopBarBackground(string dir)
        {
            _topBarBackground = null;
            try
            {
                byte[] Read(int i) => GraphicAssets.Unsqueeze(Member(i));
                NitroBgCodec.BgImage bg = NitroBgCodec.Composite(
                    Read(DSPRE.ROMFiles.NamingScreenIcons.BackgroundTilesFile),
                    Read(DSPRE.ROMFiles.NamingScreenIcons.BackgroundColoursFile),
                    Read(DSPRE.ROMFiles.NamingScreenIcons.TopScreenMapFile), transparentZero: false);
                if (bg != null && bg.Width == DsBgScreen.Width && bg.Height >= TopBarHeight) _topBarBackground = bg.Rgba;
            }
            catch (Exception ex) { AppLogger.Error("Naming screen top bar: " + ex.Message); }
        }

        private const int TopBarHeight = 48;
        private int _topBarFrame, _topBarHold;
        private global::Avalonia.Threading.DispatcherTimer _topBarTimer;
        private DSPRE.ROMFiles.FieldFont _topBarFont;

        private void StartTopBar()
        {
            if (_topBarTimer != null) return;
            _topBarTimer = new global::Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1.0 / 60) };
            _topBarTimer.Tick += (_, _) => StepTopBar();
            _topBarTimer.Start();
        }

        public void StopTopBar() { _topBarTimer?.Stop(); _topBarTimer = null; }

        private void StepTopBar()
        {
            if (_namingAnimations == null || _trClassID >= _namingAnimations.Sequences.Count) return;
            List<NanrFile.Frame> frames = _namingAnimations.Sequences[_trClassID].Frames;
            if (frames.Count < 2) return;
            if (++_topBarHold < Math.Max(1, (int)frames[_topBarFrame].Delay)) return;
            _topBarHold = 0;
            _topBarFrame = (_topBarFrame + 1) % frames.Count;
            RebuildTopBar();
        }

        // The naming screen's top as the game draws it: background, icon, underlines, sample name.
        private void RebuildTopBar()
        {
            if (!_set.NamingScreen || _sprite == null) return;
            int w = DsBgScreen.Width, h = DsBgScreen.Height;
            byte[] rgba = new byte[w * h * 4];
            if (_topBarBackground != null) Array.Copy(_topBarBackground, rgba, rgba.Length);
            else for (int i = 3; i < rgba.Length; i += 4) rgba[i] = 255;

            IReadOnlyList<NamingScreenIcons.Icon> icons = DSPRE.ROMFiles.NamingScreenIcons.All;
            int letters = icons.FirstOrDefault(i => i.Animation == _trClassID)?.Letters ?? 7;
            int slotCell = _namingAnimations?.CellOf(DSPRE.ROMFiles.NamingScreenIcons.LetterSlotAnimation, 0) ?? -1;
            for (int i = 0; i < letters && slotCell >= 0; i++)
                BlitCell(rgba, slotCell, DSPRE.ROMFiles.NamingScreenIcons.NameX + i * DSPRE.ROMFiles.NamingScreenIcons.LetterStep, DSPRE.ROMFiles.NamingScreenIcons.NameY);

            int iconCell = _namingAnimations?.CellOf(_trClassID, _topBarFrame) ?? -1;
            bool gender = DSPRE.ROMFiles.NamingScreenIcons.IsGender(_trClassID);
            if (iconCell >= 0)
                BlitCell(rgba, iconCell,
                    gender ? DSPRE.ROMFiles.NamingScreenIcons.GenderX : DSPRE.ROMFiles.NamingScreenIcons.IconX,
                    gender ? DSPRE.ROMFiles.NamingScreenIcons.GenderY : DSPRE.ROMFiles.NamingScreenIcons.IconY);

            try { _topBarFont ??= DSPRE.ROMFiles.FieldFont.LoadSystemFont(); } catch (Exception ex) { AppLogger.Warn("Trainer sprite editor: system font: " + ex.Message); }
            for (int i = 0; i < _sampleName.Length && i < letters; i++)
            {
                string ch = _sampleName[i].ToString();
                // naming_screen.c: each letter sits left in its 12-pixel step, window at tile 10,3.
                int x = DSPRE.ROMFiles.NamingScreenIcons.NameX + i * DSPRE.ROMFiles.NamingScreenIcons.LetterStep;
                DsBgScreen.DrawText(rgba, _topBarFont, ch, x, DSPRE.ROMFiles.NamingScreenIcons.NameY - 15, 0x294A, 0x5EF7);
            }

            byte[] bgra = new byte[w * TopBarHeight * 4];
            for (int i = 0; i < bgra.Length; i += 4)
            {
                bgra[i] = rgba[i + 2]; bgra[i + 1] = rgba[i + 1]; bgra[i + 2] = rgba[i]; bgra[i + 3] = 255;
            }
            TopBarPreview = ImageConverter.ToAvaloniaBitmap(new DSPRE.RawImage(w, TopBarHeight, bgra));
        }

        // Draws any archive cell, not just this icon's, with its origin at x, y.
        private void BlitCell(byte[] rgba, int cell, int x, int y)
        {
            if (cell < 0 || cell >= _sprite.Cells.Count) return;
            RawImage raw = CellRenderer.Draw(_sprite, cell, _tile.ToBytes(), Bgr555(_pal), CanvasSize, CanvasSize);
            int w = DsBgScreen.Width, h = DsBgScreen.Height;
            for (int py = 0; py < CanvasSize; py++)
                for (int px = 0; px < CanvasSize; px++)
                {
                    int s = (py * CanvasSize + px) * 4;
                    if (raw.Bgra[s + 3] == 0) continue;
                    int X = x - CanvasSize / 2 + px, Y = y - CanvasSize / 2 + py;
                    if (X < 0 || Y < 0 || X >= w || Y >= h) continue;
                    int d = (Y * w + X) * 4;
                    rgba[d] = raw.Bgra[s + 2]; rgba[d + 1] = raw.Bgra[s + 1]; rgba[d + 2] = raw.Bgra[s]; rgba[d + 3] = 255;
                }
        }

        // File 4: the 80x80 poses for the Hall of Fame and link rosters. Halves take only edited pixels.
        private const int ScanFrame = 80;

        private sealed class ScanCopy
        {
            public string Path;
            public byte[] File;
            public int DataOff, Size;
            public bool FromEnd;
            public byte[] Pixels;   // unscrambled, two pixels a byte
        }

        private ScanCopy ReadScan(int entry, out string error)
        {
            error = null;
            int id = TrainerGraphicsLayout.ScanEntry(entry);
            if (id < 0) return null;
            string path = ArchiveMembers.PathOf(_set.Archive, id);
            if (!ArchiveMembers.Has(_set.Archive, id)) return null;

            byte[] file = Member(id);
            if (!(NitroBgCodec.ReadDrawingHeader(file) is NitroBgCodec.DrawingHeader head)) { error = "the two-frame copy of the sprite has no pixels"; return null; }
            int size = head.DataSize, dataOff = head.DataAt;
            int width = ScanFrame * 2, height = ScanFrame;
            if (head.TilesAcross * 8 != width || head.TilesDown * 8 != height || head.EightBit || size != width * height / 2)
            {
                error = $"the two-frame copy of the sprite isn't the usual {width}x{height} 16-colour picture";
                return null;
            }

            bool fromEnd = TrainerGraphicsLayout.ScanScrambledFromEnd;
            byte[] plain = (byte[])file.Clone();
            SpriteScrambling.Unscramble(plain, dataOff, size, fromEnd);
            byte[] pixels = new byte[size];
            Array.Copy(plain, dataOff, pixels, 0, size);
            return new ScanCopy { Path = path, File = file, DataOff = dataOff, Size = size, FromEnd = fromEnd, Pixels = pixels };
        }

        private static int ScanPixel(byte[] pixels, int pos) => (pos & 1) == 0 ? pixels[pos >> 1] & 0xF : pixels[pos >> 1] >> 4;

        private sealed class ScanHalf
        {
            public int Frame;
            public int[] Base;
        }

        // The middle 80x80 of a drawn frame, colour index within its palette.
        private static int[] ScanCrop(int[] canvas)
        {
            int margin = (CanvasSize - ScanFrame) / 2;
            int[] crop = new int[ScanFrame * ScanFrame];
            for (int y = 0; y < ScanFrame; y++)
                for (int x = 0; x < ScanFrame; x++)
                    crop[y * ScanFrame + x] = canvas[(y + margin) * CanvasSize + x + margin] & 0xF;
            return crop;
        }

        // Ties each scan half to the frame it is closest to, per part. Runs before any edit.
        private void MapScanFrames()
        {
            if (_set.NamingScreen) return;
            int keep = _activePart;
            for (int p = 0; p < _parts.Count; p++)
            {
                Activate(p);
                ScanCopy scan = BankCount > 0 ? ReadScan(_parts[p].Entry, out _) : null;
                ScanHalf[] halves = new ScanHalf[2];
                if (scan != null)
                {
                    try
                    {
                        Func<int, int[]> draw = FrameDrawer(p, out int count);
                        List<int[]> crops = Enumerable.Range(0, count).Select(f => ScanCrop(draw(f))).ToList();
                        for (int half = 0; half < 2; half++)
                        {
                            int inked = 0, best = int.MaxValue, bestFrame = -1;
                            for (int i = 0; i < ScanFrame * ScanFrame; i++)
                                if (ScanPixel(scan.Pixels, (i / ScanFrame) * ScanFrame * 2 + half * ScanFrame + i % ScanFrame) != 0) inked++;
                            for (int f = 0; f < crops.Count; f++)
                            {
                                int diff = 0;
                                for (int i = 0; i < ScanFrame * ScanFrame; i++)
                                    if (crops[f][i] != ScanPixel(scan.Pixels, (i / ScanFrame) * ScanFrame * 2 + half * ScanFrame + i % ScanFrame)) diff++;
                                if (diff < best) { best = diff; bestFrame = f; }
                            }
                            // A half further off than this is its own drawing and is never redrawn.
                            if (inked > 0 && bestFrame >= 0 && best <= inked / 20)
                                halves[half] = new ScanHalf { Frame = bestFrame, Base = crops[bestFrame] };
                        }
                    }
                    catch (Exception ex) { AppLogger.Error("TrainerSpriteEditorViewModel: scan copy couldn't be matched: " + ex.Message); }
                }
                _parts[p].Scan = halves;
                AppLogger.Debug($"Trainer sprite {_parts[p].Entry}: scan halves follow frames {halves[0]?.Frame ?? -1} and {halves[1]?.Frame ?? -1}.");
            }
            Activate(keep);
        }

        private string WriteScan(int entry)
        {
            ScanCopy scan = ReadScan(entry, out string error);
            if (scan == null) return error;
            ScanHalf[] halves = _parts.Count > 0 ? _parts[_activePart].Scan : null;
            if (halves == null) return null;

            Func<int, int[]> draw = FrameDrawer(_activePart, out int count);
            byte[] pixels = (byte[])scan.Pixels.Clone();
            int[][] now = new int[2][];
            for (int half = 0; half < 2; half++)
            {
                ScanHalf h = halves[half];
                if (h == null || h.Frame >= count) continue;
                now[half] = ScanCrop(draw(h.Frame));
                for (int i = 0; i < now[half].Length; i++)
                {
                    if (now[half][i] == h.Base[i]) continue;
                    int pos = (i / ScanFrame) * ScanFrame * 2 + half * ScanFrame + i % ScanFrame, v = now[half][i];
                    pixels[pos >> 1] = (byte)((pos & 1) == 0 ? (pixels[pos >> 1] & 0xF0) | v : (pixels[pos >> 1] & 0x0F) | (v << 4));
                }
            }
            if (!pixels.AsSpan().SequenceEqual(scan.Pixels))
            {
                ushort seed = SpriteScrambling.Seed(scan.File, scan.DataOff, scan.Size, scan.FromEnd);
                byte[] file = (byte[])scan.File.Clone();
                Array.Copy(pixels, 0, file, scan.DataOff, scan.Size);
                SpriteScrambling.Scramble(file, scan.DataOff, scan.Size, seed, scan.FromEnd);
                ArchiveMembers.Write(scan.Path, file);
            }
            for (int half = 0; half < 2; half++)
                if (now[half] != null) halves[half].Base = now[half];
            return null;
        }

        // hg-engine builds the scan copy from NNN_enc.png, reading only its pixel numbers.
        private string WriteScanPng(int entry, string png)
        {
            if (!HgEngineTrainerGraphicsSource.Has(png)) return null;
            ScanCopy scan = ReadScan(entry, out string error);
            if (scan == null) return error;
            byte[] file = HgEngineTrainerGraphicsSource.ReadBytes(png);
            if (!IndexedPng.TryRead(file, out byte[] old, out uint[] colours, out int w, out int h)) return $"{Path.GetFileName(png)} isn't an indexed PNG.";
            if (w != ScanFrame * 2 || h != ScanFrame) return $"{Path.GetFileName(png)} is {w}x{h}, not the {ScanFrame * 2}x{ScanFrame} hg-engine builds the scan copy from.";
            byte[] indices = new byte[w * h];
            for (int i = 0; i < indices.Length; i++) indices[i] = (byte)ScanPixel(scan.Pixels, i);
            if (indices.AsSpan().SequenceEqual(old)) return null;
            PlainFiles.WriteBytes(png, IndexedPng.Write(indices, colours, w, h, file.Length > 24 && file[24] == 4 ? 4 : 8));
            return null;
        }
    }
}
