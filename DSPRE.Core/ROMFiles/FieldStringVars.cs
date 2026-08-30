using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// One place a message leaves a gap for a word the game fills in when it is shown.
    /// </summary>
    public sealed class FieldStringVar
    {
        /// <summary>Which STRVAR family the tag belongs to. Nearly everything is 1.</summary>
        public int Family;

        /// <summary>What kind of word goes here: a name, an item, a number and so on.</summary>
        public int Kind;

        /// <summary>Which of the game's word slots a script has to fill for this to read.</summary>
        public int Buffer;

        /// <summary>The scripts that show a message using this, in the order they were found.</summary>
        public List<int> Scripts = new List<int>();

        /// <summary>The messages it turns up in, so somebody can go and look at them.</summary>
        public List<int> Messages = new List<int>();

        public string Key => FieldStringVars.KeyOf(Family, Kind, Buffer);
        public string KindName => FieldStringVars.KindName(Kind);

        /// <summary>What to show in the box until somebody types something better.</summary>
        public string Suggested;

        /// <summary>What the box will actually show.</summary>
        public string Value;

        public string Label
        {
            get
            {
                string where = Scripts.Count > 0
                    ? $"script {string.Join(", ", Scripts.Take(3))}{(Scripts.Count > 3 ? "…" : "")}"
                    : Messages.Count > 0
                        ? $"message {string.Join(", ", Messages.Take(3))}{(Messages.Count > 3 ? "…" : "")}"
                        : "not used by any script here";
                return $"Slot {Buffer} · {KindName} · {where}";
            }
        }
    }

    /// <summary>
    /// The gaps a message leaves for words the game fills in, and what to put in them so a preview reads
    /// like the real thing instead of showing the tag.
    ///
    /// A message holds a tag: the word 0xFFFE, then the tag type, then how many parameters follow, then
    /// the parameters (fntsys.c:20-62). It counts as a word gap when the top byte of the type is 0x01,
    /// 0x03 or 0x04 (STRCODE_CheckWordSetTagType, fntsys.c:37), and the first parameter says which of the
    /// game's word slots to read, which is what WORDSET_ExpandStr swaps in (wordset.c:1654).
    ///
    /// The text converter writes all that out as "{STRVAR_1, 3, 0, 0}": the family from the top byte, the
    /// kind from the bottom byte, then the two parameters. Checked against the raw bytes of HeartGold
    /// text archive 542 message 1, which reads FFFE 0103 0002 0000 0000. Across all 829 archives and
    /// 49984 messages there are 9150 of these tags in 6635 messages; every one carries exactly two
    /// parameters and every second parameter is zero, so only the first one means anything.
    /// </summary>
    public static class FieldStringVars
    {
        private static readonly Regex TagPattern =
            new Regex(@"\{STRVAR_(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*\}", RegexOptions.Compiled);

        public static string KeyOf(int family, int kind, int buffer) => $"{family}:{kind}:{buffer}";

        /// <summary>Every word gap in a message, in the order they appear.</summary>
        public static IEnumerable<(int family, int kind, int buffer, string whole)> Find(string message)
        {
            if (string.IsNullOrEmpty(message)) yield break;
            foreach (Match m in TagPattern.Matches(message))
                yield return (int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value),
                              int.Parse(m.Groups[3].Value), m.Value);
        }

        /// <summary>Whether a message has any gaps at all, which is cheaper than listing them.</summary>
        public static bool Any(string message) =>
            !string.IsNullOrEmpty(message) && message.Contains("{STRVAR_") && TagPattern.IsMatch(message);

        /// <summary>
        /// What kind of word a gap wants. The numbers are the bottom byte of the tag type and the
        /// meanings come from the reference DSPRE already ships in its string buffer help.
        /// </summary>
        public static string KindName(int kind)
        {
            switch (kind)
            {
                case 0:
                case 1: return "Pokémon name";
                case 3: return "Person's name";
                case 4: return "Map name";
                case 6: return "Move name";
                case 7: return "Nature";
                case 8: return "Item name";
                case 10: return "Seal";
                case 14: return "Trainer class";
                case 15: return "Type name";
                case 18:
                case 31: return "Bag pocket";
                case 24: return "Pokétch app";
                case 25: return "Decoration";
                case 28: return "Stone";
                case 39: return "Ribbon";
                default:
                    if (kind >= 50 && kind <= 55) return "Number";
                    return $"Kind {kind}";
            }
        }

        /// <summary>
        /// What to put in a gap before anybody types anything. A name gap starts as PLAYER, because that
        /// is the one people want to read back. Anything whose kind is not known says which slot it is
        /// and where it came from, so the box stays readable and the gap is obvious.
        /// </summary>
        public static string SuggestFor(int kind, int buffer, IReadOnlyList<int> scripts)
        {
            switch (kind)
            {
                case 3: return "PLAYER";
                case 0:
                case 1: return "POKéMON";
                case 4: return "MAP";
                case 6: return "MOVE";
                case 7: return "NATURE";
                case 8: return "ITEM";
                case 14: return "TRAINER";
                case 15: return "TYPE";
                case 18:
                case 31: return "POCKET";
                case 25: return "DECORATION";
                case 28: return "STONE";
                case 39: return "RIBBON";
                default:
                    if (kind >= 50 && kind <= 55) return "0";
                    return scripts != null && scripts.Count > 0
                        ? $"strvar {buffer} (script {scripts[0]})"
                        : $"strvar {buffer}";
            }
        }

        /// <summary>
        /// Puts the words into a message. Anything the caller has nothing for is left as it was, so a
        /// gap nobody has filled still shows rather than disappearing.
        /// </summary>
        public static string Expand(string message, Func<int, int, int, string> valueFor)
        {
            if (string.IsNullOrEmpty(message) || valueFor == null) return message;
            return TagPattern.Replace(message, m =>
            {
                string v = valueFor(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value),
                                    int.Parse(m.Groups[3].Value));
                return v ?? m.Value;
            });
        }

        /// <summary>
        /// Gathers the gaps out of a run of messages. <paramref name="scriptOf"/> says which script shows
        /// a message, when that is known, so a gap can point back at where it is used.
        /// </summary>
        public static List<FieldStringVar> Gather(IEnumerable<(int message, string text)> messages,
                                                  Func<int, IEnumerable<int>> scriptOf = null)
        {
            var found = new Dictionary<string, FieldStringVar>();
            var order = new List<FieldStringVar>();
            if (messages == null) return order;

            foreach (var (id, text) in messages)
            {
                if (!Any(text)) continue;
                foreach (var (family, kind, buffer, _) in Find(text))
                {
                    string key = KeyOf(family, kind, buffer);
                    if (!found.TryGetValue(key, out var v))
                    {
                        v = new FieldStringVar { Family = family, Kind = kind, Buffer = buffer };
                        found[key] = v;
                        order.Add(v);
                    }
                    if (!v.Messages.Contains(id)) v.Messages.Add(id);
                    if (scriptOf != null)
                        foreach (int s in scriptOf(id))
                            if (!v.Scripts.Contains(s)) v.Scripts.Add(s);
                }
            }

            foreach (var v in order)
            {
                v.Scripts.Sort();
                v.Messages.Sort();
                v.Suggested = SuggestFor(v.Kind, v.Buffer, v.Scripts);
                v.Value = v.Suggested;
            }
            return order;
        }
    }
}
