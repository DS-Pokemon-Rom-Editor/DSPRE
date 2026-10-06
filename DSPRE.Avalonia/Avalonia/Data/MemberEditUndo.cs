using System;
using System.Collections.Generic;
using System.Linq;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// Undo for editors that import straight into archive members: each step keeps only the members one import
    /// changed, before and after. The owner calls <see cref="Touching"/> before writing a member and
    /// <see cref="Commit"/> once the import is done.
    /// </summary>
    public sealed class MemberEditUndo
    {
        private sealed class Step
        {
            public readonly Dictionary<int, byte[]> Before = new(), After = new();
        }

        private readonly Func<int, byte[]> _get;
        private readonly Action<int, byte[]> _put;
        private readonly Stack<Step> _undo = new(), _redo = new();
        private readonly Dictionary<int, byte[]> _pending = new();
        private const int Limit = 100;

        /// <param name="get">A member's current bytes.</param>
        /// <param name="put">Writes a member back; the owner keeps its own session backup up to date there.</param>
        public MemberEditUndo(Func<int, byte[]> get, Action<int, byte[]> put) { _get = get; _put = put; }

        public bool CanUndo => _undo.Count > 0;
        public bool CanRedo => _redo.Count > 0;

        /// <summary>Raised when CanUndo or CanRedo may have changed.</summary>
        public event Action Changed;

        /// <summary>Notes a member's bytes before an import writes it; only the first call per import counts.</summary>
        public void Touching(int id)
        {
            if (!_pending.ContainsKey(id)) _pending[id] = Copy(_get(id));
        }

        /// <summary>Ends an import: the members it really changed become one step.</summary>
        public void Commit()
        {
            Step step = new Step();
            foreach ((int id, byte[] before) in _pending)
            {
                byte[] after = Copy(_get(id));
                if (before.AsSpan().SequenceEqual(after)) continue;
                step.Before[id] = before;
                step.After[id] = after;
            }
            _pending.Clear();
            if (step.Before.Count == 0) return;
            _undo.Push(step);
            if (_undo.Count > Limit)
            {
                List<Step> keep = _undo.Take(Limit).Reverse().ToList();
                _undo.Clear();
                foreach (Step s in keep) _undo.Push(s);
            }
            _redo.Clear();
            Changed?.Invoke();
        }

        /// <summary>Forgets every step, e.g. after a revert puts the opened state back.</summary>
        public void Clear()
        {
            _undo.Clear(); _redo.Clear(); _pending.Clear();
            Changed?.Invoke();
        }

        public void Undo()
        {
            if (_undo.Count == 0) return;
            Step step = _undo.Pop();
            foreach ((int id, byte[] bytes) in step.Before) _put(id, Copy(bytes));
            _redo.Push(step);
            Changed?.Invoke();
        }

        public void Redo()
        {
            if (_redo.Count == 0) return;
            Step step = _redo.Pop();
            foreach ((int id, byte[] bytes) in step.After) _put(id, Copy(bytes));
            _undo.Push(step);
            Changed?.Invoke();
        }

        private static byte[] Copy(byte[] b) => b == null ? Array.Empty<byte>() : (byte[])b.Clone();
    }
}
