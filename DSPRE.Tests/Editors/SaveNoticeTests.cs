using DSPRE.Avalonia;
using Xunit;

namespace DSPRE.Tests.Editors
{
    /// <summary>
    /// The save confirmation is raised from inside every editor's save. Two things about it are
    /// worth holding still: the wording, because forty-odd editors now share one phrase, and the
    /// promise that raising it can never disturb the save it is reporting on.
    /// </summary>
    public class SaveNoticeTests
    {
        [Theory]
        [InlineData("Matrix 3", "Saved Matrix 3")]
        [InlineData("Learnset (Mon 25)", "Saved Learnset (Mon 25)")]
        [InlineData("  Event file 42  ", "Saved Event file 42")]
        public void TheConfirmationReadsAsOneSentenceAcrossEveryEditor(string subject, string expected)
        {
            Assert.Equal(expected, SaveNotice.Compose(subject));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void AnEditorWithNoDescriptionStillGetsAWholeSentence(string subject)
        {
            // Not "Saved " with nothing after it, which is what a naive concatenation produces for
            // the editors whose UnsavedChangesDescription can come back empty.
            Assert.Equal("Saved.", SaveNotice.Compose(subject));
        }

        [Fact]
        public void RaisingANoticeWithNoUiDoesNotThrow()
        {
            // Saves happen in tests, in headless runs and during shutdown, when there is no window to
            // draw on. The confirmation must stay out of the way rather than take the save down with
            // it, so this asserts the swallow actually holds.
            SaveNotice.Saved("something with nowhere to appear");
            SaveNotice.Show("a bare message");
        }
    }
}
