using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Rendering;
using DSPRE.Avalonia.Data;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia.Views.Controls
{
    /// <summary>
    /// HeartGold and SoulSilver's bottom screen. The touch menu reacts to a touch the way the game does without
    /// opening anything: the A button presses A, the running shoes flip, an icon lights up while held. While a
    /// script runs everything but the A button is dimmed and ignores touches. The Poké Ball screen takes a touch
    /// on one of its entries as picking it.
    /// </summary>
    public sealed class HgssTouchScreenView : Control, ICustomHitTest
    {
        public static readonly StyledProperty<bool> ShowsChoicesProperty =
            AvaloniaProperty.Register<HgssTouchScreenView, bool>(nameof(ShowsChoices));
        public static readonly StyledProperty<double> BrightnessProperty =
            AvaloniaProperty.Register<HgssTouchScreenView, double>(nameof(Brightness), 1.0);
        public static readonly StyledProperty<bool> DimmedProperty =
            AvaloniaProperty.Register<HgssTouchScreenView, bool>(nameof(Dimmed));
        public static readonly StyledProperty<int> ALabelProperty =
            AvaloniaProperty.Register<HgssTouchScreenView, int>(nameof(ALabel), HgssTouchScreen.TalkMessage);
        public static readonly StyledProperty<IReadOnlyList<string>> ChoicesProperty =
            AvaloniaProperty.Register<HgssTouchScreenView, IReadOnlyList<string>>(nameof(Choices));
        public static readonly StyledProperty<bool> YesNoProperty =
            AvaloniaProperty.Register<HgssTouchScreenView, bool>(nameof(YesNo));
        public static readonly StyledProperty<int> CursorProperty =
            AvaloniaProperty.Register<HgssTouchScreenView, int>(nameof(Cursor));
        public static readonly StyledProperty<bool> CursorShownProperty =
            AvaloniaProperty.Register<HgssTouchScreenView, bool>(nameof(CursorShown), true);

        /// <summary>The Poké Ball screen instead of the touch menu.</summary>
        public bool ShowsChoices { get => GetValue(ShowsChoicesProperty); set => SetValue(ShowsChoicesProperty, value); }
        /// <summary>1 as normal, down to 0 at the black middle of a swap.</summary>
        public double Brightness { get => GetValue(BrightnessProperty); set => SetValue(BrightnessProperty, value); }
        /// <summary>A script is running.</summary>
        public bool Dimmed { get => GetValue(DimmedProperty); set => SetValue(DimmedProperty, value); }
        /// <summary>The text bank entry the A button shows.</summary>
        public int ALabel { get => GetValue(ALabelProperty); set => SetValue(ALabelProperty, value); }
        /// <summary>The entries of the touch question on show, or none.</summary>
        public IReadOnlyList<string> Choices { get => GetValue(ChoicesProperty); set => SetValue(ChoicesProperty, value); }
        public bool YesNo { get => GetValue(YesNoProperty); set => SetValue(YesNoProperty, value); }
        public int Cursor { get => GetValue(CursorProperty); set => SetValue(CursorProperty, value); }
        public bool CursorShown { get => GetValue(CursorShownProperty); set => SetValue(CursorShownProperty, value); }

        /// <summary>The ROM's touch screen graphics.</summary>
        public static HgssTouchScreen Screen { get; set; }
        /// <summary>The font the touch screen writes in.</summary>
        public static FieldFont Font { get; set; }
        /// <summary>The touch menu's words by text bank entry, gaps already filled.</summary>
        public static Func<int, string> Text { get; set; }

        public Action<int> PlaySound { get; set; }
        public Action<int> ChoiceTouched { get; set; }
        public Action APressed { get; set; }
        public Action AReleased { get; set; }

        private bool _aHeld, _shoesOn;
        private int _highlight = -1;
        private WriteableBitmap _picture;
        private string _key;

        static HgssTouchScreenView()
        {
            AffectsRender<HgssTouchScreenView>(ShowsChoicesProperty, BrightnessProperty, DimmedProperty, ALabelProperty,
                                               ChoicesProperty, YesNoProperty, CursorProperty, CursorShownProperty);
        }

        public HgssTouchScreenView()
        {
            RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);
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
            if (Screen == null || Brightness < 1) return;
            var (scale, ox, oy) = Fit();
            if (scale <= 0) return;
            var p = e.GetPosition(this);
            int x = (int)((p.X - ox) / scale), y = (int)((p.Y - oy) / scale);
            e.Handled = true;

            if (ShowsChoices)
            {
                int count = Choices?.Count ?? 0;
                if (count == 0) return;
                int index = HgssTouchScreen.HitChoice(count, YesNo, x, y);
                if (index >= 0) ChoiceTouched?.Invoke(index);
                return;
            }

            var (spot, which) = HgssTouchScreen.HitMenu(x, y);
            switch (spot)
            {
                case HgssTouchScreen.MenuSpot.AButton:
                    _aHeld = true;
                    e.Pointer.Capture(this);
                    APressed?.Invoke();
                    break;
                case HgssTouchScreen.MenuSpot.Shoes when !Dimmed:
                    _shoesOn = !_shoesOn;
                    break;
                case HgssTouchScreen.MenuSpot.Icon when !Dimmed:
                    _highlight = which;
                    e.Pointer.Capture(this);
                    PlaySound?.Invoke(1500);
                    break;
                default:
                    return;
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
            if (!_aHeld && _highlight < 0) return;
            if (_aHeld) AReleased?.Invoke();
            _aHeld = false;
            _highlight = -1;
            InvalidateVisual();
        }

        public override void Render(DrawingContext ctx)
        {
            var (scale, ox, oy) = Fit();
            if (scale <= 0) return;
            var target = new Rect(ox, oy, DsBgScreen.Width * scale, DsBgScreen.Height * scale);
            var screen = Screen;
            if (screen == null) { ctx.FillRectangle(Brushes.Black, target); return; }

            var choices = Choices;
            string key = ShowsChoices
                ? $"c|{(choices == null ? "" : string.Join("", choices))}|{YesNo}|{Cursor}|{CursorShown}|{Brightness:0.00}"
                : $"m|{Dimmed}|{_aHeld}|{_shoesOn}|{_highlight}|{ALabel}|{Brightness:0.00}";
            if (_picture == null || _key != key)
            {
                byte[] rgba = ShowsChoices
                    ? screen.RenderChoices(Font, choices, YesNo, Cursor, CursorShown)
                    : screen.RenderMenu(Font, Text, Dimmed, _aHeld, _shoesOn, _highlight, ALabel);
                DsBgScreen.Fade(rgba, Brightness);
                _picture?.Dispose();
                _picture = ToBitmap(rgba);
                _key = key;
            }
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
