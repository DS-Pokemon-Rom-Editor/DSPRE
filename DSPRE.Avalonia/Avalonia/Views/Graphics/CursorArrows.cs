using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using DSPRE.Avalonia.ViewModels.Graphics;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia.Views.Graphics
{
    /// <summary>A dot on every cursor stop and an arrow to where each direction goes; the selected stop's arrows are bold.</summary>
    public sealed class CursorArrows : Control
    {
        public const double HitRadius = 14;
        private const double Dot = 6;

        private List<CursorStopRow> _stops = new List<CursorStopRow>();
        private CursorStopRow _selected;

        public void Show(List<CursorStopRow> stops, CursorStopRow selected)
        {
            _stops = stops ?? new List<CursorStopRow>();
            _selected = selected;
            InvalidateVisual();
        }

        public override void Render(DrawingContext context)
        {
            Dictionary<int, CursorStopRow> byStop = new Dictionary<int, CursorStopRow>();
            foreach (CursorStopRow row in _stops) byStop[row.Stop] = row;
            // The selected stop's arrows go last so they sit on top.
            foreach (bool selected in new[] { false, true })
                foreach (CursorStopRow row in _stops)
                {
                    if (ReferenceEquals(row, _selected) != selected) continue;
                    IBrush brush = Brush.Parse(row.Colour);
                    Pen pen = new Pen(brush, selected ? 3 : 1.5) { DashStyle = selected ? null : DashStyle.Dash };
                    foreach (CursorDirection d in CursorDirections.All)
                    {
                        if (!byStop.TryGetValue(row.Target(d), out CursorStopRow target) || ReferenceEquals(target, row)) continue;
                        Arrow(context, pen, brush, row.At, target.At, d);
                    }
                }
            foreach (CursorStopRow row in _stops)
            {
                bool selected = ReferenceEquals(row, _selected);
                context.DrawEllipse(Brush.Parse(row.Colour), new Pen(Brushes.Black, selected ? 2.5 : 1.5),
                    new Point(row.At.X, row.At.Y), selected ? Dot + 2 : Dot, selected ? Dot + 2 : Dot);
            }
        }

        // Curves each arrow to one side by direction so Up and Down between two stops don't overlap.
        private static void Arrow(DrawingContext context, Pen pen, IBrush brush, (double X, double Y) from, (double X, double Y) to,
                                  CursorDirection d)
        {
            Vector along = new Vector(to.X - from.X, to.Y - from.Y);
            double length = Math.Max(1, along.Length);
            Vector unit = along / length, side = new Vector(-unit.Y, unit.X);
            double bend = d is CursorDirection.Up or CursorDirection.Left ? 18 : -18;
            Point a = new Point(from.X, from.Y) + unit * (Dot + 2), b = new Point(to.X, to.Y) - unit * (Dot + 4);
            Point control = new Point((a.X + b.X) / 2, (a.Y + b.Y) / 2) + side * bend;
            StreamGeometry curve = new StreamGeometry();
            using (StreamGeometryContext g = curve.Open())
            {
                g.BeginFigure(a, false);
                g.QuadraticBezierTo(control, b);
                g.EndFigure(false);
            }
            context.DrawGeometry(null, pen, curve);
            Vector tip = b - control;
            tip /= Math.Max(1, tip.Length);
            Vector across = new Vector(-tip.Y, tip.X);
            StreamGeometry head = new StreamGeometry();
            using (StreamGeometryContext g = head.Open())
            {
                g.BeginFigure(b, true);
                g.LineTo(b - tip * 10 + across * 5);
                g.LineTo(b - tip * 10 - across * 5);
                g.EndFigure(true);
            }
            context.DrawGeometry(brush, null, head);
        }
    }
}
