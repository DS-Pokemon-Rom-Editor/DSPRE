using System;
using System.IO;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests
{
    /// <summary>
    /// Where the tests look for the extracted games. Everybody keeps them somewhere different, so these
    /// paths come from testroms.json or the environment rather than being written into each test.
    /// </summary>
    public class TestRomsTests
    {
        private readonly ITestOutputHelper _out;
        public TestRomsTests(ITestOutputHelper o) => _out = o;

        [Fact]
        public void ThePathsResolveToSomethingAndAtLeastOneGameIsHere()
        {
            foreach (var (name, path) in new[]
            {
                ("HeartGold", TestRoms.HeartGold),
                ("Platinum", TestRoms.Platinum),
                ("Diamond", TestRoms.Diamond),
            })
            {
                Assert.False(string.IsNullOrWhiteSpace(path), $"{name} resolved to nothing");
                _out.WriteLine($"{name}: {path}  {(Directory.Exists(path) ? "(here)" : "(not on this machine)")}");
            }

            Assert.True(Directory.Exists(TestRoms.HeartGold)
                     || Directory.Exists(TestRoms.Platinum)
                     || Directory.Exists(TestRoms.Diamond),
                "None of the game projects were found. Set them in testroms.json beside DS_Map.sln, or in "
                + "DSPRE_TEST_HEARTGOLD / DSPRE_TEST_PLATINUM / DSPRE_TEST_DIAMOND. See docs/Development.md.");
        }

        /// <summary>
        /// An environment variable beats the default layout, and a trailing slash does not survive: a path
        /// that ends in one would give every Path.Combine below it a doubled separator.
        /// </summary>
        [Fact]
        public void WhatIsSetInTheEnvironmentIsWhatGetsUsed()
        {
            const string variable = "DSPRE_TEST_MADE_UP_GAME";
            string before = Environment.GetEnvironmentVariable(variable);
            try
            {
                Environment.SetEnvironmentVariable(variable, null);
                string fallback = TestRoms.Resolve(variable, "madeUpGame", @"Fake\Game_DSPRE_contents");
                _out.WriteLine("with nothing set: " + fallback);
                Assert.EndsWith(@"Fake\Game_DSPRE_contents", fallback, StringComparison.Ordinal);

                Environment.SetEnvironmentVariable(variable, @"D:\somewhere\else\");
                string set = TestRoms.Resolve(variable, "madeUpGame", @"Fake\Game_DSPRE_contents");
                _out.WriteLine("with the variable set: " + set);
                Assert.Equal(@"D:\somewhere\else", set);
            }
            finally
            {
                Environment.SetEnvironmentVariable(variable, before);
            }
        }
    }
}
