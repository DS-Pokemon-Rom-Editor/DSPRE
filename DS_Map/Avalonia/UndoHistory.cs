using System.Collections.Generic;

namespace DSPRE.Avalonia
{
    /// <summary>
    /// A pure undo/redo stack of immutable state snapshots (mementos). The owning editor decides what a
    /// snapshot IS (e.g. a file's <c>ToByteArray()</c>) and how to apply one; this class only sequences them,
    /// so it has no ROM or UI dependency and is fully unit-testable.
    ///
    /// Model: <c>_current</c> is the live state. <see cref="Capture"/> pushes the previous current onto the
    /// undo stack and clears the redo branch. <see cref="Undo"/>/<see cref="Redo"/> shuffle <c>_current</c>
    /// between the two stacks and return the state to apply. <see cref="IsDirty"/> is true whenever the current
    /// state is not the one last marked saved. Tracked by reference, so snapshots must be distinct instances
    /// (which byte-array snapshots naturally are).
    /// </summary>
    public sealed class UndoHistory<T> where T : class
    {
        private readonly Stack<T> _undo = new();
        private readonly Stack<T> _redo = new();
        private readonly int _limit;
        private T _current;
        private T _saved;

        public UndoHistory(int limit = 200) { _limit = limit; }

        public bool CanUndo => _undo.Count > 0;
        public bool CanRedo => _redo.Count > 0;

        /// <summary>True when the current state differs from the one last <see cref="Reset"/> or <see cref="MarkSaved"/>.</summary>
        public bool IsDirty => !ReferenceEquals(_current, _saved);

        /// <summary>Establish a fresh baseline (on load): clears history and marks this state as saved.</summary>
        public void Reset(T state)
        {
            _undo.Clear();
            _redo.Clear();
            _current = state;
            _saved = state;
        }

        /// <summary>
        /// Record a transition to <paramref name="state"/> after an edit, clearing any redo branch.
        /// When <paramref name="coalesce"/> is true the previous current is NOT pushed; the new state simply
        /// replaces it in the same undo step, so a burst of rapid edits collapses into one undoable change.
        /// </summary>
        public void Capture(T state, bool coalesce = false)
        {
            if (!coalesce && _current != null)
            {
                _undo.Push(_current);
                TrimToLimit();
            }
            _current = state;
            _redo.Clear();
        }

        /// <summary>Mark the current state as saved (after a Save); flips <see cref="IsDirty"/> off without
        /// touching history, so the user can still undo past a save.</summary>
        public void MarkSaved() => _saved = _current;

        /// <summary>Step back one state; returns the state to apply (or the current one if nothing to undo).</summary>
        public T Undo()
        {
            if (_undo.Count == 0) return _current;
            _redo.Push(_current);
            _current = _undo.Pop();
            return _current;
        }

        /// <summary>Step forward one state; returns the state to apply (or the current one if nothing to redo).</summary>
        public T Redo()
        {
            if (_redo.Count == 0) return _current;
            _undo.Push(_current);
            _current = _redo.Pop();
            return _current;
        }

        private void TrimToLimit()
        {
            if (_undo.Count <= _limit) return;
            // Drop the oldest entry (bottom of the stack). Rare for editor sessions; keeps memory bounded.
            var newest = _undo.ToArray();   // index 0 = newest
            _undo.Clear();
            for (int i = _limit - 1; i >= 0; i--) _undo.Push(newest[i]);
        }
    }
}
