using System;
using System.Collections.Generic;
using System.Linq;

namespace DSPRE.HgEngine
{
    /// <summary>One source field of an editor's record: where it sits in the entry, how it maps onto the
    /// editor's model and how a changed value is spelled. Loading and saving walk the same list, so the
    /// fields an editor shows are exactly the fields it writes.</summary>
    public sealed class HgEngineSourceField<T>
    {
        public IReadOnlyList<FieldPathSegment> Path { get; init; }
        public Func<T, int> Get { get; init; }
        public Action<T, int> Set { get; init; }
        /// <summary>What the model can hold; a source value outside it is refused rather than truncated.</summary>
        public int Min { get; init; } = int.MinValue;
        public int Max { get; init; } = int.MaxValue;
        public bool IsFlags { get; init; }
        /// <summary>Source text for a changed value. Null writes the number.</summary>
        public Func<int, string> Format { get; init; }
        /// <summary>Name family a changed value is spelled in when there is no Format; null writes the number.</summary>
        public string Prefix { get; init; }
        /// <summary>Headers this field's names resolve against when it has its own.</summary>
        public IReadOnlyList<string> Headers { get; init; } = Array.Empty<string>();
        /// <summary>Written for 0, where a reverse lookup could pick a range marker instead.</summary>
        public string ZeroName { get; init; }

        public string Name => string.Concat(Path.Select(p => p.ToString()));
    }

    public static class HgEngineSourceFields
    {
        // TRUE/FALSE and the config switches data files test are valid in every field.
        private static readonly string[] CommonHeaders = { "include/types.h", "include/config.h" };

        /// <summary>Resolves a name against the linked checkout's headers.</summary>
        public static Func<string, int?> NameLookup(IReadOnlyList<string> headers) => name =>
        {
            foreach (string header in (headers ?? Array.Empty<string>()).Concat(CommonHeaders))
                if (HgEngineSourceBlock.TryResolveToken(name, header, out int value)) return value;
            return null;
        };

        /// <summary>Fills <paramref name="target"/> from the entry. Every value is resolved first, so a refused
        /// entry changes nothing. A field the entry doesn't declare is skipped; the save reports it.</summary>
        public static bool TryRead<T>(HgEngineSourceBlock entry, IEnumerable<HgEngineSourceField<T>> fields, T target,
            Func<string, int?> lookup, out string error)
            => ReadAll(entry, fields, target, _ => lookup, out error);

        /// <summary>Resolves names against the headers the loader gives.</summary>
        public static Func<string, int?> NameLookup(IReadOnlyList<string> headers, Func<string, HgEngineSymbolTable> loadTable) => name =>
        {
            foreach (string header in (headers ?? Array.Empty<string>()).Concat(CommonHeaders))
                if (loadTable(header)?.TryGetValue(name, out int value) == true) return value;
            return null;
        };

        /// <summary>As <see cref="TryRead{T}(HgEngineSourceBlock, IEnumerable{HgEngineSourceField{T}}, T, Func{string, int?}, out string)"/>,
        /// with each field resolving against its own headers, so a name from another family is refused.</summary>
        public static bool TryRead<T>(HgEngineSourceBlock entry, IEnumerable<HgEngineSourceField<T>> fields, T target,
            Func<string, HgEngineSymbolTable> loadTable, out string error)
            => ReadAll(entry, fields, target, f => NameLookup(f.Headers, loadTable), out error);

        private static bool ReadAll<T>(HgEngineSourceBlock entry, IEnumerable<HgEngineSourceField<T>> fields, T target,
            Func<HgEngineSourceField<T>, Func<string, int?>> lookupFor, out string error)
        {
            error = null;
            var values = new List<(HgEngineSourceField<T> Field, int Value)>();
            foreach (var field in fields)
            {
                if (!entry.TryGetRaw(field.Path, out string raw)) continue;
                if (!HgEngineSourceExpression.TryEvaluate(raw, lookupFor(field), out int value))
                {
                    error = $"{field.Name} = {raw} could not be read.";
                    return false;
                }
                if (value < field.Min || value > field.Max)
                {
                    error = $"{field.Name} = {raw} is out of range.";
                    return false;
                }
                values.Add((field, value));
            }
            foreach (var (field, value) in values) field.Set(target, value);
            return true;
        }

        /// <summary>The model's current values as spelling-preserving writes.</summary>
        public static List<HgEngineValueSpelling.Write> Writes<T>(IEnumerable<HgEngineSourceField<T>> fields, T source, string[] headers) =>
            fields.Select(f =>
            {
                int value = f.Get(source);
                return new HgEngineValueSpelling.Write
                {
                    Path = f.Path,
                    Value = value,
                    Literal = f.Format?.Invoke(value) ?? value.ToString(),
                    IsFlags = f.IsFlags,
                    Headers = headers ?? Array.Empty<string>(),
                };
            }).ToList();

        /// <summary>As <see cref="Writes{T}(IEnumerable{HgEngineSourceField{T}}, T, string[])"/>, with each field
        /// spelled by <see cref="Spell"/> and resolving against its own headers.</summary>
        public static List<HgEngineValueSpelling.Write> Writes<T>(IEnumerable<HgEngineSourceField<T>> fields, T source, Func<string, HgEngineSymbolTable> loadTable) =>
            fields.Select(f =>
            {
                int value = f.Get(source);
                return new HgEngineValueSpelling.Write
                {
                    Path = f.Path,
                    Value = value,
                    Literal = Spell(f, value, loadTable),
                    IsFlags = f.IsFlags,
                    Headers = f.Headers.ToArray(),
                };
            }).ToList();

        /// <summary>Source text for a changed value: Format, else the field's zero name or prefixed name, else the number.</summary>
        public static string Spell<T>(HgEngineSourceField<T> field, int value, Func<string, HgEngineSymbolTable> loadTable)
        {
            if (field.Format != null) return field.Format(value);
            if (field.Prefix == null) return value.ToString();
            if (value == 0 && field.ZeroName != null) return field.ZeroName;
            foreach (string header in field.Headers)
                if (loadTable(header)?.TryGetNameWithPrefix(value, field.Prefix, out string name) == true) return name;
            return value.ToString();
        }

        /// <summary>A symbolic name for a value, or the number when the header has none.</summary>
        public static string Symbol(string header, string prefix, int value) =>
            HgEngineSymbolTable.Load(header)?.TryGetNameWithPrefix(value, prefix, out string name) == true ? name : value.ToString();

        public static string FlagsSymbol(string header, string prefix, int value) =>
            HgEngineSymbolTable.Load(header)?.TryGetFlagsExpression(value, prefix, out string name) == true ? name : value.ToString();

        public static FieldPathSegment[] PathOf(params object[] segments) =>
            segments.Select(s => s is int i ? FieldPathSegment.At(i) : FieldPathSegment.Field((string)s)).ToArray();
    }
}
