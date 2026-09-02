using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE;
using DSPRE.Avalonia.Data;
using Xunit;
using Xunit.Abstractions;
using static DSPRE.RomInfo;

namespace DSPRE.Tests
{
    /// <summary>
    /// Every 3D entry can either be shown, or says why it cannot, and the same for saving it out.
    /// </summary>
    [Collection("rom")]
    public class ModelAssetsTests
    {
        private readonly ITestOutputHelper _out;
        public ModelAssetsTests(ITestOutputHelper o) { _out = o; }

        private const string Diamond =
            @"C:\Romhacking\ROMs\NDS\DP\Pokemon Diamond (v05) (U)(Legacy)\1015 - Pokemon Diamond (v05) (U)(Legacy)_DSPRE_contents";
        private const string Platinum =
            @"C:\Romhacking\ROMs\NDS\Plat\Pokemon - Platinum Version (USA) (Rev 1)\Pokemon - Platinum Version (USA) (Rev 1)_DSPRE_contents";
        private const string HeartGold =
            @"C:\Romhacking\ROMs\NDS\HGSS\HeartGold (USA)_DSPRE_contents";

        public static IEnumerable<object[]> Games => new[]
        {
            new object[] { "ADAE", Diamond,  "Diamond" },
            new object[] { "CPUE", Platinum, "Platinum" },
            new object[] { "IPKE", HeartGold, "HeartGold" },
        };

        [Theory]
        [MemberData(nameof(Games))]
        public void EveryEntryCanBeShownOrSaysWhyNot(string code, string path, string game)
        {
            if (!Directory.Exists(path)) { _out.WriteLine($"{game}: not unpacked here"); return; }
            new RomInfo(code, path);

            int archives = 0, looked = 0, showable = 0, saveable = 0;
            var silent = new List<string>();
            var kinds = new SortedDictionary<string, int>(StringComparer.Ordinal);
            var perArchive = new List<string>();

            foreach (var a in ModelAssets.All)
            {
                int count = ModelAssets.Count(a);
                if (count == 0) continue;
                archives++;
                int aShow = 0, aSave = 0;

                for (int i = 0; i < count; i++)
                {
                    var o = ModelAssets.WhatCanBeDone(a, i);
                    looked++;
                    string k = o.Kind.ToString();
                    kinds[k] = kinds.TryGetValue(k, out int n) ? n + 1 : 1;

                    if (o.CanShow) { showable++; aShow++; }
                    else if (string.IsNullOrWhiteSpace(o.ShowNote)) silent.Add($"{a.Title}[{i}] cannot be shown and does not say why");

                    if (o.CanSaveModel) { saveable++; aSave++; }
                    else if (string.IsNullOrWhiteSpace(o.SaveNote)) silent.Add($"{a.Title}[{i}] cannot be saved as 3D and does not say why");
                }
                perArchive.Add($"  {a.Title,-30} {count,5} entries: {aShow} can be shown, {aSave} can be saved as a 3D file");
            }

            _out.WriteLine($"{game}: {archives} archives, {looked} entries, {showable} showable, {saveable} saveable as 3D");
            foreach (var l in perArchive) _out.WriteLine(l);
            _out.WriteLine("  what they are: " + string.Join(", ", kinds.Select(k => $"{k.Value} {k.Key}")));

            Assert.True(archives > 0, $"{game}: no 3D archive was found, so this proves nothing");
            Assert.True(looked > 100, $"{game}: only {looked} entries were looked at");
            Assert.True(silent.Count == 0,
                $"{game}: {silent.Count} entries gave no answer and no reason: " + string.Join("; ", silent.Take(10)));
        }

        [Theory]
        [MemberData(nameof(Games))]
        public void TheModelsActuallyRead(string code, string path, string game)
        {
            if (!Directory.Exists(path)) { _out.WriteLine($"{game}: not unpacked here"); return; }
            new RomInfo(code, path);

            // Reading every model in three games is slow and proves the same thing as reading a good spread
            // of them, which is that a file calling itself a model really opens as one.
            int tried = 0, opened = 0;
            var failed = new List<string>();

            foreach (var a in ModelAssets.All)
            {
                int count = ModelAssets.Count(a);
                if (count == 0) continue;
                int step = Math.Max(1, count / 20);
                for (int i = 0; i < count; i += step)
                {
                    if (ModelAssets.Identify(new ScriptNarc(a.Dir).Get(i)) != ModelAssets.Kind.Model) continue;
                    tried++;
                    var m = ModelAssets.LoadModel(a, i);
                    if (m != null) opened++;
                    else failed.Add($"{a.Title}[{i}]");
                }
            }

            _out.WriteLine($"{game}: {tried} models opened out of {tried}, {failed.Count} would not read");
            foreach (var f in failed.Take(10)) _out.WriteLine("  " + f);

            Assert.True(tried > 20, $"{game}: only {tried} models were tried, too few to prove anything");
            Assert.True(failed.Count == 0,
                $"{game}: {failed.Count} files call themselves models but will not open: "
                + string.Join(", ", failed.Take(10)));
        }
    }
}
