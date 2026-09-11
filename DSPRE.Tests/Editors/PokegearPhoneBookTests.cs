using System;
using System.IO;
using System.Linq;
using DSPRE;
using DSPRE.ROMFiles;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests
{
    /// <summary>The Pokégear phone book, every field of it, and the names its contacts go by.</summary>
    [Collection("rom")]
    public class PokegearPhoneBookTests
    {
        private readonly ITestOutputHelper _out;
        public PokegearPhoneBookTests(ITestOutputHelper o) => _out = o;

        private static bool OpenHeartGold()
        {
            if (!Directory.Exists(TestRoms.HeartGold)) return false;
            SettingsManager.Load();
            try { new RomInfo("IPKE", TestRoms.HeartGold); } catch { return false; }
            return PokegearPhoneBook.IsSupported && File.Exists(PokegearPhoneBook.FilePath);
        }

        [SkippableFact]
        public void EveryFieldOfAContactIsRead()
        {
            Skip.If(!OpenHeartGold(), "HeartGold not unpacked here");

            PokegearPhoneBook.Book book = PokegearPhoneBook.Load(out string error);
            Assert.Null(error);
            Assert.Equal(PokegearPhoneBook.GameContactCount, book.Entries.Count);

            PokegearPhoneBook.Entry joey = book.Entries[10];
            Assert.Equal(10, joey.Id);
            Assert.Equal(PokegearPhoneBook.TypeTrainer, joey.Type);
            Assert.Equal(2, joey.Title);
            Assert.Equal(8, joey.TrainerId);
            Assert.Equal(34, joey.MapId);
            Assert.Equal(45, joey.Gift);
            Assert.Equal(29, joey.LocalScript);
            Assert.Equal(0, joey.Greeting);
            Assert.Equal(1, joey.Weekday);
            Assert.Equal(1, joey.TimeOfDay);
            Assert.Equal(1, joey.RandomGroup);
            Assert.Equal(new byte[] { 75, 40, 47 }, new[] { joey.TitleRank, joey.NameRank, joey.LocationRank });

            PokegearPhoneBook.Entry mom = book.Entries[0];
            Assert.Equal(1, mom.Type);
            Assert.Equal(PokegearPhoneBook.TitleNone, mom.Title);
            Assert.Equal(0, mom.TrainerId);
            Assert.Equal(PokegearPhoneBook.NoGreeting, mom.Greeting);
        }

        [SkippableFact]
        public void AnUnchangedBookWritesBackIdentically()
        {
            Skip.If(!OpenHeartGold(), "HeartGold not unpacked here");

            byte[] original = File.ReadAllBytes(PokegearPhoneBook.FilePath);
            PokegearPhoneBook.Book book = PokegearPhoneBook.Book.Parse(original, out string error);
            Assert.Null(error);
            Assert.Equal(original, book.ToBytes());
        }

        [Fact]
        public void EditingOneFieldChangesOnlyItsBytesAndKeepsWhatFollowsTheRecords()
        {
            byte[] data = new byte[PokegearPhoneBook.HeaderSize + 2 * PokegearPhoneBook.EntrySize + 3];
            BitConverter.GetBytes(2u).CopyTo(data, 0);
            for (int i = PokegearPhoneBook.HeaderSize; i < data.Length; i++) data[i] = (byte)(i * 7);

            PokegearPhoneBook.Book book = PokegearPhoneBook.Book.Parse(data, out string error);
            Assert.Null(error);
            book.Entries[1].Gift = 0x1234;
            byte[] written = book.ToBytes();

            int giftAt = PokegearPhoneBook.HeaderSize + PokegearPhoneBook.EntrySize + 8;
            Assert.Equal(data.Length, written.Length);
            for (int i = 0; i < data.Length; i++)
            {
                if (i == giftAt) Assert.Equal(0x34, written[i]);
                else if (i == giftAt + 1) Assert.Equal(0x12, written[i]);
                else Assert.Equal(data[i], written[i]);
            }
        }

        private static PokegearPhoneBook.Book BookWithNameRanks(params byte[] ranks)
        {
            byte[] data = new byte[PokegearPhoneBook.HeaderSize + ranks.Length * PokegearPhoneBook.EntrySize];
            BitConverter.GetBytes((uint)ranks.Length).CopyTo(data, 0);
            for (int i = 0; i < ranks.Length; i++) data[PokegearPhoneBook.HeaderSize + i * PokegearPhoneBook.EntrySize + 17] = ranks[i];
            return PokegearPhoneBook.Book.Parse(data, out _);
        }

        private static byte[] NameRanks(PokegearPhoneBook.Book book) =>
            book.Entries.Select(e => e.Rank(PokegearPhoneBook.SortKey.Name)).ToArray();

        [Fact]
        public void MovingAContactInAnOrderShiftsTheOnesBetween()
        {
            PokegearPhoneBook.Book book = BookWithNameRanks(1, 2, 3, 4, 5);

            book.MoveInOrder(4, PokegearPhoneBook.SortKey.Name, 2);
            Assert.Equal(new byte[] { 1, 3, 4, 5, 2 }, NameRanks(book));

            book.MoveInOrder(4, PokegearPhoneBook.SortKey.Name, 5);
            Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, NameRanks(book));

            // Only the order being edited moves.
            Assert.All(book.Entries, e => Assert.Equal(0, e.TitleRank));
        }

        [Fact]
        public void AReorderedListBecomesTheNewPositions()
        {
            PokegearPhoneBook.Book book = BookWithNameRanks(2, 0, 1, 2);

            // Unranked contacts go last, and ties keep phone book order.
            Assert.Equal(new[] { 2, 0, 3, 1 }, book.InOrder(PokegearPhoneBook.SortKey.Name));

            book.ReorderTo(PokegearPhoneBook.SortKey.Name, new[] { 1, 3, 0, 2 });
            Assert.Equal(new byte[] { 3, 1, 4, 2 }, NameRanks(book));
            Assert.Equal(new[] { 1, 3, 0, 2 }, book.InOrder(PokegearPhoneBook.SortKey.Name));
        }

        [Fact]
        public void RankingByTextOrdersByTheTextThenTheTieBreak()
        {
            PokegearPhoneBook.Book book = BookWithNameRanks(0, 0, 0, 0);
            book.RankBy(PokegearPhoneBook.SortKey.Title, new[] { "Picnicker", "", "Bug Catcher", "Bug Catcher" }, new[] { "Liz", "Mother", "Wade", "Arnie" });
            Assert.Equal(new byte[] { 4, 1, 3, 2 }, book.Entries.Select(e => e.TitleRank).ToArray());
        }

        [SkippableFact]
        public void SortingNamesAlphabeticallyMatchesTheOrderTheGameShips()
        {
            Skip.If(!OpenHeartGold(), "HeartGold not unpacked here");
            PokegearContactArchives.Forget();

            PokegearPhoneBook.Book book = PokegearPhoneBook.Load(out _);
            byte[] shipped = NameRanks(book);
            string[] names = PokegearContactArchives.Names(book.Entries.Count);

            book.RankBy(PokegearPhoneBook.SortKey.Name, names);
            byte[] sorted = NameRanks(book);

            for (int i = 0; i < shipped.Length; i++)
                if (shipped[i] != sorted[i]) _out.WriteLine($"{names[i]}: shipped {shipped[i]}, sorted {sorted[i]}");
            Assert.Equal(shipped, sorted);
        }

        [SkippableFact]
        public void SortingTitlesAlphabeticallyMatchesTheOrderTheGameShips()
        {
            Skip.If(!OpenHeartGold(), "HeartGold not unpacked here");
            PokegearContactArchives.Forget();

            PokegearPhoneBook.Book book = PokegearPhoneBook.Load(out _);
            byte[] shipped = book.Entries.Select(e => e.TitleRank).ToArray();
            string[] names = PokegearContactArchives.Names(book.Entries.Count);
            string[] classes = RomInfo.GetTrainerClassNames();
            var phone = new TextArchive(RomInfo.pokegearPhoneMessageArchive).messages;
            var titles = book.Entries.Select(e => PokegearPhoneBook.TitleText(e.Title, classes, phone)).ToList();

            book.RankBy(PokegearPhoneBook.SortKey.Title, titles, names);
            byte[] sorted = book.Entries.Select(e => e.TitleRank).ToArray();

            for (int i = 0; i < shipped.Length; i++)
                if (shipped[i] != sorted[i]) _out.WriteLine($"{names[i]} ({titles[i]}): shipped {shipped[i]}, sorted {sorted[i]}");
            Assert.Equal(shipped, sorted);
        }

        [Fact]
        public void ATruncatedBookIsRefused()
        {
            byte[] data = new byte[PokegearPhoneBook.HeaderSize + PokegearPhoneBook.EntrySize];
            BitConverter.GetBytes(2u).CopyTo(data, 0);
            Assert.Null(PokegearPhoneBook.Book.Parse(data, out string error));
            Assert.NotNull(error);
        }

        [SkippableFact]
        public void ContactNamesComeFromEachContactsOwnArchive()
        {
            Skip.If(!OpenHeartGold(), "HeartGold not unpacked here");
            PokegearContactArchives.Forget();

            int[] archives = PokegearContactArchives.Find(PokegearPhoneBook.GameContactCount);
            Assert.NotNull(archives);
            Assert.Equal(664, archives[0]);
            Assert.Equal(716, archives[1]);
            Assert.Equal(675, archives[10]);

            string[] names = PokegearContactArchives.Names(PokegearPhoneBook.GameContactCount);
            _out.WriteLine(string.Join(", ", names.Take(12)));
            Assert.Equal("Joey", names[10]);
            Assert.Equal("Prof. Elm", names[1]);
        }

        [Fact]
        public void TheArchiveTableMustBeTheOnlyCandidate()
        {
            const uint load = 0x02000000;
            byte[] arm9 = new byte[0x400];
            void Table(int at) { for (int i = 0; i < 4; i++) BitConverter.GetBytes((ushort)(100 + i)).CopyTo(arm9, at + i * 2); }
            void Pointer(int at, int target) => BitConverter.GetBytes(load + (uint)target).CopyTo(arm9, at);

            Table(0x100);
            Pointer(0x10, 0x100);
            int[] found = PokegearContactArchives.Locate(arm9, load, 4, 200);
            Assert.Equal(new[] { 100, 101, 102, 103 }, found);

            // A second table that fits just as well leaves no way to tell them apart.
            Table(0x200);
            Pointer(0x14, 0x200);
            Assert.Null(PokegearContactArchives.Locate(arm9, load, 4, 200));
        }
    }
}
