using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// Archive entries an editor has changed, added or removed but not saved: edits land here, the editor reads
    /// them back from here, and only Save writes them to the project. Each import or change is one undo step.
    /// Bytes kept outside the archives, such as pieces in a battle overlay, can be staged with them as named targets.
    /// </summary>
    public sealed class PendingArchiveWrites
    {
        /// <summary>Bytes outside the archives that an import can change along with them.</summary>
        public sealed record Target(string Key, Func<byte[]> Read, Action<byte[]> Write);

        private sealed record State(Dictionary<(DirNames, int), byte[]> Members, Dictionary<string, byte[]> Others);

        // What each touched entry held on disk before its first import. An entry missing from a state stands
        // at these bytes, which is what makes undoing past a save write the old bytes back.
        private readonly ConcurrentDictionary<(DirNames, int), byte[]> _first = new();
        private readonly ConcurrentDictionary<string, (Target Target, byte[] Bytes)> _firstOthers = new();
        private State _state = new(new(), new());
        private State _saved;
        private Dictionary<(DirNames, int), byte[]> _importing;
        private Dictionary<string, byte[]> _importingOthers;
        private readonly UndoHistory<State> _history = new();
        private readonly ScriptNarc.Staging _staging;
        private bool _saving;
        private object _lastGroup;

        // Every store still in use, so a member another writer saves reaches the ones holding it.
        private static readonly List<WeakReference<PendingArchiveWrites>> Live = new();

        static PendingArchiveWrites() => ArchiveMembers.Written += OnWritten;

        /// <summary>Raised whenever the pending entries, or whether there are any, may have changed.</summary>
        public event Action Changed;

        public PendingArchiveWrites()
        {
            _saved = _state;
            _history.Reset(_state);
            _staging = new ScriptNarc.Staging { Read = Read, Write = Write };
            lock (Live)
            {
                Live.RemoveAll(w => !w.TryGetTarget(out _));
                Live.Add(new WeakReference<PendingArchiveWrites>(this));
            }
        }

        private static void OnWritten(DirNames dir, int id, byte[] before, byte[] after)
        {
            List<PendingArchiveWrites> stores = new();
            lock (Live)
                foreach (WeakReference<PendingArchiveWrites> w in Live)
                    if (w.TryGetTarget(out PendingArchiveWrites store)) stores.Add(store);
            foreach (PendingArchiveWrites store in stores) store.WrittenElsewhere((dir, id));
        }

        // A painter or another editor saved a member this store holds. Without an unsaved change to it the store lets
        // it go, so reads see the new bytes and neither Save nor Undo can put the old ones back.
        private void WrittenElsewhere((DirNames, int) key)
        {
            if (_saving || !_first.ContainsKey(key)) return;
            if (!Same(MemberIn(_state, key), MemberIn(_saved, key))) return;
            _first.TryRemove(key, out _);
            State saved = new(new(_saved.Members), new(_saved.Others));
            State now = new(new(_state.Members), new(_state.Others));
            saved.Members.Remove(key);
            now.Members.Remove(key);
            _saved = saved;
            _state = now;
            bool differs = DiffersFromSaved();
            _history.Reset(saved);
            if (differs) _history.Capture(now);
            else _state = saved;
            Changed?.Invoke();
        }

        // Stands for a member removed until Save; compared by reference, so an empty member is still a member.
        private static readonly byte[] Removed = new byte[0];

        // Steps that end where the last save stood, such as removing a member and putting it back, leave nothing unsaved.
        public bool IsDirty => _history.IsDirty && DiffersFromSaved();

        public enum PendingKind { Added, Removed, Changed }

        private byte[] MemberIn(State state, (DirNames, int) key)
            => state.Members.TryGetValue(key, out byte[] b) ? b : _first.TryGetValue(key, out byte[] f) ? f : null;

        private static bool Absent(byte[] b) => b == null || ReferenceEquals(b, Removed);

        private static bool Same(byte[] a, byte[] b)
            => Absent(a) ? Absent(b) : !Absent(b) && a.AsSpan().SequenceEqual(b);

        private bool DiffersFromSaved()
        {
            foreach ((DirNames, int) key in _state.Members.Keys.Union(_saved.Members.Keys))
                if (!Same(MemberIn(_state, key), MemberIn(_saved, key))) return true;
            foreach (string key in _state.Others.Keys.Union(_saved.Others.Keys))
            {
                byte[] first = _firstOthers.TryGetValue(key, out (Target, byte[] Bytes) f) ? f.Bytes : null;
                byte[] now = _state.Others.TryGetValue(key, out byte[] n) ? n : first;
                byte[] saved = _saved.Others.TryGetValue(key, out byte[] v) ? v : first;
                if (!Same(now, saved)) return true;
            }
            return false;
        }

        /// <summary>The members of <paramref name="dir"/> that Save would add, remove or change, lowest first.</summary>
        public List<(int Id, PendingKind Kind)> Pending(DirNames dir)
        {
            List<(int Id, PendingKind Kind)> pending = new();
            foreach ((DirNames, int) key in _state.Members.Keys.Union(_saved.Members.Keys).Where(k => k.Item1 == dir).OrderBy(k => k.Item2))
            {
                byte[] now = MemberIn(_state, key), saved = MemberIn(_saved, key);
                if (Same(now, saved)) continue;
                pending.Add((key.Item2, Absent(saved) ? PendingKind.Added : Absent(now) ? PendingKind.Removed : PendingKind.Changed));
            }
            return pending;
        }
        public bool CanUndo => _history.CanUndo;
        public bool CanRedo => _history.CanRedo;

        /// <summary>Reads on this thread see the pending entries until the result is disposed.</summary>
        public IDisposable Reading() => ScriptNarc.Use(_staging);

        /// <summary>The pending bytes for an entry, or null when it has none. Safe from any thread.</summary>
        public byte[] Read(DirNames dir, int id)
        {
            Dictionary<(DirNames, int), byte[]> importing = _importing;
            if (importing != null && importing.TryGetValue((dir, id), out byte[] fresh)) return ReferenceEquals(fresh, Removed) ? null : fresh;
            if (_state.Members.TryGetValue((dir, id), out byte[] now)) return ReferenceEquals(now, Removed) ? null : now;
            return _first.TryGetValue((dir, id), out byte[] first) ? first : null;
        }

        /// <summary>A member as the editor sees it: its pending bytes, else the project's, or null when it is removed or missing.</summary>
        public byte[] ReadMember(DirNames dir, int id)
        {
            if (_state.Members.TryGetValue((dir, id), out byte[] now)) return ReferenceEquals(now, Removed) ? null : (byte[])now.Clone();
            string path = ArchiveMembers.PathOf(dir, id);
            return File.Exists(path) ? File.ReadAllBytes(path) : null;
        }

        /// <summary>How many members the archive has with the pending additions and removals applied.</summary>
        public int Count(DirNames dir)
        {
            string folder = GameDirs[dir].unpackedDir;
            int top = NarcAPI.Narc.MemberCount(folder) - 1;
            foreach (KeyValuePair<(DirNames, int), byte[]> kv in _state.Members)
                if (kv.Key.Item1 == dir && !ReferenceEquals(kv.Value, Removed) && kv.Key.Item2 > top) top = kv.Key.Item2;
            while (top >= 0 && _state.Members.TryGetValue((dir, top), out byte[] b) && ReferenceEquals(b, Removed)) top--;
            return top + 1;
        }

        /// <summary>
        /// The pending members as bytes, for an editor whose own undo history covers them: it keeps these in each
        /// state and hands them back to <see cref="Restore"/>. Equal pending members give equal bytes.
        /// </summary>
        public byte[] Take() => ByteStateUndo.Pack(w =>
        {
            List<KeyValuePair<(DirNames, int), byte[]>> members = _state.Members.OrderBy(kv => kv.Key.Item1).ThenBy(kv => kv.Key.Item2).ToList();
            w.Write(members.Count);
            foreach (KeyValuePair<(DirNames, int), byte[]> kv in members)
            {
                w.Write((int)kv.Key.Item1);
                w.Write(kv.Key.Item2);
                bool removed = ReferenceEquals(kv.Value, Removed);
                w.Write(removed);
                if (removed) continue;
                w.Write(kv.Value.Length);
                w.Write(kv.Value);
            }
        });

        /// <summary>Puts back pending members taken by <see cref="Take"/>.</summary>
        public void Restore(byte[] taken)
        {
            Dictionary<(DirNames, int), byte[]> members = new();
            ByteStateUndo.Unpack(taken, r =>
            {
                for (int n = r.ReadInt32(); n > 0; n--)
                {
                    (DirNames, int) key = ((DirNames)r.ReadInt32(), r.ReadInt32());
                    members[key] = r.ReadBoolean() ? Removed : r.ReadBytes(r.ReadInt32());
                    if (!_first.ContainsKey(key))
                    {
                        string path = ArchiveMembers.PathOf(key.Item1, key.Item2);
                        _first[key] = File.Exists(path) ? File.ReadAllBytes(path) : null;
                    }
                }
            });
            _state = new State(members, new(_state.Others));
            Changed?.Invoke();
        }

        /// <summary>Whether a member has a pending put or removal.</summary>
        public bool Changes(DirNames dir, int id) => _state.Members.ContainsKey((dir, id));

        /// <summary>The members one change puts or removes.</summary>
        public sealed class Step
        {
            internal readonly Dictionary<(DirNames, int), byte[]> Members = new();
            public Step Put(DirNames dir, int id, byte[] bytes) { Members[(dir, id)] = (byte[])(bytes ?? throw new ArgumentNullException(nameof(bytes))).Clone(); return this; }
            public Step Remove(DirNames dir, int id) { Members[(dir, id)] = Removed; return this; }
        }

        /// <summary>Makes <paramref name="make"/>'s puts and removals one pending step.</summary>
        /// <param name="group">Changes made with the same group, one after another, are one undo step.</param>
        public void Change(Action<Step> make, object group = null)
        {
            Step step = new Step();
            make(step);
            if (step.Members.Count == 0) return;
            State next = new(new(_state.Members), new(_state.Others));
            foreach (KeyValuePair<(DirNames, int), byte[]> kv in step.Members)
            {
                if (!_first.ContainsKey(kv.Key))
                {
                    string path = ArchiveMembers.PathOf(kv.Key.Item1, kv.Key.Item2);
                    _first[kv.Key] = File.Exists(path) ? File.ReadAllBytes(path) : null;
                }
                next.Members[kv.Key] = kv.Value;
            }
            _state = next;
            _history.Capture(next, group != null && ReferenceEquals(group, _lastGroup) && _history.CanUndo);
            _lastGroup = group;
            Changed?.Invoke();
        }

        /// <summary>The pending bytes for a named target, or what it holds now when nothing is pending.</summary>
        public byte[] Read(Target target)
        {
            Dictionary<string, byte[]> importing = _importingOthers;
            if (importing != null && importing.TryGetValue(target.Key, out byte[] fresh)) return fresh;
            if (_state.Others.TryGetValue(target.Key, out byte[] now)) return now;
            return _firstOthers.TryGetValue(target.Key, out (Target, byte[] Bytes) first) ? first.Bytes : target.Read();
        }

        private void Write(DirNames dir, int id, byte[] bytes)
        {
            if (_importing == null) return;
            if (!_first.ContainsKey((dir, id))) _first[(dir, id)] = new ScriptNarc(dir).GetFromDisk(id);
            _importing[(dir, id)] = bytes;
        }

        /// <summary>Stages bytes for a named target during an import; only valid inside <see cref="Import"/>.</summary>
        public void Stage(Target target, byte[] bytes)
        {
            if (_importingOthers == null) throw new InvalidOperationException("Staging happens inside an import.");
            _firstOthers.TryAdd(target.Key, (target, target.Read()));
            _importingOthers[target.Key] = bytes;
        }

        /// <summary>
        /// Runs an import whose archive writes become one pending step. <paramref name="import"/> returns its
        /// error, or null when it worked; a failed import leaves nothing behind.
        /// </summary>
        public string Import(Func<string> import)
        {
            _importing = new Dictionary<(DirNames, int), byte[]>();
            _importingOthers = new Dictionary<string, byte[]>();
            string error;
            try
            {
                using (ScriptNarc.Use(_staging)) error = import();
            }
            catch { _importing = null; _importingOthers = null; throw; }
            Dictionary<(DirNames, int), byte[]> written = _importing;
            Dictionary<string, byte[]> others = _importingOthers;
            _importing = null;
            _importingOthers = null;
            if (error != null || written.Count + others.Count == 0) return error;

            State next = new(new(_state.Members), new(_state.Others));
            foreach (KeyValuePair<(DirNames, int), byte[]> kv in written) next.Members[kv.Key] = kv.Value;
            foreach (KeyValuePair<string, byte[]> kv in others) next.Others[kv.Key] = kv.Value;
            _state = next;
            _history.Capture(next);
            _lastGroup = null;
            Changed?.Invoke();
            return null;
        }

        public void Undo() { if (!_history.CanUndo) return; _state = _history.Undo(); _lastGroup = null; Changed?.Invoke(); }
        public void Redo() { if (!_history.CanRedo) return; _state = _history.Redo(); _lastGroup = null; Changed?.Invoke(); }

        /// <summary>
        /// Writes every pending entry that differs from the project: puts lowest first, so an archive grows without
        /// gaps, then removals highest first, so it shrinks from the end.
        /// </summary>
        public void Save()
        {
            _saving = true;
            try { SaveMembers(); }
            finally { _saving = false; }
            _saved = _state;
            _history.MarkSaved();
            Changed?.Invoke();
        }

        private void SaveMembers()
        {
            List<((DirNames Dir, int Id) Key, byte[] Bytes)> wanted = _first.Keys
                .Select(k => (k, _state.Members.TryGetValue(k, out byte[] now) ? now : _first[k])).ToList();
            foreach (((DirNames dir, int id), byte[] bytes) in wanted.Where(w => w.Bytes != null && !ReferenceEquals(w.Bytes, Removed))
                         .OrderBy(w => w.Key.Dir).ThenBy(w => w.Key.Id))
            {
                string path = ArchiveMembers.PathOf(dir, id);
                byte[] onDisk = File.Exists(path) ? File.ReadAllBytes(path) : null;
                if (onDisk == null || !onDisk.AsSpan().SequenceEqual(bytes)) ArchiveMembers.Put(dir, id, bytes);
            }
            // A member that was not there before and is not wanted now (added then undone) goes too.
            foreach (((DirNames dir, int id), byte[] _) in wanted.Where(w => w.Bytes == null || ReferenceEquals(w.Bytes, Removed))
                         .OrderBy(w => w.Key.Dir).ThenByDescending(w => w.Key.Id))
                ArchiveMembers.Delete(dir, id);
            foreach (KeyValuePair<string, (Target Target, byte[] Bytes)> kv in _firstOthers)
            {
                byte[] bytes = _state.Others.TryGetValue(kv.Key, out byte[] now) ? now : kv.Value.Bytes;
                if (bytes == null) continue;
                byte[] onDisk = kv.Value.Target.Read();
                if (onDisk == null || !onDisk.AsSpan().SequenceEqual(bytes)) kv.Value.Target.Write(bytes);
            }
        }

        /// <summary>Drops everything not saved.</summary>
        public void Discard()
        {
            _state = _saved;
            _history.Reset(_state);
            _lastGroup = null;
            Changed?.Invoke();
        }
    }
}
