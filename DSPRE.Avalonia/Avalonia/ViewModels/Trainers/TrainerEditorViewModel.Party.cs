using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using global::Avalonia.Controls;
using global::Avalonia.Media.Imaging;
using global::Avalonia.Platform.Storage;
using DSPRE.Avalonia;
using DSPRE.Editors;
using DSPRE.HgEngine;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;
using Avalonia.Threading;

namespace DSPRE.Avalonia.ViewModels.Trainers
{
    public partial class TrainerEditorViewModel
    {
        public void ReorderParty(List<int> newOrder)
        {
            int count = newOrder.Count;
            if (count == 0) return;
            List<TrainerPartyMonViewModel.State> snap = new List<TrainerPartyMonViewModel.State>();
            for (int i = 0; i < count && i < Party.Count; i++) snap.Add(Party[i].Capture());
            for (int i = 0; i < count && i < Party.Count; i++)
            {
                int from = newOrder[i];
                if (from < 0 || from >= snap.Count) continue;
                Party[i].Restore(snap[from]);
            }
            SetDirty();
        }

        public void GoToTrainer(int index)
        {
            if (index >= 0 && index < TrainerNames.Count) SelectedTrainerIndex = index;
        }

        public (ushort trainerId, byte trainerClass, List<(int pokeId, int level, int gender, int ability, int dv)> party) GetDVCalcInput()
        {
            List<(int, int, int, int, int)> list = new List<(int, int, int, int, int)>();
            int count = (int)_partyCount;
            for (int i = 0; i < count && i < Party.Count; i++)
            {
                TrainerPartyMonViewModel m = Party[i];
                // The calculator takes vanilla flags. hg-engine feeds abilitySlot to that routine: 0x02 is female.
                int ability = m.AbilityIndex, gender = m.GenderIndex;
                if (IsHgeActive)
                {
                    ability = m.AbilityIndex == 1 ? 2 : 0;
                    gender = m.AbilityIndex == 2 ? 2 : 0;
                }
                list.Add((m.SpeciesIndex, (int)m.Level, gender, ability, (int)m.Difficulty));
            }
            ushort tid = _trainer?.Trp.TrainerID ?? (ushort)Math.Max(0, _selectedTrainerIndex);
            return (tid, (byte)Math.Max(0, _trainerClassIndex), list);
        }

        // Ticking custom moves otherwise saves four empty moves, which the game gives the Pokémon.
        private void FillLevelUpMoves()
        {
            if (!IsHgeActive)
            {
                try { DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.Learnsets }); }
                catch (Exception ex) { AppLogger.Error("Learnsets couldn't be unpacked: " + ex.Message); return; }
            }
            for (int i = 0; i < (int)_partyCount && i < Party.Count; i++)
            {
                TrainerPartyMonViewModel mon = Party[i];
                if (mon.SpeciesIndex <= 0 || (mon.FormId > 0 && !IsHgeActive) || mon.Move1 > 0 || mon.Move2 > 0 || mon.Move3 > 0 || mon.Move4 > 0) continue;   // vanilla learnsets are per species, so a vanilla form is left to fill by hand
                try
                {
                    ushort[] moves = LevelUpMoves(mon);
                    if (moves == null) continue;
                    mon.Move1 = moves[0]; mon.Move2 = moves[1]; mon.Move3 = moves[2]; mon.Move4 = moves[3];
                }
                catch (Exception ex) { AppLogger.Error($"Level-up moves for species {mon.SpeciesIndex} couldn't be read: {ex.Message}"); }
            }
        }

        // The same pick as a learnset file, from learnsets.json, not the last build.
        private static ushort[] HgeLevelUpMoves(int speciesId, int level)
        {
            if (!HgEngineLearnsets.TryGetLevelMoves(speciesId, out List<(int level, int move)> list, out string error))
            {
                AppLogger.Error($"Level-up moves for species {speciesId} couldn't be read: {error}");
                return null;
            }
            return new LearnsetData(list).GetLearnsetAtLevel(level);
        }

        // An unset ability or ball would be written as ABILITY_NONE or ITEM_NONE.
        private void FillHgeDefaults(string flagName)
        {
            if (flagName == "TRAINER_DATA_TYPE_MOVES") { FillLevelUpMoves(); return; }
            int pokeBall = HgEngineSymbolTable.Load(ItemHeader)?.TryGetValue("ITEM_POKE_BALL", out int ball) == true ? ball : 4;
            for (int i = 0; i < (int)_partyCount && i < Party.Count; i++)
            {
                TrainerPartyMonHgeExtras extras = Party[i].HgeExtras;
                if (flagName == "TRAINER_DATA_TYPE_ABILITY" && extras.AbilityId <= 0) extras.AbilityId = Party[i].SlotAbilityId();
                if (flagName == "TRAINER_DATA_TYPE_BALL" && extras.BallId <= 0) extras.BallId = pokeBall;
            }
        }
    }
}
