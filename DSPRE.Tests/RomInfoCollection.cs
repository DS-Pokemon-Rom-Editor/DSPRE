using Xunit;

namespace DSPRE.Tests
{
    /// <summary>
    /// Tests that open a ROM share one of these, because RomInfo keeps what it reads in static fields.
    /// Two classes building it at once tread on each other part way through and one of them falls over
    /// with a null reference, which looks like a fault in whatever was being tested rather than the
    /// race it really is. Marking them a collection makes xunit run them one after another.
    /// </summary>
    [CollectionDefinition("rom")]
    public class RomInfoCollection { }
}
