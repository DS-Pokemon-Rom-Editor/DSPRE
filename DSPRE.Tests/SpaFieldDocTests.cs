using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DSPRE.Avalonia.Data;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests
{
    /// <summary>
    /// Every field of an emitter is accounted for, and the document says so.
    ///
    /// The field list comes from the type itself by reflection rather than by reading the source with a
    /// pattern, because a pattern that quietly stops matching would shrink the list and make the check
    /// pass while proving less. Three things are required: nothing is in the written-down lists that is
    /// not a real field, nothing that only the parser touches is missing from them, and the document on
    /// disk matches what the table generates.
    /// </summary>
    public class SpaFieldDocTests
    {
        private readonly ITestOutputHelper _out;
        public SpaFieldDocTests(ITestOutputHelper o) { _out = o; }

        private static string RepoRoot()
        {
            var d = new DirectoryInfo(AppContext.BaseDirectory);
            while (d != null && !File.Exists(Path.Combine(d.FullName, "DS_Map.sln"))) d = d.Parent;
            return d?.FullName;
        }

        private static List<string> EmitterFields() =>
            typeof(SpaEmitter)
                .GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Select(f => f.Name)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToList();

        [Fact]
        public void EveryNameWrittenDownIsARealFieldOfAnEmitter()
        {
            var fields = new HashSet<string>(EmitterFields(), StringComparer.Ordinal);
            Assert.True(fields.Count > 90, $"only {fields.Count} fields were found, so the check itself is wrong");

            var strays = SpaFieldNotes.NotSimulated.Select(n => n.Field)
                            .Concat(SpaFieldNotes.DrawnNotMoved)
                            .Where(n => !fields.Contains(n))
                            .ToList();
            _out.WriteLine($"{fields.Count} fields on an emitter; "
                           + $"{SpaFieldNotes.NotSimulated.Count} written down as not acted on, "
                           + $"{SpaFieldNotes.DrawnNotMoved.Count} as drawing only");
            Assert.True(strays.Count == 0,
                $"{strays.Count} names are written down that no longer exist: {string.Join(", ", strays)}");
        }

        [Fact]
        public void EveryFieldNothingElseReadsIsWrittenDownWithWhatItWouldChange()
        {
            string root = RepoRoot();
            Assert.True(root != null, "could not find the repository, so nothing was checked");

            string avalonia = Path.Combine(root, "DSPRE.Avalonia");
            string consumers = string.Concat(Directory
                .GetFiles(avalonia, "*.cs", SearchOption.AllDirectories)
                .Where(f => Path.GetFileName(f) is not "SpaArchive.cs" and not "SpaFieldNotes.cs")
                .Select(File.ReadAllText));

            var fields = EmitterFields();
            Assert.True(fields.Count > 90, $"only {fields.Count} fields were found, so the check itself is wrong");

            var unread = fields.Where(f => !consumers.Contains("." + f)).ToList();
            var writtenDown = new HashSet<string>(SpaFieldNotes.NotSimulated.Select(n => n.Field), StringComparer.Ordinal);

            _out.WriteLine($"{fields.Count} fields; {unread.Count} that nothing outside the parser reads");
            foreach (var u in unread) _out.WriteLine("  " + u);

            var missing = unread.Where(f => !writtenDown.Contains(f)).ToList();
            Assert.True(missing.Count == 0,
                $"{missing.Count} fields are read out of the ROM and then dropped with no note saying what "
                + $"acting on them would change: {string.Join(", ", missing)}");

            // And the other way round: a note claiming a field is unused when something now reads it is
            // out of date, and would leave the document telling somebody the wrong thing.
            var stale = writtenDown.Where(f => consumers.Contains("." + f)).ToList();
            Assert.True(stale.Count == 0,
                $"{stale.Count} fields are written down as not acted on but something reads them now: "
                + string.Join(", ", stale));
        }

        [Fact]
        public void TheDocumentSaysWhatTheTableSays()
        {
            string root = RepoRoot();
            Assert.True(root != null, "could not find the repository, so nothing was checked");

            string path = Path.Combine(root, "Research", "Moves", "Animation", "MoveAnimationParticleFields.md");
            string want = SpaFieldNotes.BuildDocument(EmitterFields()).Replace("\r\n", "\n");
            string have = File.Exists(path) ? File.ReadAllText(path).Replace("\r\n", "\n") : "";

            if (want != have)
            {
                string side = path + ".expected";
                File.WriteAllText(side, want);
                Assert.Fail($"MoveAnimationParticleFields.md is out of step with SpaFieldNotes.cs. The text it should have is in {side}.");
            }

            Assert.Contains("what acting on it would change", want);
        }
    }
}
