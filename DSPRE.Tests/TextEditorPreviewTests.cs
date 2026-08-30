using DSPRE.Avalonia.ViewModels;
using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>
    /// The message box preview under the text editor's line list: it has to play a line out the way the
    /// game would, stop where the game stops, and say when a line will not fit.
    /// </summary>
    public class TextEditorPreviewTests
    {
        private static TextEditorViewModel WithLines(params string[] texts)
        {
            var vm = new TextEditorViewModel { MeasureText = t => (t ?? "").Length * 6 };
            for (int i = 0; i < texts.Length; i++) vm.Lines.Add(new TextLineVM(i, texts[i]));
            vm.SelectedLineIndex = 0;
            return vm;
        }

        [Fact]
        public void PickingALineShowsItInTheBox()
        {
            var vm = WithLines("Hello there!");
            Assert.True(vm.HasPreview);
            Assert.Equal("Hello there!", vm.PreviewText);
            Assert.False(vm.PreviewHasMore);
        }

        [Fact]
        public void PickingADifferentLineShowsThatOneInstead()
        {
            var vm = WithLines("first", "second");
            Assert.Equal("first", vm.PreviewText);

            vm.SelectedLineIndex = 1;
            Assert.Equal("second", vm.PreviewText);
        }

        [Fact]
        public void EditingTheLineUpdatesWhatTheBoxShows()
        {
            var vm = WithLines("before");
            vm.Lines[0].Text = "after";
            vm.RefreshPreview();
            Assert.Equal("after", vm.PreviewText);
        }

        [Fact]
        public void SteppingThroughFollowsTheGamesOwnBreaks()
        {
            // A clear and then a scroll, so the two different behaviours both show up.
            var vm = WithLines("one\\rtwo\\nthree\\ffour");

            Assert.Equal("one", vm.PreviewText);
            Assert.True(vm.PreviewHasMore);
            Assert.Contains("clears", vm.PreviewWaitText);

            vm.NextPreviewStep();
            Assert.Equal("two\nthree", vm.PreviewText);
            Assert.Contains("scrolls", vm.PreviewWaitText);

            vm.NextPreviewStep();
            // The scroll keeps the line that was at the bottom, which a clear would have thrown away.
            Assert.Equal("three\nfour", vm.PreviewText);
            Assert.False(vm.PreviewHasMore);
            Assert.Contains("end", vm.PreviewWaitText);
        }

        [Fact]
        public void SteppingPastTheEndGoesBackToTheStart()
        {
            var vm = WithLines("one\\rtwo");
            Assert.Equal("one", vm.PreviewText);
            vm.NextPreviewStep();
            Assert.Equal("two", vm.PreviewText);
            vm.NextPreviewStep();
            Assert.Equal("one", vm.PreviewText);
        }

        [Fact]
        public void ItCountsTheStopsWhenThereIsMoreThanOne()
        {
            Assert.Equal("", WithLines("just the one").PreviewStepText);

            var vm = WithLines("one\\rtwo\\rthree");
            Assert.Equal("Stop 1 of 3", vm.PreviewStepText);
            vm.NextPreviewStep();
            Assert.Equal("Stop 2 of 3", vm.PreviewStepText);
        }

        [Fact]
        public void ALineThatWillNotFitTheBoxIsCalledOut()
        {
            var vm = WithLines(new string('W', 60) + "\\rfine");
            Assert.True(vm.HasPreviewWarning);
            Assert.Contains("right edge", vm.PreviewWarning);

            vm.NextPreviewStep();
            Assert.False(vm.HasPreviewWarning);
        }

        [Fact]
        public void TooManyLinesForTheBoxIsCalledOut()
        {
            var vm = WithLines("one\\ntwo\\nthree");
            Assert.True(vm.HasPreviewWarning);
            Assert.Contains("more lines", vm.PreviewWarning);
        }

        [Fact]
        public void TurningThePreviewOffLeavesNothingToShow()
        {
            var vm = WithLines("Hello");
            Assert.True(vm.HasPreview);

            vm.ShowPreview = false;
            Assert.False(vm.HasPreview);
            Assert.Null(vm.PreviewText);

            vm.ShowPreview = true;
            Assert.True(vm.HasPreview);
        }

        [Fact]
        public void AnEmptyLineShowsNoBox()
        {
            var vm = WithLines("");
            Assert.False(vm.HasPreview);
            Assert.Null(vm.PreviewText);
        }
    }
}
