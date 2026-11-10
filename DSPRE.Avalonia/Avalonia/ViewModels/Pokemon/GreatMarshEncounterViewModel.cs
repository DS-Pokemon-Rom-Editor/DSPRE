using global::Avalonia.Controls;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia.ViewModels.Pokemon
{
    /// <summary>The Great Marsh daily Pokémon pools (DPPt), species only. A tab of the Special encounters editor.</summary>
    public class GreatMarshEncounterViewModel : SpecialEncounterTabViewModel
    {
        public override string UnsavedChangesDescription => "Great Marsh encounters";
        public override string ListHeading => "Encounter list";
        public override string SlotsNote => "Replaces walking slots 7 and 8";
        public override string SlotsNoteTip => "5% each, only during a Safari Game.";

        public GreatMarshEncounterViewModel()
        {
            if (Design.IsDesignMode) DesignSample("Post-National Dex", "Slot 00: Bidoof");
        }

        public GreatMarshEncounterViewModel(bool _) { }

        protected override SpeciesSlotArchive LoadArchive() => GreatMarshEncounterFile.Load();
    }
}
