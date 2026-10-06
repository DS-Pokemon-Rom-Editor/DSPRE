using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// Archive entries an editor has changed but not saved: imports land here, the editor reads them back
    /// from here, and only Save writes them to the project. Each import is one undo step.
    /// </summary>
    public sealed class PendingArchiveWrites
    {
        // What each touched entry held on disk before its first import. An entry missing from a state stands
        // at these bytes, which is what makes undoing past a save write the old bytes back.
        private readonly ConcurrentDictionary<(DirNames, int), byte[]> _first = new();
        private Dictionary<(DirNames, int), byte[]> _state = new();
        private Dictionary<(DirNames, int), byte[]> _saved;
        private Dictionary<(DirNames, int), byte[]> _importing;
        private readonly UndoHistory<Dictionary<(DirNames, int), byte[]>> _history = new();
        private readonly ScriptNarc.Staging _staging;

        /// <summary>Raised whenever the pending entries, or whether there are any, may have changed.</summary>
        public event Action Changed;

        public PendingArchiveWrites()
        {
            _saved = _state;
            _history.Reset(_state);
            _staging = new ScriptNarc.Staging { Read = Read, Write = Write };
        }

        public bool IsDirty => _history.IsDirty;
        public bool CanUndo => _history.CanUndo;
        public bool CanRedo => _history.CanRedo;

        /// <summary>Reads on this thread see the pending entries until the result is disposed.</summary>
        public IDisposable Reading() => ScriptNarc.Use(_staging);

        /// <summary>The pending bytes for an entry, or null when it has none. Safe from any thread.</summary>
        public byte[] Read(DirNames dir, int id)
        {
            Dictionary<(DirNames, int), byte[]> importing = _importing;
            if (importing != null && importing.TryGetValue((dir, id), out byte[] fresh)) return fresh;
            if (_state.TryGetValue((dir, id), out byte[] now)) return now;
            return _first.TryGetValue((dir, id), out byte[] first) ? first : null;
        }

        private void Write(DirNames dir, int id, byte[] bytes)
        {
            if (_importing == null) return;
            if (!_first.ContainsKey((dir, id))) _first[(dir, id)] = new ScriptNarc(dir).GetFromDisk(id);
            _importing[(dir, id)] = bytes;
        }

        /// <summary>
        /// Runs an import whose archive writes become one pending step. <paramref name="import"/> returns its
        /// error, or null when it worked; a failed import leaves nothing behind.
        /// </summary>
        public string Import(Func<string> import)
        {
            _importing = new Dictionary<(DirNames, int), byte[]>();
            string error;
            try
            {
                using (ScriptNarc.Use(_staging)) error = import();
            }
            catch { _importing = null; throw; }
            Dictionary<(DirNames, int), byte[]> written = _importing;
            _importing = null;
            if (error != null || written.Count == 0) return error;

            Dictionary<(DirNames, int), byte[]> next = new Dictionary<(DirNames, int), byte[]>(_state);
            foreach (KeyValuePair<(DirNames, int), byte[]> kv in written) next[kv.Key] = kv.Value;
            _state = next;
            _history.Capture(next);
            Changed?.Invoke();
            return null;
        }

        public void Undo() { if (!_history.CanUndo) return; _state = _history.Undo(); Changed?.Invoke(); }
        public void Redo() { if (!_history.CanRedo) return; _state = _history.Redo(); Changed?.Invoke(); }

        /// <summary>Writes every pending entry that differs from the project.</summary>
        public void Save()
        {
            foreach (KeyValuePair<(DirNames, int), byte[]> kv in _first)
            {
                byte[] bytes = _state.TryGetValue(kv.Key, out byte[] now) ? now : kv.Value;
                if (bytes == null) continue;
                ScriptNarc narc = new ScriptNarc(kv.Key.Item1);
                byte[] onDisk = narc.GetFromDisk(kv.Key.Item2);
                if (onDisk == null || !onDisk.AsSpan().SequenceEqual(bytes)) narc.Put(kv.Key.Item2, bytes);
            }
            _saved = _state;
            _history.MarkSaved();
            Changed?.Invoke();
        }

        /// <summary>Drops everything not saved.</summary>
        public void Discard()
        {
            _state = _saved;
            _history.Reset(_state);
            Changed?.Invoke();
        }
    }
}
