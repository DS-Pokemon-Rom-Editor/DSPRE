using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE.Avalonia.ViewModels.Battle;
using DSPRE.ROMFiles;
using Xunit;
using Xunit.Abstractions;
using static DSPRE.RomInfo;

namespace DSPRE.Tests
{
    /// <summary>
    /// Platinum and HGSS take send-out animation, delay and frame runs from the 89-byte sprite record, not pokeanm.
    /// </summary>
    [Collection("rom")]
    public class BattleSpriteRecordTests
    {
        private const int Pineco = 204, Aggron = 306;
        private readonly ITestOutputHelper _out;
        public BattleSpriteRecordTests(ITestOutputHelper o) => _out = o;

        [Fact]
        public void EachFieldSitsWhereTheGameReadsIt()
        {
            var bytes = new byte[SpeciesSpriteData.Size];
            for (int i = 0; i < bytes.Length; i++) bytes[i] = (byte)(i + 1);
            bytes[3] = 0xFF;            // front slot 0 frame -1
            bytes[46 + 2] = 0xFC;       // back slot 0 shift -4
            bytes[86] = 0xFE;           // Y offset -2

            var rec = SpeciesSpriteData.Parse(bytes);

            Assert.Equal(1, rec.Front.CryDelay);
            Assert.Equal(2, rec.Front.Animation);
            Assert.Equal(3, rec.Front.StartDelay);
            Assert.Equal(-1, rec.Front.Frames[0].FrameNo);
            Assert.Equal(5, rec.Front.Frames[0].Duration);
            Assert.Equal(44, rec.Back.CryDelay);
            Assert.Equal(45, rec.Back.Animation);
            Assert.Equal(46, rec.Back.StartDelay);
            Assert.Equal(-4, rec.Back.Frames[0].HorizontalShift);
            Assert.Equal(86, rec.Back.Frames[9].VerticalShift);
            Assert.Equal(-2, rec.YOffset);
            Assert.Equal(88, rec.ShadowXOffset);
            Assert.Equal(89, rec.ShadowSize);
            Assert.Equal(bytes, rec.ToBytes());
        }

        [SkippableTheory]
        [InlineData("IPKE", "HeartGold")]
        [InlineData("CPUE", "Platinum")]
        public void EveryRecordRoundTripsByteForByte(string code, string game)
        {
            byte[] blob = LoadBlob(code, game);
            int checkedRecords = 0;
            for (int at = 0; at + SpeciesSpriteData.Size <= blob.Length; at += SpeciesSpriteData.Size)
            {
                var slice = blob.AsSpan(at, SpeciesSpriteData.Size).ToArray();
                Assert.Equal(slice, SpeciesSpriteData.Parse(slice).ToBytes());
                checkedRecords++;
            }
            _out.WriteLine($"{game}: {checkedRecords} records");
            Assert.True(checkedRecords >= 494, $"only {checkedRecords} records read");
        }

        [SkippableTheory]
        [InlineData("IPKE", "HeartGold", 9)]
        [InlineData("CPUE", "Platinum", 14)]
        public void TheEditorShowsTheRecordsAnimationNotThePokeanmTables(string code, string game, int pinecoAnimation)
        {
            byte[] blob = LoadBlob(code, game);
            var rec = SpeciesSpriteData.Parse(blob.AsSpan(Pineco * SpeciesSpriteData.Size, SpeciesSpriteData.Size).ToArray());
            Assert.Equal(pinecoAnimation, rec.Front.Animation);

            var vm = new BattleDisplayEditorViewModel();
            vm.LoadMon(Pineco);

            Assert.Equal(rec.Front.Animation, vm.AnimFrontProgNum);
            Assert.Equal(rec.Front.StartDelay, vm.AnimFrontWait);
            Assert.Equal(rec.Front.CryDelay, vm.AnimFrontCryDelay);
            Assert.Single(vm.AnimBack);
            Assert.Equal(rec.Back.Animation, vm.AnimBack[0].Number);
            Assert.Equal(rec.Back.StartDelay, vm.AnimBack[0].Wait);
            Assert.True(vm.HasFrameRuns);
            Assert.Equal(rec.Front.Frames.Select(f => (f.FrameNo, f.Duration, f.HorizontalShift)),
                         vm.FrontFrameEntries.Select(f => (f.FrameNo, f.Duration, f.HorizontalShift)));

            int pokeanm = PokeanmFrontAnimation(Pineco);
            _out.WriteLine($"{game} Pineco: record {rec.Front.Animation}, pokeanm {pokeanm}");
            if (code == "IPKE") Assert.NotEqual(pokeanm, vm.AnimFrontProgNum);   // so this cannot pass by reading pokeanm
        }

        [SkippableFact]
        public void SavingPutsOffsetsAnimationAndFramesIntoTheSameRecord()
        {
            LoadBlob("IPKE", "HeartGold");
            string file = BlobPath();
            byte[] original = File.ReadAllBytes(file);
            try
            {
                var vm = new BattleDisplayEditorViewModel();
                vm.LoadMon(Pineco);
                var before = SpeciesSpriteData.Parse(original.AsSpan(Pineco * SpeciesSpriteData.Size, SpeciesSpriteData.Size).ToArray());

                vm.SpriteY = before.YOffset + 1;
                vm.AnimFrontWait = before.Front.StartDelay + 1;
                vm.AnimBackCryDelay = before.Back.CryDelay + 1;
                vm.FrontFrameEntries[0].Duration = before.Front.Frames[0].Duration + 1;
                vm.Save();

                byte[] written = File.ReadAllBytes(file);
                var after = SpeciesSpriteData.Parse(written.AsSpan(Pineco * SpeciesSpriteData.Size, SpeciesSpriteData.Size).ToArray());
                Assert.Equal(before.YOffset + 1, after.YOffset);
                Assert.Equal(before.Front.StartDelay + 1, after.Front.StartDelay);
                Assert.Equal(before.Back.CryDelay + 1, after.Back.CryDelay);
                Assert.Equal(before.Front.Frames[0].Duration + 1, after.Front.Frames[0].Duration);

                for (int i = 0; i < original.Length; i++)
                {
                    int record = i / SpeciesSpriteData.Size;
                    if (record != Pineco) Assert.True(original[i] == written[i], $"record {record} changed at byte {i % SpeciesSpriteData.Size}");
                }
            }
            finally
            {
                File.WriteAllBytes(file, original);
            }
        }

        [SkippableFact]
        public void AggronsRecordHoldsTheRunsTheSendOutTestPlays()
        {
            // SendOutPreviewTests times its checks off these: frame 0 for 18+1 ticks, frame 1 for 24+1, script 3.
            byte[] blob = LoadBlob("IPKE", "HeartGold");
            var rec = SpeciesSpriteData.Parse(blob.AsSpan(Aggron * SpeciesSpriteData.Size, SpeciesSpriteData.Size).ToArray());
            Assert.Equal((0, 18), (rec.Front.Frames[0].FrameNo, rec.Front.Frames[0].Duration));
            Assert.Equal((1, 24), (rec.Front.Frames[1].FrameNo, rec.Front.Frames[1].Duration));
            Assert.Equal(3, rec.Front.Animation);
        }

        [SkippableFact]
        public void OneSideCanPlayWithoutTheOther()
        {
            LoadBlob("IPKE", "HeartGold");
            var vm = new BattleDisplayEditorViewModel();
            vm.LoadMon(Aggron);

            bool BackMoves()
            {
                bool moved = false;
                vm.ToggleAnimationPlayback();
                for (int i = 0; i < 120 && vm.IsPlaying; i++)
                {
                    vm.GameTick();
                    moved |= !vm.AnimBackMatrix.Equals(global::Avalonia.Matrix.Identity);
                }
                vm.StopPlayback();
                return moved;
            }

            Assert.True(BackMoves(), "Aggron's back animation should move the back sprite");
            vm.SideIndex = 1;   // theirs only
            Assert.False(BackMoves());
        }

        [SkippableFact]
        public void TheFramesButtonPlaysOnlyTheFrameRuns()
        {
            LoadBlob("IPKE", "HeartGold");
            var vm = new BattleDisplayEditorViewModel();
            vm.LoadMon(Aggron);
            vm.ToggleFramePlayback();
            Assert.True(vm.IsPlaying);

            for (int i = 0; i < 19; i++) vm.GameTick();
            Assert.Equal(1, vm.FrontFrameShown);
            Assert.Equal(1.0, vm.AnimScaleX);   // Aggron's pulse would have shrunk it by now

            for (int i = 19; i < 44; i++) vm.GameTick();
            Assert.False(vm.IsPlaying);
            Assert.Equal(0, vm.FrontFrameShown);
        }

        [SkippableFact]
        public void TheAnimationButtonPlaysOnlyTheMovement()
        {
            LoadBlob("IPKE", "HeartGold");
            var vm = new BattleDisplayEditorViewModel();
            vm.LoadMon(Aggron);
            vm.ToggleAnimationPlayback();
            Assert.True(vm.IsPlaying);

            for (int i = 0; i < 19; i++) vm.GameTick();
            Assert.Equal(0, vm.FrontFrameShown);   // the frame run would have switched pose here
            Assert.True(vm.AnimScaleX < 1, $"scale {vm.AnimScaleX} at tick 19, the pulse should be running");
        }

        private static byte[] LoadBlob(string code, string game)
        {
            string project = code == "IPKE" ? TestRoms.HeartGold : TestRoms.Platinum;
            Skip.If(!Directory.Exists(project), $"{game} not unpacked here");
            SettingsManager.Load();
            new RomInfo(code, project);
            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.pokemonSpriteOffsets, DirNames.pokeAnim, DirNames.pokeAnimDefs });
            return File.ReadAllBytes(BlobPath());
        }

        private static string BlobPath()
        {
            var files = RomFiles.Settled(gameDirs[DirNames.pokemonSpriteOffsets].unpackedDir);
            Assert.Single(files);
            return files[0];
        }

        private static int PokeanmFrontAnimation(int species)
        {
            var files = RomFiles.Settled(gameDirs[DirNames.pokeAnim].unpackedDir);
            if (files.Length > 1) return File.ReadAllBytes(files.First(f => Path.GetFileName(f) == species.ToString("D4")))[0];
            return File.ReadAllBytes(files[0])[species * 28];
        }
    }
}
