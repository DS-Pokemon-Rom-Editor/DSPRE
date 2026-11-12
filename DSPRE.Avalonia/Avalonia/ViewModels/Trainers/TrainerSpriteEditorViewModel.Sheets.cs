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
        public bool CanUseSheets => _tile != null && !_set.NamingScreen && !HgEngineProject.IsActive && !_cellsFromSource;
        public bool CanUseAnimationSheets => CanUseSheets && !IsFlatSheetMode;

        private byte[] Member(int entry) => ArchiveMembers.Read(_set.Archive, entry);

        private string EntryPath(int entry) => ArchiveMembers.PathOf(_set.Archive, entry);

        // nitrogfx leaves out the block the frame reader needs, so hg-engine frames are drawn from cells.
        private Func<int, int[]> FrameDrawer(int part, out int count)
        {
            if (HgEngineProject.IsActive && _cellsFromSource)
            {
                count = BankCount;
                return FrameIndices;
            }
            TrainerSpriteFrames frames = OpenFrames(part);
            count = frames.FrameCount;
            return frames.Draw;
        }

        private TrainerSpriteFrames OpenFrames(int part)
        {
            SpritePart p = _parts[part];
            TrainerSpriteFrames frames = TrainerSpriteFrames.Read(
                p.Ncgr ?? Member(TrainerGraphicsLayout.DrawingEntry(p.Entry)),
                p.Ncer ?? Member(TrainerGraphicsLayout.CellsEntry(p.Entry)),
                p.Nanr ?? Member(TrainerGraphicsLayout.AnimationEntry(p.Entry)), out string why);
            if (frames == null) throw new InvalidOperationException(why);
            frames.ReplaceTiles(p.Tile.Tiles);
            return frames;
        }

        private ISheetFrames OpenSheetFrames(int part) =>
            IsFlatSheetMode ? new FlatFrames((int[])_flatIndices.Clone(), _flatWidth, _flatHeight) : OpenFrames(part);

        private int SheetPartCount => IsFlatSheetMode ? 1 : Math.Max(1, _parts.Count);

        // Up to the highest palette used.
        private uint[][] SheetPalettes(IEnumerable<ISheetFrames> frames)
        {
            int used = 1;
            foreach (ISheetFrames f in frames)
                for (int i = 0; i < f.FrameCount; i++)
                    foreach (int mask in f.PalettesOf(i))
                        for (int b = 0; b < 16; b++) if ((mask & (1 << b)) != 0) used = Math.Max(used, b + 1);
            used = Math.Min(used, _pal.Length);
            uint[][] result = new uint[used][];
            for (int b = 0; b < used; b++)
                result[b] = Enumerable.Range(0, 16).Select(i => SwatchColor(b, i)).ToArray();
            return result;
        }

        public string SheetFileName(int? animation) =>
            $"{_set.Noun} {_set.FileStem(_trClassID)} {(animation == null ? "frames" : (SheetLabel(_activePart) is string set ? set.ToLowerInvariant() + " " : "") + "animation " + animation)}.png";

        private string SheetLabel(int part) => SheetPartCount > 1 ? _parts[part].Label : null;

        /// <summary>One row per set. Returns why not, or null.</summary>
        public string ExportFramesSheet(string path)
        {
            try
            {
                List<ISheetFrames> sets = Enumerable.Range(0, SheetPartCount).Select(OpenSheetFrames).ToList();
                List<IReadOnlyList<TrainerSpriteSheet.Cell>> rows = sets.Select(f => (IReadOnlyList<TrainerSpriteSheet.Cell>)Enumerable.Range(0, f.FrameCount)
                    .Select(i => new TrainerSpriteSheet.Cell(f.Draw(i))).ToList()).ToList();
                (byte[] px, uint[] colours, int w, int h) = TrainerSpriteSheet.Compose(rows, SheetPalettes(sets));
                PlainFiles.WriteBytes(path, IndexedPng.Write(px, colours, w, h));

                TrainerSpriteSheet.FramesFile file = new TrainerSpriteSheet.FramesFile();
                for (int p = 0; p < sets.Count; p++)
                    file.Rows.Add(new TrainerSpriteSheet.FramesRow
                    {
                        Set = SheetLabel(p),
                        Frames = Enumerable.Range(0, sets[p].FrameCount).Select(i => new TrainerSpriteSheet.FrameJson { Frame = i, Blank = sets[p].IsBlank(i) }).ToList(),
                    });
                PlainFiles.WriteText(Path.ChangeExtension(path, ".json"), TrainerSpriteSheet.WriteFrames(file));
                SaveNotice.Exported(path);
                return null;
            }
            catch (Exception ex) when (AppLogger.Noted("TrainerSpriteEditorViewModel.ExportFramesSheet", ex)) { return ex.Message; }
        }

        private const int AnimationSheetColumns = 8;

        public string ExportAnimationSheet(int animation, string path)
        {
            try
            {
                TrainerSpriteFrames frames = OpenFrames(_activePart);
                IReadOnlyList<TrainerSpriteFrames.Step> steps = frames.StepsOf(animation);
                if (steps.Count == 0) return $"This sprite has no animation {animation}.";
                List<TrainerSpriteSheet.Cell> cells = steps.Select(st => new TrainerSpriteSheet.Cell(frames.Draw(st.Frame))).ToList();
                List<IReadOnlyList<TrainerSpriteSheet.Cell>> rows = cells.Chunk(AnimationSheetColumns).Select(r => (IReadOnlyList<TrainerSpriteSheet.Cell>)r).ToList();
                (byte[] px, uint[] colours, int w, int h) = TrainerSpriteSheet.Compose(rows, SheetPalettes(new[] { frames }));
                PlainFiles.WriteBytes(path, IndexedPng.Write(px, colours, w, h));

                bool shifts = frames.SequenceShifts(animation);
                TrainerSpriteSheet.StepsFile file = new TrainerSpriteSheet.StepsFile
                {
                    Set = SheetLabel(_activePart),
                    Animation = animation,
                    Steps = steps.Select(st => new TrainerSpriteSheet.StepJson { Frame = st.Frame, Hold = st.Hold, X = shifts ? st.X : null, Y = shifts ? st.Y : null }).ToList(),
                };
                PlainFiles.WriteText(Path.ChangeExtension(path, ".json"), TrainerSpriteSheet.WriteSteps(file));
                SaveNotice.Exported(path);
                return null;
            }
            catch (Exception ex) when (AppLogger.Noted("TrainerSpriteEditorViewModel.ExportAnimationSheet", ex)) { return ex.Message; }
        }

        public List<TrainerSheetImportViewModel.AnimationChoice> AnimationSheetChoices(bool includeNew)
        {
            int count;
            try { count = OpenFrames(_activePart).Animations.Sequences.Count; }
            catch (Exception ex) { AppLogger.WarnOnce("TrainerSpriteEditorViewModel.AnimationSheetChoices", ex); return new(); }
            string Name(int i) => i switch
            {
                0 => "Standing",
                1 => _set.IsBack ? "Throwing the ball" : "Intro after sliding in",
                2 when !_set.IsBack => "While sliding in",
                _ => $"Animation {i}",
            };
            List<TrainerSheetImportViewModel.AnimationChoice> list = Enumerable.Range(0, count).Select(i => new TrainerSheetImportViewModel.AnimationChoice(i, $"{i}: {Name(i)}")).ToList();
            // A front plays 2 while sliding in and 1 on arrival; a back throws with 1.
            int most = _set.IsBack ? 2 : 3;
            if (includeNew && count < most) list.Add(new TrainerSheetImportViewModel.AnimationChoice(count, $"{count}: {Name(count)} (new)"));
            return list;
        }

        public TrainerSheetImportViewModel OpenSheetImport(bool animation, string pngPath, out string why)
        {
            why = null;
            try
            {
                TrainerSpriteSheet.Read sheet = TrainerSpriteSheet.OpenFile(pngPath, out why);
                if (sheet == null) return null;
                TrainerSpriteSheet.StepsFile steps = animation ? TrainerSpriteSheet.ReadStepsBeside(pngPath, out why) : null;
                if (why != null) return null;

                List<TrainerSheetImportViewModel.Target> targets = new List<TrainerSheetImportViewModel.Target>();
                for (int p = 0; p < SheetPartCount; p++)
                {
                    int part = p;
                    targets.Add(new TrainerSheetImportViewModel.Target { Label = SheetLabel(p) ?? "", Open = () => OpenSheetFrames(part) });
                }
                uint[][] palettes = SheetPalettes(targets.Select(t => t.Open()));
                return new TrainerSheetImportViewModel(
                    animation ? TrainerSheetImportViewModel.Jobs.Animation : TrainerSheetImportViewModel.Jobs.Drawings,
                    targets, palettes, sheet, Path.GetFileName(pngPath), steps, animation ? AnimationSheetChoices(true) : new(), _activePart);
            }
            catch (Exception ex) when (AppLogger.Noted("TrainerSpriteEditorViewModel.OpenSheetImport", ex)) { why = ex.Message; return null; }
        }

        public void ApplySheetImport(TrainerSheetImportViewModel wizard)
        {
            TrainerSheetImportViewModel.Result result = wizard.Outcomes;
            if (result == null) return;
            int keep = _activePart;
            for (int p = 0; p < result.Frames.Count; p++)
            {
                switch (result.Frames[p])
                {
                    case FlatFrames flat:
                        _flatIndices = flat.Indices;
                        break;
                    case TrainerSpriteFrames frames:
                        SpritePart part = _parts[p];
                        (part.Ncgr, part.Ncer, part.Nanr) = frames.Write();
                        part.Tile = ReadDrawing(part.Ncgr);
                        part.Sprite = ReadCells(part.Ncer);
                        break;
                }
            }

            if (result.Palettes != null)
            {
                for (int b = 0; b < result.Palettes.Length; b++)
                    for (int i = 0; i < 16; i++)
                    {
                        uint argb = result.Palettes[b][i];
                        System.Drawing.Color color = System.Drawing.Color.FromArgb((int)((argb >> 16) & 0xF8), (int)((argb >> 8) & 0xF8), (int)(argb & 0xF8));
                        if (b < _pal.Length && i < _pal[b].Length) _pal[b][i] = color;
                        foreach (SpritePart part in _parts)
                            if (b < part.Pal.Length && i < part.Pal[b].Length) part.Pal[b][i] = color;
                    }
                _paletteDirty = true;
            }

            HasUnsavedChanges = true;
            if (IsFlatSheetMode)
            {
                BuildPaletteSwatches(0);
                RebuildFlatCanvas();
                StatusText = wizard.Outcome;
                return;
            }

            Activate(Math.Min(keep, _parts.Count - 1));
            BuildFrameThumbnails();
            _activePaletteBank = -1;
            _selectedStripFrame = -1;
            SelectedFrameIndex = 0;
            OnPropertyChanged(nameof(FrameCount));
            RebuildAnimCellChoices();
            StopAnimPreview();
            _romAnim = OpenRomAnimations();
            OnPropertyChanged(nameof(CanEditAnimFrames));
            SetAnimJsonTextSilent(AnimationJsonOf(_romAnim ?? OpenFrames(0).Animations) ?? "");
            OnPropertyChanged(nameof(HasAnimation));
            StatusText = wizard.Outcome;
        }

        // One model frame per file frame, in order, so edits map back to the file.
        private static string AnimationJsonOf(NanrFile anims)
        {
            AnimJsonRoot root = new AnimJsonRoot();
            for (int seq = 0; seq < anims.Sequences.Count; seq++)
            {
                AnimSequenceJson model = new AnimSequenceJson { AnimationType = 1, PlaybackMode = 2 };
                for (int f = 0; f < anims.Sequences[seq].Frames.Count; f++)
                    model.FrameData.Add(new AnimFrameDataJson { CellIndex = anims.CellOf(seq, f), FrameDelay = Math.Max(1, (int)anims.Sequences[seq].Frames[f].Delay) });
                root.Sequences.Add(model);
            }
            return root.Sequences.Count == 0 ? null : root.Serialize();
        }

        private void WriteStagedSheetFiles(SpritePart part)
        {
            if (part.Ncer != null) ArchiveMembers.Write(EntryPath(TrainerGraphicsLayout.CellsEntry(part.Entry)), part.Ncer);
            if (part.Nanr != null) ArchiveMembers.Write(EntryPath(TrainerGraphicsLayout.AnimationEntry(part.Entry)), part.Nanr);
            part.Ncgr = part.Ncer = part.Nanr = null;
        }

        // Diamond and Pearl's two frames in one flat drawing, side by side or stacked.
        private sealed class FlatFrames : ISheetFrames
        {
            public int[] Indices { get; }
            private readonly int _w, _fw, _fh, _count;
            private readonly bool _across;
            private const int N = TrainerSpriteFrames.Canvas;

            public FlatFrames(int[] indices, int w, int h)
            {
                Indices = indices; _w = w;
                _across = w == 2 * h;
                _count = _across || h == 2 * w ? 2 : 1;
                _fw = _across ? w / 2 : w;
                _fh = _count == 2 && !_across ? h / 2 : h;
            }

            public int FrameCount => _count;
            public bool CanChangeFrameCount => false;
            public bool IsBlank(int frame) => false;

            private (int X, int Y) Corner(int frame) => _across ? (frame * _fw, 0) : (0, frame * _fh);
            private int Left => (N - _fw) / 2;
            private int Top => (N - _fh) / 2;

            public int[] Draw(int frame)
            {
                int[] canvas = new int[N * N];
                (int cx, int cy) = Corner(frame);
                for (int y = 0; y < _fh && y < N; y++)
                    for (int x = 0; x < _fw && x < N; x++)
                        canvas[(Top + y) * N + Left + x] = Indices[(cy + y) * _w + cx + x];
                return canvas;
            }

            public int[] PalettesOf(int frame)
            {
                int[] canvas = new int[N * N];
                for (int y = 0; y < _fh && y < N; y++)
                    for (int x = 0; x < _fw && x < N; x++) canvas[(Top + y) * N + Left + x] = 1;
                return canvas;
            }

            public string SetDrawing(int frame, int[] canvas)
            {
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                        if (canvas[y * N + x] != 0 && (x < Left || x >= Left + _fw || y < Top || y >= Top + _fh))
                            return $"Frame {frame} draws outside the middle {_fw}x{_fh}, which is all a frame holds here.";
                (int cx, int cy) = Corner(frame);
                for (int y = 0; y < _fh; y++)
                    for (int x = 0; x < _fw; x++)
                        Indices[(cy + y) * _w + cx + x] = canvas[(Top + y) * N + Left + x] & 0xF;
                return null;
            }
        }
    }
}
