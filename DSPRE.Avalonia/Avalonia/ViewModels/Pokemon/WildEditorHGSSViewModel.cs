using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using DSPRE.HgEngine;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia.ViewModels.Pokemon
{
    public class WildEditorHGSSViewModel : WildEditorViewModel<EncounterFileHGSS>
    {
        // ─── hg-engine source banner ──────────────────────────────────────────────
        public string HgEngineBanner => DSPRE.HgEngine.HgEngineProject.BannerText;
        public bool ShowHgEngineBanner => HgEngineBanner != null;

        // Set when the table's Encounters.c entry couldn't be read; saving would write the shown values over it.
        private string _sourceLoadError;
        public string SourceLoadError { get => _sourceLoadError; private set { if (Set(ref _sourceLoadError, value)) OnPropertyChanged(nameof(HasSourceLoadError)); } }
        public bool HasSourceLoadError => _sourceLoadError != null;

        private int _rockSmashRate;
        public int RockSmashRate { get => _rockSmashRate; set { if (Set(ref _rockSmashRate, value) && !Loading) { Current.RockSmashRate = (byte)value; SetDirty(); } } }

        // ── Walking encounter rows (time-based, 12 slots each) ────────────
        public ObservableCollection<WildEncounterRow> MorningRows { get; } = new();
        public ObservableCollection<WildEncounterRow> DayRows     { get; } = new();
        public ObservableCollection<WildEncounterRow> NightRows   { get; } = new();
        public ObservableCollection<WildTimeRow> WalkingRows { get; } = new();

        // HGSS keeps one list of grass levels; morning, day and night only change the species. A level
        // edited on any of the three is the same slot's level on the other two.
        private readonly HashSet<WildEncounterRow> _levelLinked = new();
        private bool _linkingLevels;
        private void LinkWalkingLevels()
        {
            ObservableCollection<WildEncounterRow>[] sets = new[] { MorningRows, DayRows, NightRows };
            foreach (ObservableCollection<WildEncounterRow> rows in sets)
                foreach (WildEncounterRow row in rows)
                {
                    if (!_levelLinked.Add(row)) continue;
                    row.PropertyChanged += (sender, e) =>
                    {
                        if (e.PropertyName != nameof(WildEncounterRow.Level) || _linkingLevels) return;
                        WildEncounterRow changed = (WildEncounterRow)sender;
                        int slot = -1;
                        foreach (ObservableCollection<WildEncounterRow> set in sets) { slot = set.IndexOf(changed); if (slot >= 0) break; }
                        if (slot < 0) return;
                        _linkingLevels = true;
                        try { foreach (ObservableCollection<WildEncounterRow> set in sets) if (slot < set.Count && !ReferenceEquals(set[slot], changed)) set[slot].Level = changed.Level; }
                        finally { _linkingLevels = false; }
                    };
                }
        }

        // ── Swarm / Rock Smash / Radio ─────────────────────────────────────
        public ObservableCollection<WildEncounterRow> SwarmRows        { get; } = new();
        public ObservableCollection<WildEncounterRow> RockSmashRows    { get; } = new();
        public ObservableCollection<WildEncounterRow> HoennRadioRows   { get; } = new();
        public ObservableCollection<WildEncounterRow> SinnohRadioRows  { get; } = new();
        public ObservableCollection<WildRadioRow> RadioRows { get; } = new();

        private WildEditorHGSSViewModel(string[] pokemonNames, int totalHeaders) : base(pokemonNames, totalHeaders) { }

        public static WildEditorHGSSViewModel Open(string[] pokemonNames, int encToOpen, int totalHeaders)
        {
            WildEditorHGSSViewModel vm = new WildEditorHGSSViewModel(pokemonNames, totalHeaders);
            vm.LoadFirst(encToOpen);
            return vm;
        }

        public WildEditorHGSSViewModel()
        {
            if (!Design.IsDesignMode) return;

            Title = "Wild Pokémon editor HGSS (Preview)";
            for (int i = 0; i < 30; i++) PokemonNames.Add($"Pokémon {i:000}");
            EncounterNames.Add("[0] Route 29"); EncounterNames.Add("[1] Route 30"); EncounterNames.Add("[2] Unused");

            StayQuiet();
            WalkingRate = 25; SurfRate = 10; RockSmashRate = 5;
            OldRodRate = 25; GoodRodRate = 50; SuperRodRate = 75;

            string[] walkLabels = Enumerable.Range(0, 12).Select(i => EncounterSlotOdds.SlotLabel("Walking", i)).ToArray();
            for (int i = 0; i < 12; i++)
            {
                MorningRows.Add(new WildEncounterRow(PokemonNames, null) { Label = walkLabels[i], PokemonIndex = i % PokemonNames.Count, Level = 5 });
                DayRows.Add(new WildEncounterRow(PokemonNames, null) { Label = walkLabels[i], PokemonIndex = i % PokemonNames.Count, Level = 6 });
                NightRows.Add(new WildEncounterRow(PokemonNames, null) { Label = walkLabels[i], PokemonIndex = i % PokemonNames.Count, Level = 7 });
            }
            string[] dtSwarmLabels = { "Swarm while walking", "Swarm while surfing", "Night fishing", "Swarm while fishing" };
            for (int i = 0; i < 4; i++)
                SwarmRows.Add(new WildEncounterRow(PokemonNames, null) { Label = dtSwarmLabels[i], PokemonIndex = i % PokemonNames.Count, Level = 15 });
            for (int i = 0; i < 2; i++)
            {
                RockSmashRows.Add(new WildEncounterRow(PokemonNames, null) { Label = $"Rock Smash {i + 1}", PokemonIndex = i % PokemonNames.Count, MinLevel = 10, MaxLevel = 20 });
                HoennRadioRows.Add(new WildEncounterRow(PokemonNames, null) { Label = $"Slot {i + 1}",  PokemonIndex = i % PokemonNames.Count, Level = 15 });
                SinnohRadioRows.Add(new WildEncounterRow(PokemonNames, null) { Label = $"Slot {i + 1}", PokemonIndex = i % PokemonNames.Count, Level = 15 });
            }
            for (int i = 0; i < 5; i++)
            {
                SurfRows.Add(new WildEncounterRow(PokemonNames, null) { Label = $"Surf {i + 1}",      PokemonIndex = i % PokemonNames.Count, MinLevel = 20, MaxLevel = 30 });
                OldRodRows.Add(new WildEncounterRow(PokemonNames, null) { Label = $"Old Rod {i + 1}",   PokemonIndex = i % PokemonNames.Count, MinLevel = 5,  MaxLevel = 10 });
                GoodRodRows.Add(new WildEncounterRow(PokemonNames, null) { Label = $"Good Rod {i + 1}",  PokemonIndex = i % PokemonNames.Count, MinLevel = 10, MaxLevel = 20 });
                SuperRodRows.Add(new WildEncounterRow(PokemonNames, null) { Label = $"Super Rod {i + 1}", PokemonIndex = i % PokemonNames.Count, MinLevel = 30, MaxLevel = 40 });
            }
        }

        /// <summary>On hg-engine the table comes from Encounters.c; the built copy only fills a field the entry
        /// lacks, which the save then reports.</summary>
        private static EncounterFileHGSS ReadRecord(byte[] bytes, int id, out string error)
        {
            error = null;
            if (HgEngineProject.IsActive && bytes == null)
            {
                EncounterFileHGSS fresh = new EncounterFileHGSS();
                HgEngineEncounterSource.TryLoad(id, fresh, out error);
                return fresh;
            }
            EncounterFileHGSS file = new EncounterFileHGSS(new MemoryStream(bytes ?? throw new FileNotFoundException("The encounter file is missing.")));
            if (HgEngineProject.IsActive) HgEngineEncounterSource.TryLoad(id, file, out error);
            return file;
        }

        protected override EncounterFileHGSS LoadRecord(byte[] bytes, int id, out string error) => ReadRecord(bytes, id, out error);

        protected override EncounterFileHGSS Parse(Stream stream) => new EncounterFileHGSS(stream);

        protected override void RecordLoaded(string error) => SourceLoadError = error;

        protected override bool ShowForms => HgEngineProject.IsActive;

        protected override string RodOdds(string rod) => "Fishing (all rods)";

        protected override int EncounterFileOf(MapHeader header, out int locationName)
        {
            locationName = ((HeaderHGSS)header).LocationName;
            return header.WildPokemon == MapHeader.HgssNullEncounterFileId ? -1 : header.WildPokemon;
        }

        protected override bool SpecialTabHasEncounters => Current.RockSmashRate > 0;

        protected override IEnumerable<ObservableCollection<WildEncounterRow>> EditableRows => new[]
        {
            MorningRows, DayRows, NightRows, SwarmRows, RockSmashRows, HoennRadioRows, SinnohRadioRows,
            SurfRows, OldRodRows, GoodRodRows, SuperRodRows,
        };

        protected override bool SpeciesFeedsNotes(ObservableCollection<WildEncounterRow> rows) =>
            rows == SwarmRows || rows == HoennRadioRows || rows == SinnohRadioRows;

        protected override async Task<bool> CanSaveAsync()
        {
            if (!HgEngineProject.IsActive || SourceLoadError == null) return true;
            await DialogHelper.ShowErrorAsync($"Encounter table {SelectedEncounterIndex} wasn't saved.\n{SourceLoadError}", EditorTitle);
            return false;
        }

        protected override void CollectProblems(List<string> problems)
        {
            WildEncounterChecks.Check(problems, "Walking", WalkingRate, MorningRows, false);
            WildEncounterChecks.CheckSpecies(problems, "Day walking", WalkingRate, DayRows);
            WildEncounterChecks.CheckSpecies(problems, "Night walking", WalkingRate, NightRows);
            foreach (ObservableCollection<WildEncounterRow> rows in new[] { SwarmRows, HoennRadioRows, SinnohRadioRows })
                foreach (WildEncounterRow r in rows) if (r.Warning != null) problems.Add(r.Warning);
            WildEncounterChecks.Check(problems, "Surf", SurfRate, SurfRows, true);
            WildEncounterChecks.Check(problems, "Rock Smash", RockSmashRate, RockSmashRows, true);
            WildEncounterChecks.Check(problems, "Old Rod", OldRodRate, OldRodRows, true);
            WildEncounterChecks.Check(problems, "Good Rod", GoodRodRate, GoodRodRows, true);
            WildEncounterChecks.Check(problems, "Super Rod", SuperRodRate, SuperRodRows, true);
        }

        // The next sync rebuilds from the source, so a save that can't write it fails.
        protected override async Task<bool> WriteSourceAsync()
        {
            if (!HgEngineProject.IsActive) return true;
            int table = SelectedEncounterIndex;
            EncounterFileHGSS encounters = Current;
            (bool saved, string error) = await HgEngineSave.RunAsync(() => HgEngineEncounterSource.TryWrite(table, encounters, out string writeError) ? null : writeError);
            if (!saved && error != null) await DialogHelper.ShowErrorAsync($"Encounter table {table} wasn't saved.\n{error}", EditorTitle);
            return saved;
        }

        public override void RefreshSlotLabels()
        {
            foreach (ObservableCollection<WildEncounterRow> rows in new[] { MorningRows, DayRows, NightRows })
                for (int i = 0; i < rows.Count; i++) rows[i].Label = EncounterSlotOdds.SlotLabel("Walking", i);
            for (int i = 0; i < RockSmashRows.Count; i++) RockSmashRows[i].Label = EncounterSlotOdds.SlotLabel("Rock Smash", i, $"Rock Smash {i+1}");
            base.RefreshSlotLabels();
        }

        protected override void PopulateFamilyRows()
        {
            RockSmashRate = Current.RockSmashRate;

            string[] walkLabels = Enumerable.Range(0, 12).Select(i => EncounterSlotOdds.SlotLabel("Walking", i)).ToArray();
            SyncRows(MorningRows, 12, i => walkLabels[i], i => Current.MorningPokemon[i], i => Current.WalkingLevels[i], null, null, false);
            SyncRows(DayRows,     12, i => walkLabels[i], i => Current.DayPokemon[i],     i => Current.WalkingLevels[i], null, null, false);
            SyncRows(NightRows,   12, i => walkLabels[i], i => Current.NightPokemon[i],   i => Current.WalkingLevels[i], null, null, false);
            LinkWalkingLevels();
            if (WalkingRows.Count == 0)
                for (int i = 0; i < 12; i++) WalkingRows.Add(new WildTimeRow(MorningRows[i], DayRows[i], NightRows[i]));

            // Slot 2 is the night-only fishing Pokémon, not a swarm.
            string[] swarmLabels = { "Swarm while walking", "Swarm while surfing", "Night fishing", "Swarm while fishing" };
            SyncRows(SwarmRows,       4, i => swarmLabels[i],       i => Current.SwarmPokemon[i],       _ => 0,  null, null, false);
            string[] radioLabels = { "Slot 1", "Slot 2" };
            SyncRows(HoennRadioRows,  2, i => radioLabels[i],       i => Current.HoennMusicPokemon[i],  _ => 0,  null, null, false);
            SyncRows(SinnohRadioRows, 2, i => radioLabels[i],       i => Current.SinnohMusicPokemon[i], _ => 0,  null, null, false);
            if (RadioRows.Count == 0)
                for (int i = 0; i < 2; i++) RadioRows.Add(new WildRadioRow(HoennRadioRows[i], SinnohRadioRows[i]));
            SyncRows(RockSmashRows,   2, i => EncounterSlotOdds.SlotLabel("Rock Smash", i, $"Rock Smash {i+1}"), i => Current.RockSmashPokemon[i],   _ => 0,
                i => Current.RockSmashMinLevels[i], i => Current.RockSmashMaxLevels[i], true);
        }

        protected override void WriteFamilyRowsToFile()
        {
            for (int i = 0; i < MorningRows.Count && i < 12; i++) Current.MorningPokemon[i] = (ushort)MorningRows[i].PackedSpecies;
            for (int i = 0; i < DayRows.Count     && i < 12; i++) Current.DayPokemon[i]     = (ushort)DayRows[i].PackedSpecies;
            for (int i = 0; i < NightRows.Count   && i < 12; i++) Current.NightPokemon[i]   = (ushort)NightRows[i].PackedSpecies;
            for (int i = 0; i < 12; i++) Current.WalkingLevels[i] = (byte)(MorningRows.Count > i ? MorningRows[i].Level : 0);
            for (int i = 0; i < SwarmRows.Count   && i < 4; i++) Current.SwarmPokemon[i]       = (ushort)SwarmRows[i].PackedSpecies;
            for (int i = 0; i < HoennRadioRows.Count  && i < 2; i++) Current.HoennMusicPokemon[i]  = (ushort)HoennRadioRows[i].PackedSpecies;
            for (int i = 0; i < SinnohRadioRows.Count && i < 2; i++) Current.SinnohMusicPokemon[i] = (ushort)SinnohRadioRows[i].PackedSpecies;
            for (int i = 0; i < RockSmashRows.Count && i < 2; i++)
            {
                Current.RockSmashPokemon[i]   = (ushort)RockSmashRows[i].PackedSpecies;
                Current.RockSmashMinLevels[i] = (byte)RockSmashRows[i].MinLevel;
                Current.RockSmashMaxLevels[i] = (byte)RockSmashRows[i].MaxLevel;
            }
        }

        // Slots each method replaces: swarm 0-1, radio 2-3 and 4-5, surf swarm 0, fishing swarm Old 2, Good 0/2/3, all Super; night Good 3, Super 1.
        protected override void SetNotes()
        {
            for (int i = 0; i < MorningRows.Count; i++)
                MorningRows[i].Replacements = i switch
                {
                    0 or 1 => Reps(Rep(SwarmRows, 0, "Swarm")),
                    2 or 3 => Reps(Rep(HoennRadioRows, 0, "Hoenn Sound"), Rep(SinnohRadioRows, 0, "Sinnoh Sound")),
                    4 or 5 => Reps(Rep(HoennRadioRows, 1, "Hoenn Sound"), Rep(SinnohRadioRows, 1, "Sinnoh Sound")),
                    _ => null,
                };
            for (int i = 0; i < SurfRows.Count; i++) SurfRows[i].Replacements = i == 0 ? Reps(Rep(SwarmRows, 1, "Swarm")) : null;
            for (int i = 0; i < OldRodRows.Count; i++) OldRodRows[i].Replacements = i == 2 ? Reps(Rep(SwarmRows, 3, "Swarm")) : null;
            for (int i = 0; i < GoodRodRows.Count; i++)
                GoodRodRows[i].Replacements = i == 3 ? Reps(Rep(SwarmRows, 3, "Swarm"), Rep(SwarmRows, 2, "Night fishing"))
                    : i == 0 || i == 2 ? Reps(Rep(SwarmRows, 3, "Swarm")) : null;
            for (int i = 0; i < SuperRodRows.Count; i++)
                SuperRodRows[i].Replacements = i == 1 ? Reps(Rep(SwarmRows, 3, "Swarm"), Rep(SwarmRows, 2, "Night fishing")) : Reps(Rep(SwarmRows, 3, "Swarm"));

            // The game copies these in without checking for an empty slot, so an empty one makes a glitch Pokémon.
            bool walks = WalkingRate > 0, anyRod = OldRodRate > 0 || GoodRodRate > 0 || SuperRodRate > 0;
            string[] swarmWarn =
            {
                walks ? "Empty: if a swarm comes here, walking slots 1 and 2 give a glitch Pokémon." : null,
                SurfRate > 0 ? "Empty: if a swarm comes here, surf slot 1 gives a glitch Pokémon." : null,
                GoodRodRate > 0 || SuperRodRate > 0 ? "Empty: at night, Good Rod slot 4 and Super Rod slot 2 give a glitch Pokémon." : null,
                anyRod ? "Empty: if a fishing swarm comes here, the fishing slots it replaces give a glitch Pokémon." : null,
            };
            for (int i = 0; i < SwarmRows.Count && i < swarmWarn.Length; i++) SwarmRows[i].Warning = SwarmRows[i].PokemonIndex <= 0 ? swarmWarn[i] : null;
            foreach ((ObservableCollection<WildEncounterRow> radio, string sound) in new[] { (HoennRadioRows, "Hoenn"), (SinnohRadioRows, "Sinnoh") })
                for (int i = 0; i < radio.Count; i++)
                    radio[i].Warning = walks && radio[i].PokemonIndex <= 0
                        ? $"Empty: while the radio plays {sound} Sound, walking slots {(i == 0 ? "3 and 4" : "5 and 6")} give a glitch Pokémon." : null;
            for (int i = 0; i < MorningRows.Count; i++)
                MorningRows[i].Warning = i switch
                {
                    0 or 1 => SwarmRows.Count > 0 ? SwarmRows[0].Warning : null,
                    2 or 3 => HoennRadioRows.Concat(SinnohRadioRows).Where((r, k) => k % 2 == 0).Select(r => r.Warning).FirstOrDefault(w => w != null),
                    4 or 5 => HoennRadioRows.Concat(SinnohRadioRows).Where((r, k) => k % 2 == 1).Select(r => r.Warning).FirstOrDefault(w => w != null),
                    _ => null,
                };
            if (SurfRows.Count > 0 && SwarmRows.Count > 1) SurfRows[0].Warning = SwarmRows[1].Warning;
            foreach (ObservableCollection<WildEncounterRow> rod in new[] { OldRodRows, GoodRodRows, SuperRodRows })
                foreach (WildEncounterRow r in rod) r.Warning = null;
            if (SwarmRows.Count > 3 && SwarmRows[3].Warning != null)
            {
                if (OldRodRate > 0 && OldRodRows.Count > 2) OldRodRows[2].Warning = SwarmRows[3].Warning;
                if (GoodRodRate > 0) foreach (int k in new[] { 0, 2, 3 }) if (k < GoodRodRows.Count) GoodRodRows[k].Warning = SwarmRows[3].Warning;
                if (SuperRodRate > 0) foreach (WildEncounterRow r in SuperRodRows) r.Warning = SwarmRows[3].Warning;
            }
            if (SwarmRows.Count > 2 && SwarmRows[2].PokemonIndex <= 0)
            {
                if (GoodRodRate > 0 && GoodRodRows.Count > 3) GoodRodRows[3].Warning = "The night fishing Pokémon is empty: at night this slot gives a glitch Pokémon.";
                if (SuperRodRate > 0 && SuperRodRows.Count > 1) SuperRodRows[1].Warning = "The night fishing Pokémon is empty: at night this slot gives a glitch Pokémon.";
            }

            const string Swarm = "A swarm here replaces this slot.";
            for (int i = 0; i < MorningRows.Count; i++)
                (MorningRows[i].Note, MorningRows[i].NoteTip) = i switch
                {
                    0 or 1 => ("Swarm", "A walking swarm here replaces this slot at every time of day."),
                    2 or 3 => ("Radio 1", "Hoenn or Sinnoh Sound on the radio puts its first Pokémon here."),
                    4 or 5 => ("Radio 2", "Hoenn or Sinnoh Sound on the radio puts its second Pokémon here."),
                    _ => (null, null),
                };
            for (int i = 0; i < SurfRows.Count; i++) (SurfRows[i].Note, SurfRows[i].NoteTip) = i == 0 ? ("Swarm", Swarm) : (null, null);
            for (int i = 0; i < OldRodRows.Count; i++) (OldRodRows[i].Note, OldRodRows[i].NoteTip) = i == 2 ? ("Swarm", Swarm) : (null, null);
            const string Night = "At night the night fishing Pokémon replaces this slot; a swarm here wins over it.";
            for (int i = 0; i < GoodRodRows.Count; i++)
                (GoodRodRows[i].Note, GoodRodRows[i].NoteTip) = i == 3 ? ("Night swarm", Night) : i == 0 || i == 2 ? ("Swarm", Swarm) : (null, null);
            for (int i = 0; i < SuperRodRows.Count; i++)
                (SuperRodRows[i].Note, SuperRodRows[i].NoteTip) = i == 1 ? ("Night swarm", Night) : ("Swarm", Swarm);

            string[] swarm = { "Walking 1, 2", "Surf 1", "Good 4, Super 2", "Old 3, Good 1, 3, 4, all Super" };
            string[] swarmTips =
            {
                "During a swarm here it replaces walking slots 1 and 2 at every time of day.",
                "During a swarm here it replaces the first surf slot.",
                "At night it replaces Good Rod slot 4 and Super Rod slot 2, unless a fishing swarm is on.",
                "During a swarm here it replaces these fishing slots.",
            };
            for (int i = 0; i < SwarmRows.Count && i < swarm.Length; i++) (SwarmRows[i].Note, SwarmRows[i].NoteTip) = (swarm[i], swarmTips[i]);
            for (int i = 0; i < HoennRadioRows.Count; i++)
                (HoennRadioRows[i].Note, HoennRadioRows[i].NoteTip) = (i == 0 ? "Walking 3, 4" : "Walking 5, 6", "While the radio plays Hoenn Sound.");
            for (int i = 0; i < SinnohRadioRows.Count; i++)
                (SinnohRadioRows[i].Note, SinnohRadioRows[i].NoteTip) = (i == 0 ? "Walking 3, 4" : "Walking 5, 6", "While the radio plays Sinnoh Sound.");
        }
    }
}
