using System;
using System.Collections.Generic;
using System.Linq;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// Undo, redo and dirty state for editors whose imports and painting write unpacked members at once:
    /// <see cref="Remember"/> before each write, Accept on Save, Revert on Discard.
    /// </summary>
    public sealed class ArchiveEditSession
    {
        private sealed record Step(DirNames Dir, int Member, byte[] Bytes, string What);

        private readonly Stack<Step> _undo = new(), _redo = new();
        private readonly Dictionary<(DirNames Dir, int Member), byte[]> _originals = new();

        /// <summary>Raised when the steps or the unsaved state may have changed.</summary>
        public event Action Changed;

        public bool HasChanges => _originals.Count > 0;
        public bool CanUndo => _undo.Count > 0;
        public bool CanRedo => _redo.Count > 0;
        public string UndoWhat => _undo.Count == 0 ? "Nothing to undo" : "Undo " + _undo.Peek().What;
        public string RedoWhat => _redo.Count == 0 ? "Nothing to redo" : "Redo " + _redo.Peek().What;

        /// <summary>Keeps a member's bytes before a write, including one made by another window.</summary>
        public void Remember(DirNames dir, int member, string what)
        {
            if (member < 0) return;
            byte[] before;
            try { before = new ScriptNarc(dir).Get(member); }
            catch (Exception ex) { AppLogger.Error("Archive edit remember: " + ex.Message); return; }
            if (before == null) return;
            _originals.TryAdd((dir, member), (byte[])before.Clone());
            _undo.Push(new Step(dir, member, before, what));
            _redo.Clear();
            Changed?.Invoke();
        }

        /// <summary>Drops the last remembered step when its member was never written, so nothing shows as changed.</summary>
        public void DropLastStepIfUnchanged()
        {
            if (_undo.Count == 0) return;
            Step step = _undo.Peek();
            byte[] now;
            try { now = new ScriptNarc(step.Dir).Get(step.Member); }
            catch (Exception ex) { AppLogger.Error("Archive edit drop: " + ex.Message); return; }
            if (now == null || !now.AsSpan().SequenceEqual(step.Bytes)) return;
            _undo.Pop();
            if (!_undo.Any(s => s.Dir == step.Dir && s.Member == step.Member)) _originals.Remove((step.Dir, step.Member));
            Changed?.Invoke();
        }

        /// <summary>Puts the last change back; returns what was put back, or null.</summary>
        public string Undo() => StepAcross(_undo, _redo);

        /// <summary>Does the last undone change again; returns what was redone, or null.</summary>
        public string Redo() => StepAcross(_redo, _undo);

        private string StepAcross(Stack<Step> from, Stack<Step> to)
        {
            if (from.Count == 0) return null;
            Step step = from.Pop();
            try
            {
                ScriptNarc narc = new ScriptNarc(step.Dir);
                byte[] now = narc.Get(step.Member);
                (DirNames, int) key = (step.Dir, step.Member);
                if (now != null) _originals.TryAdd(key, (byte[])now.Clone());
                narc.Put(step.Member, step.Bytes);
                // A member stepped back to its saved bytes no longer counts as changed.
                if (_originals.TryGetValue(key, out byte[] saved) && saved.AsSpan().SequenceEqual(step.Bytes)) _originals.Remove(key);
                if (now != null) to.Push(new Step(step.Dir, step.Member, now, step.What));
            }
            catch (Exception ex) { AppLogger.Error("Archive edit step: " + ex.Message); }
            Changed?.Invoke();
            return step.What;
        }

        /// <summary>After a save: the members as they are now become what Discard goes back to.</summary>
        public void Accept()
        {
            _originals.Clear();
            Changed?.Invoke();
        }

        /// <summary>Puts every changed member back to its opened bytes and forgets the steps.</summary>
        public void Revert()
        {
            foreach (((DirNames dir, int member), byte[] bytes) in _originals)
            {
                try { new ScriptNarc(dir).Put(member, bytes); }
                catch (Exception ex) { AppLogger.Error("Archive edit revert: " + ex.Message); }
            }
            _originals.Clear();
            _undo.Clear();
            _redo.Clear();
            Changed?.Invoke();
        }
    }
}
