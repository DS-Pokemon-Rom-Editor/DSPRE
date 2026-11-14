using Avalonia.Controls;
using DSPRE.Editors;
using DSPRE.HgEngine;
using DSPRE.ROMFiles;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Items
{
    public partial class ItemTableEditorViewModel
    {

        public bool ShowRockSmashTab { get; }
        public bool ShowRockSmashItemTables { get; private set; }
        public ObservableCollection<RockSmashHeaderRow> RockSmashRows { get; } = new();
        public RockSmashItemSlotsRow RockSmashDefaultTable { get; private set; }
        public RockSmashItemSlotsRow RockSmashRuinsOfAlphTable { get; private set; }
        public RockSmashItemSlotsRow RockSmashCliffCaveTable { get; private set; }

        public string RockSmashCount => $"{RockSmashShown.Count} of {RockSmashRows.Count} headers";

        /// <summary>The headers the list shows. RockSmashRows keeps every header.</summary>
        public ObservableCollection<RockSmashHeaderRow> RockSmashShown { get; } = new();

        private string _rockSmashSearch = "";
        public string RockSmashSearch { get => _rockSmashSearch; set { if (_rockSmashSearch == (value ?? "")) return; _rockSmashSearch = value ?? ""; OnPropertyChanged(); FilterRockSmash(); } }

        private bool _rockSmashOnlyWithOdds = true;
        public bool RockSmashOnlyWithOdds { get => _rockSmashOnlyWithOdds; set { if (_rockSmashOnlyWithOdds == value) return; _rockSmashOnlyWithOdds = value; OnPropertyChanged(); FilterRockSmash(); } }

        // Filtered only when the search or the toggle changes, so a row being edited to 0 stays in view.
        private void FilterRockSmash()
        {
            RockSmashShown.Clear();
            string search = _rockSmashSearch.Trim();
            foreach (RockSmashHeaderRow r in RockSmashRows)
            {
                if (_rockSmashOnlyWithOdds && r.Odds == 0) continue;
                if (search.Length > 0 && !SearchMatch.Contains(r.HeaderLabel, search)) continue;
                RockSmashShown.Add(r);
            }
            OnPropertyChanged(nameof(RockSmashCount));
        }

        private void LoadRockSmash()
        {
            RockSmashRows.Clear();
            int headerCount = RomInfo.GetHeaderCount();
            for (int i = 0; i < headerCount; i++)
            {
                RockSmashData data = new RockSmashData((ushort)i);
                string name = i < _headerNames.Length ? _headerNames[i] : $"Header {i}";
                RockSmashRows.Add(new RockSmashHeaderRow(data, name, SetRockSmashDirty));
            }
            FilterRockSmash();

            if (ShowRockSmashItemTables)
            {
                try { LoadRockSmashItemTables(); }
                // When DSPRE can't read the hg-engine source, the tables are hidden so no wrong ones show.
                catch (InvalidOperationException e)
                {
                    AppLogger.Error("Item Tables, Rock Smash items: " + e.Message);
                    ShowRockSmashItemTables = false;
                    OnPropertyChanged(nameof(ShowRockSmashItemTables));
                }
            }
        }

        private void LoadRockSmashItemTables()
        {
            RockSmashDefaultTable = new RockSmashItemSlotsRow("Default",
                RockSmashItemSlots.Read(RockSmashItemSlots.DefaultOffset), ItemNames, SetRockSmashDirty);
            RockSmashRuinsOfAlphTable = new RockSmashItemSlotsRow("Ruins of Alph",
                RockSmashItemSlots.Read(RockSmashItemSlots.RuinsOfAlphOffset), ItemNames, SetRockSmashDirty);
            RockSmashCliffCaveTable = new RockSmashItemSlotsRow("Cliff Cave",
                RockSmashItemSlots.Read(RockSmashItemSlots.CliffCaveOffset), ItemNames, SetRockSmashDirty);

            OnPropertyChanged(nameof(RockSmashDefaultTable));
            OnPropertyChanged(nameof(RockSmashRuinsOfAlphTable));
            OnPropertyChanged(nameof(RockSmashCliffCaveTable));
        }

        private void BuildRockSmashDummyRows()
        {
            for (int i = 0; i < 6; i++)
                RockSmashRows.Add(new RockSmashHeaderRow(new RockSmashData((ushort)i, ""), $"Route {i}", SetRockSmashDirty));

            ushort[] Dummy() => new ushort[] { 1, 2, 3, 4, 5, 6, 7, 8 };
            RockSmashDefaultTable      = new RockSmashItemSlotsRow("Default", Dummy(), ItemNames, SetRockSmashDirty);
            RockSmashRuinsOfAlphTable  = new RockSmashItemSlotsRow("Ruins of Alph", Dummy(), ItemNames, SetRockSmashDirty);
            RockSmashCliffCaveTable    = new RockSmashItemSlotsRow("Cliff Cave", Dummy(), ItemNames, SetRockSmashDirty);
        }
    }
}
