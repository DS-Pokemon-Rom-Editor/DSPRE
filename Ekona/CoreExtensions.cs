using System;
using System.Collections.Generic;
using System.Globalization;

namespace DSPRE {
    /// <summary>
    /// UI-free extension methods used throughout the ROM core (scripts, text, data files).
    /// Moved here from the WinForms-coupled <c>Extensions</c> class so the core stays
    /// cross-platform; call sites are unchanged (same namespace, extension resolution is
    /// class-name-agnostic).
    /// </summary>
    public static class CoreExtensions {
        public static int IndexOfFirstNumber(this string str) {
            return str.IndexOfAny("0123456789".ToCharArray());
        }
        public static bool ContainsNumber(this string str) {
            return str.IndexOfFirstNumber() > 0;
        }
        public static T[] SubArray<T>(this T[] array, int offset, int length) {
            T[] result = new T[length];
            Array.Copy(array, offset, result, 0, length);
            return result;
        }
        public static void Move<T>(this IList<T> l, int currentIndex, int newIndex) {
            T item = l[currentIndex];
            l.RemoveAt(currentIndex);
            l.Insert(newIndex, item);
        }
        public static Dictionary<string, ushort> Reverse (this Dictionary<ushort, string> source) {
            var dictionary = new Dictionary<string, ushort>(StringComparer.InvariantCultureIgnoreCase);
            foreach (var entry in source) {
                string newKey = entry.Value;
                if (!dictionary.ContainsKey(newKey)) {
                    dictionary.Add(newKey, entry.Key);
                }
            }
            return dictionary;
        }
        public static byte[] ToByteArrayChooseSize(this int num, byte size) {
            switch (size) {
                case 1:
                    return new byte[] { checked((byte)num) };
                case 2:
                    return BitConverter.GetBytes(checked((ushort)num));
                case 4:
                    return BitConverter.GetBytes(num);
                default:
                    AppMessages.Error("Invalid size for number conversion!", "Error!");
                    throw new InvalidOperationException();
            }
        }
        public static string PurgeSpecial(this string str, char[] special) {
            foreach (char c in special) {
                int pos = str.IndexOf(c);
                if (pos >= 0) {
                    return str.Substring(pos + 1);
                }
            }
            return str;
        }
        public static NumberStyles GetNumberStyle(this string s) {
            int posOfPrefix = s.IndexOf("0x", StringComparison.InvariantCultureIgnoreCase);
            if (posOfPrefix >= 0) {
                foreach (char c in s.Substring(posOfPrefix + 2)) {
                    if (!char.IsDigit(c) && char.ToUpper(c) > 'F') {
                        return NumberStyles.None;
                    }
                }
                return NumberStyles.HexNumber;
            } else {
                foreach (char c in s) {
                    if (!char.IsDigit(c)) {
                        return NumberStyles.None;
                    }
                }
                return NumberStyles.Integer;
            }
        }
        public static bool IgnoreCaseEquals(this string str, string other) {
            return str.Equals(other, StringComparison.InvariantCultureIgnoreCase);
        }

        /// <summary>Levenshtein edit distance (used for closest-match suggestions in the script editor).</summary>
        public static int Levenshtein(string s1, string s2)
        {
            s1 ??= "";
            s2 ??= "";
            int[,] d = new int[s1.Length + 1, s2.Length + 1];
            for (int i = 0; i <= s1.Length; i++) d[i, 0] = i;
            for (int j = 0; j <= s2.Length; j++) d[0, j] = j;
            for (int i = 1; i <= s1.Length; i++)
                for (int j = 1; j <= s2.Length; j++)
                {
                    int cost = s2[j - 1] == s1[i - 1] ? 0 : 1;
                    d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
                }
            return d[s1.Length, s2.Length];
        }
    }
}
