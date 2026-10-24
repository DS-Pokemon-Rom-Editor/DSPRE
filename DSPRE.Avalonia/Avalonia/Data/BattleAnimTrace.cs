using System.Collections.Generic;

namespace DSPRE.Avalonia.Data
{
    /// <summary>What a move animation did and when, recorded pass by pass by <see cref="BattleAnimPlayer"/>, for the
    /// preview's timeline.</summary>
    public sealed class BattleAnimTrace
    {
        public enum Lane { Script, Particles, Effects, Background, Sound }

        /// <summary>One bar: from the pass it started to the pass it ended (equal for something instant), and the
        /// command it came from.</summary>
        public sealed class Item
        {
            public Lane Lane;
            public int Start, End;
            public int Command = -1;
            public string Label = "", Tip = "";
            // For particles, the slot and emitter; for sounds, the sound.
            public int A = -1, B = -1;
        }

        /// <summary>A value a bar can be dragged by: each step of the value moves the bar's end, or the whole bar, by
        /// <see cref="FramesPerStep"/> frames.</summary>
        public sealed class Knob
        {
            public int Command, Arg, Value, Min, Max;
            public int FramesPerStep;
            public bool MovesBar;
            public string Name = "";
        }

        public List<Item> Items { get; } = new List<Item>();

        /// <summary>Passes the whole move takes, End included.</summary>
        public int TotalPasses { get; set; }
    }
}
