using System.Reflection;
using DSPRE.Avalonia.ViewModels;
using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>
    /// The Z box is shared by all four event kinds but the underlying field is not: overworlds store
    /// 16.16 fixed point, triggers and spawnables store a plain value. These pin the conversion and,
    /// more importantly, that setting it actually reaches the selected event.
    /// </summary>
    public class EventZPositionTests
    {
        private static EventEditorViewModel VmWithSelected(Event e)
        {
            if (DSPRE.SettingsManager.Settings == null) DSPRE.SettingsManager.Load();
            var vm = new EventEditorViewModel();
            typeof(EventEditorViewModel)
                .GetField("_current", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(vm, e);
            return vm;
        }

        [Fact]
        public void TriggerZReachesTheTrigger()
        {
            var t = new Trigger(0, 0);
            var vm = VmWithSelected(t);

            vm.ZPos = 13;

            Assert.Equal(13, t.zPosition);
        }

        [Fact]
        public void SpawnableZReachesTheSpawnable()
        {
            var s = new Spawnable(0, 0);
            var vm = VmWithSelected(s);

            vm.ZPos = 7;

            Assert.Equal(7, s.zPosition);
        }

        [Fact]
        public void OverworldZIsScaledToFixedPoint()
        {
            var o = new Overworld(0, 0, 0);
            var vm = VmWithSelected(o);

            vm.ZPos = 3;

            Assert.Equal(3 * 65536, o.zPosition);
        }

        [Fact]
        public void SettingTheSameDisplayValueOnADifferentEventStillWrites()
        {
            // The box keeps its own backing field. Selecting another event that happens to show the
            // same number must still push the value down, or the second event never gets written.
            var first = new Trigger(0, 0);
            var vm = VmWithSelected(first);
            vm.ZPos = 5;
            Assert.Equal(5, first.zPosition);

            var second = new Trigger(0, 0);
            typeof(EventEditorViewModel)
                .GetField("_current", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(vm, second);

            vm.ZPos = 5;

            Assert.Equal(5, second.zPosition);
        }
    }
}
