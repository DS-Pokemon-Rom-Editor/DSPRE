using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Rendering;
using Avalonia.Threading;
using DSPRE.Avalonia.Data;

namespace DSPRE.Avalonia.Views.Controls
{
    /// <summary>
    /// Platinum's bottom screen. Touching it reacts the way the Pokétch does, without ever changing app:
    /// during a script the side buttons take their locked look and the face beeps; otherwise the buttons go
    /// down and the face lights up while held.
    /// </summary>
    public sealed class PoketchView : Control, ICustomHitTest
    {
        public static readonly StyledProperty<bool> OwnedProperty =
            AvaloniaProperty.Register<PoketchView, bool>(nameof(Owned), true);
        public static readonly StyledProperty<bool> FemaleProperty =
            AvaloniaProperty.Register<PoketchView, bool>(nameof(Female));
        public static readonly StyledProperty<bool> ScriptRunningProperty =
            AvaloniaProperty.Register<PoketchView, bool>(nameof(ScriptRunning));

        /// <summary>Whether the player has been given the Pokétch yet.</summary>
        public bool Owned { get => GetValue(OwnedProperty); set => SetValue(OwnedProperty, value); }
        public bool Female { get => GetValue(FemaleProperty); set => SetValue(FemaleProperty, value); }
        public bool ScriptRunning { get => GetValue(ScriptRunningProperty); set => SetValue(ScriptRunningProperty, value); }

        /// <summary>The Pokétch graphics of the ROM that is open.</summary>
        public static PoketchScreen Screen { get; set; }

        /// <summary>Plays a sound effect by sequence number.</summary>
        public Action<int> PlaySound { get; set; }

        private PoketchScreen.Look _up, _down;
        private bool _lit;
        private WriteableBitmap _picture;
        private string _key;
        private readonly DispatcherTimer _clock;

        static PoketchView()
        {
            AffectsRender<PoketchView>(OwnedProperty, FemaleProperty, ScriptRunningProperty);
        }

        public PoketchView()
        {
            RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);
            _clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _clock.Tick += (_, _) => InvalidateVisual();
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            _clock.Start();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            _clock.Stop();
            base.OnDetachedFromVisualTree(e);
        }

        public bool HitTest(Point point) => new Rect(Bounds.Size).Contains(point);

        private (double scale, double ox, double oy) Fit()
        {
            double scale = Math.Min(Bounds.Width / DsBgScreen.Width, Bounds.Height / DsBgScreen.Height);
            return (scale, (Bounds.Width - DsBgScreen.Width * scale) / 2, (Bounds.Height - DsBgScreen.Height * scale) / 2);
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            if (Screen == null || !Owned) return;
            var (scale, ox, oy) = Fit();
            if (scale <= 0) return;
            var p = e.GetPosition(this);
            var spot = PoketchScreen.HitTest((int)((p.X - ox) / scale), (int)((p.Y - oy) / scale));
            if (spot == PoketchScreen.Spot.None) return;

            e.Pointer.Capture(this);
            e.Handled = true;
            if (spot == PoketchScreen.Spot.Screen)
            {
                if (ScriptRunning) PlaySound?.Invoke(PoketchScreen.BeepSound);
                else _lit = true;
            }
            else
            {
                var look = ScriptRunning ? PoketchScreen.Look.Lock : PoketchScreen.Look.Hold;
                if (spot == PoketchScreen.Spot.Up) _up = look; else _down = look;
                PlaySound?.Invoke(ScriptRunning ? PoketchScreen.LockSound : PoketchScreen.HoldSound);
            }
            InvalidateVisual();
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);
            LetGo();
            e.Pointer.Capture(null);
        }

        protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
        {
            base.OnPointerCaptureLost(e);
            LetGo();
        }

        private void LetGo()
        {
            if (_up == PoketchScreen.Look.Free && _down == PoketchScreen.Look.Free && !_lit) return;
            _up = _down = PoketchScreen.Look.Free;
            _lit = false;
            InvalidateVisual();
        }

        public override void Render(DrawingContext ctx)
        {
            var (scale, ox, oy) = Fit();
            if (scale <= 0) return;
            var screen = Screen;
            var now = DateTime.Now;
            string key = screen == null ? "none"
                : $"{Owned}|{Female}|{_lit}|{_up}|{_down}|{now.Hour}:{now.Minute}|{screen.GetHashCode()}";
            if (_picture == null || _key != key)
            {
                byte[] rgba = screen == null ? null
                    : Owned ? screen.RenderWatch(Female, 0, _lit, now.Hour, now.Minute, _up, _down)
                    : screen.RenderUnavailable();
                _picture?.Dispose();
                _picture = rgba == null ? null : ToBitmap(rgba);
                _key = key;
            }
            var target = new Rect(ox, oy, DsBgScreen.Width * scale, DsBgScreen.Height * scale);
            if (_picture == null) { ctx.FillRectangle(Brushes.Black, target); return; }
            ctx.DrawImage(_picture, new Rect(0, 0, DsBgScreen.Width, DsBgScreen.Height), target);
        }

        private static WriteableBitmap ToBitmap(byte[] rgba)
        {
            var bmp = new WriteableBitmap(new PixelSize(DsBgScreen.Width, DsBgScreen.Height), new Vector(96, 96),
                                          PixelFormat.Bgra8888, AlphaFormat.Unpremul);
            using var buf = bmp.Lock();
            unsafe
            {
                for (int y = 0; y < DsBgScreen.Height; y++)
                {
                    var row = (byte*)buf.Address + y * buf.RowBytes;
                    for (int x = 0; x < DsBgScreen.Width; x++)
                    {
                        int at = (y * DsBgScreen.Width + x) * 4;
                        row[x * 4] = rgba[at + 2];
                        row[x * 4 + 1] = rgba[at + 1];
                        row[x * 4 + 2] = rgba[at];
                        row[x * 4 + 3] = 255;
                    }
                }
            }
            return bmp;
        }
    }
}
