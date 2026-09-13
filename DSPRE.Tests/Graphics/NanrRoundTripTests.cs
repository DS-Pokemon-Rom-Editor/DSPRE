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
    /// Every cell animation file in a game, read and written back, compared byte for byte. This is the
    /// whole foundation of animation editing: until an untouched file comes out exactly as it went in,
    /// nothing can safely be changed inside one.
    /// </summary>
    [Collection("rom")]
    public class NanrRoundTripTests
    {
        private readonly ITestOutputHelper _out;
        public NanrRoundTripTests(ITestOutputHelper output) => _out = output;

        private static bool Open(string project, string id)
        {
            if (!Directory.Exists(project)) return false;
            try { new RomInfo(id, project); } catch { return false; }
            return true;
        }

        /// <summary>Every archive this game maps, so the sweep is not limited to the ones we happen to know.</summary>
        private static IEnumerable<RomInfo.DirNames> Archives() =>
            RomInfo.gameDirs?.Keys.ToList() ?? new List<RomInfo.DirNames>();

        private sealed class Tally
        {
            public int Files, Sequences, Frames, Shared, Extended, Compressed;
            public readonly List<string> Bad = new();
        }

        private Tally Sweep()
        {
            var t = new Tally();
            foreach (var dir in Archives())
            {
                ScriptNarc narc;
                try { narc = new ScriptNarc(dir); } catch { continue; }
                if (!narc.Available) continue;

                for (int i = 0; i < narc.Count; i++)
                {
                    byte[] stored;
                    try { stored = narc.Get(i); } catch { continue; }
                    if (stored == null || stored.Length < 4) continue;

                    // Compare what the game would actually read, so compression is not in the way.
                    byte[] raw;
                    try { raw = NitroBgCodec.Inflate(stored); } catch { continue; }
                    if (raw == null || raw.Length < 0x30) continue;
                    if (raw[0] != 'R' || raw[1] != 'N' || raw[2] != 'A' || raw[3] != 'N') continue;
                    if (!ReferenceEquals(raw, stored)) t.Compressed++;

                    var f = NanrFile.Read(raw);
                    if (f == null) { t.Bad.Add($"{dir}#{i} would not read"); continue; }

                    t.Files++;
                    t.Sequences += f.Sequences.Count;
                    t.Frames += f.Sequences.Sum(s => s.Frames.Count);
                    t.Shared += f.Results.Count(r => r.Referrers > 1);
                    if (f.HasExtendedData) t.Extended++;

                    byte[] back = f.Write();
                    if (back.Length != raw.Length)
                    {
                        t.Bad.Add($"{dir}#{i} came back {back.Length} bytes, not {raw.Length}");
                        continue;
                    }
                    for (int b = 0; b < raw.Length; b++)
                        if (raw[b] != back[b])
                        {
                            t.Bad.Add($"{dir}#{i} differs at 0x{b:X}: {raw[b]:X2} became {back[b]:X2}");
                            break;
                        }
                }
            }
            return t;
        }

        private void Check(string project, string id, string game)
        {
            Skip.If(!Open(project, id), $"{game} is not unpacked here");
            var t = Sweep();

            _out.WriteLine($"{game}: {t.Files} files, {t.Sequences} sequences, {t.Frames} frames, "
                         + $"{t.Shared} shared results, {t.Extended} with an extended block, "
                         + $"{t.Compressed} compressed");

            Assert.True(t.Files > 0, "the sweep found no animation files at all");
            Assert.True(t.Bad.Count == 0,
                $"{t.Bad.Count} of {t.Files} did not survive: {string.Join("; ", t.Bad.Take(8))}");
        }

        [SkippableFact]
        public void EveryAnimationFileInHeartGoldComesBackUnchanged()
            => Check(TestRoms.HeartGold, "IPKE", "HeartGold");

        [SkippableFact]
        public void EveryAnimationFileInPlatinumComesBackUnchanged()
            => Check(TestRoms.Platinum, "CPUE", "Platinum");

        /// <summary>
        /// Each of these is a constant the writer leans on, and a game breaking one breaks it silently.
        /// </summary>
        [SkippableFact]
        public void TheThingsTheWriterReliesOnHoldEverywhere()
        {
            Skip.If(!Open(TestRoms.Platinum, "CPUE"), "Platinum is not unpacked here");

            int files = 0, frames = 0;
            foreach (var dir in Archives())
            {
                ScriptNarc narc;
                try { narc = new ScriptNarc(dir); } catch { continue; }
                if (!narc.Available) continue;

                for (int i = 0; i < narc.Count; i++)
                {
                    byte[] raw;
                    try { raw = NitroBgCodec.Inflate(narc.Get(i)); } catch { continue; }
                    if (raw == null || raw.Length < 4) continue;
                    if (raw[0] != 'R' || raw[1] != 'N' || raw[2] != 'A' || raw[3] != 'N') continue;

                    var f = NanrFile.Read(raw);
                    if (f == null) continue;
                    files++;

                    foreach (var s in f.Sequences)
                    {
                        // A sequence that starts partway through its own frames has never been seen, and the
                        // old reader could not even represent one.
                        Assert.Equal(0, s.LoopStartFrame);

                        // Only these two kinds of animation exist in these games.
                        Assert.InRange(s.AnimationType, (ushort)1, (ushort)2);
                        Assert.InRange(s.ElementType, (ushort)0, (ushort)2);

                        foreach (var fr in s.Frames)
                        {
                            Assert.Equal(NanrFile.Beef, fr.Pad);
                            frames++;
                        }
                    }
                }
            }

            Assert.True(files > 0, "no animation files were inspected");
            Assert.True(frames > 0, "no frames were inspected");
            _out.WriteLine($"held across {files} files and {frames} frames");
        }
    }
}
