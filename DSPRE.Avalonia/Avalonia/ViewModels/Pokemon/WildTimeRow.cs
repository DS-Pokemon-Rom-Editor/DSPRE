using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DSPRE.Avalonia.ViewModels.Pokemon
{
    /// <summary>One walking slot across the times of day; a time with no Pokémon of its own is null.</summary>
    public sealed class WildTimeRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        public WildEncounterRow Main { get; }
        public WildEncounterRow Day { get; }
        public WildEncounterRow Night { get; }

        public WildTimeRow(WildEncounterRow main, WildEncounterRow day, WildEncounterRow night)
        {
            Main = main;
            Day = day;
            Night = night;
            main.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(WildEncounterRow.Level)) OnPropertyChanged(nameof(Level));
                else if (e.PropertyName == nameof(WildEncounterRow.Label)) OnPropertyChanged(nameof(Label));
                else if (e.PropertyName == nameof(WildEncounterRow.Note) || e.PropertyName == nameof(WildEncounterRow.NoteTip)
                         || e.PropertyName == nameof(WildEncounterRow.Replacements) || e.PropertyName == nameof(WildEncounterRow.Warning))
                    OnPropertyChanged(e.PropertyName);
            };
        }

        public string Label => Main.Label;
        public string Note => Main.Note;
        public string NoteTip => Main.NoteTip;
        public IReadOnlyList<WildEncounterRow.Replacement> Replacements => Main.Replacements;
        public string Warning => Main.Warning;
        public int Level { get => Main.Level; set => Main.Level = value; }
    }

    /// <summary>One HGSS radio slot: the Hoenn and Sinnoh Sound Pokémon replace the same walking slots.</summary>
    public sealed class WildRadioRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        public WildEncounterRow Hoenn { get; }
        public WildEncounterRow Sinnoh { get; }

        public WildRadioRow(WildEncounterRow hoenn, WildEncounterRow sinnoh)
        {
            Hoenn = hoenn;
            Sinnoh = sinnoh;
            foreach (WildEncounterRow r in new[] { hoenn, sinnoh })
                r.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(WildEncounterRow.Label) || e.PropertyName == nameof(WildEncounterRow.Note)
                        || e.PropertyName == nameof(WildEncounterRow.Warning))
                        OnPropertyChanged(e.PropertyName);
                };
        }

        public string Label => Hoenn.Label;
        public string Note => Hoenn.Note;
        public string NoteTip => "While the radio plays Hoenn or Sinnoh Sound.";

        public string Warning => Hoenn.Warning == null ? Sinnoh.Warning
            : Sinnoh.Warning == null ? Hoenn.Warning : Hoenn.Warning + "\n" + Sinnoh.Warning;
    }

    /// <summary>Slots a save should question: ones the game can roll that hold no Pokémon or an impossible level.</summary>
    public static class WildEncounterChecks
    {
        /// <param name="rows">The method's slots, in slot order.</param>
        /// <param name="rate">The method's encounter rate; at zero the game never rolls these slots.</param>
        public static void Check(List<string> problems, string method, int rate, IReadOnlyList<WildEncounterRow> rows, bool range)
        {
            if (rate <= 0) return;
            for (int i = 0; i < rows.Count; i++)
            {
                WildEncounterRow r = rows[i];
                string slot = $"{method} slot {i + 1}";
                if (r.PokemonIndex <= 0) problems.Add($"{slot} has no Pokémon.");
                if (!range && (r.Level < 1 || r.Level > 100)) problems.Add($"{slot} is level {r.Level}.");
                if (range)
                {
                    if (r.MinLevel < 1 || r.MaxLevel < 1 || r.MinLevel > 100 || r.MaxLevel > 100) problems.Add($"{slot} levels {r.MinLevel} to {r.MaxLevel} are outside 1 to 100.");
                    else if (r.MinLevel > r.MaxLevel) problems.Add($"{slot}: the lowest level is above the highest (the game swaps them).");
                }
            }
        }

        /// <summary>Same check for one time of day's species, where the level lives on the main row.</summary>
        public static void CheckSpecies(List<string> problems, string method, int rate, IReadOnlyList<WildEncounterRow> rows)
        {
            if (rate <= 0) return;
            for (int i = 0; i < rows.Count; i++)
                if (rows[i].PokemonIndex <= 0) problems.Add($"{method} slot {i + 1} has no Pokémon.");
        }

        public static string Describe(List<string> problems, int shown = 12)
        {
            List<string> lines = problems.Count > shown ? problems.GetRange(0, shown) : problems;
            string text = string.Join("\n", lines);
            return problems.Count > shown ? text + $"\nand {problems.Count - shown} more" : text;
        }
    }
}
