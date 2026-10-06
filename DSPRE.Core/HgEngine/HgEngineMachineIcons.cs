using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace DSPRE.HgEngine
{
    /// <summary>
    /// The bag icons of hg-engine's TMs, HMs and TRs. hg-engine gives every item its own icon and palette, built
    /// from the PNG data/graphics/itemgra.mk names for it, so a disc's colour is that PNG's palette rather than a
    /// shared palette id as in the base games.
    /// </summary>
    public static class HgEngineMachineIcons
    {
        private const string ItemHeaderRelPath = "include/constants/item.h";
        private const string IconRulesRelPath = "data/graphics/itemgra.mk";

        // Item i's icon is $(ITEMGFX_DIR)/NNNN-00.NCGR with NNNN = i + 2: GetItemIndex reads member i * 2 + 2, and the
        // two members before item 0's are the shared 0000.NANR and 0001.NCER.
        private static readonly Regex IconRule = new(@"^\$\(ITEMGFX_DIR\)/(\d+)-00\.NCGR:\$\(ITEMGFX_DEPENDENCIES_DIR\)/(\S+\.png)\s*$", RegexOptions.Multiline);

        private static string _rulesText;
        private static Dictionary<int, string> _iconByItem;

        /// <summary>The item a machine is, from the label its sMachineMoves line carries (TM001 is ITEM_TM001); -1 when none.</summary>
        public static int ItemIdFor(string label)
        {
            HgEngineSymbolTable items = HgEngineSymbolTable.Load(ItemHeaderRelPath);
            return items != null && items.TryGetValue("ITEM_" + label, out int id) ? id : -1;
        }

        /// <summary>The PNG an item's icon is built from, or null when the checkout names none.</summary>
        public static string IconPathFor(int item)
        {
            if (!HgEngineProject.IsActive || item < 0) return null;
            string rules = Path.Combine(HgEngineProject.RepoPathUnc, IconRulesRelPath.Replace('/', Path.DirectorySeparatorChar));
            string text = File.Exists(rules) ? HgEngineFileCache.GetText(rules) : null;
            if (text == null) return null;
            if (!string.Equals(text, _rulesText, StringComparison.Ordinal))
            {
                Dictionary<int, string> map = new Dictionary<int, string>();
                string dir = MakeVariable(text, "ITEMGFX_DEPENDENCIES_DIR") ?? "data/graphics/item";
                foreach (Match m in IconRule.Matches(text))
                    map[int.Parse(m.Groups[1].Value) - 2] = Path.Combine(HgEngineProject.RepoPathUnc,
                        (dir + "/" + m.Groups[2].Value).Replace('/', Path.DirectorySeparatorChar));
                _iconByItem = map;
                _rulesText = text;
            }
            return _iconByItem.TryGetValue(item, out string png) && File.Exists(png) ? png : null;
        }

        /// <summary>A PNG's palette chunk, or null when it has none.</summary>
        public static byte[] ReadPalette(string png)
        {
            byte[] file = File.ReadAllBytes(png);
            return FindChunk(file, "PLTE", out int at, out int length) ? file.AsSpan(at, length).ToArray() : null;
        }

        /// <summary>
        /// Gives <paramref name="toPng"/> the colours of <paramref name="fromPng"/>. Only the palette changes when the two
        /// palettes are the same size, so the icon keeps its own picture; otherwise the whole icon is copied.
        /// </summary>
        public static void CopyColours(string fromPng, string toPng)
        {
            byte[] from = File.ReadAllBytes(fromPng);
            byte[] to = File.ReadAllBytes(toPng);
            if (!FindChunk(from, "PLTE", out int fromAt, out int fromLength)
                || !FindChunk(to, "PLTE", out int toAt, out int toLength) || fromLength != toLength)
            {
                File.WriteAllBytes(toPng, from);
                return;
            }
            Buffer.BlockCopy(from, fromAt, to, toAt, toLength);
            byte[] crc = Ekona.Helper.CRC32.Calculate(to.AsSpan(toAt - 4, toLength + 4).ToArray());
            Buffer.BlockCopy(crc, 0, to, toAt + toLength, 4);
            File.WriteAllBytes(toPng, to);
        }

        private static bool FindChunk(byte[] png, string type, out int dataAt, out int length)
        {
            dataAt = length = 0;
            for (int i = 8; i + 8 <= png.Length;)
            {
                int size = (png[i] << 24) | (png[i + 1] << 16) | (png[i + 2] << 8) | png[i + 3];
                if (size < 0 || i + 12 + size > png.Length) return false;
                if (Encoding.ASCII.GetString(png, i + 4, 4) == type)
                {
                    dataAt = i + 8;
                    length = size;
                    return true;
                }
                i += 12 + size;
            }
            return false;
        }

        private static string MakeVariable(string text, string name)
        {
            Match m = Regex.Match(text, @"^\s*" + Regex.Escape(name) + @"\s*:?=\s*(\S+)\s*$", RegexOptions.Multiline);
            return m.Success && !m.Groups[1].Value.Contains("$(") ? m.Groups[1].Value : null;
        }
    }
}
