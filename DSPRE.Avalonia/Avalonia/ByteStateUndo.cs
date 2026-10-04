using System;
using System.Linq;

namespace DSPRE.Avalonia
{
    /// <summary>
    /// Undo for editors whose whole state serializes to bytes. <see cref="Record"/> after each edit captures the new
    /// state, folding quick bursts (typing a number) into one step and ignoring calls that changed nothing.
    /// </summary>
    public sealed class ByteStateUndo
    {
        private readonly UndoHistory<byte[]> _history = new();
        private readonly Func<byte[]> _take;
        private readonly Action<byte[]> _apply;
        private readonly Action _raise;
        private byte[] _last;
        private DateTime _lastCapture = DateTime.MinValue;
        private bool _applying;
        private const int CoalesceMs = 500;

        /// <param name="raise">Called whenever CanUndo or CanRedo may have changed.</param>
        public ByteStateUndo(Func<byte[]> take, Action<byte[]> apply, Action raise)
        {
            _take = take; _apply = apply; _raise = raise;
            _last = _take();
            _history.Reset(_last);
        }

        /// <summary>State that isn't already bytes, written field by field.</summary>
        public static byte[] Pack(Action<System.IO.BinaryWriter> write)
        {
            using var ms = new System.IO.MemoryStream();
            using (var w = new System.IO.BinaryWriter(ms)) write(w);
            return ms.ToArray();
        }

        public static void Unpack(byte[] state, Action<System.IO.BinaryReader> read)
        {
            using var r = new System.IO.BinaryReader(new System.IO.MemoryStream(state));
            read(r);
        }

        public bool CanUndo => _history.CanUndo;
        public bool CanRedo => _history.CanRedo;

        /// <summary>False when undo or redo has returned to the state last saved.</summary>
        public bool IsDirty => _history.IsDirty;
        public void MarkSaved() => _history.MarkSaved();

        public void Record()
        {
            if (_applying) return;
            byte[] now = _take();
            if (now.AsSpan().SequenceEqual(_last)) return;
            bool coalesce = (DateTime.UtcNow - _lastCapture).TotalMilliseconds < CoalesceMs;
            _history.Capture(now, coalesce);
            _last = now;
            _lastCapture = DateTime.UtcNow;
            _raise();
        }

        public void Undo() { if (CanUndo) Apply(_history.Undo()); }
        public void Redo() { if (CanRedo) Apply(_history.Redo()); }

        private void Apply(byte[] state)
        {
            _applying = true;
            try { _apply((byte[])state.Clone()); }
            finally { _applying = false; }
            _last = state;
            _lastCapture = DateTime.MinValue;   // the next edit starts a new step
            _raise();
        }
    }
}
