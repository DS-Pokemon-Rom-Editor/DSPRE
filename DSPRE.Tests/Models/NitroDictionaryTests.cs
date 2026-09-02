using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DSPRE.Avalonia.Data;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests
{
    /// <summary>
    /// The name lookup a Nitro 3D file carries, checked against every name list in the ROM's own
    /// models: the tree this builds has to resolve each name to its own place.
    /// </summary>
    public class NitroDictionaryTests
    {
        private readonly ITestOutputHelper _out;
        public NitroDictionaryTests(ITestOutputHelper o) { _out = o; }

        private const string Models =
            @"C:\Romhacking\ROMs\NDS\HGSS\HeartGold (USA)_DSPRE_contents\unpacked\exteriorBuildingModels";

        [Fact]
        public void OneNameResolvesToItself()
        {
            var nodes = NitroDictionary.BuildTree(new[] { "en_fs" });
            Assert.Equal(2, nodes.Count);                       // the head and one node
            Assert.Equal(0, NitroDictionary.Find(nodes, "en_fs"));
        }

        [Fact]
        public void EveryNameInAListResolvesToItsOwnPlace()
        {
            var names = new[] { "polygon0", "polygon1", "polygon2", "polygon3", "polygon4", "polygon5" };
            var nodes = NitroDictionary.BuildTree(names);
            Assert.Equal(names.Length + 1, nodes.Count);
            for (int i = 0; i < names.Length; i++)
                Assert.Equal(i, NitroDictionary.Find(nodes, names[i]));
        }

        [Fact]
        public void NamesThatShareAlmostEverythingStillResolveApart()
        {
            // These are real material names out of HeartGold, and they are the awkward sort: one is
            // another with a letter added.
            var names = new[] { "gs_pc_a", "gs_pc_a_", "gs_pc_b", "h_kage" };
            var nodes = NitroDictionary.BuildTree(names);
            for (int i = 0; i < names.Length; i++)
                Assert.Equal(i, NitroDictionary.Find(nodes, names[i]));
        }

        [Fact]
        public void TwoThingsWithTheSameNameAreRefusedRatherThanWrittenWrong()
        {
            var ex = Assert.Throws<InvalidOperationException>(
                () => NitroDictionary.BuildTree(new[] { "wall", "wall" }));
            Assert.Contains("wall", ex.Message);
        }

        [Fact]
        public void WhatIsWrittenReadsBackTheWayTheGamesFilesDo()
        {
            var names = new[] { "fs_a", "fs_ax", "fs_ay", "kage" };
            var entries = Enumerable.Range(0, names.Length)
                .Select(i => new byte[] { (byte)i, 0, 0, 0 }).ToList();
            byte[] d = NitroDictionary.Write(names, entries);

            Assert.Equal(NitroDictionary.SizeFor(names.Length, 4), d.Length);
            Assert.Equal(0, d[0]);                                  // revision
            Assert.Equal(names.Length, d[1]);
            Assert.Equal(d.Length, d[2] | (d[3] << 8));             // it says its own size

            int ofsEntry = d[6] | (d[7] << 8);
            Assert.Equal(8 + (names.Length + 1) * 4, ofsEntry);
            int unit = d[ofsEntry] | (d[ofsEntry + 1] << 8);
            int ofsName = d[ofsEntry + 2] | (d[ofsEntry + 3] << 8);
            Assert.Equal(4, unit);
            Assert.Equal(4 + names.Length * unit, ofsName);

            for (int i = 0; i < names.Length; i++)
            {
                Assert.Equal(i, d[ofsEntry + 4 + i * unit]);
                int at = ofsEntry + ofsName + i * 16;
                string got = Encoding.ASCII.GetString(d, at, 16).TrimEnd('\0');
                Assert.Equal(names[i], got);
            }
        }

        [Fact]
        public void ANameTooLongForTheSixteenBytesIsCutRatherThanOverrunning()
        {
            byte[] p = NitroDictionary.Padded("a_very_long_material_name_indeed");
            Assert.Equal(16, p.Length);
            Assert.Equal("a_very_long_mate", Encoding.ASCII.GetString(p));
        }

        // ── against the ROM's own name lists ──────────────────────────────────────────────────────

        [Fact]
        public void EveryNameListInTheRomsModelsBuildsATreeThatResolvesAllOfThem()
        {
            if (!Directory.Exists(Models))
            { Assert.Fail($"{Models} is not there, so this proved nothing."); return; }

            int lists = 0, resolved = 0;
            var wrong = new List<string>();

            foreach (string path in Directory.GetFiles(Models).OrderBy(x => x))
            {
                byte[] file;
                try { file = File.ReadAllBytes(path); } catch { continue; }
                if (file.Length < 16 || file[0] != 'B' || file[1] != 'M' || file[2] != 'D') continue;

                foreach (var names in NameListsIn(file))
                {
                    if (names.Count < 1 || names.Distinct().Count() != names.Count) continue;
                    lists++;
                    List<NitroDictionary.Node> nodes;
                    try { nodes = NitroDictionary.BuildTree(names); }
                    catch (Exception ex)
                    { wrong.Add($"{Path.GetFileName(path)} {string.Join(",", names)}: {ex.Message}"); continue; }

                    if (nodes.Count != names.Count + 1)
                    { wrong.Add($"{Path.GetFileName(path)}: {nodes.Count} nodes for {names.Count} names"); continue; }

                    bool all = true;
                    for (int i = 0; i < names.Count; i++)
                        if (NitroDictionary.Find(nodes, names[i]) != i)
                        { wrong.Add($"{Path.GetFileName(path)} '{names[i]}' resolved elsewhere"); all = false; break; }
                    if (all) resolved++;
                }
            }

            _out.WriteLine($"{lists} name lists out of the ROM's models, {resolved} of them resolved fully.");
            // A run that found no lists would pass every check below while testing nothing.
            Assert.True(lists > 500, $"only {lists} name lists were read, which is too few to prove anything");
            Assert.True(wrong.Count == 0, string.Join(Environment.NewLine, wrong.Take(6)));
            Assert.Equal(lists, resolved);
        }

        /// <summary>
        /// Every dictionary's names out of a model file, found by walking the blocks rather than by
        /// using the model reader, so this does not lean on the thing it is checking.
        /// </summary>
        private static IEnumerable<List<string>> NameListsIn(byte[] d)
        {
            int blocks = d[14] | (d[15] << 8);
            for (int b = 0; b < blocks; b++)
            {
                int at = (int)U32(d, 16 + b * 4);
                if (at + 8 > d.Length) yield break;
                if (Tag(d, at) != "MDL0") continue;

                var set = ReadNames(d, at + 8);
                if (set == null) continue;
                yield return set.Names;

                for (int i = 0; i < set.Entries.Count; i++)
                {
                    int m = at + (int)U32(set.Entries[i], 0);
                    if (m + 64 > d.Length) continue;
                    var nodes = ReadNames(d, m + 64);
                    if (nodes != null) yield return nodes.Names;

                    var mats = ReadNames(d, m + (int)U32(d, m + 8) + 4);
                    if (mats != null) yield return mats.Names;

                    var shapes = ReadNames(d, m + (int)U32(d, m + 12));
                    if (shapes != null) yield return shapes.Names;
                }
            }
        }

        private sealed class Dict { public List<string> Names; public List<byte[]> Entries; }

        private static Dict ReadNames(byte[] d, int at)
        {
            if (at < 0 || at + 8 > d.Length) return null;
            int count = d[at + 1];
            if (count == 0 || count > 250) return null;
            int ofsEntry = d[at + 6] | (d[at + 7] << 8);
            int eh = at + ofsEntry;
            if (eh + 4 > d.Length) return null;
            int unit = d[eh] | (d[eh + 1] << 8);
            int ofsName = d[eh + 2] | (d[eh + 3] << 8);
            if (unit <= 0 || unit > 64) return null;
            int names = eh + ofsName;
            if (names + count * 16 > d.Length) return null;

            var o = new Dict { Names = new List<string>(), Entries = new List<byte[]>() };
            for (int i = 0; i < count; i++)
            {
                string s = Encoding.ASCII.GetString(d, names + i * 16, 16).TrimEnd('\0');
                if (s.Length == 0) return null;
                o.Names.Add(s);
                var e = new byte[unit];
                Array.Copy(d, eh + 4 + i * unit, e, 0, unit);
                o.Entries.Add(e);
            }
            return o;
        }

        private static string Tag(byte[] d, int at) => Encoding.ASCII.GetString(d, at, 4);
        private static uint U32(byte[] d, int at) =>
            (uint)(d[at] | (d[at + 1] << 8) | (d[at + 2] << 16) | (d[at + 3] << 24));
    }
}
