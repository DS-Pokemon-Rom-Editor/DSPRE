using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Pokemon
{
    public class WildEditorDPPtViewModel : WildEditorViewModel<EncounterFileDPPt>
    {
        // ── Walking encounters (12 slots) ─────────────────────────────────
        public ObservableCollection<WildEncounterRow> WalkingRows { get; } = new();
        public ObservableCollection<WildTimeRow> WalkingSlots { get; } = new();

        // ── Time-specific (day/night, 2 each) ────────────────────────────
        public ObservableCollection<WildEncounterRow> DayRows    { get; } = new();
        public ObservableCollection<WildEncounterRow> NightRows  { get; } = new();
        public ObservableCollection<WildEncounterRow> SwarmRows  { get; } = new();

        // ── Dual slot / Radar ─────────────────────────────────────────────
        public ObservableCollection<WildEncounterRow> RadarRows      { get; } = new();
        public ObservableCollection<WildEncounterRow> RubyRows       { get; } = new();
        public ObservableCollection<WildEncounterRow> SapphireRows   { get; } = new();
        public ObservableCollection<WildEncounterRow> EmeraldRows    { get; } = new();
        public ObservableCollection<WildEncounterRow> FireRedRows    { get; } = new();
        public ObservableCollection<WildEncounterRow> LeafGreenRows  { get; } = new();

        // ── Form data ─────────────────────────────────────────────────────
        public ObservableCollection<string> ShellosFormNames { get; } = new() { "West Sea", "East Sea" };
        /// <summary>Which Unown letters appear here. </summary>
        public ObservableCollection<string> UnownTableNames  { get; } = new()
        {
            "Most forms (0)",
            "Most forms", "Only F", "Only R", "Only I", "Only N",
            "Only E", "Only D", "! and ?"
        };

        // Stored as a percentage the game only tests for zero, so the original number is kept unless the form changes.
        private uint _shellosRaw, _gastrodonRaw;

        private int _shellosFormIndex;
        public int ShellosFormIndex
        {
            get => _shellosFormIndex;
            set { if (Set(ref _shellosFormIndex, value) && !Loading) { Current.FormRates[0] = EastWest(value, _shellosRaw); SetDirty(); } }
        }

        private int _gastrodonFormIndex;
        public int GastrodonFormIndex
        {
            get => _gastrodonFormIndex;
            set { if (Set(ref _gastrodonFormIndex, value) && !Loading) { Current.FormRates[1] = EastWest(value, _gastrodonRaw); SetDirty(); } }
        }

        /// <summary>West sea is zero; east sea is anything else, so a number already there is kept.</summary>
        private static uint EastWest(int index, uint had) =>
            index == 0 ? 0u : (had != 0 ? had : 1u);

        private int _unownTableIndex;
        public int UnownTableIndex
        {
            get => _unownTableIndex;
            set { if (Set(ref _unownTableIndex, value) && !Loading && value >= 0 && value < KnownUnownTables) { Current.UnownTable = (uint)value; SetDirty(); } }
        }

        // Past these, an extra row names the stored table.
        private const int KnownUnownTables = 9;

        private WildEditorDPPtViewModel(string[] pokemonNames, int totalHeaders) : base(pokemonNames, totalHeaders) { }

        public static WildEditorDPPtViewModel Open(string[] pokemonNames, int encToOpen, int totalHeaders)
        {
            WildEditorDPPtViewModel vm = new WildEditorDPPtViewModel(pokemonNames, totalHeaders);
            vm.LoadFirst(encToOpen);
            return vm;
        }

        public WildEditorDPPtViewModel()
        {
            if (!Design.IsDesignMode) return;

            Title = "Wild Pokémon editor DPPt (Preview)";
            for (int i = 0; i < 30; i++) PokemonNames.Add($"Pokémon {i:000}");
            EncounterNames.Add("[0] Route 201"); EncounterNames.Add("[1] Route 202"); EncounterNames.Add("[2] Unused");

            StayQuiet();
            WalkingRate = 25; SurfRate = 10; OldRodRate = 25; GoodRodRate = 50; SuperRodRate = 75;

            string[] walkLabels = Enumerable.Range(0, 12).Select(i => EncounterSlotOdds.SlotLabel("Walking", i)).ToArray();
            for (int i = 0; i < 12; i++)
                WalkingRows.Add(new WildEncounterRow(PokemonNames, null) { Label = walkLabels[i], PokemonIndex = i % PokemonNames.Count, Level = 5 });
            for (int i = 0; i < 2; i++)
            {
                DayRows.Add(new WildEncounterRow(PokemonNames, null) { Label = $"Day {i + 1}",    PokemonIndex = i % PokemonNames.Count, Level = 10 });
                NightRows.Add(new WildEncounterRow(PokemonNames, null) { Label = $"Night {i + 1}",  PokemonIndex = i % PokemonNames.Count, Level = 10 });
                SwarmRows.Add(new WildEncounterRow(PokemonNames, null) { Label = $"Swarm {i + 1}",  PokemonIndex = i % PokemonNames.Count, Level = 15 });
                RadarRows.Add(new WildEncounterRow(PokemonNames, null) { Label = $"Radar {i + 1}",  PokemonIndex = i % PokemonNames.Count, Level = 20 });
                RubyRows.Add(new WildEncounterRow(PokemonNames, null) { Label = $"Ruby {i + 1}",   PokemonIndex = i % PokemonNames.Count, Level = 20 });
                SapphireRows.Add(new WildEncounterRow(PokemonNames, null) { Label = $"Sapphire {i + 1}", PokemonIndex = i % PokemonNames.Count, Level = 20 });
                EmeraldRows.Add(new WildEncounterRow(PokemonNames, null) { Label = $"Emerald {i + 1}",  PokemonIndex = i % PokemonNames.Count, Level = 20 });
                FireRedRows.Add(new WildEncounterRow(PokemonNames, null) { Label = $"FR {i + 1}",        PokemonIndex = i % PokemonNames.Count, Level = 20 });
                LeafGreenRows.Add(new WildEncounterRow(PokemonNames, null) { Label = $"LG {i + 1}",       PokemonIndex = i % PokemonNames.Count, Level = 20 });
            }
            for (int i = 0; i < 5; i++)
            {
                SurfRows.Add(new WildEncounterRow(PokemonNames, null) { Label = $"Surf {i + 1}",     PokemonIndex = i % PokemonNames.Count, MinLevel = 20, MaxLevel = 30 });
                OldRodRows.Add(new WildEncounterRow(PokemonNames, null) { Label = $"Old Rod {i + 1}",  PokemonIndex = i % PokemonNames.Count, MinLevel = 5,  MaxLevel = 10 });
                GoodRodRows.Add(new WildEncounterRow(PokemonNames, null) { Label = $"Good Rod {i + 1}", PokemonIndex = i % PokemonNames.Count, MinLevel = 10, MaxLevel = 20 });
                SuperRodRows.Add(new WildEncounterRow(PokemonNames, null) { Label = $"Super Rod {i + 1}",PokemonIndex = i % PokemonNames.Count, MinLevel = 30, MaxLevel = 40 });
            }
        }

        protected override EncounterFileDPPt LoadRecord(byte[] bytes, int id, out string error)
        {
            error = null;
            return new EncounterFileDPPt(new MemoryStream(bytes ?? throw new FileNotFoundException($"Encounter file {id} is missing.")));
        }

        protected override EncounterFileDPPt Parse(Stream stream) => new EncounterFileDPPt(stream);

        protected override int EncounterFileOf(MapHeader header, out int locationName)
        {
            locationName = GameFamily == GameFamilies.DP ? ((HeaderDP)header).LocationName : ((HeaderPt)header).LocationName;
            return header.WildPokemon == NullEncounterID ? -1 : header.WildPokemon;
        }

        // Special slots replace walking ones, so the Special tab needs a walking rate.
        protected override bool SpecialTabHasEncounters => Current.WalkingRate > 0;

        protected override IEnumerable<ObservableCollection<WildEncounterRow>> EditableRows => new[]
        {
            WalkingRows, DayRows, NightRows, SwarmRows, RadarRows, RubyRows, SapphireRows, EmeraldRows, FireRedRows, LeafGreenRows,
            SurfRows, OldRodRows, GoodRodRows, SuperRodRows,
        };

        protected override bool SpeciesFeedsNotes(ObservableCollection<WildEncounterRow> rows) => rows != WalkingRows;

        protected override void CollectProblems(List<string> problems)
        {
            WildEncounterChecks.Check(problems, "Walking", WalkingRate, WalkingRows, false);
            foreach (ObservableCollection<WildEncounterRow> rows in new[] { WalkingRows, SwarmRows, RadarRows, RubyRows, SapphireRows, EmeraldRows, FireRedRows, LeafGreenRows })
                foreach (WildEncounterRow r in rows) if (r.Warning != null) problems.Add(r.Warning);
            WildEncounterChecks.Check(problems, "Surf", SurfRate, SurfRows, true);
            WildEncounterChecks.Check(problems, "Old Rod", OldRodRate, OldRodRows, true);
            WildEncounterChecks.Check(problems, "Good Rod", GoodRodRate, GoodRodRows, true);
            WildEncounterChecks.Check(problems, "Super Rod", SuperRodRate, SuperRodRows, true);
        }

        public override void RefreshSlotLabels()
        {
            for (int i = 0; i < WalkingRows.Count; i++) WalkingRows[i].Label = EncounterSlotOdds.SlotLabel("Walking", i);
            base.RefreshSlotLabels();
        }

        protected override void PopulateFamilyRows()
        {
            // Form data: formRates[0]=Shellos, [1]=Gastrodon (0=West, anything else=East)
            // unownTable: 1..8 are the letter tables and 0 reads as 1, so the list index is the value.
            _shellosRaw = Current.FormRates[0];
            _gastrodonRaw = Current.FormRates[1];
            _shellosFormIndex  = (int)(Current.FormRates[0] == 0 ? 0 : 1);
            OnPropertyChanged(nameof(ShellosFormIndex));
            _gastrodonFormIndex = (int)(Current.FormRates[1] == 0 ? 0 : 1);
            OnPropertyChanged(nameof(GastrodonFormIndex));
            while (UnownTableNames.Count > KnownUnownTables) UnownTableNames.RemoveAt(KnownUnownTables);
            if (Current.UnownTable >= KnownUnownTables) UnownTableNames.Add($"Table {Current.UnownTable}");
            _unownTableIndex = (int)Math.Min(Current.UnownTable, KnownUnownTables);
            OnPropertyChanged(nameof(UnownTableIndex));

            string[] walkLabels = Enumerable.Range(0, 12).Select(i => EncounterSlotOdds.SlotLabel("Walking", i)).ToArray();
            SyncRows(WalkingRows,  12, i => walkLabels[i], i => (int)Current.WalkingPokemon[i], i => Current.WalkingLevels[i], null, null, false);
            SyncRows(DayRows,       2, i => $"Day {i+1}",   i => (int)Current.DayPokemon[i],     _ => 0,                         null, null, false);
            SyncRows(NightRows,     2, i => $"Night {i+1}", i => (int)Current.NightPokemon[i],   _ => 0,                         null, null, false);
            SyncRows(SwarmRows,     2, i => $"Swarm {i+1}", i => (int)Current.SwarmPokemon[i],   _ => 0,                         null, null, false);
            SyncRows(RadarRows,     4, i => $"Radar {i+1}", i => (int)Current.RadarPokemon[i],   _ => 0,                         null, null, false);
            SyncRows(RubyRows,      2, i => $"Ruby {i+1}",      i => (int)Current.RubyPokemon[i],      _ => 0, null, null, false);
            SyncRows(SapphireRows,  2, i => $"Sapphire {i+1}",  i => (int)Current.SapphirePokemon[i],  _ => 0, null, null, false);
            SyncRows(EmeraldRows,   2, i => $"Emerald {i+1}",   i => (int)Current.EmeraldPokemon[i],   _ => 0, null, null, false);
            SyncRows(FireRedRows,   2, i => $"FireRed {i+1}",   i => (int)Current.FireRedPokemon[i],   _ => 0, null, null, false);
            SyncRows(LeafGreenRows, 2, i => $"LeafGreen {i+1}", i => (int)Current.LeafGreenPokemon[i], _ => 0, null, null, false);
            if (WalkingSlots.Count == 0)
                for (int i = 0; i < 12; i++)
                    WalkingSlots.Add(new WildTimeRow(WalkingRows[i], i is 2 or 3 ? DayRows[i - 2] : null, i is 2 or 3 ? NightRows[i - 2] : null));
        }

        protected override void WriteFamilyRowsToFile()
        {
            for (int i = 0; i < WalkingRows.Count && i < 12; i++)
            {
                Current.WalkingPokemon[i] = (uint)WalkingRows[i].PokemonIndex;
                Current.WalkingLevels[i]  = (byte)WalkingRows[i].Level;
            }
            for (int i = 0; i < DayRows.Count   && i < 2; i++) Current.DayPokemon[i]   = (uint)DayRows[i].PokemonIndex;
            for (int i = 0; i < NightRows.Count  && i < 2; i++) Current.NightPokemon[i] = (uint)NightRows[i].PokemonIndex;
            for (int i = 0; i < SwarmRows.Count  && i < 2; i++) Current.SwarmPokemon[i] = (ushort)SwarmRows[i].PokemonIndex;
            for (int i = 0; i < RadarRows.Count  && i < 4; i++) Current.RadarPokemon[i] = (uint)RadarRows[i].PokemonIndex;
            for (int i = 0; i < RubyRows.Count   && i < 2; i++) Current.RubyPokemon[i]      = (uint)RubyRows[i].PokemonIndex;
            for (int i = 0; i < SapphireRows.Count && i < 2; i++) Current.SapphirePokemon[i] = (uint)SapphireRows[i].PokemonIndex;
            for (int i = 0; i < EmeraldRows.Count  && i < 2; i++) Current.EmeraldPokemon[i]  = (uint)EmeraldRows[i].PokemonIndex;
            for (int i = 0; i < FireRedRows.Count   && i < 2; i++) Current.FireRedPokemon[i]  = (uint)FireRedRows[i].PokemonIndex;
            for (int i = 0; i < LeafGreenRows.Count && i < 2; i++) Current.LeafGreenPokemon[i] = (uint)LeafGreenRows[i].PokemonIndex;

            Current.FormRates[0] = EastWest(_shellosFormIndex, _shellosRaw);
            Current.FormRates[1] = EastWest(_gastrodonFormIndex, _gastrodonRaw);
            // The extra row keeps the stored table.
            if (_unownTableIndex < KnownUnownTables) Current.UnownTable = (uint)_unownTableIndex;
        }

        // Walking slots (from 0) each method replaces: day/night 2-3, swarm 0-1, Trophy Garden and Great Marsh 6-7, Dual Slot 8-9, Radar 4-5 and 10-11.
        protected override void SetNotes()
        {
            for (int i = 0; i < WalkingRows.Count; i++)
                (WalkingRows[i].Note, WalkingRows[i].NoteTip) = i switch
                {
                    0 or 1 => ("Swarm", "During a swarm here the swarm Pokémon replaces this slot."),
                    4 or 5 or 10 or 11 => ("Poké Radar", "A shaking patch found with the Poké Radar can give the radar Pokémon here."),
                    6 or 7 => ("Trophy Garden, Great Marsh", "The day's Pokémon replaces this slot in the Trophy Garden, and in the Great Marsh during a Safari Game."),
                    8 or 9 => ("Dual Slot", "A GBA game in the second slot puts its Pokémon here, once the National Pokédex is obtained."),
                    _ => (null, null),
                };
            (string Game, ObservableCollection<WildEncounterRow> Rows)[] gba =
                { ("Ruby", RubyRows), ("Sapphire", SapphireRows), ("Emerald", EmeraldRows), ("FireRed", FireRedRows), ("LeafGreen", LeafGreenRows) };
            for (int i = 0; i < WalkingRows.Count; i++)
                WalkingRows[i].Replacements = i switch
                {
                    0 or 1 => Reps(Rep(SwarmRows, i, "Swarm")),
                    4 or 5 => Reps(Rep(RadarRows, i - 4, "Poké Radar")),
                    10 or 11 => Reps(Rep(RadarRows, i - 8, "Poké Radar")),
                    8 or 9 => Reps(gba.Select(g => Rep(g.Rows, i - 8, "Dual Slot " + g.Game)).ToArray()),
                    _ => null,
                };
            // The game copies these in without checking for an empty slot, so an empty one makes a glitch Pokémon.
            bool walks = WalkingRate > 0;
            foreach (WildEncounterRow r in WalkingRows) r.Warning = null;
            for (int i = 0; i < SwarmRows.Count; i++)
                SwarmRows[i].Warning = walks && SwarmRows[i].PokemonIndex <= 0 ? $"Empty: if a swarm comes here, walking slot {i + 1} gives a glitch Pokémon." : null;
            for (int i = 0; i < DayRows.Count && i + 2 < WalkingRows.Count; i++)
            {
                List<string> times = new List<string>();
                if (walks && DayRows[i].PokemonIndex <= 0) times.Add("day");
                if (walks && NightRows.Count > i && NightRows[i].PokemonIndex <= 0) times.Add("night");
                WalkingRows[i + 2].Warning = times.Count > 0 ? $"The {string.Join(" and ", times)} Pokémon {(times.Count == 1 ? "is" : "are")} empty, so at {(times.Count == 1 ? "that time" : "those times")} this slot gives a glitch Pokémon." : null;
            }
            int[] radarSlots = { 5, 6, 11, 12 };
            for (int i = 0; i < RadarRows.Count && i < radarSlots.Length; i++)
                RadarRows[i].Warning = walks && RadarRows[i].PokemonIndex <= 0 ? $"Empty: in a Poké Radar patch, walking slot {radarSlots[i]} gives a glitch Pokémon." : null;
            foreach ((string game, ObservableCollection<WildEncounterRow> rows) in gba)
                for (int i = 0; i < rows.Count; i++)
                    rows[i].Warning = walks && rows[i].PokemonIndex <= 0 ? $"Empty: with {game} in the second slot, walking slot {i + 9} gives a glitch Pokémon." : null;
            for (int i = 0; i < SwarmRows.Count; i++) (SwarmRows[i].Note, SwarmRows[i].NoteTip) = ($"Walking {i + 1}", "During a swarm here.");
            for (int i = 0; i < RadarRows.Count && i < radarSlots.Length; i++) (RadarRows[i].Note, RadarRows[i].NoteTip) = ($"Walking {radarSlots[i]}", "In a shaking patch found with the Poké Radar.");
            foreach (ObservableCollection<WildEncounterRow> game in new[] { RubyRows, SapphireRows, EmeraldRows, FireRedRows, LeafGreenRows })
                for (int i = 0; i < game.Count; i++) (game[i].Note, game[i].NoteTip) = ($"Walking {i + 9}", "With that game in the second slot, once the National Pokédex is obtained.");
        }
    }
}
