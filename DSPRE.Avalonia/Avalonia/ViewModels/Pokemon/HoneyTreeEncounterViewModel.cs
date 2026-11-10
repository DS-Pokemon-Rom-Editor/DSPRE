using global::Avalonia.Controls;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia.ViewModels.Pokemon
{
    /// <summary>
    /// The honey tree encounter groups (DPPt), species only since the slot rates are fixed. A tab of the
    /// Special encounters editor.
    /// </summary>
    public class HoneyTreeEncounterViewModel : SpecialEncounterTabViewModel
    {
        public override string UnsavedChangesDescription => "Honey Tree encounters";
        public override string ListHeading => "Encounter group";

        public HoneyTreeEncounterViewModel()
        {
            if (Design.IsDesignMode) DesignSample("Group A", "Slot 0 (40%): Bidoof");
        }

        public HoneyTreeEncounterViewModel(bool _) { }

        protected override SpeciesSlotArchive LoadArchive() => HoneyTreeEncounterFile.Load();

        private static string Rate(int slot) =>
            slot >= 0 && slot < HoneyTreeEncounterFile.SlotRates.Length ? $"{HoneyTreeEncounterFile.SlotRates[slot]}%" : null;

        protected override string SlotLabel(int slot, SpeciesSlot entry) => $"Slot {slot} ({Rate(slot) ?? "?%"}): {entry}";

        protected override string SlotInfo(int slot) => $"Encounter rate: {Rate(slot) ?? "N/A"}";
    }
}
