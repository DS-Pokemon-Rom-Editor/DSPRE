using System.Collections.Generic;

namespace DSPRE.ROMFiles
{
    /// <summary>Plays a battle sprite frame run as the game does: once, ending on frame 0.</summary>
    public sealed class SpriteFramePlayer
    {
        /// <summary>MAX_ANIMATION_FRAMES: the array is fixed at 10 slots and running off its end ends the run.</summary>
        public const int MaxFrames = 10;

        private readonly int[] _loopTimers = new int[MaxFrames];
        private IReadOnlyList<SpriteFrameSlot> _slots;
        private int _index;
        private int _delay;

        /// <summary>Which half of the two-frame sheet to draw right now.</summary>
        public int SpriteFrame { get; private set; }

        /// <summary>Current per-frame horizontal shift, applied to the sprite and its shadow alike.</summary>
        public int HorizontalShift { get; private set; }

        /// <summary>False once the run has ended; it then rests on frame 0 until restarted.</summary>
        public bool Active { get; private set; }

        /// <summary>Restarts from slot 0; a run that starts with -1 never plays.</summary>
        public void Start(IReadOnlyList<SpriteFrameSlot> slots)
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

        /// <summary>Returns to rest on frame 0 without playing.</summary>
        public void Stop() => Start(null);

        /// <summary>One game tick. A slot with duration N is held for N+1 ticks, as the engine counts it.</summary>
        public void Tick()
        {
            if (!Active) return;
            if (_delay != 0) { _delay--; return; }

            _index++;

            // Jump to slot (-frameNo - 2) until this slot's counter reaches its duration. The bound is ours: a
            // jump chain that points at itself would spin forever.
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

        private SpriteFrameSlot SlotAt(int index) =>
            index < _slots.Count ? _slots[index] : new SpriteFrameSlot(-1, 0, 0, 0);   // a short list behaves as trailing terminators

        /// <summary>Every frame the run can show, always including frame 0, which it ends on.</summary>
        public static IReadOnlyList<int> ReachableFrames(IReadOnlyList<SpriteFrameSlot> slots)
        {
            var seen = new List<int> { 0 };
            var player = new SpriteFramePlayer();
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
