using System;
using System.Collections.Generic;
using System.Linq;

namespace DSPRE.ROMFiles
{
    /// <summary>The three text speeds the Options menu offers, as the delay the printer waits per letter.</summary>
    public enum FieldTextSpeed { Slow = 8, Mid = 4, Fast = 1 }

    /// <summary>
    /// Prints a message into the box a letter at a time, the way RenderText does. It ticks twice per field
    /// frame, since the print queue runs twice for every pass of the main loop.
    /// </summary>
    public sealed class FieldTextPrinter
    {
        /// <summary>Print ticks for every field frame.</summary>
        public const int TicksPerFrame = 2;

        /// <summary>A scroll moves the text 4 pixels a tick over a 16 pixel line.</summary>
        public const int ScrollTicks = 4;
        public const int ScrollPixelsPerTick = 4;

        /// <summary>The arrow changes its height every 9 ticks: 0, 1, 2 and 1 pixels down.</summary>
        public const int ArrowTicksPerPose = 9;
        private static readonly int[] ArrowPoses = { 0, 1, 2, 1 };

        private readonly IReadOnlyList<FieldMessageFrame> _pages;
        private readonly int _delay;
        private readonly bool _skippable;

        private int _page;
        private string _content = "";
        private IReadOnlyList<string> _carried = Array.Empty<string>();
        private int _printed;
        private int _run, _nextRun;
        private bool _speedUp;
        private int _scrollLeft;
        private int _arrowTicks;

        /// <summary>Called when a waiting page is pressed on, which is when the games play sound 1500.</summary>
        public Action PageTurned { get; set; }

        /// <param name="instant">Prints everything at once and never waits, the way MessageInstant does.</param>
        public FieldTextPrinter(IReadOnlyList<FieldMessageFrame> pages, FieldTextSpeed speed = FieldTextSpeed.Mid,
                                bool skippable = true, bool instant = false)
        {
            _pages = pages ?? Array.Empty<FieldMessageFrame>();
            _delay = Math.Max(1, (int)speed);
            _skippable = skippable;

            if (_pages.Count == 0) { Finished = true; return; }
            StartPage(0);

            // An instant message cannot wait for a press that has not happened, so only the text up to its
            // first wait ever shows.
            if (instant)
            {
                _printed = _content.Length;
                Finished = true;
            }
        }

        /// <summary>Whether the printer has read to the end of the text.</summary>
        public bool Finished { get; private set; }

        /// <summary>Whether the arrow is up and a press is needed before anything else happens.</summary>
        public bool WaitingForPress { get; private set; }

        /// <summary>How far down the arrow is bobbing, in pixels.</summary>
        public int ArrowOffset => ArrowPoses[(_arrowTicks / ArrowTicksPerPose) % ArrowPoses.Length];

        /// <summary>How far the lines have slid up part way through a scroll, in pixels.</summary>
        public int ScrollPixels => _scrollLeft > 0 ? (ScrollTicks - _scrollLeft + 1) * ScrollPixelsPerTick : 0;

        /// <summary>The lines on show right now, top first, the last one maybe only part written.</summary>
        public IReadOnlyList<string> Lines
        {
            get
            {
                var lines = new List<string>(_carried);
                if (_printed > 0 || _carried.Count == 0)
                    lines.AddRange(_content.Substring(0, Math.Min(_printed, _content.Length)).Split('\n'));
                return lines;
            }
        }

        /// <summary>The lines joined as the message box wants them.</summary>
        public string Text => string.Join("\n", Lines);

        private void StartPage(int index)
        {
            _page = index;
            var page = _pages[index];
            int carried = 0;
            if (index > 0 && _pages[index - 1].Wait == MessageWait.Scroll)
                carried = Math.Min(page.Lines.Count, Math.Max(0, _pages[index - 1].Lines.Count - 1));
            else if (index > 0 && _pages[index - 1].Wait == MessageWait.Simple)
                carried = Math.Min(page.Lines.Count, _pages[index - 1].Lines.Count);

            _carried = page.Lines.Take(carried).ToList();
            _content = string.Join("\n", page.Lines.Skip(carried));
            _printed = 0;
            _nextRun = _run;
        }

        /// <summary>Runs one print tick.</summary>
        /// <param name="newPress">A or B went down since the last field frame.</param>
        /// <param name="held">A or B is being held.</param>
        public void Tick(bool newPress, bool held)
        {
            if (Finished) return;
            _run++;

            if (_scrollLeft > 0)
            {
                if (--_scrollLeft == 0) StartPage(_page + 1);
                return;
            }

            if (WaitingForPress)
            {
                _arrowTicks++;
                if (!newPress) return;
                WaitingForPress = false;
                PageTurned?.Invoke();
                if (_pages[_page].Wait == MessageWait.Scroll) _scrollLeft = ScrollTicks;
                else StartPage(_page + 1);
                return;
            }

            if (_run < _nextRun)
            {
                if (_skippable && newPress) _speedUp = true;
                else if (!(_speedUp && held)) return;
                _nextRun = _run;
            }

            if (_printed < _content.Length)
            {
                char c = _content[_printed++];
                // A line break draws nothing and spends one of its waits on the same tick.
                _nextRun = _run + (c == '\n' ? _delay - 1 : _delay);
                if (c == '\n' && _nextRun <= _run) { _run--; Tick(false, false); }
                return;
            }

            // The code after the last letter is read on the letter's next turn.
            var pageNow = _pages[_page];
            if (pageNow.Wait != MessageWait.None && _page + 1 < _pages.Count)
            {
                WaitingForPress = true;
                _arrowTicks = 0;
                return;
            }

            Finished = true;
        }

        /// <summary>Runs a field frame's worth of ticks.</summary>
        public void Frame(bool newPress, bool held)
        {
            for (int i = 0; i < TicksPerFrame && !Finished; i++)
                Tick(i == 0 && newPress, held);
        }
    }
}
