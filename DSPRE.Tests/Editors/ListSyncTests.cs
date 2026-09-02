using System.Collections.ObjectModel;
using DSPRE.Avalonia.Data;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>
    /// <see cref="ListSync.Apply"/> rewrites a bound label collection in place (the whole point: preserve a
    /// bound SelectedIndex during a live name/label refresh). These pin down grow / shrink / change / no-op.
    /// </summary>
    public class ListSyncTests
    {
        [Fact]
        public void Apply_AppendsWhenSourceIsLonger()
        {
            var target = new ObservableCollection<string> { "a" };
            ListSync.Apply(target, new[] { "a", "b", "c" });
            Assert.Equal(new[] { "a", "b", "c" }, target);
        }

        [Fact]
        public void Apply_TrimsWhenSourceIsShorter()
        {
            var target = new ObservableCollection<string> { "a", "b", "c" };
            ListSync.Apply(target, new[] { "a" });
            Assert.Equal(new[] { "a" }, target);
        }

        [Fact]
        public void Apply_RewritesChangedEntries()
        {
            var target = new ObservableCollection<string> { "a", "b", "c" };
            ListSync.Apply(target, new[] { "a", "B", "c" });
            Assert.Equal(new[] { "a", "B", "c" }, target);
        }

        [Fact]
        public void Apply_LeavesUnchangedEntriesUntouched()
        {
            // Only index 1 differs; assert index 1 changed and the rest are intact (no Clear+Add churn).
            var target = new ObservableCollection<string> { "a", "b", "c" };
            int changes = 0;
            target.CollectionChanged += (_, _) => changes++;
            ListSync.Apply(target, new[] { "a", "X", "c" });
            Assert.Equal(new[] { "a", "X", "c" }, target);
            Assert.Equal(1, changes);   // exactly one replace, not a full rebuild
        }

        [Fact]
        public void Apply_EqualListsMakeNoChanges()
        {
            var target = new ObservableCollection<string> { "a", "b" };
            int changes = 0;
            target.CollectionChanged += (_, _) => changes++;
            ListSync.Apply(target, new[] { "a", "b" });
            Assert.Equal(0, changes);
        }

        [Fact]
        public void Apply_NullArgumentsAreNoOps()
        {
            var target = new ObservableCollection<string> { "a" };
            ListSync.Apply(target, null);            // must not throw
            ListSync.Apply(null, new[] { "a" });     // must not throw
            Assert.Equal(new[] { "a" }, target);
        }

        [Fact]
        public void Apply_ToEmptyTargetPopulatesIt()
        {
            var target = new ObservableCollection<string>();
            ListSync.Apply(target, new[] { "x", "y" });
            Assert.Equal(new[] { "x", "y" }, target);
        }
    }
}
