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
        private void LoadTrainer(int index)
        {
            if (IsHgeActive) { LoadTrainerFromSource(index); return; }
            try
            {
                string[] trNames = GetSimpleTrainerNames();
                bool error = index >= trNames.Length;
                _trainer = TrainerRecords.Load(index, error ? TrainerFile.NAME_NOT_FOUND : trNames[index]);
                _loadedTrainerId = index;
                _savedName = _trainer.Name;
                _savedClass = _trainer.Trp.TrainerClass;

                _suppress = true;
                try { PopulateFromTrainer(); }
                finally { _suppress = false; }

                SetClean();
                _history.Reset(Snapshot());   // loaded state is the clean undo baseline for this trainer
                _lastCaptureUtc = DateTime.MinValue;
                RaiseUndoState();
                StatusText = _statusAfterLoad ?? "";
                _statusAfterLoad = null;
                OnPropertyChanged(nameof(UnsavedChangesDescription));
                LoadBattleLines(index);
                RaisePrize();
                QueueDerived();
                Uses.Clear();
                UsesStatus = "";
            }
            catch (Exception ex) when (AppLogger.Noted("TrainerEditorViewModel.LoadTrainer", ex))
            {
                _ = DialogHelper.ShowErrorAsync($"Couldn't load trainer {index}:\n{ex.Message}", "Trainer editor");
            }
        }

        private const string TrainerClassHeader = "include/constants/trainerclass.h";
        private const string SpeciesHeader = "include/constants/species.h";
        private const string ItemHeader = "include/constants/item.h";
        private const string AbilityHeader = "include/constants/ability.h";
        private const string MovesHeader = "include/constants/moves.h";
        private const string TrainerDataHeader = "include/trainer_data.h";
        private const string PokemonConstantsHeader = "include/constants/pokemon.h";
        private const string BattleConstantsHeader = "include/constants/battle_constants.h";

        // hg-engine trainers come from Trainers.c text, never the a055/a056 narcs or a fixed layout.
        private void LoadTrainerFromSource(int index)
        {
            if (!HgEngineTrainerSource.TryLoadForEditing(index, out HgEngineSourceBlock block, out string error))
            {
                _ = DialogHelper.ShowErrorAsync($"Couldn't load trainer {index} from hg-engine source:\n{error}", "Trainer editor");
                return;
            }
            _trainer = null;   // no binary model for hg-engine trainers: the fields come from Trainers.c
            _loadedTrainerId = index;

            _suppress = true;
            try { PopulateFromSourceBlock(block); }
            finally { _suppress = false; }

            SetClean();
            _history.Reset(Snapshot());
            _lastCaptureUtc = DateTime.MinValue;
            RaiseUndoState();
            ShowHgeBattleLines(block);
            StatusText = $"Trainer {index} loaded from hg-engine source.";
            OnPropertyChanged(nameof(UnsavedChangesDescription));
        }

        // The built text can lag behind Trainers.c until the next build, so the lines come from source.
        private void LoadHgeBattleLines(int trainer)
        {
            if (HgEngineTrainerSource.TryLoad(trainer, out HgEngineSourceBlock block, out string error)) ShowHgeBattleLines(block);
            else AppLogger.Warn($"Battle messages of trainer {trainer}: {error}");
        }

        private void ShowHgeBattleLines(HgEngineSourceBlock block)
        {
            BattleLines.Clear();
            foreach ((string _, int trigger, string text) in BattleMessageEditorViewModel.HgeMessages(block).OrderBy(m => m.Trigger))
                BattleLines.Add(new BattleLine(BattleMessageEditorViewModel.TriggerDescription((ushort)trigger), Shown(text ?? "")));
            OnPropertyChanged(nameof(HasBattleLines));
        }

        // The caller guards with _suppress.
        private void PopulateFromSourceBlock(HgEngineSourceBlock block)
        {
            TrainerName = block.TryGetString(new[] { FieldPathSegment.Field("name") }, out string name) ? name : "";

            // An unresolved class stays blank and unwritten, not the previous trainer's.
            TrainerClassIndex = block.TryGetSymbol(new[] { FieldPathSegment.Field("data"), FieldPathSegment.Field("trainerClass") }, TrainerClassHeader, out int classId)
                ? classId : -1;

            bool gotTrainerType = block.TryGetFlagsValue(new[] { FieldPathSegment.Field("data"), FieldPathSegment.Field("trainerType") }, TrainerDataHeader, out int trainerTypeValue);
            _hgeTrainerTypeKnown = gotTrainerType;
            foreach (AiFlagViewModel f in TrainerDataTypeFlags) f.Checked = gotTrainerType && (trainerTypeValue & f.Value) != 0;
            RaiseHgeFlagCheckedChanged();
            ChooseMoves = HgeMovesFlagChecked;
            ChooseItems = HgeItemsFlagChecked;

            bool gotAiFlags = block.TryGetFlagsValue(new[] { FieldPathSegment.Field("data"), FieldPathSegment.Field("aiFlags") }, TrainerDataHeader, out int aiFlagsValue);
            _hgeAiFlagsKnown = gotAiFlags;
            foreach (AiFlagViewModel f in AiFlags) f.Checked = gotAiFlags && (aiFlagsValue & f.Value) != 0;

            // A battle type with no row stays -1 so saving leaves it alone.
            FieldPathSegment[] battleTypePath = new[] { FieldPathSegment.Field("data"), FieldPathSegment.Field("battleType") };
            BattleTypeIndex = block.TryGetSymbol(battleTypePath, TrainerDataHeader, out int battleTypeValue)
                ? Array.IndexOf(_battleTypeValues, battleTypeValue)
                : block.TryGetRaw(battleTypePath, out _) ? -1 : 0;   // absent means 0, SINGLE_BATTLE
            DoubleBattle = BattleTypeIndex > 0;   // kept loosely in sync for display only; save uses BattleTypeIndex
            RepushBattleTypeIndex();

            IReadOnlyList<HgEngineSourceBlock> itemsRaw = block.GetArrayElements(new[] { FieldPathSegment.Field("data"), FieldPathSegment.Field("items") });
            for (int i = 0; i < TrainerItems.Count; i++)
            {
                _hgeItemRaw[i] = i < itemsRaw.Count ? itemsRaw[i].Raw.Trim() : null;
                TrainerItems[i].Load(_hgeItemRaw[i] != null && HgEngineSourceBlock.TryResolveToken(_hgeItemRaw[i], ItemHeader, out int itemVal) ? itemVal : -1);
            }

            IReadOnlyList<HgEngineSourceBlock> partyRaw = block.GetArrayElements(new[] { FieldPathSegment.Field("party") });
            PartyCount = partyRaw.Count;
            for (int i = 0; i < Party.Count; i++)
            {
                if (i < partyRaw.Count) LoadMonFromSource(Party[i], partyRaw[i]);
                else Party[i].Load(0, 0, 1, null, 0, 0, 0, 0, 0);
            }

            ApplyMovesEnabled();
            ApplyItemsEnabled();
            ApplyPartyVisibility();
            ApplyHgeGatingToParty();
        }

        private void LoadMonFromSource(TrainerPartyMonViewModel mon, HgEngineSourceBlock monBlock)
        {
            monBlock.TryGetInt(new[] { FieldPathSegment.Field("ivs") }, out int ivs);
            // Unreadable species stays -1 so saving leaves the source value alone.
            int species = -1, form = 0;
            if (monBlock.TryGetRaw(new[] { FieldPathSegment.Field("species") }, out string speciesRaw)
                && HgEngineTrainerSource.TryParseSpecies(speciesRaw, SpeciesHeader, out int parsedSpecies, out int parsedForm))
            { species = parsedSpecies; form = parsedForm; }
            monBlock.TryGetInt(new[] { FieldPathSegment.Field("level") }, out int level);
            monBlock.TryGetInt(new[] { FieldPathSegment.Field("ballSeal") }, out int ballSeal);

            int itemVal = 0;
            if (HgeItemsFlagChecked) monBlock.TryGetSymbol(new[] { FieldPathSegment.Field("item") }, ItemHeader, out itemVal);

            int[] moves = null;
            if (HgeMovesFlagChecked)
            {
                moves = new int[4];
                for (int j = 0; j < 4; j++)
                    monBlock.TryGetSymbol(new[] { FieldPathSegment.Field("moves"), FieldPathSegment.At(j) }, MovesHeader, out moves[j]);
            }

            // A slot value with no row stays -1 so saving leaves it alone.
            int abilityIndex = 0;
            if (monBlock.TryGetSymbol(new[] { FieldPathSegment.Field("abilitySlot") }, TrainerDataHeader, out int slotValue))
            {
                HgEngineTrainerFieldSchema.NamedFlag slot = HgEngineTrainerFieldSchema.GetAbilitySlots().FirstOrDefault(s => s.Bit == slotValue && s.Name != null);
                abilityIndex = slot.Name == null ? -1 : Array.IndexOf(TrainerPartyMonViewModel.HgeAbilitySlotNames, slot.Name);
            }

            // hg-engine has no gender field, so this is always Default.
            mon.Load(species, form, Math.Max(1, level), moves, itemVal, 0, abilityIndex, ivs, ballSeal);
            LoadHgeExtras(mon, monBlock);
            LoadHgeAdditional(mon.HgeExtras, monBlock);
        }

        private void LoadHgeExtras(TrainerPartyMonViewModel mon, HgEngineSourceBlock monBlock)
        {
            TrainerPartyMonHgeExtras extras = mon.HgeExtras;
            extras.AbilityId = HgeAbilityFlagChecked && monBlock.TryGetSymbol(new[] { FieldPathSegment.Field("ability") }, AbilityHeader, out int abilityId) ? abilityId : -1;
            mon.RememberSlotAbility();
            extras.BallId = HgeBallFlagChecked && monBlock.TryGetSymbol(new[] { FieldPathSegment.Field("ball") }, ItemHeader, out int ballId) ? ballId : -1;

            if (HgeIvEvFlagChecked)
            {
                extras.SetIvs.Load(
                    GetInt(monBlock, "setIvs", "hp"), GetInt(monBlock, "setIvs", "attack"), GetInt(monBlock, "setIvs", "defense"),
                    GetInt(monBlock, "setIvs", "speed"), GetInt(monBlock, "setIvs", "spAttack"), GetInt(monBlock, "setIvs", "spDefense"));
                extras.SetEvs.Load(
                    GetInt(monBlock, "setEvs", "hp"), GetInt(monBlock, "setEvs", "attack"), GetInt(monBlock, "setEvs", "defense"),
                    GetInt(monBlock, "setEvs", "speed"), GetInt(monBlock, "setEvs", "spAttack"), GetInt(monBlock, "setEvs", "spDefense"));
            }
            else
            {
                extras.SetIvs.Load(0, 0, 0, 0, 0, 0);
                extras.SetEvs.Load(0, 0, 0, 0, 0, 0);
            }

            extras.NatureIndex = HgeNatureFlagChecked && monBlock.TryGetSymbol(new[] { FieldPathSegment.Field("nature") }, PokemonConstantsHeader, out int nature) ? nature : 0;
            extras.ShinyLocked = HgeShinyLockFlagChecked && monBlock.TryGetRaw(new[] { FieldPathSegment.Field("shinyLock") }, out string shinyRaw)
                && HgEngineTrainerSource.TryParseBool(shinyRaw, out bool shiny) && shiny;
        }

        private void LoadHgeAdditional(TrainerPartyMonHgeExtras extras, HgEngineSourceBlock monBlock)
        {
            int extraFlagsValue = 0;
            bool gotExtraFlags = HgeAdditionalFlagsFlagChecked &&
                monBlock.TryGetFlagsValue(new[] { FieldPathSegment.Field("additionalFlags") }, TrainerDataHeader, out extraFlagsValue);
            IReadOnlyList<HgEngineTrainerFieldSchema.NamedFlag> extraFlagBits = gotExtraFlags ? HgEngineTrainerFieldSchema.GetExtraFlags() : System.Array.Empty<HgEngineTrainerFieldSchema.NamedFlag>();
            int ExtraBit(string suffix) { foreach (HgEngineTrainerFieldSchema.NamedFlag f in extraFlagBits) if (f.Name.EndsWith(suffix)) return f.Bit; return 0; }

            extras.ExtraStatusEnabled = gotExtraFlags && (extraFlagsValue & ExtraBit("STATUS")) != 0;
            extras.ExtraStatus = extras.ExtraStatusEnabled && monBlock.TryGetFlagsValue(new[] { FieldPathSegment.Field("status") }, BattleConstantsHeader, out int status) ? status : 0;

            extras.ExtraHpEnabled = gotExtraFlags && (extraFlagsValue & ExtraBit("HP")) != 0;
            extras.ExtraHp = extras.ExtraHpEnabled ? GetInt(monBlock, null, "hp") : 0;
            extras.ExtraAttackEnabled = gotExtraFlags && (extraFlagsValue & ExtraBit("ATK")) != 0;
            extras.ExtraAttack = extras.ExtraAttackEnabled ? GetInt(monBlock, null, "attack") : 0;
            extras.ExtraDefenseEnabled = gotExtraFlags && (extraFlagsValue & ExtraBit("DEF")) != 0;
            extras.ExtraDefense = extras.ExtraDefenseEnabled ? GetInt(monBlock, null, "defense") : 0;
            extras.ExtraSpeedEnabled = gotExtraFlags && (extraFlagsValue & ExtraBit("SPEED")) != 0;
            extras.ExtraSpeed = extras.ExtraSpeedEnabled ? GetInt(monBlock, null, "speed") : 0;
            extras.ExtraSpAtkEnabled = gotExtraFlags && (extraFlagsValue & ExtraBit("SP_ATK")) != 0;
            extras.ExtraSpAtk = extras.ExtraSpAtkEnabled ? GetInt(monBlock, null, "spAttack") : 0;
            extras.ExtraSpDefEnabled = gotExtraFlags && (extraFlagsValue & ExtraBit("SP_DEF")) != 0;
            extras.ExtraSpDef = extras.ExtraSpDefEnabled ? GetInt(monBlock, null, "spDefense") : 0;

            extras.ExtraPpCountsEnabled = gotExtraFlags && (extraFlagsValue & ExtraBit("PP_COUNTS")) != 0;
            if (extras.ExtraPpCountsEnabled)
            {
                IReadOnlyList<HgEngineSourceBlock> pp = monBlock.GetArrayElements(new[] { FieldPathSegment.Field("ppCounts") });
                extras.ExtraPp1 = pp.Count > 0 && HgEngineSourceBlock.TryResolveToken(pp[0].Raw, null, out int p1) ? p1 : 0;
                extras.ExtraPp2 = pp.Count > 1 && HgEngineSourceBlock.TryResolveToken(pp[1].Raw, null, out int p2) ? p2 : 0;
                extras.ExtraPp3 = pp.Count > 2 && HgEngineSourceBlock.TryResolveToken(pp[2].Raw, null, out int p3) ? p3 : 0;
                extras.ExtraPp4 = pp.Count > 3 && HgEngineSourceBlock.TryResolveToken(pp[3].Raw, null, out int p4) ? p4 : 0;
            }
            else { extras.ExtraPp1 = extras.ExtraPp2 = extras.ExtraPp3 = extras.ExtraPp4 = 0; }

            extras.ExtraNicknameEnabled = gotExtraFlags && (extraFlagsValue & ExtraBit("NICKNAME")) != 0;
            extras.ExtraNickname = extras.ExtraNicknameEnabled && monBlock.TryGetString(new[] { FieldPathSegment.Field("nicknameStr") }, out string nick) ? nick : "";
        }

        private static int GetInt(HgEngineSourceBlock block, string nestedField, string statField)
        {
            FieldPathSegment[] path = nestedField != null
                ? new[] { FieldPathSegment.Field(nestedField), FieldPathSegment.Field(statField) }
                : new[] { FieldPathSegment.Field(statField) };
            return block.TryGetInt(path, out int v) ? v : 0;
        }

        private void PopulateFromTrainer()
        {
            TrainerProperties trp = _trainer.Trp;
            TrainerName = _trainer.Name;
            TrainerClassIndex = trp.TrainerClass;
            SetBattleType(trp.BattleType);
            ChooseMoves = trp.ChooseMoves;
            ChooseItems = trp.ChooseItems;
            // The minimum drops before a 0 arrives and rises after it leaves, so the spinner never clamps it.
            if (trp.PartyCount == 0) SetEmptyPartyAsRead(true);
            PartyCount = trp.PartyCount;
            if (trp.PartyCount > 0) SetEmptyPartyAsRead(false);

            for (int i = 0; i < TrainerItems.Count && i < trp.TrainerItems.Length; i++)
                TrainerItems[i].Load(trp.TrainerItems[i]);

            for (int i = 0; i < AiFlags.Count; i++)
                AiFlags[i].Checked = trp.AI != null && i < trp.AI.Count && trp.AI[i];

            for (int i = 0; i < Party.Count; i++)
                LoadMon(Party[i], _trainer.Party[i]);

            ApplyMovesEnabled();
            ApplyItemsEnabled();
            ApplyPartyVisibility();
        }

        // Shared by Save and the undo snapshot.
        private void SyncToTrainer()
        {
            if (_trainer == null) return;
            TrainerProperties trp = _trainer.Trp;
            trp.PartyCount = (byte)_partyCount;
            trp.ChooseMoves = _chooseMoves;
            trp.ChooseItems = _chooseItems;
            trp.BattleType = _battleType;
            trp.TrainerClass = (byte)Math.Max(0, _trainerClassIndex);

            for (int i = 0; i < trp.TrainerItems.Length && i < TrainerItems.Count; i++)
                trp.TrainerItems[i] = (ushort)Math.Max(0, TrainerItems[i].ItemIndex);

            for (int i = 0; i < AiFlags.Count && trp.AI != null && i < trp.AI.Count; i++)
                trp.AI[i] = AiFlags[i].Checked;

            for (int i = 0; i < TrainerFile.PokeInParty; i++)
                _trainer.Party[i].Moves = _chooseMoves ? new ushort[4] : null;

            // Slots past the party count are synced too, so undo keeps them. The file stops at the count.
            for (int i = 0; i < Party.Count && i < TrainerFile.PokeInParty; i++)
            {
                TrainerPartyMonViewModel mon = Party[i];
                PartyPokemon p = _trainer.Party[i];
                p.PokeID = (ushort)Math.Max(0, mon.SpeciesIndex);
                p.FormID = (ushort)mon.FormId;
                p.Level = (ushort)mon.Level;

                if (_chooseMoves)
                    p.Moves = new ushort[] { (ushort)Math.Max(0, mon.Move1), (ushort)Math.Max(0, mon.Move2), (ushort)Math.Max(0, mon.Move3), (ushort)Math.Max(0, mon.Move4) };

                p.HeldItem = _chooseItems ? (ushort)Math.Max(0, mon.ItemIndex) : null;   // the party file has room for an item only when the trainer's flag is set

                p.Difficulty = (byte)mon.Difficulty;

                if (_genderEditable)   // elsewhere this byte is the high half of the u16 difficulty and is kept as read
                {
                    const PartyPokemon.GenderAndAbilityFlags Edited = PartyPokemon.GenderAndAbilityFlags.FORCE_MALE | PartyPokemon.GenderAndAbilityFlags.FORCE_FEMALE
                        | PartyPokemon.GenderAndAbilityFlags.ABILITY_SLOT1 | PartyPokemon.GenderAndAbilityFlags.ABILITY_SLOT2;
                    PartyPokemon.GenderAndAbilityFlags flags = p.GenderAndAbility & ~Edited;   // bits the editor has no field for stay as read
                    if (mon.GenderIndex == 1) flags |= PartyPokemon.GenderAndAbilityFlags.FORCE_MALE;
                    else if (mon.GenderIndex == 2) flags |= PartyPokemon.GenderAndAbilityFlags.FORCE_FEMALE;
                    if (mon.AbilityIndex == 1) flags |= PartyPokemon.GenderAndAbilityFlags.ABILITY_SLOT1;
                    else if (mon.AbilityIndex == 2) flags |= PartyPokemon.GenderAndAbilityFlags.ABILITY_SLOT2;
                    p.GenderAndAbility = flags;
                    if (_shinyPatch) p.ForceShiny = mon.ForceShiny;
                }

                p.BallSeals = (ushort)mon.BallSeals;
            }
        }

        private void LoadMon(TrainerPartyMonViewModel mon, PartyPokemon p)
        {
            int gender = 0;
            if (p.GenderAndAbility.HasFlag(PartyPokemon.GenderAndAbilityFlags.FORCE_MALE)) gender = 1;
            else if (p.GenderAndAbility.HasFlag(PartyPokemon.GenderAndAbilityFlags.FORCE_FEMALE)) gender = 2;

            int ability = 0;
            if (p.GenderAndAbility.HasFlag(PartyPokemon.GenderAndAbilityFlags.ABILITY_SLOT1)) ability = 1;
            else if (p.GenderAndAbility.HasFlag(PartyPokemon.GenderAndAbilityFlags.ABILITY_SLOT2)) ability = 2;

            int[] moves = null;
            if (p.Moves != null)
            {
                moves = new int[4];
                for (int j = 0; j < 4 && j < p.Moves.Length; j++) moves[j] = p.Moves[j];
            }

            mon.Load(p.PokeID ?? 0, p.FormID, Math.Max(1, (int)p.Level), moves, p.HeldItem ?? 0, gender, ability, p.Difficulty, p.BallSeals,
                     _genderEditable && p.ForceShiny);
            mon.ShinyVisible = _genderEditable && GameFamily == GameFamilies.HGSS && !HgEngineProject.IsActive;
            mon.ShinyEnabled = _shinyPatch;
        }

        private void ApplyMovesEnabled() { foreach (TrainerPartyMonViewModel m in Party) m.MovesEnabled = _chooseMoves; }
        private void ApplyItemsEnabled() { foreach (TrainerPartyMonViewModel m in Party) m.ItemEnabled = _chooseItems; }
        private void ApplyPartyVisibility()
        {
            int count = (int)_partyCount;
            for (int i = 0; i < Party.Count; i++) Party[i].IsVisible = i < count;
        }
    }
}
