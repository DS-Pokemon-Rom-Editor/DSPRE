using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace DSPRE.Tracking
{
    /// <summary>Changes between two texts as hunks. Each hunk has a start in the old text, the lines removed and the lines added.</summary>
    public static class LineDiff
    {
        private const int Fuzz = 200;

        public static JsonArray Hunks(IReadOnlyList<string> from, IReadOnlyList<string> to)
        {
            JsonArray hunks = new JsonArray();
            int i = 0, j = 0;
            foreach ((int a, int b) in Matches(from, to).Append((from.Count, to.Count)))
            {
                if (a > i || b > j)
                {
                    hunks.Add(new JsonObject
                    {
                        ["at"] = i + 1,
                        [ObjectDiff.Remove] = new JsonArray(from.Skip(i).Take(a - i).Select(l => (JsonNode)l).ToArray()),
                        [ObjectDiff.Add] = new JsonArray(to.Skip(j).Take(b - j).Select(l => (JsonNode)l).ToArray()),
                    });
                }
                i = a + 1;
                j = b + 1;
            }
            return hunks;
        }

        /// <summary>
        /// Applies hunks to <paramref name="lines"/>. A hunk whose removed lines aren't at its line is looked for
        /// nearby; where they aren't found, <paramref name="resolve"/> decides whether to replace the lines there anyway.
        /// </summary>
        /// <param name="key">What two lines are compared by.</param>
        public static List<string> Apply(IReadOnlyList<string> lines, JsonArray hunks, string record, Func<Conflict, bool> resolve, Func<string, string> key = null)
        {
            List<string> result = lines.ToList();
            int shift = 0;
            foreach (JsonObject hunk in hunks.Cast<JsonObject>())
            {
                int at = hunk["at"].GetValue<int>() - 1;
                string[] remove = ((JsonArray)hunk[ObjectDiff.Remove]).Select(n => n.GetValue<string>()).ToArray();
                string[] add = ((JsonArray)hunk[ObjectDiff.Add]).Select(n => n.GetValue<string>()).ToArray();
                Func<string, string> k = key ?? (l => l);
                int near = Math.Clamp(at + shift, 0, result.Count);
                int found = Find(result, remove, near, k);
                // The target already has this hunk, as when a patch is applied twice.
                int applied = add.Length == 0 || found == near && remove.Length > 0 ? -1 : Find(result, add, near, k);
                if (applied >= 0 && (found < 0 || Math.Abs(applied - near) <= Math.Abs(found - near)))
                {
                    shift = applied - at;
                    continue;
                }
                if (found < 0)
                {
                    int place = Math.Clamp(at + shift, 0, result.Count);
                    JsonArray here = new JsonArray(result.Skip(place).Take(remove.Length).Select(l => (JsonNode)l).ToArray());
                    if (!resolve(new Conflict("line " + (at + 1), (JsonArray)hunk[ObjectDiff.Remove].DeepClone(), (JsonArray)hunk[ObjectDiff.Add].DeepClone(), here) { Record = record }))
                        continue;
                    found = place;
                }
                int count = Math.Min(remove.Length, result.Count - found);
                result.RemoveRange(found, count);
                result.InsertRange(found, add);
                shift = found - at + add.Length - remove.Length;
            }
            return result;
        }

        // A block found away from its place is used only when it's the one match nearby; a common line, like one move in a
        // long list, would otherwise land on whichever copy is closest.
        private static int Find(List<string> lines, string[] block, int near, Func<string, string> key)
        {
            if (block.Length == 0) return near <= lines.Count ? near : -1;
            bool At(int start)
            {
                if (start < 0 || start + block.Length > lines.Count) return false;
                for (int k = 0; k < block.Length; k++)
                    if (key(lines[start + k]) != key(block[k])) return false;
                return true;
            }
            if (At(near)) return near;
            int found = -1;
            for (int start = near - Fuzz; start <= near + Fuzz; start++)
            {
                if (!At(start)) continue;
                if (found >= 0) return -1;
                found = start;
            }
            return found;
        }

        /// <summary>Pairs of equal lines, in order, from a shortest edit script (Myers).</summary>
        private static List<(int, int)> Matches(IReadOnlyList<string> a, IReadOnlyList<string> b)
        {
            int n = a.Count, m = b.Count, max = n + m;
            int offset = max + 1;
            int[] v = new int[2 * max + 3];
            List<int[]> trace = new List<int[]>();
            for (int d = 0; d <= max; d++)
            {
                trace.Add((int[])v.Clone());
                for (int k = -d; k <= d; k += 2)
                {
                    int x = k == -d || k != d && v[offset + k - 1] < v[offset + k + 1] ? v[offset + k + 1] : v[offset + k - 1] + 1;
                    int y = x - k;
                    while (x < n && y < m && a[x] == b[y]) { x++; y++; }
                    v[offset + k] = x;
                    if (x >= n && y >= m) return Backtrack(trace, a, b, offset, d);
                }
            }
            return new List<(int, int)>();
        }

        private static List<(int, int)> Backtrack(List<int[]> trace, IReadOnlyList<string> a, IReadOnlyList<string> b, int offset, int last)
        {
            List<(int, int)> matches = new List<(int, int)>();
            int x = a.Count, y = b.Count;
            for (int d = last; d >= 0; d--)
            {
                int[] v = trace[d];
                int k = x - y;
                int prevK = k == -d || k != d && v[offset + k - 1] < v[offset + k + 1] ? k + 1 : k - 1;
                int prevX = d == 0 ? 0 : v[offset + prevK];
                int prevY = d == 0 ? 0 : prevX - prevK;
                while (x > prevX && y > prevY)
                {
                    x--;
                    y--;
                    matches.Add((x, y));
                }
                if (d > 0)
                {
                    x = prevX;
                    y = prevY;
                }
            }
            matches.Reverse();
            return matches;
        }
    }
}
