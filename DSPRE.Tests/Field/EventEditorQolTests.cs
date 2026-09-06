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
            var vm = new EventEditorViewModel();
            typeof(EventEditorViewModel)
                .GetField("_file", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(vm, events);
            return vm;
        }

        [Fact]
        public void RemoveOverworldDeletesSelectionAndMarksFileDirty()
        {
            var events = new EventFile();
            events.overworlds.Add(new Overworld(0, 0, 0));
            events.overworlds.Add(new Overworld(1, 0, 0));

            EventEditorViewModel vm = ViewModelFor(events);
            vm.SelectedOverworldIndex = 0;

            vm.RemoveOverworld();

            Assert.Single(events.overworlds);
            Assert.Equal((ushort)1, events.overworlds[0].owID);
            Assert.Equal(-1, vm.SelectedOverworldIndex);
            Assert.False(vm.HasOw);
            Assert.True(vm.HasUnsavedChanges);
        }

        [Fact]
        public void DuplicateOverworldCopiesSelectionAndSelectsTheCopy()
        {
            var original = new Overworld(7, 2, 3) { scriptNumber = 42 };
            var events = new EventFile();
            events.overworlds.Add(original);

            EventEditorViewModel vm = ViewModelFor(events);
            vm.SelectedOverworldIndex = 0;

            vm.DuplicateOverworld();

            Assert.Equal(2, events.overworlds.Count);
            Assert.NotSame(original, events.overworlds[1]);
            Assert.Equal(original.ToByteArray(), events.overworlds[1].ToByteArray());
            Assert.Equal(1, vm.SelectedOverworldIndex);
            Assert.True(vm.HasUnsavedChanges);
        }
    }
}
