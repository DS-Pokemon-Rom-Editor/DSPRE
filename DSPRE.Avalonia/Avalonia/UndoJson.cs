using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace DSPRE.Avalonia
{
    /// <summary>
    /// Snapshots for <see cref="ByteStateUndo"/> taken from row objects' public properties. Restoring copies the
    /// values into the rows already on screen, so bindings and selection stay with them.
    /// </summary>
    public static class UndoJson
    {
        public static byte[] Take<T>(IEnumerable<T> rows) => JsonSerializer.SerializeToUtf8Bytes(rows.ToList());

        public static byte[] Take<T>(T value) => JsonSerializer.SerializeToUtf8Bytes(value);

        public static T Read<T>(byte[] state) => JsonSerializer.Deserialize<T>(state);

        /// <summary>Copies a snapshot back into rows of the same count.</summary>
        public static void ApplyInto<T>(byte[] state, IList<T> rows)
        {
            List<T> saved = JsonSerializer.Deserialize<List<T>>(state);
            for (int i = 0; i < saved.Count && i < rows.Count; i++) CopyInto(saved[i], rows[i]);
        }

        public static void CopyInto<T>(T from, T to)
        {
            foreach (PropertyInfo p in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance))
                if (p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0 && p.GetSetMethod() != null)
                    p.SetValue(to, p.GetValue(from));
        }
    }
}
