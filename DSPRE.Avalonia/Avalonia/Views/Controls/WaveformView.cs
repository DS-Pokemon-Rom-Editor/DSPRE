using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace DSPRE.Avalonia.Views.Controls
{
    /// <summary>
    /// Draws a sound so it can be looked at as well as heard: the two channels one above the other, and a
    /// line showing where playing has got to.
    /// </summary>
    public sealed class WaveformView : Control
    {
        private short[] _pcm;
        private int _sampleRate = 32000;
        private double _playedSeconds = -1;
        private double _markSeconds;

        public static readonly StyledProperty<IBrush> WaveBrushProperty =
            AvaloniaProperty.Register<WaveformView, IBrush>(nameof(WaveBrush), Brushes.SteelBlue);

        public IBrush WaveBrush
        {
            get => GetValue(WaveBrushProperty);
            set => SetValue(WaveBrushProperty, value);
        }

        public static readonly StyledProperty<IBrush> PlayheadBrushProperty =
            AvaloniaProperty.Register<WaveformView, IBrush>(nameof(PlayheadBrush), Brushes.OrangeRed);

        public IBrush PlayheadBrush
        {
            get => GetValue(PlayheadBrushProperty);
            set => SetValue(PlayheadBrushProperty, value);
        }

        static WaveformView()
        {
            AffectsRender<WaveformView>(WaveBrushProperty, PlayheadBrushProperty);
        }

        /// <summary>How long the sound lasts, in seconds.</summary>
        public double Seconds => _pcm == null || _sampleRate <= 0 ? 0 : _pcm.Length / 2.0 / _sampleRate;

        /// <summary>Where somebody clicked, which is where playing starts from.</summary>
        public double MarkSeconds => _markSeconds;

        /// <summary>Show a different sound. Clears the mark and the line.</summary>
        public void Show(short[] interleavedStereoPcm, int sampleRate)
        {
            _pcm = interleavedStereoPcm;
            _sampleRate = sampleRate > 0 ? sampleRate : 32000;
            _markSeconds = 0;
            _playedSeconds = -1;
            InvalidateVisual();
        }

        /// <summary>Move the line. A negative value takes it away.</summary>
        public void ShowPlayedTo(double seconds)
        {
            if (Math.Abs(seconds - _playedSeconds) < 0.005) return;
            _playedSeconds = seconds;
            InvalidateVisual();
        }

        /// <summary>Start playing from wherever was clicked.</summary>
        public void MarkAt(double seconds)
        {
            _markSeconds = Math.Clamp(seconds, 0, Seconds);
            InvalidateVisual();
        }

        public override void Render(DrawingContext ctx)
        {
            double w = Bounds.Width, h = Bounds.Height;
            if (w <= 1 || h <= 1) return;

            var mid = new Pen(new SolidColorBrush(Color.FromArgb(70, 128, 128, 128)), 1);
            double halfH = h / 2;

            if (_pcm == null || _pcm.Length < 4)
            {
                ctx.DrawLine(mid, new Point(0, halfH), new Point(w, halfH));
                return;
            }

            int frames = _pcm.Length / 2;
            var brush = WaveBrush ?? Brushes.SteelBlue;

            for (int channel = 0; channel < 2; channel++)
            {
                double top = channel * halfH;
                double centre = top + halfH / 2;
                double scale = halfH / 2 - 2;
                ctx.DrawLine(mid, new Point(0, centre), new Point(w, centre));

                for (int x = 0; x < (int)w; x++)
                {
                    int from = (int)((long)x * frames / (long)w);
                    int to = (int)((long)(x + 1) * frames / (long)w);
                    if (to <= from) to = from + 1;
                    if (from >= frames) break;
                    if (to > frames) to = frames;

                    int lo = short.MaxValue, hi = short.MinValue;
                    for (int i = from; i < to; i++)
                    {
                        int s = _pcm[i * 2 + channel];
                        if (s < lo) lo = s;
                        if (s > hi) hi = s;
                    }
                    double yTop = centre - hi / 32768.0 * scale;
                    double yBottom = centre - lo / 32768.0 * scale;
                    if (yBottom - yTop < 1) yBottom = yTop + 1;
                    ctx.FillRectangle(brush, new Rect(x, yTop, 1, yBottom - yTop));
                }
            }

            double total = Seconds;
            if (total <= 0) return;

            if (_markSeconds > 0)
            {
                double mx = _markSeconds / total * w;
                ctx.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(120, 160, 160, 160)), 1),
                    new Point(mx, 0), new Point(mx, h));
            }
            if (_playedSeconds >= 0)
            {
                double px = Math.Clamp(_playedSeconds / total, 0, 1) * w;
                ctx.DrawLine(new Pen(PlayheadBrush ?? Brushes.OrangeRed, 1.5), new Point(px, 0), new Point(px, h));
            }
        }
    }
}
