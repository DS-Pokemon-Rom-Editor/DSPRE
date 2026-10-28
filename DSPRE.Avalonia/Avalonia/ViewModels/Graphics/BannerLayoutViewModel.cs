using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia.Media.Imaging;
using DSPRE.Avalonia.Data;
using DSPRE.ROMFiles;
using Avalonia.Threading;
using IEditorWithUnsavedChanges = global::DSPRE.Editors.IEditorWithUnsavedChanges;

namespace DSPRE.Avalonia.ViewModels.Graphics
{
    /// <summary>Edits the place-name banner's size, timing, slide and motion, and its position once patched.</summary>
    public sealed class BannerLayoutViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, ISupportsUndo, IDetachable, IScreenPreviewSource
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        public event Action Changed;

        private readonly PlaceNameBanner _banner;
        private readonly PendingArchiveWrites.Target _target;
        private readonly PendingArchiveWrites _pending = new();
        private PlaceNameBanner.Look _saved;
        private byte[] _disk;

        public BannerLayoutViewModel()
        {
            _banner = PlaceNameBanner.Read();
            if (_banner == null) return;
            _saved = _banner.Current;
            _disk = _banner.FileBytes();
            _target = new("banner", () => _disk, b => { _banner.WriteFile(b); _disk = b; });
            _pending.Changed += () =>
            {
                foreach (string n in new[] { nameof(Across), nameof(Down), nameof(Width), nameof(Height), nameof(Time), nameof(Speed), nameof(SpeedOut), nameof(DirectionIndex), nameof(MotionIndex), nameof(EffectIndex),
                                             nameof(HasUnsavedChanges), nameof(CanUndo), nameof(CanRedo), nameof(MostWidth) }) Raise(n);
                Redraw();
            };
            int bank = RomInfo.LocationNamesTextNumber;
            if (bank >= 0) Places = new TextArchive(bank).Messages.Select((name, i) => $"{i}: {name}").ToList();
            _motion = new("banner-motion", () => BannerMotionPatch.Read() is BannerMotionPatch.Style s ? StyleBytes(s) : null, WriteMotion);
            AppEvents.RomPatchStateChanged += OnPatchStateChanged;
            Redraw();
        }

        // ── motion ──

        private PendingArchiveWrites.Target _motion;
        private bool _hasMotion = BannerMotionPatch.IsApplied;

        public bool HasMotion => _hasMotion;
        public bool OffersMotionPatch => _banner != null && !_hasMotion && BannerMotionPatch.SupportsCurrentRom && !RomInfo.IsHGE;
        public void OpenMotionPatch() => AvaloniaEditorLauncher.OpenPatchToolboxAt("bannerMotion");

        public string[] Directions { get; } = { "From the top", "From the bottom", "In place" };
        public string[] Motions { get; } = { "Straight", "Ease", "Overshoot", "Bounce" };
        public string[] Effects { get; } = { "None", "Fade", "Pixelate" };

        private static byte[] StyleBytes(BannerMotionPatch.Style s) => new[] { (byte)s.Direction, (byte)s.Motion, (byte)s.Effect };

        private BannerMotionPatch.Style Style =>
            _hasMotion && _pending.Read(_motion) is byte[] b && b.Length == 3
                ? new BannerMotionPatch.Style((BannerMotionPatch.Direction)b[0], (BannerMotionPatch.Motion)b[1], (BannerMotionPatch.Effect)b[2])
                : BannerMotionPatch.Game;

        // Runs after the banner is saved so the motion tables match its saved position and size.
        private void WriteMotion(byte[] b)
        {
            PlaceNameBanner now = PlaceNameBanner.Read();
            if (now == null || b == null || b.Length != 3) return;
            BannerMotionPatch.Style style = new((BannerMotionPatch.Direction)b[0], (BannerMotionPatch.Motion)b[1], (BannerMotionPatch.Effect)b[2]);
            BannerMotionPatch.WriteStyle(style, now.Current, now.HiddenScroll(now.Current));
        }

        private void ChangeStyle(BannerMotionPatch.Style style)
        {
            if (!_hasMotion || style == Style) return;
            _pending.Import(() => { _pending.Stage(_motion, StyleBytes(style)); return null; });
        }

        public int DirectionIndex { get => (int)Style.Direction; set { if (value >= 0) ChangeStyle(Style with { Direction = (BannerMotionPatch.Direction)value }); } }
        public int MotionIndex { get => (int)Style.Motion; set { if (value >= 0) ChangeStyle(Style with { Motion = (BannerMotionPatch.Motion)value }); } }
        public int EffectIndex { get => (int)Style.Effect; set { if (value >= 0) ChangeStyle(Style with { Effect = (BannerMotionPatch.Effect)value }); } }

        public bool Available => _banner != null;
        public List<string> Places { get; } = new();

        private int _place = 1;
        public int Place { get => _place; set { if (value < 0 || value == _place) return; _place = value; Raise(); Redraw(); } }

        public bool Patched => _banner != null && Shown(b => b.Patched);
        public string ColumnTip => Patched ? "In tiles from the left" : "Needs the banner position patch";
        public string RowTip => Patched ? "In tiles from the top" : "Needs the banner position patch";
        public bool OffersPatch => _banner != null && !Patched;
        public void OpenPatch() => AvaloniaEditorLauncher.OpenPatchToolboxAt("bannerPosition");

        private void OnPatchStateChanged(object sender, EventArgs e)
        {
            if (_banner != null && !_hasMotion && BannerMotionPatch.IsApplied)
            {
                _hasMotion = true;
                foreach (string n in new[] { nameof(HasMotion), nameof(OffersMotionPatch), nameof(DirectionIndex), nameof(MotionIndex), nameof(EffectIndex) }) Raise(n);
            }
            if (_banner == null || !BannerPositionPatch.IsApplied || Patched) return;
            _pending.Discard();
            _disk = _banner.FileBytes();
            _banner.FromBytes(_disk);
            _saved = _banner.Current;
            foreach (string n in new[] { nameof(Patched), nameof(OffersPatch), nameof(ColumnTip), nameof(RowTip), nameof(Across), nameof(Down) }) Raise(n);
        }

        public void Detach()
        {
            AppEvents.RomPatchStateChanged -= OnPatchStateChanged;
            StopPlaying();
        }

        private T Shown<T>(Func<PlaceNameBanner, T> read)
        {
            _banner.FromBytes(_pending.Read(_target));
            return read(_banner);
        }

        private PlaceNameBanner.Look Look => Shown(b => b.Current);

        private void Change(Func<PlaceNameBanner.Look, PlaceNameBanner.Look> change)
        {
            if (_banner == null) return;
            PlaceNameBanner.Look wanted = change(Look);
            string why = Shown(b => b.WhyNot(wanted));
            Problem = why;
            if (why != null) return;
            _pending.Import(() =>
            {
                _banner.FromBytes(_pending.Read(_target));
                _banner.Set(wanted);
                _pending.Stage(_target, _banner.ToBytes());
                return null;
            });
        }

        public int Across { get => _banner == null ? 0 : Look.Across; set => Change(l => l with { Across = value }); }
        public int Down { get => _banner == null ? 0 : Look.Down; set => Change(l => l with { Down = value }); }
        public int Width { get => _banner == null ? 0 : Look.Width; set => Change(l => l with { Width = value }); }
        public int Height { get => _banner == null ? 0 : Look.Height; set => Change(l => l with { Height = value }); }
        public int Time { get => _banner == null ? 0 : Look.Time; set => Change(l => l with { Time = value }); }
        public int Speed { get => _banner == null ? 0 : Look.Speed; set => Change(l => l with { Speed = value }); }
        public int SpeedOut { get => _banner == null ? 0 : Look.SpeedOut; set => Change(l => l with { SpeedOut = value }); }

        // ── dragging ──

        private PlaceNameBanner.Look _dragged;

        /// <summary>The banner's box on the screen, in pixels.</summary>
        public (int X, int Y, int W, int H) Box
        {
            get { PlaceNameBanner.Look l = _dragged ?? Look; return (l.Across * 8, l.Down * 8, l.Width * 8, l.Height * 8); }
        }

        /// <summary>Shows the banner at a column and row during a drag, without staging it.</summary>
        public void DragTo(int across, int down)
        {
            if (_banner == null || !Patched) return;
            PlaceNameBanner.Look l = Look, wanted = l with { Across = Math.Clamp(across, 0, PlaceNameBanner.ScreenColumns - l.Width), Down = Math.Max(0, down) };
            while (wanted.Down > 0 && Shown(b => b.WhyNot(wanted)) != null) wanted = wanted with { Down = wanted.Down - 1 };
            if (wanted == (_dragged ?? l)) return;
            _dragged = wanted;
            Redraw();
        }

        public void EndDrag()
        {
            if (_dragged is not PlaceNameBanner.Look l) return;
            _dragged = null;
            Change(_ => l);
            Redraw();
        }

        // ── slide preview ──

        private DispatcherTimer _timer;
        private int _offset, _phase, _held;
        private (int Scroll, int Level) _step;

        public bool Playing => _timer != null;

        /// <summary>Slides the banner in, holds it and slides it out at the game's 30 steps a second.</summary>
        public void Play()
        {
            if (_banner == null) return;
            StopPlaying();
            _offset = Shown(b => b.HiddenScroll(b.Current));
            _phase = 0;
            _held = 0;
            _timer = new DispatcherTimer(TimeSpan.FromSeconds(1.0 / 30), DispatcherPriority.Render, (_, _) => Step());
            _timer.Start();
            Raise(nameof(Playing));
        }

        private void Step()
        {
            PlaceNameBanner.Look l = Look;
            int hidden = Shown(b => b.HiddenScroll(l));
            if (_phase == 0 && (_offset = Math.Max(0, _offset - l.Speed)) == 0) _phase = 1;
            else if (_phase == 1 && ++_held >= l.Time) _phase = 2;
            else if (_phase == 2 && (_offset = Math.Min(hidden, _offset + l.SpeedOut)) == hidden) { StopPlaying(); return; }
            _step = _hasMotion ? BannerMotionPatch.Step(Style, l, hidden, _offset) : (_offset, 0);
            Redraw();
        }

        private void StopPlaying()
        {
            if (_timer == null) return;
            _timer.Stop();
            _timer = null;
            _step = (0, 0);
            Raise(nameof(Playing));
            Redraw();
        }

        public int MostHeight => _banner == null ? 1 : Shown(b => b.MostRows);
        public int MostWidth => _banner == null ? 1 : Shown(b => b.MostWidth(b.Current.Height));

        private string _problem;
        public string Problem { get => _problem; private set { _problem = value; Raise(); Raise(nameof(HasProblem)); } }
        public bool HasProblem => _problem != null;

        private Bitmap _picture;
        public Bitmap Picture { get => _picture; private set { _picture = value; Raise(); } }

        public IDisposable DrawScope() => _banner == null ? null : GameTextScreens.UseBanner(_saved, _dragged ?? Look);

        private void Redraw()
        {
            if (_banner == null) return;
            GameTextScreens.Shot shot;
            using (_banner == null ? null : GameTextScreens.UseBanner(_saved, _dragged ?? Look, _step.Scroll, _step.Level, Style.Effect)) shot = GameTextScreens.BannerOf(_place);
            Picture = shot == null ? null : ImageConverter.FromRgba(shot.Rgba, 256, 192);
            Changed?.Invoke();
        }

        public bool HasUnsavedChanges => _pending.IsDirty;
        public string UnsavedChangesDescription => "Place-name banner";
        public bool CanUndo => _pending.CanUndo;
        public bool CanRedo => _pending.CanRedo;
        public void Undo() => _pending.Undo();
        public void Redo() => _pending.Redo();

        public void SaveChanges()
        {
            _pending.Save();
            _saved = Look;
            Redraw();
            SaveNotice.Saved(UnsavedChangesDescription);
        }

        public void DiscardChanges()
        {
            Problem = null;
            _pending.Discard();
        }
    }
}
