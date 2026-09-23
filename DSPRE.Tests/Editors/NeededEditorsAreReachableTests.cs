using DSPRE;
using System;
using Xunit;

namespace DSPRE.Tests.Editors
{
    /// <summary>
    /// An editor still being tried out is switched off in an ordinary build. That is right until DSPRE
    /// itself tells somebody to go and use one, which is what happens when it finds an hg-engine ROM
    /// whose tables have moved: the only thing that mends it is the ROM review, and a build without the
    /// switch greyed out the very tool the message named. Somebody was left with a broken ROM and
    /// nothing to do about it.
    /// </summary>
    public class NeededEditorsAreReachableTests : IDisposable
    {
        private readonly bool _was = BetaEditors.Enabled;

        public NeededEditorsAreReachableTests()
        {
            // An ordinary build, which is where this goes wrong. A debug run has them all anyway.
            BetaEditors.Set(false);
            BetaEditors.ForgetWhatWasNeeded();
        }

        public void Dispose()
        {
            BetaEditors.Set(_was);
            BetaEditors.ForgetWhatWasNeeded();
        }

        [Fact]
        public void AnEditorStillBeingTriedOutIsOffUntilSomethingNeedsIt()
        {
            Assert.True(BetaEditors.IsBeta("HgeRomReviewView"));
            Assert.False(BetaEditors.Allows("HgeRomReviewView"));
            Assert.NotNull(BetaEditors.WhyNot("HgeRomReviewView"));
        }

        [Fact]
        public void TheOneThingThatMendsAFaultOpensWhenTheFaultIsThere()
        {
            BetaEditors.AllowBecauseNeeded("HgeRomReviewView");

            Assert.True(BetaEditors.Allows("HgeRomReviewView"));
            Assert.True(BetaEditors.IsNeeded("HgeRomReviewView"));

            // Nothing may say it is greyed out once it is not, or the menu and the window disagree.
            Assert.Null(BetaEditors.WhyNot("HgeRomReviewView"));
        }

        [Fact]
        public void LettingOneThroughLetsNothingElseThrough()
        {
            BetaEditors.AllowBecauseNeeded("HgeRomReviewView");

            // The switch is not flipped: everything else being tried out stays off.
            Assert.False(BetaEditors.Enabled);
            Assert.False(BetaEditors.Allows("ProjectChecksView"));
            Assert.False(BetaEditors.Allows("AudioEditorView"));
            Assert.False(BetaEditors.Allows("DistortionWorldView"));
        }

        [Fact]
        public void TheNextRomHasToEarnItForItself()
        {
            BetaEditors.AllowBecauseNeeded("HgeRomReviewView");
            Assert.True(BetaEditors.Allows("HgeRomReviewView"));

            // A healthy ROM opened afterwards must not inherit what a broken one needed.
            BetaEditors.ForgetWhatWasNeeded();
            Assert.False(BetaEditors.Allows("HgeRomReviewView"));
            Assert.False(BetaEditors.IsNeeded("HgeRomReviewView"));
        }

        [Fact]
        public void AnEditorThatWasNeverBetaIsUnaffected()
        {
            Assert.False(BetaEditors.IsBeta("MapEditorView"));
            Assert.True(BetaEditors.Allows("MapEditorView"));
            Assert.Null(BetaEditors.WhyNot("MapEditorView"));
        }

        [Fact]
        public void NothingIsLetThroughByAskingForNothing()
        {
            BetaEditors.AllowBecauseNeeded(null);
            BetaEditors.AllowBecauseNeeded("");

            Assert.False(BetaEditors.IsNeeded(null));
            Assert.False(BetaEditors.IsNeeded(""));
            Assert.False(BetaEditors.Allows("HgeRomReviewView"));
        }
    }
}
