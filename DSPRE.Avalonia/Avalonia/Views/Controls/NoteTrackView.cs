using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using DSPRE.Avalonia.Data;

namespace DSPRE.Avalonia.Views.Controls
{
    /// <summary>
    /// The notes of a sequence drawn out, time running left to right and pitch running up.
    /// </summary>
    public sealed class NoteTrackView : Control
    {
        private IReadOnlyList<SseqPlayer.Note> _notes;
        private double _seconds;
        private double _playhead;

        /// <summary>Hands over the notes to draw and how long the whole thing runs for.</summary>
        private string _emptyBecause = "Nothing to show. Pick a piece of music, a fanfare or a sound effect.";

        /// <summary>What to say when there are no notes, since "pick something" is wrong when something
        /// is already picked and simply has no notes to show.</summary>
        public void SayWhyEmpty(string because)
        {
            _emptyBecause = string.IsNullOrWhiteSpace(because)
                ? "Nothing to show. Pick a piece of music, a fanfare or a sound effect."
                : because;
            InvalidateVisual();
        }

        public void SetNotes(IReadOnlyList<SseqPlayer.Note> notes, double totalSeconds)
        {
            _notes = notes;
            _seconds = totalSeconds > 0 ? totalSeconds : 1;
            InvalidateVisual();
        }

        /// <summary>Where playback has got to, in seconds.</summary>
        public double Playhead
        {
            get => _playhead;
            set { _playhead = value; InvalidateVisual(); }
        }

        /// <summary>The moment a point across the control stands for, so a click can seek.</summary>
        public double SecondsAt(double x)
        {
            double w = Bounds.Width;
            return w <= 0 ? 0 : Math.Clamp(x / w * _seconds, 0, _seconds);
        }

        // Enough colours that the parts stay apart, chosen to read on a light and a dark ground alike.
        private static readonly Color[] TrackColours =
        {
            Color.FromRgb(0x4F, 0x9D, 0xD9), Color.FromRgb(0xE0, 0x8A, 0x3C),
            Color.FromRgb(0x5F, 0xB8, 0x7A), Color.FromRgb(0xD1, 0x63, 0x8C),
            Color.FromRgb(0x9B, 0x7F, 0xD4), Color.FromRgb(0xD6, 0xC0, 0x4A),
            Color.FromRgb(0x4C, 0xC0, 0xB8), Color.FromRgb(0xC2, 0x5C, 0x5C),
        };

        public override void Render(DrawingContext ctx)
        {
            double w = Bounds.Width, h = Bounds.Height;
            if (w <= 0 || h <= 0) return;

            if (_notes == null || _notes.Count == 0)
            {
                var faint = new SolidColorBrush(Color.FromArgb(110, 128, 128, 128));
                var text = new FormattedText(_emptyBecause,
                    System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                    Typeface.Default, 12, faint);
                ctx.DrawText(text, new Point(10, h / 2 - text.Height / 2));
                return;
            }

            // Fit the pitches that are actually played rather than all hundred and twenty eight, so a
            // sequence using one octave is not a thin line across the middle.
            int lowest = _notes.Min(n => n.Number), highest = _notes.Max(n => n.Number);
            if (highest - lowest < 11) { int mid = (lowest + highest) / 2; lowest = mid - 6; highest = mid + 6; }
            lowest = Math.Max(0, lowest - 1); highest = Math.Min(127, highest + 1);
            int span = Math.Max(1, highest - lowest + 1);
            double lane = h / span;

            // A line at every C, so the pitch can be read off rather than guessed at.
            var ruler = new Pen(new SolidColorBrush(Color.FromArgb(48, 128, 128, 128)), 1);
            for (int note = lowest; note <= highest; note++)
                if (note % 12 == 0)
                {
                    double y = h - (note - lowest + 1) * lane;
                    ctx.DrawLine(ruler, new Point(0, y), new Point(w, y));
                }

            double minWidth = 1.5;
            foreach (var n in _notes)
            {
                double x = n.StartSeconds / _seconds * w;
                double length = n.NoLengthGiven || n.DurationSeconds <= 0 ? 0.25 : n.DurationSeconds;
                double bw = Math.Max(minWidth, length / _seconds * w);
                if (x > w) continue;
                if (x + bw > w) bw = w - x;

                double y = h - (n.Number - lowest + 1) * lane;
                var c = TrackColours[Math.Abs(n.Track) % TrackColours.Length];
                // Quieter notes are drawn fainter, so the tune stands out from what is behind it.
                byte alpha = (byte)Math.Clamp(90 + n.Velocity, 90, 255);
                var brush = new SolidColorBrush(Color.FromArgb(alpha, c.R, c.G, c.B));
                ctx.FillRectangle(brush, new Rect(x, y, bw, Math.Max(1.5, lane - 1)));
            }

            if (_playhead > 0 && _playhead <= _seconds)
            {
                double x = _playhead / _seconds * w;
                var head = new Pen(new SolidColorBrush(Color.FromArgb(200, 220, 80, 80)), 1);
                ctx.DrawLine(head, new Point(x, 0), new Point(x, h));
            }
        }
    }
}
