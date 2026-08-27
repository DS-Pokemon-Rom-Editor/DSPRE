using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace DSPRE.Avalonia
{
    /// <summary>
    /// AvaloniaEdit background renderer that draws red wavy "squiggle" underlines beneath error ranges (document
    /// offset + length), the way an IDE flags syntax errors. The hosting view pushes the current parse-error ranges
    /// via <see cref="SetErrors"/> and repaints the layer; ranges outside the document are clamped/skipped.
    /// </summary>
    public sealed class SquiggleRenderer : IBackgroundRenderer
    {
        private readonly List<(int Offset, int Length)> _errors = new();
        private static readonly IPen Pen = new Pen(Brushes.Red, 1);

        public KnownLayer Layer => KnownLayer.Selection;

        public void SetErrors(IEnumerable<(int Offset, int Length)> errors)
        {
            _errors.Clear();
            if (errors != null) _errors.AddRange(errors);
        }

        public void Draw(TextView textView, DrawingContext drawingContext)
        {
            if (_errors.Count == 0 || textView?.Document == null || !textView.VisualLinesValid) return;
            int docLen = textView.Document.TextLength;
            foreach (var (off, len) in _errors)
            {
                if (len <= 0 || off < 0 || off >= docLen) continue;
                int length = Math.Min(len, docLen - off);
                var seg = new TextSegment { StartOffset = off, Length = length };
                foreach (var r in BackgroundGeometryBuilder.GetRectsForSegment(textView, seg))
                    DrawWave(drawingContext, r.BottomLeft, r.BottomRight);
            }
        }

        // A short saw/wave along the bottom of the error's rect.
        private static void DrawWave(DrawingContext dc, Point start, Point end)
        {
            const double step = 3.0, amp = 2.0;
            if (end.X - start.X < 1) return;
            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                ctx.BeginFigure(start, false);
                bool up = true;
                for (double x = start.X; x < end.X;)
                {
                    x = Math.Min(x + step, end.X);
                    ctx.LineTo(new Point(x, start.Y - (up ? amp : 0)));
                    up = !up;
                }
            }
            dc.DrawGeometry(null, Pen, geo);
        }
    }
}
