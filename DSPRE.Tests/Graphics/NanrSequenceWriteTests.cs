using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE;
using DSPRE.Avalonia.Data;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests
{
    /// <summary>
    /// The writes that turn the animation reader into an editor: how a sequence plays, the frame it comes
    /// back to, a frame's turn, stretch and position, and adding or taking out a frame.
    ///
    /// Adding a frame is the only one of these that changes the file's size, and it moves the tables that
    /// say where each sequence's frames begin as well as the extended block that some files carry. There is
    /// no need to trust arithmetic for that: adding a frame and taking it straight back out returns the
    /// counts to what they were, so the rebuilt file has to come back byte for byte identical to the one on
    /// disk. Every real file in the game is used as that check.
    /// </summary>
    [Collection("rom")]
    public class NanrSequenceWriteTests
    {
        private readonly ITestOutputHelper _out;
        public NanrSequenceWriteTests(ITestOutputHelper output) => _out = output;

        private static bool Open(string project, string id)
        {
            if (!Directory.Exists(project)) return false;
            try { new RomInfo(id, project); } catch { return false; }
            return true;
        }

        private static IEnumerable<RomInfo.DirNames> Archives() =>
            RomInfo.gameDirs?.Keys.ToList() ?? new List<RomInfo.DirNames>();

        /// <summary>A real file, inflated, or null when that member is not an animation.</summary>
        private static byte[] Animation(ScriptNarc narc, int i)
        {
            byte[] raw;
            try { raw = NitroBgCodec.Inflate(narc.Get(i)); } catch { return null; }
            if (raw == null || raw.Length < 0x30) return null;
            if (raw[0] != 'R' || raw[1] != 'N' || raw[2] != 'A' || raw[3] != 'N') return null;
            return raw;
        }

        /// <summary>
        /// The first sequence anywhere in the game that answers the question, so a test is never pinned to
        /// one member number that a different game numbers differently.
        /// </summary>
        private static (byte[] Raw, NanrFile File, int Sequence, string Where) Find(
            Func<NanrFile, int, bool> wanted)
        {
            foreach (var dir in Archives())
            {
                ScriptNarc narc;
                try { narc = new ScriptNarc(dir); } catch { continue; }
                if (!narc.Available) continue;

                for (int i = 0; i < narc.Count; i++)
                {
                    byte[] raw = Animation(narc, i);
                    if (raw == null) continue;
                    var f = NanrFile.Read(raw);
                    if (f == null) continue;
                    for (int s = 0; s < f.Sequences.Count; s++)
                        if (wanted(f, s)) return (raw, f, s, $"{dir}#{i} sequence {s}");
                }
            }
            return (null, null, -1, null);
        }

        private static List<int> Moved(byte[] before, byte[] after) =>
            before.Length != after.Length
                ? null
                : Enumerable.Range(0, before.Length).Where(i => before[i] != after[i]).ToList();

        // ── how a sequence plays ─────────────────────────────────────────────────────

        [SkippableFact]
        public void HowASequencePlaysAndWhereItComesBackToLandWhereTheGameReadsThem()
        {
            Skip.If(!Open(TestRoms.Platinum, "CPUE"), "Platinum is not unpacked here");
            var (raw, f, seq, where) = Find((file, s) => file.Sequences[s].Frames.Count >= 4);
            Skip.If(f == null, "no sequence in this game has four frames");
            _out.WriteLine("using " + where);

            f.SetPlayMode(seq, 4);
            f.SetLoopStart(seq, 3);
            byte[] after = f.Write();

            // Both live in the sequence's own row, so nothing else can have moved.
            var moved = Moved(raw, after);
            Assert.NotNull(moved);
            Assert.True(moved.Count <= 6, $"{moved.Count} bytes changed for a play mode and a loop start");

            var again = NanrFile.Read(after);
            Assert.NotNull(again);
            Assert.Equal(4u, again.Sequences[seq].PlayMode);
            Assert.Equal((ushort)3, again.Sequences[seq].LoopStartFrame);
        }

        [SkippableFact]
        public void ASequenceCannotBeToldToComeBackToAFrameItDoesNotHave()
        {
            Skip.If(!Open(TestRoms.Platinum, "CPUE"), "Platinum is not unpacked here");
            var (_, f, seq, _) = Find((file, s) => file.Sequences[s].Frames.Count >= 2);
            Skip.If(f == null, "no sequence in this game has two frames");

            int frames = f.Sequences[seq].Frames.Count;
            f.SetLoopStart(seq, frames + 50);
            Assert.Equal((ushort)(frames - 1), f.Sequences[seq].LoopStartFrame);

            f.SetLoopStart(seq, -4);
            Assert.Equal((ushort)0, f.Sequences[seq].LoopStartFrame);
        }

        [SkippableFact]
        public void OnlyTheFourPlayModesTheHardwareKnowsAreAccepted()
        {
            Skip.If(!Open(TestRoms.Platinum, "CPUE"), "Platinum is not unpacked here");
            var (_, f, seq, _) = Find((file, s) => file.Sequences[s].Frames.Count >= 1);
            Skip.If(f == null, "no animation file was found");

            uint was = f.Sequences[seq].PlayMode;
            f.SetPlayMode(seq, 0);
            f.SetPlayMode(seq, 5);
            Assert.Equal(was, f.Sequences[seq].PlayMode);

            f.SetPlayMode(seq, 3);
            Assert.Equal(3u, f.Sequences[seq].PlayMode);
        }

        // ── a frame's turn, stretch and position ─────────────────────────────────────

        [SkippableFact]
        public void ATurnAndAStretchComeBackAsTheyWereAskedFor()
        {
            Skip.If(!Open(TestRoms.Platinum, "CPUE"), "Platinum is not unpacked here");
            var (_, f, seq, where) = Find((file, s) => file.Sequences[s].ElementType == 1
                                                   && file.Sequences[s].Frames.Count >= 1);
            Skip.If(f == null, "no sequence in this game carries a turn");
            _out.WriteLine("using " + where);

            Assert.Null(f.SetTurn(seq, 0, 90, 2.0, 0.5, everywhere: true));
            var again = NanrFile.Read(f.Write());
            Assert.NotNull(again);

            var (degrees, across, down) = again.TurnOf(seq, 0);
            Assert.Equal(90.0, degrees, 2);
            Assert.Equal(2.0, across, 3);
            Assert.Equal(0.5, down, 3);
        }

        [SkippableFact]
        public void AStretchOfNothingIsRefusedRatherThanMakingTheDrawingVanish()
        {
            Skip.If(!Open(TestRoms.Platinum, "CPUE"), "Platinum is not unpacked here");
            var (raw, f, seq, _) = Find((file, s) => file.Sequences[s].ElementType == 1
                                                  && file.Sequences[s].Frames.Count >= 1);
            Skip.If(f == null, "no sequence in this game carries a turn");

            Assert.NotNull(f.SetTurn(seq, 0, 0, 0, 1));
            Assert.NotNull(f.SetTurn(seq, 0, 0, 1, 0));

            // Refused means nothing was written.
            Assert.Equal(raw, f.Write());
        }

        [SkippableFact]
        public void AFrameThatOnlyNamesADrawingHasNowhereToBeMovedTo()
        {
            Skip.If(!Open(TestRoms.Platinum, "CPUE"), "Platinum is not unpacked here");
            var (raw, f, seq, _) = Find((file, s) => file.Sequences[s].ElementType == 0
                                                  && file.Sequences[s].Frames.Count >= 1);
            Skip.If(f == null, "every sequence in this game carries a position");

            Assert.NotNull(f.SetShift(seq, 0, 12, 20));
            Assert.NotNull(f.SetTurn(seq, 0, 45, 1, 1));
            Assert.Equal(raw, f.Write());
        }

        [SkippableFact]
        public void MovingAFrameThatSharesItsPositionLeavesTheOtherFramesWhereTheyWere()
        {
            Skip.If(!Open(TestRoms.Platinum, "CPUE"), "Platinum is not unpacked here");
            var (_, f, seq, where) = Find((file, s) =>
                file.Sequences[s].ElementType != 0
                && Enumerable.Range(0, file.Sequences[s].Frames.Count).Any(i => file.SharedWith(s, i) > 0));
            Skip.If(f == null, "no sequence in this game shares a position between frames");
            _out.WriteLine("using " + where);

            int frame = Enumerable.Range(0, f.Sequences[seq].Frames.Count)
                                  .First(i => f.SharedWith(seq, i) > 0);
            int sharedAt = f.Sequences[seq].Frames[frame].ResultAt;

            var before = NanrFile.Read(f.Write());
            var siblings = new List<(int S, int F)>();
            for (int s = 0; s < f.Sequences.Count; s++)
                for (int i = 0; i < f.Sequences[s].Frames.Count; i++)
                    if (f.Sequences[s].Frames[i].ResultAt == sharedAt && !(s == seq && i == frame))
                        siblings.Add((s, i));
            Assert.NotEmpty(siblings);

            Assert.Null(f.SetShift(seq, frame, 40, -30));
            var after = NanrFile.Read(f.Write());
            Assert.NotNull(after);

            Assert.Equal((40, -30), after.ShiftOf(seq, frame));
            foreach (var (s, i) in siblings)
                Assert.Equal(before.ShiftOf(s, i), after.ShiftOf(s, i));

            // It was given a result of its own rather than the shared one being rewritten.
            Assert.NotEqual(sharedAt, after.Sequences[seq].Frames[frame].ResultAt);
        }

        // ── adding and taking out frames ─────────────────────────────────────────────

        [SkippableFact]
        public void AddedFrameRepeatsTheOneItWasCopiedFromAndLeavesEverySequenceAloneButItsOwn()
        {
            Skip.If(!Open(TestRoms.Platinum, "CPUE"), "Platinum is not unpacked here");
            var (raw, f, seq, where) = Find((file, s) => file.Sequences[s].Frames.Count >= 2
                                                     && file.Sequences.Count >= 2);
            Skip.If(f == null, "no file in this game has two sequences of two frames");
            _out.WriteLine("using " + where);

            var before = NanrFile.Read(raw);
            int was = f.Sequences[seq].Frames.Count;

            Assert.Null(f.AddFrame(seq, 0));
            byte[] grown = f.Write();
            var after = NanrFile.Read(grown);
            Assert.NotNull(after);

            Assert.Equal(was + 1, after.Sequences[seq].Frames.Count);
            Assert.True(grown.Length > raw.Length, "adding a frame should make the file longer");

            // The copy shows what it was copied from, for as long.
            Assert.Equal(before.CellOf(seq, 0), after.CellOf(seq, 1));
            Assert.Equal(before.Sequences[seq].Frames[0].Delay, after.Sequences[seq].Frames[1].Delay);

            // The frames after it kept their place in the sequence.
            for (int i = 0; i < was - 1; i++)
                Assert.Equal(before.CellOf(seq, i + 1), after.CellOf(seq, i + 2));

            // And no other sequence changed at all.
            for (int s = 0; s < before.Sequences.Count; s++)
            {
                if (s == seq) continue;
                Assert.Equal(before.Sequences[s].Frames.Count, after.Sequences[s].Frames.Count);
                for (int i = 0; i < before.Sequences[s].Frames.Count; i++)
                {
                    Assert.Equal(before.CellOf(s, i), after.CellOf(s, i));
                    Assert.Equal(before.Sequences[s].Frames[i].Delay, after.Sequences[s].Frames[i].Delay);
                }
            }
        }

        [SkippableFact]
        public void ASequenceKeepsItsLastFrame()
        {
            Skip.If(!Open(TestRoms.Platinum, "CPUE"), "Platinum is not unpacked here");
            var (raw, f, seq, _) = Find((file, s) => file.Sequences[s].Frames.Count == 1);
            Skip.If(f == null, "no sequence in this game has a single frame");

            Assert.NotNull(f.RemoveFrame(seq, 0));
            Assert.Equal(raw, f.Write());
        }

        [SkippableFact]
        public void TakingOutAFrameShortensItsSequenceAndNothingElse()
        {
            Skip.If(!Open(TestRoms.Platinum, "CPUE"), "Platinum is not unpacked here");
            var (raw, f, seq, where) = Find((file, s) => file.Sequences[s].Frames.Count >= 3);
            Skip.If(f == null, "no sequence in this game has three frames");
            _out.WriteLine("using " + where);

            var before = NanrFile.Read(raw);
            int was = f.Sequences[seq].Frames.Count;

            Assert.Null(f.RemoveFrame(seq, 1));
            var after = NanrFile.Read(f.Write());
            Assert.NotNull(after);

            Assert.Equal(was - 1, after.Sequences[seq].Frames.Count);
            Assert.Equal(before.CellOf(seq, 0), after.CellOf(seq, 0));
            for (int i = 2; i < was; i++)
                Assert.Equal(before.CellOf(seq, i), after.CellOf(seq, i - 1));

            for (int s = 0; s < before.Sequences.Count; s++)
            {
                if (s == seq) continue;
                Assert.Equal(before.Sequences[s].Frames.Count, after.Sequences[s].Frames.Count);
                for (int i = 0; i < before.Sequences[s].Frames.Count; i++)
                    Assert.Equal(before.CellOf(s, i), after.CellOf(s, i));
            }
        }

        /// <summary>
        /// Adding a frame and taking it straight back out puts every count back where it started, so the
        /// file the writer builds from scratch has to match the one the game ships. This is what proves the
        /// rebuilt frame tables and the regenerated extended block are right, using real files as the
        /// answer rather than restating the arithmetic that produced them.
        /// </summary>
        private void AddThenRemoveGivesBackTheOriginal(string project, string id, string game)
        {
            Skip.If(!Open(project, id), $"{game} is not unpacked here");

            int looked = 0, extended = 0;
            var bad = new List<string>();

            foreach (var dir in Archives())
            {
                ScriptNarc narc;
                try { narc = new ScriptNarc(dir); } catch { continue; }
                if (!narc.Available) continue;

                for (int i = 0; i < narc.Count; i++)
                {
                    byte[] raw = Animation(narc, i);
                    if (raw == null) continue;
                    var f = NanrFile.Read(raw);
                    if (f == null) continue;

                    // A sequence with a frame to copy, so there is something to add and take out again.
                    int seq = -1;
                    for (int s = 0; s < f.Sequences.Count; s++)
                        if (f.Sequences[s].Frames.Count >= 1) { seq = s; break; }
                    if (seq < 0) continue;

                    looked++;
                    if (f.HasExtendedData) extended++;

                    if (f.AddFrame(seq, 0) != null) { bad.Add($"{dir}#{i} would not take a frame"); continue; }
                    if (f.RemoveFrame(seq, 1) != null) { bad.Add($"{dir}#{i} would not give it back"); continue; }

                    byte[] back = f.Write();
                    if (back.Length != raw.Length)
                    {
                        bad.Add($"{dir}#{i} came back {back.Length} bytes, not {raw.Length}");
                        continue;
                    }
                    for (int b = 0; b < raw.Length; b++)
                        if (raw[b] != back[b])
                        {
                            bad.Add($"{dir}#{i} differs at 0x{b:X}: {raw[b]:X2} became {back[b]:X2}");
                            break;
                        }
                }
            }

            _out.WriteLine($"{game}: {looked} files rebuilt from their own counts, "
                         + $"{extended} of them with an extended block");

            Assert.True(looked > 0, "the sweep found no animation files at all");
            Assert.True(extended > 0, "no file with an extended block was exercised");
            Assert.True(bad.Count == 0,
                $"{bad.Count} of {looked} did not survive a rebuild: {string.Join("; ", bad.Take(8))}");
        }

        [SkippableFact]
        public void EveryHeartGoldAnimationSurvivesBeingRebuiltFromItsOwnCounts()
            => AddThenRemoveGivesBackTheOriginal(TestRoms.HeartGold, "IPKE", "HeartGold");

        [SkippableFact]
        public void EveryPlatinumAnimationSurvivesBeingRebuiltFromItsOwnCounts()
            => AddThenRemoveGivesBackTheOriginal(TestRoms.Platinum, "CPUE", "Platinum");

        /// <summary>
        /// A file whose frame count really has changed still has to be readable, and reading it and writing
        /// it again has to settle. A wrong extended block would be read back as different values and the
        /// second write would not match the first.
        /// </summary>
        [SkippableFact]
        public void AFileWithAnExtendedBlockSettlesAfterItsFrameCountChanges()
        {
            Skip.If(!Open(TestRoms.HeartGold, "IPKE"), "HeartGold is not unpacked here");

            int looked = 0;
            foreach (var dir in new[] { RomInfo.DirNames.trainerGraphics, RomInfo.DirNames.trainerBackGraphics })
            {
                ScriptNarc narc;
                try { narc = new ScriptNarc(dir); } catch { continue; }
                if (!narc.Available) continue;

                for (int i = 0; i < narc.Count && looked < 24; i++)
                {
                    byte[] raw = Animation(narc, i);
                    if (raw == null) continue;
                    var f = NanrFile.Read(raw);
                    if (f == null || !f.HasExtendedData) continue;
                    if (f.Sequences.Count == 0 || f.Sequences[0].Frames.Count < 1) continue;

                    looked++;
                    Assert.Null(f.AddFrame(0, 0));
                    byte[] once = f.Write();

                    var again = NanrFile.Read(once);
                    Assert.NotNull(again);
                    Assert.True(again.HasExtendedData, $"{dir}#{i} lost its extended block");
                    Assert.Equal(f.Sequences[0].Frames.Count, again.Sequences[0].Frames.Count);

                    // Read back and written again, it has to be the same file.
                    Assert.Equal(once, again.Write());
                }
            }

            Skip.If(looked == 0, "this game has no animation files with an extended block");
            _out.WriteLine($"{looked} files with an extended block settled after gaining a frame");
        }

        // ── what a sequence's frames index ──────────────────────────────────────────

        [SkippableFact]
        public void WhatASequenceIndexesLandsWhereTheGameReadsIt()
        {
            Skip.If(!Open(TestRoms.Platinum, "CPUE"), "Platinum is not unpacked here");
            var (raw, f, seq, where) = Find((file, s) => file.Sequences[s].Frames.Count >= 1);
            Skip.If(f == null, "no animation file was found");
            _out.WriteLine("using " + where);

            // Every sequence in these games indexes cell banks, which is what makes the other value worth
            // warning about rather than refusing.
            Assert.Equal((ushort)1, f.Sequences[seq].AnimationType);

            Assert.Null(f.SetAnimationType(seq, 2));
            byte[] after = f.Write();
            var moved = Moved(raw, after);
            Assert.NotNull(moved);
            Assert.True(moved.Count <= 2, $"{moved.Count} bytes changed for one animation type");

            var again = NanrFile.Read(after);
            Assert.NotNull(again);
            Assert.Equal((ushort)2, again.Sequences[seq].AnimationType);

            // The element type shares the same word, so it must come through untouched.
            Assert.Equal(f.Sequences[seq].ElementType, again.Sequences[seq].ElementType);
        }

        [SkippableFact]
        public void OnlyTheTwoKindsTheHardwareKnowsAreAccepted()
        {
            Skip.If(!Open(TestRoms.Platinum, "CPUE"), "Platinum is not unpacked here");
            var (raw, f, seq, _) = Find((file, s) => file.Sequences[s].Frames.Count >= 1);
            Skip.If(f == null, "no animation file was found");

            Assert.NotNull(f.SetAnimationType(seq, 0));
            Assert.NotNull(f.SetAnimationType(seq, 3));
            Assert.Equal(raw, f.Write());
        }

        // ── adding a sequence on the end ────────────────────────────────────────────

        [SkippableFact]
        public void AnAddedSequenceCopiesTheLastOneAndLeavesEveryOtherAlone()
        {
            Skip.If(!Open(TestRoms.Platinum, "CPUE"), "Platinum is not unpacked here");
            var (raw, f, _, where) = Find((file, s) => file.Sequences.Count >= 2
                                                    && file.Sequences[^1].Frames.Count >= 1);
            Skip.If(f == null, "no file in this game has two sequences");
            _out.WriteLine("using " + where);

            var before = NanrFile.Read(raw);
            int was = f.Sequences.Count;
            int lastFrames = f.Sequences[^1].Frames.Count;

            Assert.Null(f.AddSequence());
            byte[] grown = f.Write();
            var after = NanrFile.Read(grown);
            Assert.NotNull(after);

            Assert.Equal(was + 1, after.Sequences.Count);
            Assert.True(grown.Length > raw.Length, "adding a sequence should make the file longer");

            // The copy repeats the last sequence, frame for frame.
            Assert.Equal(lastFrames, after.Sequences[was].Frames.Count);
            for (int i = 0; i < lastFrames; i++)
            {
                Assert.Equal(before.CellOf(was - 1, i), after.CellOf(was, i));
                Assert.Equal(before.Sequences[was - 1].Frames[i].Delay, after.Sequences[was].Frames[i].Delay);
            }

            // And every sequence that was already there keeps its number and its contents, which is the
            // whole reason a new one only ever goes on the end.
            for (int s = 0; s < was; s++)
            {
                Assert.Equal(before.Sequences[s].Frames.Count, after.Sequences[s].Frames.Count);
                Assert.Equal(before.Sequences[s].PlayMode, after.Sequences[s].PlayMode);
                Assert.Equal(before.Sequences[s].Name, after.Sequences[s].Name);
                for (int i = 0; i < before.Sequences[s].Frames.Count; i++)
                    Assert.Equal(before.CellOf(s, i), after.CellOf(s, i));
            }
        }

        [SkippableFact]
        public void AFileKeepsItsLastSequence()
        {
            Skip.If(!Open(TestRoms.Platinum, "CPUE"), "Platinum is not unpacked here");
            var (raw, f, _, _) = Find((file, s) => file.Sequences.Count == 1);
            Skip.If(f == null, "no file in this game has a single sequence");

            Assert.NotNull(f.RemoveLastSequence());
            Assert.Equal(raw, f.Write());
        }

        /// <summary>
        /// A sequence added and taken straight back off puts every count back where it started, so the file
        /// has to come back byte for byte identical. This is what proves the rebuilt frame tables, the
        /// regenerated extended block and the rebuilt name table are right, using the real files as the
        /// answer rather than restating the arithmetic that produced them.
        /// </summary>
        private void AddThenRemoveASequenceGivesBackTheOriginal(string project, string id, string game)
        {
            Skip.If(!Open(project, id), $"{game} is not unpacked here");

            int looked = 0, named = 0, extended = 0;
            var bad = new List<string>();

            foreach (var dir in Archives())
            {
                ScriptNarc narc;
                try { narc = new ScriptNarc(dir); } catch { continue; }
                if (!narc.Available) continue;

                for (int i = 0; i < narc.Count; i++)
                {
                    byte[] raw = Animation(narc, i);
                    if (raw == null) continue;
                    var f = NanrFile.Read(raw);
                    if (f == null || f.Sequences.Count == 0) continue;
                    if (f.Sequences[^1].Frames.Count == 0) continue;

                    looked++;
                    if (f.HasExtendedData) extended++;
                    if (!string.IsNullOrEmpty(f.Sequences[0].Name)) named++;

                    if (f.AddSequence() != null) { bad.Add($"{dir}#{i} would not take a sequence"); continue; }
                    if (f.RemoveLastSequence() != null) { bad.Add($"{dir}#{i} would not give it back"); continue; }

                    byte[] back = f.Write();
                    if (back.Length != raw.Length)
                    {
                        bad.Add($"{dir}#{i} came back {back.Length} bytes, not {raw.Length}");
                        continue;
                    }
                    for (int b = 0; b < raw.Length; b++)
                        if (raw[b] != back[b])
                        {
                            bad.Add($"{dir}#{i} differs at 0x{b:X}: {raw[b]:X2} became {back[b]:X2}");
                            break;
                        }
                }
            }

            _out.WriteLine($"{game}: {looked} files gained and lost a sequence, {named} of them carry names, "
                         + $"{extended} an extended block");

            Assert.True(looked > 0, "the sweep found no animation files at all");
            Assert.True(named > 0, "no file with a name table was exercised");
            Assert.True(bad.Count == 0,
                $"{bad.Count} of {looked} did not survive: {string.Join("; ", bad.Take(8))}");
        }

        [SkippableFact]
        public void EveryHeartGoldAnimationSurvivesASequenceAddedAndTakenOff()
            => AddThenRemoveASequenceGivesBackTheOriginal(TestRoms.HeartGold, "IPKE", "HeartGold");

        [SkippableFact]
        public void EveryPlatinumAnimationSurvivesASequenceAddedAndTakenOff()
            => AddThenRemoveASequenceGivesBackTheOriginal(TestRoms.Platinum, "CPUE", "Platinum");
    }
}
