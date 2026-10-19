using System.Collections.Generic;
using System.Linq;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// One archive an editor works through a member at a time. Added and removed members, and the member saved,
    /// wait here; Save writes them all, Discard drops them, and reads see them in the meantime.
    /// </summary>
    public sealed class StagedArchive
    {
        private readonly PendingArchiveWrites _pending = new();

        public StagedArchive(DirNames dir) { Dir = dir; }

        public DirNames Dir { get; }
        public bool IsDirty => _pending.IsDirty;
        public int Count => _pending.Count(Dir);

        /// <summary>A member as the editor sees it, or null when it is missing or removed.</summary>
        public byte[] Read(int id) => _pending.ReadMember(Dir, id);

        /// <summary>Adds a member after the last one and returns its number.</summary>
        public int Add(byte[] bytes)
        {
            int id = Count;
            _pending.Change(step => step.Put(Dir, id, bytes));
            return id;
        }

        public void Put(int id, byte[] bytes) => _pending.Change(step => step.Put(Dir, id, bytes));

        public void RemoveLast()
        {
            int last = Count - 1;
            if (last >= 0) _pending.Change(step => step.Remove(Dir, last));
        }

        /// <summary>Writes <paramref name="bytes"/> as member <paramref name="id"/> along with everything else waiting.</summary>
        public void Save(int id, byte[] bytes)
        {
            _pending.Change(step => step.Put(Dir, id, bytes));
            _pending.Save();
        }

        public void Save() => _pending.Save();
        public void Discard() => _pending.Discard();

        /// <summary>What Save would add, remove or change, such as "matrix 289 added", or "" when nothing.</summary>
        public string Describe(string singular, string plural) => Describe(singular, plural, _pending.Pending(Dir));

        public static string Describe(string singular, string plural, IEnumerable<(int Id, PendingArchiveWrites.PendingKind Kind)> pending)
        {
            List<string> parts = new();
            foreach (IGrouping<PendingArchiveWrites.PendingKind, int> kind in pending.GroupBy(p => p.Kind, p => p.Id))
            {
                List<int> ids = kind.OrderBy(i => i).ToList();
                string verb = kind.Key.ToString().ToLowerInvariant();
                parts.Add(ids.Count == 1 ? $"{singular} {ids[0]} {verb}"
                    : ids.Count <= 3 ? $"{plural} {string.Join(", ", ids.Take(ids.Count - 1))} and {ids[^1]} {verb}"
                    : $"{ids.Count} {plural} {verb}");
            }
            return string.Join(", ", parts);
        }

        /// <summary>
        /// The open record's name when it has edits (or nothing else is waiting, as after a save), then what else is
        /// waiting, capitalised for the unsaved-changes dialog.
        /// </summary>
        public static string Join(string open, bool openEdited, string staged)
        {
            if (!openEdited && !string.IsNullOrEmpty(staged)) open = null;
            string text = string.Join(", ", new[] { open, staged }.Where(t => !string.IsNullOrEmpty(t)));
            return text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);
        }
    }
}
