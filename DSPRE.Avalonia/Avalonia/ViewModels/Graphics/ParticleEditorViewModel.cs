using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using DSPRE.Avalonia.Data;
using IEditorWithUnsavedChanges = global::DSPRE.Editors.IEditorWithUnsavedChanges;

namespace DSPRE.Avalonia.ViewModels.Graphics
{
    /// <summary>One setting of the chosen emitter, in display units.</summary>
    public sealed class ParticleFieldRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private readonly ParticleEditorViewModel _owner;
        public SpaField Field { get; }

        internal ParticleFieldRow(ParticleEditorViewModel owner, SpaField field) { _owner = owner; Field = field; }

        public string Group => ParticleEditorViewModel.Words(Field.Block.ToString());
        public string Label => ParticleEditorViewModel.Words(Field.Name);

        public bool IsColor => Field.Kind == SpaFieldKind.Color;
        public bool IsFlag => !IsColor && (Field.Kind == SpaFieldKind.Flag || Field.Bits == 1);
        public bool IsNumber => !IsColor && !IsFlag;
        public bool CanEdit => !Field.DecidesLayout;
        public string LockedNote => Field.DecidesLayout ? "Decides which parts this emitter has" : null;

        public decimal Minimum => (decimal)(Field.MinRaw / Field.Divisor);
        public decimal Maximum => (decimal)(Field.MaxRaw / Field.Divisor);
        // Whole steps for pixels and degrees, 0.05 for scales and ratios.
        public decimal Step => Field.Divisor <= 1 || Field.Divisor < 1000 ? 1m : 0.05m;
        public string Format => Field.Divisor <= 1 ? "0" : "0.###";

        private long Raw => _owner.Raw(Field);

        public decimal? Value
        {
            get => (decimal)(Raw / Field.Divisor);
            set { if (value != null) _owner.Write(this, (long)Math.Round((double)value.Value * Field.Divisor)); }
        }

        public bool IsOn
        {
            get => Raw != 0;
            set => _owner.Write(this, value ? 1 : 0);
        }

        public IBrush Swatch
        {
            get { var (r, g, b) = SpaFields.ToRgb888(Raw); return new SolidColorBrush(Color.FromRgb(r, g, b)); }
        }

        public string ColorText { get { var (r, g, b) = SpaFields.ToRgb888(Raw); return $"{r}, {g}, {b}"; } }

        public void SetColour(byte r, byte g, byte b) => _owner.Write(this, SpaFields.FromRgb888(r, g, b));

        internal void Refresh()
        {
            foreach (var n in new[] { nameof(Value), nameof(IsOn), nameof(Swatch), nameof(ColorText) })
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        }
    }

    public sealed class ParticleTextureRow
    {
        public int Index { get; init; }
        public Bitmap Picture { get; init; }
        public string Info { get; init; }
        public string CannotReplace { get; init; }
        public bool CanReplace => CannotReplace == null;
        internal byte[] Rgba { get; init; }
        internal int Width { get; init; }
        internal int Height { get; init; }
    }

    /// <summary>Edits a particle file with a live preview; layout fields are locked so the file keeps its size.</summary>
    public sealed class ParticleEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        {
            if (EqualityComparer<T>.Default.Equals(f, v)) return false;
            f = v; OnPropertyChanged(n); return true;
        }

        public ObservableCollection<string> EmitterNames { get; } = new();
        public ObservableCollection<ParticleFieldRow> Fields { get; } = new();
        public ObservableCollection<ParticleTextureRow> Textures { get; } = new();

        private readonly ArchiveFiles _source;
        private readonly int _entry;
        private readonly Action<int> _changed;
        private readonly bool _orthographic;
        private byte[] _saved;
        private SpaDocument _doc;
        private SpaParticlePreview _preview;
        private DispatcherTimer _timer;
        private int _restWait;

        public string Title { get; } = "Particles";

        public ParticleEditorViewModel() { if (!Design.IsDesignMode) return; }

        public ParticleEditorViewModel(ArchiveFiles source, int entry, string what, Action<int> changed, bool orthographic = false)
        {
            _source = source;
            _orthographic = orthographic;
            _entry = entry;
            _changed = changed;
            _saved = source.Get(entry) ?? throw new InvalidOperationException($"Particle file {entry} is not there.");
            if (!SpaDocument.TryLoad(_saved, out _doc, out string why))
                throw new InvalidOperationException(why);

            Title = $"Particles: {what} (file {entry})";
            for (int i = 0; i < _doc.EmitterCount; i++) EmitterNames.Add($"Emitter {i + 1}");
            _emitterIndex = _doc.EmitterCount > 0 ? 0 : -1;
            RebuildFields();
            RebuildTextures();
            Replay();

            _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(1000.0 / 30), DispatcherPriority.Render, (_, _) => Tick());
            _timer.Start();
        }

        private int _emitterIndex = -1;
        public int EmitterIndex
        {
            get => _emitterIndex;
            set { if (value >= 0 && value < EmitterNames.Count && Set(ref _emitterIndex, value)) { RebuildFields(); if (_onlySelected) Replay(); } }
        }

        private bool _onlySelected;
        public bool OnlySelected { get => _onlySelected; set { if (Set(ref _onlySelected, value)) Replay(); } }

        private string _filter = "";
        public string FieldFilter { get => _filter; set { if (Set(ref _filter, value ?? "")) RebuildFields(); } }

        private Bitmap _frame;
        public Bitmap Frame { get => _frame; private set => Set(ref _frame, value); }

        private string _status = "";
        public string StatusText { get => _status; set => Set(ref _status, value); }

        private bool _dirty;
        public bool HasUnsavedChanges { get => _dirty; private set => Set(ref _dirty, value); }
        public string UnsavedChangesDescription => Title;

        internal static string Words(string pascal) =>
            Regex.Replace(pascal ?? "", "(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])", " ") is var s && s.Length > 1
                ? s[0] + s.Substring(1).ToLowerInvariant() : s;

        internal long Raw(SpaField field) => _emitterIndex >= 0 && _doc.Has(_emitterIndex, field) ? _doc.GetRaw(_emitterIndex, field) : 0;

        internal void Write(ParticleFieldRow row, long raw)
        {
            if (_emitterIndex < 0 || row.Field.DecidesLayout) return;
            raw = Math.Clamp(raw, row.Field.MinRaw, row.Field.MaxRaw);
            if (Raw(row.Field) == raw) return;
            try
            {
                _doc.SetRaw(_emitterIndex, row.Field, raw);
                HasUnsavedChanges = true;
                StatusText = $"{row.Label} changed.";
                Replay();
            }
            catch (Exception ex) { StatusText = ex.Message; }
            row.Refresh();
        }

        private void RebuildFields()
        {
            Fields.Clear();
            if (_emitterIndex < 0) return;
            foreach (var field in SpaFields.All)
            {
                if (!_doc.Has(_emitterIndex, field)) continue;
                var row = new ParticleFieldRow(this, field);
                if (_filter.Length > 0 && row.Label.IndexOf(_filter, StringComparison.OrdinalIgnoreCase) < 0
                    && row.Group.IndexOf(_filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                Fields.Add(row);
            }
        }

        private void RebuildTextures()
        {
            Textures.Clear();
            var arc = _doc.Parse();
            for (int i = 0; i < _doc.TextureCount; i++)
            {
                var info = _doc.GetTextureInfo(i);
                var tex = i < arc.Textures.Count ? arc.Textures[i] : null;
                Textures.Add(new ParticleTextureRow
                {
                    Index = i,
                    Picture = tex?.Rgba != null ? ImageConverter.FromRgba(tex.Rgba, tex.Width, tex.Height) : null,
                    Info = $"Texture {i + 1}: {info.Width} by {info.Height}, {SpaTextureEncoder.FormatName(info.Format)}"
                         + (info.PaletteColors > 0 ? $", {info.PaletteColors} colours" : ""),
                    CannotReplace = info.CannotReplace,
                    Rgba = tex?.Rgba, Width = tex?.Width ?? 0, Height = tex?.Height ?? 0,
                });
            }
        }

        // ── playback ───────────────────────────────────────────────────────────────────────────

        // Where the emitters sit on the 256 by 192 preview.
        private const double AnchorX = 128, AnchorY = 110;

        public void Replay()
        {
            _preview = new SpaParticlePreview(256, 192);
            _restWait = 0;
            SpaArchive arc;
            try { arc = _doc.Parse(); }
            catch (Exception ex) { StatusText = "This file cannot be played: " + ex.Message; return; }
            for (int i = 0; i < arc.Emitters.Count; i++)
            {
                if (_onlySelected && i != _emitterIndex) continue;
                var em = arc.Emitters[i];
                var tex = em.TexNo >= 0 && em.TexNo < arc.Textures.Count ? arc.Textures[em.TexNo] : null;
                double cx = AnchorX + em.PosX, cy = AnchorY - em.PosY;
                var sim = new SpaSimulator(em, em.AxisX, em.AxisY) { AnchorX = cx, AnchorY = cy };
                _preview.AddLayer(new SpaParticlePreview.Layer(sim, arc.Textures, tex, cx, cy, em.DrawType,
                    em.RepeatS, em.RepeatT, em.Aspect, em.DbbScale, em.OffsetX, em.OffsetY,
                    baseZ: em.PosZ, viewReversed: false, flipS: em.FlipS, flipT: em.FlipT, em: em, orthographic: _orthographic));
            }
        }

        private void Tick()
        {
            if (_preview == null) return;
            if (!_preview.HasEmitters || _preview.AllFinished)
            {
                // A short rest between plays, so the end of an effect can be seen.
                if (++_restWait >= 20) Replay();
                return;
            }
            Frame = _preview.RenderFrame();
            _preview.Step();
        }

        public void Stop() => _timer?.Stop();

        // ── textures ───────────────────────────────────────────────────────────────────────────

        public string ReplaceTexture(int index, string path)
        {
            byte[] file;
            try { file = File.ReadAllBytes(path); }
            catch (Exception ex) { return StatusText = "That file could not be read: " + ex.Message; }
            if (!AnyPng.TryReadRgba(file, out byte[] rgba, out int w, out int h, out string whynot))
                return StatusText = whynot ?? "That file is not a readable PNG.";

            var result = _doc.ReplaceTexture(index, w, h, rgba);
            if (!result.Succeeded) return StatusText = result.Error;

            HasUnsavedChanges = true;
            RebuildTextures();
            Replay();
            return StatusText = result.Quantized
                ? $"Texture {index + 1} replaced, reduced from {result.SourceColors} to {result.PaletteColorsUsed} colours."
                : $"Texture {index + 1} replaced.";
        }

        public string ExportTexture(int index, string path)
        {
            var row = Textures.FirstOrDefault(t => t.Index == index);
            if (row?.Rgba == null) return StatusText = "This texture cannot be exported.";
            try
            {
                using var bmp = ImageConverter.FromRgba(row.Rgba, row.Width, row.Height);
                bmp.Save(path);
                return StatusText = $"Texture {index + 1} saved.";
            }
            catch (Exception ex) { return StatusText = "Could not save: " + ex.Message; }
        }

        // ── saving ─────────────────────────────────────────────────────────────────────────────

        public void SaveChanges()
        {
            if (!_dirty) return;
            try
            {
                byte[] bytes = _doc.ToBytes();
                _source.Put(new Dictionary<int, byte[]> { [_entry] = bytes });
                _saved = bytes;
                HasUnsavedChanges = false;
                StatusText = "Saved.";
                _changed?.Invoke(_entry);
            }
            catch (Exception ex) { StatusText = "Could not save: " + ex.Message; }
        }

        public void DiscardChanges()
        {
            _doc = SpaDocument.Load(_saved);
            HasUnsavedChanges = false;
            RebuildFields();
            RebuildTextures();
            Replay();
            StatusText = "Changes undone.";
        }
    }
}
