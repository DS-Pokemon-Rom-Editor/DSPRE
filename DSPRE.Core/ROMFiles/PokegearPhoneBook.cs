using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DSPRE
{
    /// <summary>
    /// The HeartGold/SoulSilver phone book: a u32 entry count then fixed-size entries. A caller's trainer
    /// ID is what <see cref="PokegearRematchTable"/> is keyed by, so a row nobody can phone is unreachable.
    /// </summary>
    public static class PokegearPhoneBook
    {
        public const int HeaderSize = 4;
        public const int EntrySize = 20;
        public const int TrainerIdOffset = 4;

        /// <summary>The save file, the phone app and several tables are sized for exactly this many.</summary>
        public const int GameContactCount = 75;

        public const byte TitleNone = 200;
        public const byte FirstPhoneTitle = 201;
        public const int PhoneTitleCount = 7;
        public const byte NoGreeting = 0xFF;
        public const int GreetingSetCount = 8;
        public const byte NeverWeekday = 7;
        public const byte NeverTimeOfDay = 3;
        public const int RandomGroupCount = 3;
        public const byte TypeTrainer = 0;
        public const byte TypeGymLeader = 12;

        /// <summary>Call handler names, by the value of <see cref="Entry.Type"/>.</summary>
        public static readonly string[] TypeNames =
        {
            "Trainer", "Mom", "Prof. Elm", "Prof. Oak", "Kurt", "Bike Shop", "Kenji", "Bill",
            "Day-Care Man", "Day-Care Lady", "Buena", "Ethan / Lyra", "Gym Leader", "Safari Warden", "Irwin", "Other",
        };

        public static bool IsSupported => RomInfo.gameFamily == RomInfo.GameFamilies.HGSS;

        public static string FilePath => Path.Combine(RomInfo.dataPath, "tel", "pmtel_book.dat");

        /// <summary>Index of the first phone title in the Pokégear phone strings.</summary>
        public const int FirstPhoneTitleMessage = 38;

        /// <summary>The title shown under a contact's name, or "" when the game shows none.</summary>
        public static string TitleText(byte title, IReadOnlyList<string> classNames, IReadOnlyList<string> phoneMessages)
        {
            if (title < TitleNone) return classNames != null && title < classNames.Count ? classNames[title] ?? "" : "";
            if (title == TitleNone || title >= FirstPhoneTitle + PhoneTitleCount) return "";
            int message = FirstPhoneTitleMessage + title - FirstPhoneTitle;
            return phoneMessages != null && message < phoneMessages.Count ? phoneMessages[message] ?? "" : "";
        }

        /// <summary>Only these types are picked for random incoming calls.</summary>
        public static bool RingsAtRandom(byte type) => type is 0 or 10 or 11 or 12 or 14;

        /// <summary>The three ways the Pokégear sorts its list, each by a stored rank.</summary>
        public enum SortKey { Title, Name, Location }

        public sealed class Entry
        {
            /// <summary>Some code reads this byte and some uses the record's position, so the two must agree.</summary>
            public byte Id;
            public byte Type;
            /// <summary>Never read by the game.</summary>
            public byte Unused2;
            /// <summary>0-199 trainer class, 200 no title, 201 and up a phone title.</summary>
            public byte Title;
            public ushort TrainerId;
            public ushort MapId;
            /// <summary>Handed over after each rematch win. Cheri Berry means a random berry.</summary>
            public ushort Gift;
            /// <summary>Script definition for calling a trainer contact while on their map.</summary>
            public ushort LocalScript;
            public byte Greeting;
            public byte Weekday;
            public byte TimeOfDay;
            /// <summary>Random-call group: 0, 1 or 2 are picked 50, 30 and 20 percent of the time.</summary>
            public byte RandomGroup;
            // The list sorts by these stored ranks, not by the names or maps themselves.
            public byte TitleRank;
            public byte NameRank;
            public byte LocationRank;
            public byte Padding;

            public static Entry Read(ReadOnlySpan<byte> d) => new Entry
            {
                Id = d[0], Type = d[1], Unused2 = d[2], Title = d[3],
                TrainerId = BinaryPrimitives.ReadUInt16LittleEndian(d.Slice(4)),
                MapId = BinaryPrimitives.ReadUInt16LittleEndian(d.Slice(6)),
                Gift = BinaryPrimitives.ReadUInt16LittleEndian(d.Slice(8)),
                LocalScript = BinaryPrimitives.ReadUInt16LittleEndian(d.Slice(10)),
                Greeting = d[12], Weekday = d[13], TimeOfDay = d[14], RandomGroup = d[15],
                TitleRank = d[16], NameRank = d[17], LocationRank = d[18], Padding = d[19],
            };

            public void Write(Span<byte> d)
            {
                d[0] = Id; d[1] = Type; d[2] = Unused2; d[3] = Title;
                BinaryPrimitives.WriteUInt16LittleEndian(d.Slice(4), TrainerId);
                BinaryPrimitives.WriteUInt16LittleEndian(d.Slice(6), MapId);
                BinaryPrimitives.WriteUInt16LittleEndian(d.Slice(8), Gift);
                BinaryPrimitives.WriteUInt16LittleEndian(d.Slice(10), LocalScript);
                d[12] = Greeting; d[13] = Weekday; d[14] = TimeOfDay; d[15] = RandomGroup;
                d[16] = TitleRank; d[17] = NameRank; d[18] = LocationRank; d[19] = Padding;
            }

            public Entry Copy() => (Entry)MemberwiseClone();

            public byte Rank(SortKey key) => key switch
            {
                SortKey.Title => TitleRank,
                SortKey.Name => NameRank,
                _ => LocationRank,
            };

            public void SetRank(SortKey key, byte rank)
            {
                switch (key)
                {
                    case SortKey.Title: TitleRank = rank; break;
                    case SortKey.Name: NameRank = rank; break;
                    default: LocationRank = rank; break;
                }
            }
        }

        public sealed class Book
        {
            public List<Entry> Entries { get; } = new List<Entry>();

            // Kept so bytes past the last record survive a save.
            private byte[] _original = Array.Empty<byte>();

            public static Book Parse(byte[] data, out string error)
            {
                error = null;
                if (data == null || data.Length < HeaderSize)
                {
                    error = "The Pokégear phone book header is truncated.";
                    return null;
                }

                uint count = BinaryPrimitives.ReadUInt32LittleEndian(data);
                if (HeaderSize + (long)count * EntrySize > data.Length)
                {
                    error = "The Pokégear phone book entries are truncated.";
                    return null;
                }

                var book = new Book { _original = (byte[])data.Clone() };
                for (int i = 0; i < count; i++)
                    book.Entries.Add(Entry.Read(data.AsSpan(HeaderSize + i * EntrySize, EntrySize)));
                return book;
            }

            public byte[] ToBytes()
            {
                int recordsEnd = HeaderSize + Entries.Count * EntrySize;
                int originalRecordsEnd = _original.Length >= HeaderSize
                    ? HeaderSize + (int)BinaryPrimitives.ReadUInt32LittleEndian(_original) * EntrySize
                    : HeaderSize;
                int tail = Math.Max(0, _original.Length - originalRecordsEnd);

                byte[] data = new byte[recordsEnd + tail];
                BinaryPrimitives.WriteUInt32LittleEndian(data, (uint)Entries.Count);
                for (int i = 0; i < Entries.Count; i++)
                    Entries[i].Write(data.AsSpan(HeaderSize + i * EntrySize, EntrySize));
                if (tail > 0) Array.Copy(_original, originalRecordsEnd, data, recordsEnd, tail);
                return data;
            }

            /// <summary>
            /// Puts a contact at a position (1 to the contact count) in one sort order, moving the contacts
            /// between its old and new position along by one so no two share a place.
            /// </summary>
            public void MoveInOrder(int contact, SortKey key, int position)
            {
                int count = Entries.Count;
                if (contact < 0 || contact >= count || position < 1 || position > count) return;

                int old = Entries[contact].Rank(key);
                if (old == position) return;

                if (old >= 1 && old <= count)
                {
                    for (int i = 0; i < count; i++)
                    {
                        if (i == contact) continue;
                        int rank = Entries[i].Rank(key);
                        if (position < old && rank >= position && rank < old) Entries[i].SetRank(key, (byte)(rank + 1));
                        else if (position > old && rank > old && rank <= position) Entries[i].SetRank(key, (byte)(rank - 1));
                    }
                }
                Entries[contact].SetRank(key, (byte)position);
            }

            /// <summary>Gives one sort order the positions of the contacts as listed, first to last.</summary>
            public void ReorderTo(SortKey key, IReadOnlyList<int> contactsInOrder)
            {
                for (int place = 0; place < contactsInOrder.Count; place++)
                {
                    int contact = contactsInOrder[place];
                    if (contact >= 0 && contact < Entries.Count)
                        Entries[contact].SetRank(key, (byte)Math.Min(place + 1, byte.MaxValue));
                }
            }

            /// <summary>Contacts in the order one sort lists them, ties and unranked contacts by position.</summary>
            public List<int> InOrder(SortKey key)
            {
                int count = Entries.Count;
                return Enumerable.Range(0, count)
                    .OrderBy(i => Entries[i].Rank(key) is var rank && rank >= 1 && rank <= count ? rank : int.MaxValue)
                    .ThenBy(i => i)
                    .ToList();
            }

            /// <summary>Ranks every contact by its text, then by the tie-break text, then by position.</summary>
            public void RankBy(SortKey key, IReadOnlyList<string> text, IReadOnlyList<string> tieBreak = null)
            {
                var compare = StringComparer.Create(System.Globalization.CultureInfo.InvariantCulture, ignoreCase: true);
                string At(IReadOnlyList<string> list, int i) => list != null && i < list.Count ? list[i] ?? "" : "";

                var order = Enumerable.Range(0, Entries.Count)
                    .OrderBy(i => At(text, i), compare)
                    .ThenBy(i => At(tieBreak, i), compare)
                    .ThenBy(i => i)
                    .ToList();
                for (int place = 0; place < order.Count; place++)
                    Entries[order[place]].SetRank(key, (byte)Math.Min(place + 1, byte.MaxValue));
            }

            /// <summary>The first entry calling this trainer, which is the one the game finds, or -1.</summary>
            public int FindByTrainer(ushort trainerId)
            {
                if (trainerId == 0) return -1;
                return Entries.FindIndex(e => e.TrainerId == trainerId);
            }
        }

        public static Book Load(out string error)
        {
            error = null;
            if (!IsSupported)
            {
                error = "Only HeartGold and SoulSilver have a Pokégear phone book.";
                return null;
            }

            string path = FilePath;
            if (!File.Exists(path))
            {
                error = "The Pokégear phone book is missing from this project.";
                return null;
            }

            try { return Book.Parse(File.ReadAllBytes(path), out error); }
            catch (Exception ex)
            {
                error = $"The Pokégear phone book couldn't be read: {ex.Message}";
                return null;
            }
        }

        public static bool Save(Book book, out string error)
        {
            error = null;
            try
            {
                File.WriteAllBytes(FilePath, book.ToBytes());
                return true;
            }
            catch (Exception ex)
            {
                error = $"The Pokégear phone book couldn't be saved: {ex.Message}";
                return false;
            }
        }

        /// <summary>Trainer ID per entry, in entry order. Zero means the contact is not a trainer.</summary>
        public static bool TryReadTrainerIds(out ushort[] trainerIds, out string error)
        {
            Book book = Load(out error);
            trainerIds = book?.Entries.Select(e => e.TrainerId).ToArray() ?? Array.Empty<ushort>();
            return book != null;
        }
    }
}
