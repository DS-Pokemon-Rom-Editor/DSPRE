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
        public void Save()
        {
            if (_selectedTrainerIndex < 0) return;

            if (IsHgeActive)
            {
                DialogHelper.Watch(SaveHgEngineAsync());
                return;
            }

            if (_trainer == null) return;

            // The game reads every record up to the party count, so a blank one would battle as species 0.
            for (int i = 0; i < (int)_partyCount && i < Party.Count; i++)
            {
                if (Party[i].SpeciesIndex > 0) continue;
                string error = $"Party slot {i + 1} has no Pokémon.";
                StatusText = $"Trainer {_selectedTrainerIndex} wasn't saved: {error}";
                _ = DialogHelper.ShowErrorAsync($"Trainer {_selectedTrainerIndex} wasn't saved:\n{error}", "Trainer editor");
                return;
            }

            SyncToTrainer();
            bool listChanged = _savedName != _trainerName || _savedClass != _trainer.Trp.TrainerClass;

            TrainerRecords.Save(_selectedTrainerIndex, _trainer.Trp, _trainer.Party);

            if (UpdateTrainerName(_trainerName) is string nameError)
            {
                // The record is written; the name stays unsaved so Save or the close prompt tries it again.
                StatusText = $"Trainer {_selectedTrainerIndex}'s name wasn't saved.";
                _ = DialogHelper.ShowErrorAsync($"Trainer {_selectedTrainerIndex}'s name wasn't saved:\n{nameError}", "Trainer editor");
                return;
            }

            _savedName = _trainerName;
            _savedClass = _trainer.Trp.TrainerClass;
            if (listChanged)
            {
                RefreshTrainerListEntry(_selectedTrainerIndex);
                AppEvents.RaiseNamesChanged();
            }

            string savedLabel = TrainerLabel;
            SetClean();
            SaveNotice.Saved(savedLabel);
            _history.MarkSaved();
            RaiseUndoState();
            StatusText = $"Trainer {_selectedTrainerIndex} saved.";
        }

        async Task<bool> IEditorWithUnsavedChanges.SaveChangesAsync()
        {
            if (_dirty)
            {
                if (IsHgeActive) await SaveHgEngineAsync();
                else Save();
            }
            if (_classes?.HasUnsavedChanges == true) _classes.Save();
            return !HasUnsavedChanges;
        }

        private async Task SaveHgEngineAsync()
        {
            int trainer = _selectedTrainerIndex;
            if (trainer < 0) return;
            (bool saved, string error) = await HgEngineSave.RunAsync(WriteHgEngineSource);
            if (!saved)
            {
                if (error == null) return;
                StatusText = $"Trainer {trainer} wasn't saved: {error}";
                await DialogHelper.ShowErrorAsync($"Trainer {trainer} wasn't saved:\n{error}", "Trainer editor");
                return;
            }

            RefreshTrainerListEntry(trainer);
            AppEvents.RaiseNamesChanged();   // other editors list trainer names from Trainers.c too
            if (trainer != _selectedTrainerIndex) return;
            string savedLabel = TrainerLabel;
            SetClean();
            SaveNotice.Saved(savedLabel);
            _history.MarkSaved();
            RaiseUndoState();
            StatusText = $"Trainer {trainer} saved to hg-engine source.";
        }

        // Returns why it could not write, or null. partyMonCount and textCount are left to trainerdatagen.
        private string WriteHgEngineSource()
        {
            if (!HgEngineProject.IsActive) return "No hg-engine checkout is linked.";
            string blocker = PrepareHgeWrite();
            if (blocker != null) return blocker;

            List<HgEngineFieldWrite> fields = TrainerFieldWrites();
            IReadOnlyList<HgEngineTrainerFieldSchema.NamedFlag> abilitySlots = HgEngineTrainerFieldSchema.GetAbilitySlots();
            for (int i = 0; i < (int)_partyCount && i < Party.Count; i++) AddMonFieldWrites(fields, i, abilitySlots);

            FieldPathSegment[] partyPath = new[] { FieldPathSegment.Field("party") };
            List<HgEngineArrayCount> arrayCounts = new List<HgEngineArrayCount>();
            if ((int)_partyCount == 0) fields.Add(HgEngineFieldWrite.Remove(partyPath));
            else arrayCounts.Add(new HgEngineArrayCount(partyPath, (int)_partyCount, NewPartyEntry));
            if (!HgEngineWriter.TryWriteFields(HgEngineDomain.Trainers, _selectedTrainerIndex, fields, out List<string> unresolved, out string error,
                    allowInsert: true, arrayCounts: arrayCounts, allOrNothing: true))
            {
                AppLogger.Error($"hg-engine write failed for trainer {_selectedTrainerIndex}: {error}");
                return error;
            }
            if (unresolved.Count > 0)
            {
                string missing = string.Join(", ", unresolved);
                AppLogger.Error($"hg-engine write for trainer {_selectedTrainerIndex} couldn't place {missing}");
                return $"Trainers.c has no place for {missing}.";
            }
            return null;
        }

        private string PrepareHgeWrite()
        {
            // trainerdatagen stops the build at a gap in the party.
            for (int i = 0; i < (int)_partyCount && i < Party.Count; i++)
                if (Party[i].SpeciesIndex == 0) return $"Party slot {i + 1} has no Pokémon.";

            if (HgeAbilityFlagChecked) FillHgeDefaults("TRAINER_DATA_TYPE_ABILITY");
            if (HgeBallFlagChecked) FillHgeDefaults("TRAINER_DATA_TYPE_BALL");
            if (HgeMovesFlagChecked)
            {
                FillLevelUpMoves();
                // The game sets exactly these moves, so four empty ones leave the Pokémon with none.
                for (int i = 0; i < (int)_partyCount && i < Party.Count; i++)
                    if (Party[i].Move1 <= 0 && Party[i].Move2 <= 0 && Party[i].Move3 <= 0 && Party[i].Move4 <= 0)
                        return $"Party slot {i + 1} has no moves.";
            }
            return null;
        }

        private static string ClassSymbol(int classIndex) =>
            HgEngineSymbolTable.Load(TrainerClassHeader)?.TryGetNameWithPrefix(classIndex, "TRAINERCLASS_", out string n) == true ? n : classIndex.ToString();
        // -1 as a u16 is 0xFFFF, which the game takes as a real id.
        private static string ItemSymbol(int itemId) => itemId <= 0 ? "ITEM_NONE" :
            HgEngineSymbolTable.Load(ItemHeader)?.TryGetNameWithPrefix(itemId, "ITEM_", out string n) == true ? n : itemId.ToString();
        private static string AbilitySymbol(int abilityId) => abilityId <= 0 ? "ABILITY_NONE" :
            HgEngineSymbolTable.Load(AbilityHeader)?.TryGetNameWithPrefix(abilityId, "ABILITY_", out string n) == true ? n : abilityId.ToString();
        private static string MoveSymbol(int moveId) => moveId <= 0 ? "MOVE_NONE" :
            HgEngineSymbolTable.Load(MovesHeader)?.TryGetNameWithPrefix(moveId, "MOVE_", out string n) == true ? n : moveId.ToString();
        private static string NatureSymbol(int nature) =>
            HgEngineSymbolTable.Load(PokemonConstantsHeader)?.TryGetNameWithPrefix(nature, "NATURE_", out string n) == true ? n : nature.ToString();
        private static string FlagsExpr(string header, string prefix, int value) =>
            HgEngineSymbolTable.Load(header)?.TryGetFlagsExpression(value, prefix, out string expr) == true ? expr : value.ToString();

        private List<HgEngineFieldWrite> TrainerFieldWrites()
        {
            int trainerTypeValue = TrainerDataTypeFlags.Where(f => f.Checked).Aggregate(0, (acc, f) => acc | f.Value);
            int aiFlagsValue = AiFlags.Where(f => f.Checked).Aggregate(0, (acc, f) => acc | f.Value);
            string battleTypeLiteral = BattleTypeIndex >= 0 && BattleTypeIndex < BattleTypeOptions.Count
                ? BattleTypeOptions[BattleTypeIndex] : "SINGLE_BATTLE";

            List<HgEngineFieldWrite> fields = new List<HgEngineFieldWrite>
            {
                new(new[] { FieldPathSegment.Field("name") }, HgEngineTrainerSource.ToCStringLiteral(_trainerName ?? "")),
            };
            if (_hgeAiFlagsKnown)
                fields.Add(new(new[] { FieldPathSegment.Field("data"), FieldPathSegment.Field("aiFlags") }, FlagsExpr(TrainerDataHeader, "F_", aiFlagsValue)));
            if (_hgeTrainerTypeKnown)
                fields.Add(new(new[] { FieldPathSegment.Field("data"), FieldPathSegment.Field("trainerType") }, FlagsExpr(TrainerDataHeader, "TRAINER_DATA_TYPE_", trainerTypeValue)));
            if (BattleTypeIndex >= 0 && BattleTypeIndex < BattleTypeOptions.Count)
                fields.Add(new(new[] { FieldPathSegment.Field("data"), FieldPathSegment.Field("battleType") }, battleTypeLiteral));

            if (_trainerClassIndex >= 0)
                fields.Add(new(new[] { FieldPathSegment.Field("data"), FieldPathSegment.Field("trainerClass") }, ClassSymbol(_trainerClassIndex)));

            fields.Add(new(new[] { FieldPathSegment.Field("data"), FieldPathSegment.Field("items") },
                "{ " + string.Join(", ", TrainerItems.Select((t, i) => t.ItemIndex >= 0 ? ItemSymbol(t.ItemIndex) : _hgeItemRaw[i] ?? "ITEM_NONE")) + " }"));
            return fields;
        }

        private void AddMonFieldWrites(List<HgEngineFieldWrite> fields, int i, IReadOnlyList<HgEngineTrainerFieldSchema.NamedFlag> abilitySlots)
        {
            TrainerPartyMonViewModel mon = Party[i];
            TrainerPartyMonHgeExtras extras = mon.HgeExtras;
            FieldPathSegment p = FieldPathSegment.Field("party");
            FieldPathSegment[] Path(params FieldPathSegment[] rest)
            {
                FieldPathSegment[] full = new FieldPathSegment[rest.Length + 2];
                full[0] = p; full[1] = FieldPathSegment.At(i);
                System.Array.Copy(rest, 0, full, 2, rest.Length);
                return full;
            }

            if (mon.SpeciesIndex >= 0)
                fields.Add(new(Path(FieldPathSegment.Field("species")), HgEngineTrainerSource.FormatSpecies(mon.SpeciesIndex, Math.Min(31, (int)mon.FormId), HgEngineSymbolTable.Load(SpeciesHeader))));
            fields.Add(new(Path(FieldPathSegment.Field("level")), mon.Level.ToString()));
            fields.Add(new(Path(FieldPathSegment.Field("ivs")), mon.Difficulty.ToString()));
            fields.Add(new(Path(FieldPathSegment.Field("ballSeal")), mon.BallSeals.ToString()));

            string slotName = mon.AbilityIndex >= 0 && mon.AbilityIndex < TrainerPartyMonViewModel.HgeAbilitySlotNames.Length
                ? TrainerPartyMonViewModel.HgeAbilitySlotNames[mon.AbilityIndex] : null;
            if (slotName != null && abilitySlots.Any(s => s.Name == slotName))
                fields.Add(new(Path(FieldPathSegment.Field("abilitySlot")), slotName));

            // A field whose flag is off is removed, so a slot's old values don't return with the flag.
            void Put(bool on, string field, Func<string> literal)   // unreadable flags leave every field alone
            {
                if (!_hgeTrainerTypeKnown) return;
                fields.Add(on ? new(Path(FieldPathSegment.Field(field)), literal()) : HgEngineFieldWrite.Remove(Path(FieldPathSegment.Field(field))));
            }

            Put(HgeItemsFlagChecked, "item", () => ItemSymbol(mon.ItemIndex));
            Put(HgeMovesFlagChecked, "moves", () => "{ " + string.Join(", ", MoveSymbol(mon.Move1), MoveSymbol(mon.Move2), MoveSymbol(mon.Move3), MoveSymbol(mon.Move4)) + " }");
            Put(HgeAbilityFlagChecked, "ability", () => AbilitySymbol(extras.AbilityId));
            Put(HgeBallFlagChecked, "ball", () => ItemSymbol(extras.BallId));

            string StatBlock(StatBlockViewModel s) =>
                $"{{ .hp = {s.Hp}, .attack = {s.Attack}, .defense = {s.Defense}, .speed = {s.Speed}, .spAttack = {s.SpAttack}, .spDefense = {s.SpDefense} }}";
            Put(HgeIvEvFlagChecked, "setIvs", () => StatBlock(extras.SetIvs));
            Put(HgeIvEvFlagChecked, "setEvs", () => StatBlock(extras.SetEvs));
            Put(HgeNatureFlagChecked, "nature", () => NatureSymbol(extras.NatureIndex));
            Put(HgeShinyLockFlagChecked, "shinyLock", () => extras.ShinyLocked ? "1" : "0");

            bool extra = HgeAdditionalFlagsFlagChecked;
            Put(extra && extras.ExtraStatusEnabled, "status", () => extras.ExtraStatus.ToString());
            Put(extra && extras.ExtraHpEnabled, "hp", () => extras.ExtraHp.ToString());
            Put(extra && extras.ExtraAttackEnabled, "attack", () => extras.ExtraAttack.ToString());
            Put(extra && extras.ExtraDefenseEnabled, "defense", () => extras.ExtraDefense.ToString());
            Put(extra && extras.ExtraSpeedEnabled, "speed", () => extras.ExtraSpeed.ToString());
            Put(extra && extras.ExtraSpAtkEnabled, "spAttack", () => extras.ExtraSpAtk.ToString());
            Put(extra && extras.ExtraSpDefEnabled, "spDefense", () => extras.ExtraSpDef.ToString());
            Put(extra && extras.ExtraPpCountsEnabled, "ppCounts", () => "{ " + string.Join(", ", extras.ExtraPp1, extras.ExtraPp2, extras.ExtraPp3, extras.ExtraPp4) + " }");
            Put(extra && extras.ExtraNicknameEnabled, "nicknameStr", () => HgEngineTrainerSource.ToCStringLiteral(extras.ExtraNickname ?? ""));

            if (_hgeTrainerTypeKnown && !extra)
                fields.Add(HgEngineFieldWrite.Remove(Path(FieldPathSegment.Field("additionalFlags"))));
            else if (_hgeTrainerTypeKnown)
            {
                int extraFlagsValue = 0;
                if (extras.ExtraStatusEnabled) extraFlagsValue |= ExtraBit("STATUS");
                if (extras.ExtraHpEnabled) extraFlagsValue |= ExtraBit("HP");
                if (extras.ExtraAttackEnabled) extraFlagsValue |= ExtraBit("ATK");
                if (extras.ExtraDefenseEnabled) extraFlagsValue |= ExtraBit("DEF");
                if (extras.ExtraSpeedEnabled) extraFlagsValue |= ExtraBit("SPEED");
                if (extras.ExtraSpAtkEnabled) extraFlagsValue |= ExtraBit("SP_ATK");
                if (extras.ExtraSpDefEnabled) extraFlagsValue |= ExtraBit("SP_DEF");
                if (extras.ExtraPpCountsEnabled) extraFlagsValue |= ExtraBit("PP_COUNTS");
                if (extras.ExtraNicknameEnabled) extraFlagsValue |= ExtraBit("NICKNAME");

                fields.Add(new(Path(FieldPathSegment.Field("additionalFlags")), FlagsExpr(TrainerDataHeader, "TRAINER_DATA_EXTRA_TYPE_", extraFlagsValue)));
            }
        }

        private static string NewPartyEntry(int index, string indent) =>
            "{\n" +
            indent + "    .ivs = 0,\n" +
            indent + "    .abilitySlot = TRAINER_POKEMON_ABILITY_1,\n" +
            indent + "    .level = 1,\n" +
            indent + "    .species = SPECIES_NONE,\n" +
            indent + "    .ballSeal = 0,\n" +
            indent + "}";

        private static int ExtraBit(string suffix)
        {
            foreach (HgEngineTrainerFieldSchema.NamedFlag f in HgEngineTrainerFieldSchema.GetExtraFlags()) if (f.Name.EndsWith(suffix)) return f.Bit;
            return 0;
        }
    }
}
