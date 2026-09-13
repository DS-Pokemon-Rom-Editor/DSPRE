using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE;
using DSPRE.ROMFiles;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests
{
    /// <summary>Ball Capsule seals, seal burst timing and trainer capsules.</summary>
    [Collection("rom")]
    public class BallSealTests
    {
        private readonly ITestOutputHelper _out;
        public BallSealTests(ITestOutputHelper o) => _out = o;

        private static string Project(string name) => name switch
        {
            "Diamond" => TestRoms.Diamond, "Platinum" => TestRoms.Platinum, _ => TestRoms.HeartGold,
        };

        [SkippableTheory]
        [InlineData("ADAE", "Diamond", 185, 37)]
        [InlineData("CPUE", "Platinum", 185, 37)]
        [InlineData("IPKE", "HeartGold", 40, 53)]
        public void EveryGameDefinesEightySeals(string code, string name, int firstSprite, int firstParticle)
        {
            Skip.IfNot(Directory.Exists(Project(name)), $"{name} is not unpacked here");
            new RomInfo(code, Project(name));

            var seals = BallSeals.Read();
            Assert.Equal(BallSeals.Count + 1, seals.Count);
            Assert.Null(seals[0]);
            for (int id = 1; id <= BallSeals.Count; id++)
            {
                var seal = seals[id];
                Assert.Equal(id >= 50 && id <= 77, seal.IsLetter);
                Assert.False(string.IsNullOrWhiteSpace(seal.Name));
            }
            var real = seals.Skip(1).ToList();
            Assert.Equal(Enumerable.Range(firstSprite, BallSeals.Count), real.Select(s => s.Sprite).OrderBy(n => n));
            Assert.Equal(Enumerable.Range(firstParticle, BallSeals.Count), real.Select(s => s.Particle).OrderBy(n => n));
            // Seals 7 and 8 swap stickers, so ids are not sticker order.
            Assert.Equal(seals[8].Sprite + 1, seals[7].Sprite);
            _out.WriteLine($"{name}: {seals[1].Name}, {seals[7].Name}, {seals[50].Name}, {seals[80].Name}");
            Assert.StartsWith("Heart", seals[1].Name, StringComparison.OrdinalIgnoreCase);
            Assert.StartsWith("Star", seals[7].Name, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ASealWaitsByItsRingFromTheCentre()
        {
            var plain = new BallSeal { Id = 1 };
            var letter = new BallSeal { Id = 50, IsLetter = true };

            Assert.Equal(0, SealEffect.DelayTicks(plain, 190, 70));
            Assert.Equal(0, SealEffect.DelayTicks(plain, 190 + 18, 70));
            Assert.Equal(8, SealEffect.DelayTicks(plain, 190 + 19, 70));
            Assert.Equal(14, SealEffect.DelayTicks(plain, 190, 70 + 39));
            Assert.Equal(20, SealEffect.DelayTicks(plain, 190, 70 + 56));
            Assert.Equal(8, SealEffect.DelayTicks(letter, 190, 70));
            Assert.Equal(8, SealEffect.DelayTicks(letter, 190 + 59, 70));
        }

        [Fact]
        public void ACapsuleWritesBackTheBytesItWasReadFrom()
        {
            var bytes = Enumerable.Range(0, BallCapsule.Bytes).Select(i => (byte)(i * 7 + 1)).ToArray();
            var capsule = BallCapsule.Read(bytes, 0);
            var copy = new byte[BallCapsule.Bytes];
            capsule.Write(copy, 0);
            Assert.Equal(bytes, copy);
            Assert.True(BallCapsule.OnBoard(190 + 60, 70));
            Assert.False(BallCapsule.OnBoard(190 + 43, 70 + 43));
        }

        [SkippableTheory]
        [InlineData("CPUE", "Platinum")]
        [InlineData("IPKE", "HeartGold")]
        public void TrainerCapsulesUseOnlyRealSealsOnTheBoard(string code, string name)
        {
            Skip.IfNot(Directory.Exists(Project(name)), $"{name} is not unpacked here");
            new RomInfo(code, Project(name));
            Assert.True(TrainerCapsules.Available);

            var capsules = TrainerCapsules.ReadAll();
            Assert.Equal(TrainerCapsules.Count, capsules.Length);
            var used = capsules.Where(c => !c.IsEmpty).ToList();
            _out.WriteLine($"{name}: {used.Count} trainer capsules hold seals");
            Assert.True(used.Count >= 20, $"{name}: only {used.Count} trainer capsules hold seals");
            foreach (var placed in used.SelectMany(c => c.Seals).Where(s => s.Seal != 0))
            {
                Assert.InRange(placed.Seal, 1, BallSeals.Count);
                Assert.True(BallCapsule.OnBoard(placed.X, placed.Y), $"{name}: a seal sits at {placed.X},{placed.Y}, off the board");
            }
        }

        [SkippableFact]
        public void DiamondHasNoTrainerCapsules()
        {
            Skip.IfNot(Directory.Exists(TestRoms.Diamond), "Diamond is not unpacked here");
            new RomInfo("ADAE", TestRoms.Diamond);
            Assert.False(TrainerCapsules.Available);
        }
    }
}
