using System.Reflection;
using DSPRE.Avalonia.ViewModels.World;
using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests
{
    public class EventEditorQolTests
    {
        private static EventEditorViewModel ViewModelFor(EventFile events)
        {
            EventEditorViewModel vm = new EventEditorViewModel();
            typeof(EventEditorViewModel)
                .GetField("_file", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(vm, events);
            return vm;
        }

        [Fact]
        public void RemoveOverworldDeletesSelectionAndMarksFileDirty()
        {
            EventFile events = new EventFile();
            events.Overworlds.Add(new Overworld(0, 0, 0));
            events.Overworlds.Add(new Overworld(1, 0, 0));

            EventEditorViewModel vm = ViewModelFor(events);
            vm.SelectedOverworldIndex = 0;

            vm.RemoveOverworld();

            Assert.Single(events.Overworlds);
            Assert.Equal((ushort)1, events.Overworlds[0].OwID);
            Assert.Equal(-1, vm.SelectedOverworldIndex);
            Assert.False(vm.HasOw);
            Assert.True(vm.HasUnsavedChanges);
        }

        [Fact]
        public void DuplicateOverworldCopiesSelectionAndSelectsTheCopy()
        {
            Overworld original = new Overworld(7, 2, 3) { ScriptNumber = 42 };
            EventFile events = new EventFile();
            events.Overworlds.Add(original);

            EventEditorViewModel vm = ViewModelFor(events);
            vm.SelectedOverworldIndex = 0;

            vm.DuplicateOverworld();

            Assert.Equal(2, events.Overworlds.Count);
            Overworld copy = events.Overworlds[1];
            Assert.NotSame(original, copy);
            // Scripts and hide flags find an overworld by its id, so the copy takes the smallest free one.
            Assert.Equal(0, copy.OwID);
            Assert.Equal(original.ToByteArray()[2..], copy.ToByteArray()[2..]);
            Assert.Equal(1, vm.SelectedOverworldIndex);
            Assert.True(vm.HasUnsavedChanges);
        }
    }
}
