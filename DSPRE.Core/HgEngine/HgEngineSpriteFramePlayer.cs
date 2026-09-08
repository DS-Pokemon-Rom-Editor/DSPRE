using System.Collections.Generic;

namespace DSPRE.HgEngine
{
    /// <summary>Plays one SpriteFrame[10] idle animation (data/SpriteOffsets.c's .frontFrames/.backFrames)
    /// with the game's own state machine: a one-shot run that ends on sprite frame 0, with frameNo &lt; -1
    /// entries acting as counted jumps rather than terminators. Treating the list as a loop of its
    /// non-negative entries freezes every species whose run is a single step (Venusaur, Tauros, Charizard).</summary>
    public sealed class HgEngineSpriteFramePlayer
    {
        /// <summary>MAX_ANIMATION_FRAMES: the array is fixed at 10 slots and running off its end ends the run.</summary>
        public const int MaxFrames = 10;

        private readonly int[] _loopTimers = new int[MaxFrames];
        private IReadOnlyList<HgEngineSpriteOffsets.SpriteFrameSlot> _slots;
        private int _index;
        private int _delay;

        /// <summary>Which half of the two-frame sheet to draw right now.</summary>
        public int SpriteFrame { get; private set; }

        /// <summary>Current per-frame horizontal shift, applied to the sprite and its shadow alike.</summary>
        public int HorizontalShift { get; private set; }

        /// <summary>False once the run has ended; it then rests on frame 0 until restarted.</summary>
        public bool Active { get; private set; }

        /// <summary>Restarts from slot 0. A list whose first slot is the -1 terminator never becomes active,
        /// matching the engine's own "no animation for this species" case.</summary>
        public void Start(IReadOnlyList<HgEngineSpriteOffsets.SpriteFrameSlot> slots)
        {
            _slots = slots;
            _index = 0;
            _delay = 0;
            SpriteFrame = 0;
            HorizontalShift = 0;
            Active = false;
            for (int i = 0; i < MaxFrames; i++) _loopTimers[i] = 0;

            if (slots == null || slots.Count == 0 || slots[0].FrameNo == -1) return;

            Active = true;
            SpriteFrame = slots[0].FrameNo;
            _delay = slots[0].Duration;
            HorizontalShift = slots[0].HorizontalShift;
        }

        /// <summary>One game frame. A slot with duration N is held for N+1 ticks, as the engine counts it.</summary>
        public void Tick()
        {
            if (!Active) return;
            if (_delay != 0) { _delay--; return; }

            _index++;

            // frameNo < -1 encodes "jump to slot (-frameNo - 2)", taken until this slot's own counter
            // reaches its duration. The bound is ours: the engine reads past the array here, and a
            // self-referential jump chain would spin forever on the UI thread.
            for (int guard = 0; guard < MaxFrames * MaxFrames; guard++)
            {
                if (_index < 0 || _index >= MaxFrames || SlotAt(_index).FrameNo >= -1) break;

                var jump = SlotAt(_index);
                _loopTimers[_index]++;
                if (jump.Duration == _loopTimers[_index] || jump.Duration == 0)
                {
                    _loopTimers[_index] = 0;
                    _index++;
                }
                else
                {
                    _index = -jump.FrameNo - 2;
                }
            }

            if (_index < 0 || _index >= MaxFrames || SlotAt(_index).FrameNo == -1)
            {
                SpriteFrame = 0;
                HorizontalShift = 0;
                Active = false;
                return;
            }

            var slot = SlotAt(_index);
            SpriteFrame = slot.FrameNo;
            _delay = slot.Duration;
            HorizontalShift = slot.HorizontalShift;
            // verticalShift is deliberately not applied: the engine stores it but never reads it back.
        }

        private HgEngineSpriteOffsets.SpriteFrameSlot SlotAt(int index) =>
            index < _slots.Count ? _slots[index] : default;   // a short list behaves as trailing terminators

        /// <summary>Every sprite frame this run can actually display, for the editor's blank-frame warnings.
        /// Frame 0 is always included because the run ends on it.</summary>
        public static IReadOnlyList<int> ReachableFrames(IReadOnlyList<HgEngineSpriteOffsets.SpriteFrameSlot> slots)
        {
            var seen = new List<int> { 0 };
            var player = new HgEngineSpriteFramePlayer();
            player.Start(slots);
            if (player.Active && !seen.Contains(player.SpriteFrame)) seen.Add(player.SpriteFrame);

            // Bounded by the longest run any counted-jump chain can produce.
            for (int i = 0; i < MaxFrames * MaxFrames && player.Active; i++)
            {
                player._delay = 0;
                player.Tick();
                if (player.Active && !seen.Contains(player.SpriteFrame)) seen.Add(player.SpriteFrame);
            }
            return seen;
        }
    }
}
