using System;
using System.Collections.Generic;
using System.Threading;
using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests
{
    public class HeaderSearchEngineTests
    {
        [Fact]
        public void AdvancedSearchStopsWhenCancellationIsRequested()
        {
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            var names = new List<string> { "Twinleaf", "Jubilife" };

            Assert.Throws<OperationCanceledException>(() =>
                HeaderSearchEngine.AdvancedSearch(
                    0,
                    (ushort)names.Count,
                    names,
                    (int)MapHeader.SearchableFields.InternalName,
                    (int)HeaderSearchEngine.TextOperators.Contains,
                    "i",
                    cancellation.Token));
        }
    }
}
