using global::Avalonia.Controls;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia.ViewModels.Pokemon
{
    /// <summary>
    /// The 16-species pool Trophy Garden picks its daily Pokémon from (DP/Pt); the active two live in the save file.
    /// </summary>
    public class TrophyGardenEncounterViewModel : SpecialEncounterTabViewModel
    {
        public override string UnsavedChangesDescription => "Trophy Garden encounters";
        public override string ListHeading => "Pool";

        public string StatusText => $"{EncounterSlots.Count} slots.{(HasUnsavedChanges ? " Unsaved changes." : "")}";

        public TrophyGardenEncounterViewModel()
        {
            if (Design.IsDesignMode) DesignSample("Pool", "Slot 00: Bidoof");
        }

        public TrophyGardenEncounterViewModel(bool _)
        {
            PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(HasUnsavedChanges)) OnPropertyChanged(nameof(StatusText));
            };
            EncounterSlots.CollectionChanged += (_, _) => OnPropertyChanged(nameof(StatusText));
        }

        protected override int IconSize => 32;

        protected override SpeciesSlotArchive LoadArchive() => TrophyGardenEncounterFile.Load();

        protected override string SaveProblem(SpeciesSlotArchive file) => ((TrophyGardenEncounterFile)file).Problem();

        public void ShowHelp()
        {
            AppMessages.Info(
@"After speaking with Mr. Backlot (Pokémon Mansion, Route 212 North) with the National
Dex obtained, the Trophy Garden starts offering a special Pokémon each day.

How it works: each day, the game randomly picks from this 16-species list (avoiding
repeats of the currently active picks). Up to two daily Pokémon can be active at once,
replacing the Trophy Garden's two 5% grass encounter slots.

This editor only changes the pool of 16 possible species. Which ones are active right
now is stored in your save file, not the ROM, so it isn't shown here.

Data format: 16 Pokémon slots, 4 bytes each (2-byte species ID + 2-byte padding), 64
bytes total. File location: encdata_ex.narc index 8.",
                "Trophy Garden system help");
        }
    }
}
