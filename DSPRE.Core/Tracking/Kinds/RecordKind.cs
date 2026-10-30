using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using static DSPRE.RomInfo;

namespace DSPRE.Tracking.Kinds
{
    /// <summary>
    /// One archive member read by its format class, written as that class's fields. Refs name the fields that
    /// hold another record's number, so they're written "33 Tackle".
    /// </summary>
    public sealed class RecordKind<T> : FieldKind where T : class
    {
        private readonly DirNames _dir;
        private readonly Func<byte[], int, T> _read;
        private readonly Func<T, byte[]> _write;
        private readonly Func<int, TrackingNames, string> _name;
        private readonly string _title;
        private readonly Func<TrackingNames, ReflectedFields.RefLookup> _refs;
        private readonly HashSet<string> _skip;
        private readonly Func<Type, object> _create;

        public RecordKind(string name, string folder, DirNames dir, string title, Func<byte[], int, T> read, Func<T, byte[]> write,
            Func<int, TrackingNames, string> recordName = null, Func<TrackingNames, ReflectedFields.RefLookup> refs = null,
            IEnumerable<string> skip = null, Func<Type, object> create = null)
        {
            Name = name;
            Folder = folder;
            _dir = dir;
            _title = title;
            _read = read;
            _write = write;
            _name = recordName;
            _refs = refs;
            _skip = new HashSet<string>(skip ?? Enumerable.Empty<string>(), StringComparer.Ordinal) { "ID", "Id" };
            _create = create;
        }

        public override string Name { get; }
        public override string Folder { get; }

        public override string Title(string id, ChangeContext ctx)
        {
            string name = _name?.Invoke(Number(id), ctx.Names);
            return _title + " " + id + (string.IsNullOrWhiteSpace(name) ? "" : " " + name);
        }

        protected override string SlugName(string id, ChangeContext ctx) => _name?.Invoke(Number(id), ctx.Names);

        public override bool Owns(TrackedFile file, out string id)
        {
            id = file.TryMember(out DirNames dir, out int member) && dir == _dir ? Id(member) : null;
            return id != null;
        }

        public override IReadOnlyList<TrackedFile> Files(string id, ChangeContext ctx) => new[] { TrackedFile.Member(_dir, Number(id)) };

        protected override JsonObject Decode(string id, Func<TrackedFile, byte[]> read, ChangeContext ctx)
        {
            byte[] bytes = read(TrackedFile.Member(_dir, Number(id)));
            if (bytes == null) return null;
            return ReflectedFields.Write(_read(bytes, Number(id)), _refs?.Invoke(ctx.Names), _skip);
        }

        protected override Dictionary<TrackedFile, byte[]> Encode(string id, JsonObject record, Func<TrackedFile, byte[]> start, ChangeContext ctx)
        {
            TrackedFile file = TrackedFile.Member(_dir, Number(id));
            byte[] bytes = start(file);
            if (bytes == null) throw new InvalidOperationException("A new record needs a member to build from.");
            T target = _read(bytes, Number(id));
            ReflectedFields.Read(record, target, _refs?.Invoke(ctx.Names), _skip, _create);
            return new Dictionary<TrackedFile, byte[]> { [file] = _write(target) };
        }
    }
}
