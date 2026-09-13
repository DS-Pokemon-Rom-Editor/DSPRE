namespace DSPRE.ROMFiles
{
    /// <summary>
    /// The clock a field object's pictures run on. A person's walk is a sixteen-frame loop for each facing that
    /// moves on at a rate set by how fast the step is, so every tile moves it on one foot, eight frames. It
    /// settles back onto a foot when they stop and starts again from the first foot when they turn. Rates and
    /// the settle rule follow the field object animation code in the pokeplatinum decomp; HeartGold and
    /// SoulSilver use the same animation tables.
    /// </summary>
    public sealed class FieldWalkCycle
    {
        // Half frames, so the half-frame rate of the slow walks is exact.
        private int _half;
        private int _pattern;
        private bool _faced;
        private MoveFacing _facing;

        /// <summary>Where in the sixteen-frame loop it is, which picks the picture: a quarter of it each.</summary>
        public int Frame => _half / 2;

        /// <summary>Set while the hero is dashing, which draws from the running pictures instead.</summary>
        public bool Running { get; private set; }

        /// <summary>The dash's own sixteen-frame loop, one frame a frame.</summary>
        public int RunFrame { get; private set; }

        /// <summary>The loop a pair Pokémon bobs to, which runs whether it moves or not.</summary>
        public int PairFrame { get; private set; }

        /// <summary>One frame of the always-running clock.</summary>
        public void Tick() => PairFrame = (PairFrame + 1) % 20;

        /// <summary>Facing a new way starts the walk again from the first foot.</summary>
        public void Face(MoveFacing facing)
        {
            if (_faced && facing == _facing) return;
            _faced = true;
            _facing = facing;
            _half = 0;
            _pattern = 0;
            RunFrame = 0;
            PairFrame = 0;
        }

        /// <summary>A frame not walking: it settles back onto whichever foot it last reached.</summary>
        public void Rest()
        {
            _half = Frame / 8 * 8 * 2;
            _pattern = 0;
            Running = false;
        }

        /// <summary>One frame of a step that takes <paramref name="stepFrames"/> frames.</summary>
        public void Walk(int stepFrames)
        {
            Running = false;
            Advance(stepFrames);
        }

        /// <summary>One frame of the hero's dash, which also keeps the walk in step for anyone without running pictures.</summary>
        public void Dash()
        {
            if (!Running) { Running = true; RunFrame = RunFrame / 4 * 4; }
            RunFrame = (RunFrame + 1) % 16;
            Advance(FieldMovementScript.RunFrames);
        }

        public void CopyFrom(FieldWalkCycle other)
        {
            if (other == null) return;
            _half = other._half;
            _pattern = other._pattern;
            _faced = other._faced;
            _facing = other._facing;
            Running = other.Running;
            RunFrame = other.RunFrame;
            PairFrame = other.PairFrame;
        }

        // The uneven speeds move on by a repeating pattern that still adds up to one foot a tile.
        private static readonly int[] SixFrames = { 2, 2, 4, 2, 2, 4 };
        private static readonly int[] ThreeFrames = { 6, 4, 6 };
        private static readonly int[] SevenFrames = { 4, 2, 2, 2, 2, 2, 2 };

        private void Advance(int stepFrames)
        {
            int halves;
            switch (stepFrames)
            {
                case 32: case 16: halves = 1; break;
                case 8: halves = 2; break;
                case 4: halves = 4; break;
                case 2: halves = 8; break;
                case 1: halves = 16; break;
                case 6: halves = SixFrames[_pattern++ % SixFrames.Length]; break;
                case 3: halves = ThreeFrames[_pattern++ % ThreeFrames.Length]; break;
                case 7: halves = SevenFrames[_pattern++ % SevenFrames.Length]; break;
                default: halves = System.Math.Max(1, (16 + stepFrames / 2) / System.Math.Max(1, stepFrames)); break;
            }
            // Running off the end of the loop goes back to its start rather than wrapping round.
            _half = _half + halves > 31 ? 0 : _half + halves;
        }
    }
}
