using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;

namespace DSPRE.Tracking
{
    /// <summary>
    /// One description of a record's fields that both writes the record as JSON and reads it back into the
    /// format class, so the two directions can't drift apart.
    /// </summary>
    public sealed class Fields
    {
        public JsonObject Json { get; }
        public bool Reading { get; }

        private Fields(JsonObject json, bool reading)
        {
            Json = json;
            Reading = reading;
        }

        public static JsonObject Write(Action<Fields> bind)
        {
            Fields f = new Fields(new JsonObject(), false);
            bind(f);
            return f.Json;
        }

        public static void Read(JsonObject json, Action<Fields> bind) => bind(new Fields(json, true));

        private JsonNode Get(string key) =>
            Json.TryGetPropertyValue(key, out JsonNode node) ? node : throw new InvalidDataException($"Missing \"{key}\".");

        public void Int(string key, Func<int> get, Action<int> set)
        {
            if (Reading) set(Get(key).GetValue<int>());
            else Json[key] = get();
        }

        public void Long(string key, Func<long> get, Action<long> set)
        {
            if (Reading) set(Get(key).GetValue<long>());
            else Json[key] = get();
        }

        /// <summary>
        /// A stored whole number shown divided by <paramref name="scale"/>, such as a fixed-point position, to
        /// <paramref name="decimals"/> places at most; -1 keeps every digit, for scales that are powers of two.
        /// </summary>
        public void Scaled(string key, Func<long> get, Action<long> set, double scale, int decimals)
        {
            if (Reading)
            {
                set((long)Math.Round(Get(key).GetValue<double>() * scale));
                return;
            }
            long raw = get();
            if (decimals < 0)
            {
                Json[key] = raw / scale;
                return;
            }
            // The fewest places that still read back as the same stored number, so 50 degrees stays 50.
            double shown = Math.Round(raw / scale, decimals);
            for (int d = 0; d < decimals; d++)
                if ((long)Math.Round(Math.Round(raw / scale, d) * scale) == raw)
                {
                    shown = Math.Round(raw / scale, d);
                    break;
                }
            Json[key] = shown;
        }

        public void Bool(string key, Func<bool> get, Action<bool> set)
        {
            if (Reading) set(Get(key).GetValue<bool>());
            else Json[key] = get();
        }

        public void Text(string key, Func<string> get, Action<string> set)
        {
            if (Reading) set(Get(key)?.GetValue<string>());
            else Json[key] = get();
        }

        /// <summary>A number that refers to another record, written "33 Tackle".</summary>
        public void Ref(string key, Func<int> get, Action<int> set, Func<int, string> name)
        {
            if (Reading) set(ChangesJson.ParseRef(Get(key)));
            else
            {
                int id = get();
                Json[key] = ChangesJson.Ref(id, SafeName(name, id));
            }
        }

        public void Refs(string key, Func<IReadOnlyList<int>> get, Action<int[]> set, Func<int, string> name)
        {
            if (Reading) set(((JsonArray)Get(key)).Select(ChangesJson.ParseRef).ToArray());
            else Json[key] = new JsonArray(get().Select(id => (JsonNode)ChangesJson.Ref(id, SafeName(name, id))).ToArray());
        }

        /// <summary>A value from a fixed list, written by name; a value outside the list is written as its number.</summary>
        public void Named(string key, Func<int> get, Action<int> set, IReadOnlyList<string> names)
        {
            if (Reading)
            {
                JsonNode node = Get(key);
                if (node is JsonValue v && v.TryGetValue(out string text))
                {
                    int index = IndexOf(names, text);
                    set(index >= 0 ? index : ChangesJson.ParseRef(text));
                }
                else set(node.GetValue<int>());
            }
            else
            {
                int value = get();
                Json[key] = value >= 0 && value < names.Count && !string.IsNullOrEmpty(names[value]) && IndexOf(names, names[value]) == value
                    ? names[value]
                    : (JsonNode)value;
            }
        }

        /// <summary>
        /// Bits written as the names of those set, in bit order; unnamed bits as "bit N". With <paramref name="numbered"/>,
        /// names start with their bit's number from 1, and that number is what's read back.
        /// </summary>
        public void Flags(string key, Func<long> get, Action<long> set, IReadOnlyList<string> bitNames, bool numbered = false)
        {
            if (Reading)
            {
                long value = 0;
                foreach (JsonNode n in (JsonArray)Get(key))
                {
                    string text = n.GetValue<string>();
                    int bit = IndexOf(bitNames, text);
                    if (bit < 0 && numbered && int.TryParse(text.Split(' ')[0], NumberStyles.None, CultureInfo.InvariantCulture, out int numberedBit) && numberedBit > 0) bit = numberedBit - 1;
                    if (bit < 0 && text.StartsWith("bit ", StringComparison.Ordinal)) bit = int.Parse(text.Substring(4), CultureInfo.InvariantCulture);
                    if (bit < 0) throw new InvalidDataException($"\"{text}\" isn't a flag of \"{key}\".");
                    value |= 1L << bit;
                }
                set(value);
            }
            else
            {
                long value = get();
                JsonArray names = new JsonArray();
                for (int bit = 0; bit < 64; bit++)
                    if ((value & (1L << bit)) != 0)
                        names.Add(bit < bitNames.Count && !string.IsNullOrEmpty(bitNames[bit]) ? bitNames[bit] : "bit " + bit.ToString(CultureInfo.InvariantCulture));
                Json[key] = names;
            }
        }

        public void Ints(string key, Func<IReadOnlyList<int>> get, Action<int[]> set)
        {
            if (Reading) set(((JsonArray)Get(key)).Select(n => n.GetValue<int>()).ToArray());
            else Json[key] = new JsonArray(get().Select(v => (JsonNode)v).ToArray());
        }

        public void Bytes(string key, Func<byte[]> get, Action<byte[]> set)
        {
            if (Reading) set(ChangesJson.FromHex(Get(key).GetValue<string>()));
            else Json[key] = ChangesJson.Hex(get());
        }

        public void Colour(string key, Func<ushort> get, Action<ushort> set)
        {
            if (Reading) set(ChangesJson.ParseColour(Get(key).GetValue<string>()));
            else Json[key] = ChangesJson.Colour(get());
        }

        /// <summary>A grid written as rows keyed by their number, each cell as two hex digits.</summary>
        public void Grid(string key, int width, int height, Func<int, int, byte> get, Action<int, int, byte> set)
        {
            if (Reading)
            {
                JsonObject rows = (JsonObject)Get(key);
                for (int y = 0; y < height; y++)
                {
                    byte[] row = ChangesJson.FromHex(rows[y.ToString(CultureInfo.InvariantCulture)].GetValue<string>());
                    for (int x = 0; x < width; x++) set(x, y, row[x]);
                }
            }
            else
            {
                JsonObject rows = new JsonObject();
                byte[] row = new byte[width];
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++) row[x] = get(x, y);
                    rows[y.ToString(CultureInfo.InvariantCulture)] = ChangesJson.Hex(row);
                }
                Json[key] = rows;
            }
        }

        public void Object(string key, Action<Fields> bind)
        {
            if (Reading) bind(new Fields((JsonObject)Get(key), true));
            else
            {
                Fields inner = new Fields(new JsonObject(), false);
                bind(inner);
                Json[key] = inner.Json;
            }
        }

        /// <summary>A list of records; reading resizes it to the JSON's length, keeping existing items.</summary>
        public void List<T>(string key, IList<T> items, Func<T> create, Action<Fields, T> bind)
        {
            if (Reading)
            {
                JsonArray array = (JsonArray)Get(key);
                while (items.Count > array.Count) items.RemoveAt(items.Count - 1);
                while (items.Count < array.Count) items.Add(create());
                for (int i = 0; i < array.Count; i++) bind(new Fields((JsonObject)array[i], true), items[i]);
            }
            else
            {
                JsonArray array = new JsonArray();
                foreach (T item in items)
                {
                    Fields inner = new Fields(new JsonObject(), false);
                    bind(inner, item);
                    array.Add(inner.Json);
                }
                Json[key] = array;
            }
        }

        /// <summary>A fixed number of records addressed by index, like a table's rows.</summary>
        public void Rows(string key, int count, Action<Fields, int> bind)
        {
            if (Reading)
            {
                JsonArray array = (JsonArray)Get(key);
                if (array.Count != count) throw new InvalidDataException($"\"{key}\" has {array.Count} entries, the game has {count}.");
                for (int i = 0; i < count; i++) bind(new Fields((JsonObject)array[i], true), i);
            }
            else
            {
                JsonArray array = new JsonArray();
                for (int i = 0; i < count; i++)
                {
                    Fields inner = new Fields(new JsonObject(), false);
                    bind(inner, i);
                    array.Add(inner.Json);
                }
                Json[key] = array;
            }
        }

        private static int IndexOf(IReadOnlyList<string> names, string text)
        {
            for (int i = 0; i < names.Count; i++)
                if (string.Equals(names[i], text, StringComparison.Ordinal)) return i;
            return -1;
        }

        private static string SafeName(Func<int, string> name, int id)
        {
            if (name == null) return null;
            try { return name(id); }
            catch (Exception ex) when (ex is ArgumentOutOfRangeException || ex is IndexOutOfRangeException) { return null; }
        }
    }
}
