using System.Reflection;
using DSPRE.Avalonia.ViewModels.World;
using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests
{
    public class EventTrainerScriptMappingTests
    {
        [Fact]
        public void ExpandedTrainerIdsRoundTripWithoutSkippingTheRetailSpecialNumber()
        {
            if (SettingsManager.Settings == null) SettingsManager.Load();

            var overworld = new Overworld(0, 0, 0)
            {
                type = (ushort)Overworld.OwType.TRAINER,
                scriptNumber = 3928,
            };
            var events = new EventFile();
            events.overworlds.Add(overworld);

            var vm = new EventEditorViewModel();
            for (int trainerId = 0; trainerId <= 930; trainerId++)
            {
                vm.OwTrainerEntries.Add(trainerId.ToString());
            }

            typeof(EventEditorViewModel)
                .GetField("_file", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(vm, events);

            vm.SelectedOverworldIndex = 0;

            Assert.Equal(929, vm.OwTrainerIndex);
            Assert.Equal(3928, vm.OwScript);

            vm.OwTrainerIndex = 930;
            Assert.Equal((ushort)3929, overworld.scriptNumber);
            Assert.Equal(3929, vm.OwScript);

            vm.OwPartnerTrainer = true;
            Assert.Equal((ushort)5929, overworld.scriptNumber);
            Assert.Equal(5929, vm.OwScript);
        }
    }
}
