using DSPRE.HgEngine;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace DSPRE.Avalonia.ViewModels.Tools
{
    /// <summary>Something on the form that writes straight back into the scenario when it changes.</summary>
    public abstract class FormPart : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        internal Action Changed;

        /// <summary>The form the row sits on, for its name lists.</summary>
        public BattleTestFormViewModel Lists { get; internal set; }

        protected string Field(ref string backing, string value, Action<string> store, [CallerMemberName] string name = null)
        {
            value ??= "";
            if (backing == value) return backing;
            backing = value;
            store(value);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            Changed?.Invoke();
            return backing;
        }
    }

    public sealed class FormMon : FormPart
    {
        private readonly HgEngineBattleScenario.Mon _mon;
        public string Slot { get; }
        public FormMon(HgEngineBattleScenario.Mon mon, int slot)
        {
            _mon = mon;
            Slot = $"Slot {slot + 1}";
            _species = mon.Species; _level = mon.Level; _ability = mon.Ability; _item = mon.Item; _hp = mon.Hp; _status = mon.Status;
            _m1 = mon.Moves[0]; _m2 = mon.Moves[1]; _m3 = mon.Moves[2]; _m4 = mon.Moves[3];
        }
        private string _species, _level, _ability, _item, _hp, _status, _m1, _m2, _m3, _m4;
        public string Species { get => _species; set => Field(ref _species, value, v => _mon.Species = v); }
        public string Level { get => _level; set => Field(ref _level, value, v => _mon.Level = v); }
        public string Ability { get => _ability; set => Field(ref _ability, value, v => _mon.Ability = v); }
        public string Item { get => _item; set => Field(ref _item, value, v => _mon.Item = v); }
        public string Hp { get => _hp; set => Field(ref _hp, value, v => _mon.Hp = v); }
        public string Status { get => _status; set => Field(ref _status, value, v => _mon.Status = v); }
        public string Move1 { get => _m1; set => Field(ref _m1, value, v => _mon.Moves[0] = v); }
        public string Move2 { get => _m2; set => Field(ref _m2, value, v => _mon.Moves[1] = v); }
        public string Move3 { get => _m3; set => Field(ref _m3, value, v => _mon.Moves[2] = v); }
        public string Move4 { get => _m4; set => Field(ref _m4, value, v => _mon.Moves[3] = v); }
    }

    public sealed class FormAction : FormPart
    {
        private readonly HgEngineBattleScenario.Action _action;
        public string Turn { get; }
        public FormAction(HgEngineBattleScenario.Action action, int turn)
        {
            _action = action;
            Turn = $"Turn {turn + 1}";
            _do = action.Do; _target = action.Target;
        }
        private string _do, _target;
        public string Do { get => _do; set => Field(ref _do, value, v => _action.Do = v); }
        public string Target { get => _target; set => Field(ref _target, value, v => _action.Target = v); }
    }

    public sealed class FormBattler
    {
        public string Title { get; init; }
        public List<FormAction> Actions { get; init; }
    }

    public sealed class FormExpectation : FormPart
    {
        internal readonly HgEngineBattleScenario.Expectation Source;
        public FormExpectation(HgEngineBattleScenario.Expectation e)
        {
            Source = e;
            _type = e.Type; _battler = e.Battler; _field = e.Field; _value = e.Value;
        }
        private string _type, _battler, _field, _value;
        public string Type { get => _type; set => Field(ref _type, value, v => Source.Type = v); }
        public string Battler { get => _battler; set => Field(ref _battler, value, v => Source.Battler = v); }
        public string ValueField { get => _field; set => Field(ref _field, value, v => Source.Field = v); }
        public string Value { get => _value; set => Field(ref _value, value, v => Source.Value = v); }
    }

    /// <summary>
    /// The Form tab: a test's scenario as fields. Each change goes straight back into the test's text through
    /// <see cref="HgEngineBattleScenario.Write"/>, which only touches what changed.
    /// </summary>
    public sealed class BattleTestFormViewModel : FormPart
    {
        private readonly HgEngineBattleScenario _s;
        private readonly Action<string> _textChanged;

        public string Error { get; }
        public bool HasForm => _s != null;

        public BattleTestFormViewModel(string rel, string text, Action<string> textChanged)
        {
            _textChanged = textChanged;
            _s = HgEngineBattleScenario.Read(text, out string error);
            Error = error;
            if (_s == null) return;

            _battleType = _s.BattleType; _weather = _s.Weather; _field = _s.FieldCondition; _terrain = _s.Terrain;
            _passCount = _s.PassCount; _knownFailing = _s.KnownFailing.Trim() == "TRUE" || _s.KnownFailing.Trim() == "1";

            Player = _s.Player.Select((m, i) => Hook(new FormMon(m, i))).ToList();
            Enemy = _s.Enemy.Select((m, i) => Hook(new FormMon(m, i))).ToList();
            Scripts = new List<FormBattler>
            {
                Battler("Player, first", _s.PlayerScript[0]), Battler("Player, second", _s.PlayerScript[1]),
                Battler("Enemy, first", _s.EnemyScript[0]), Battler("Enemy, second", _s.EnemyScript[1]),
            };
            foreach (HgEngineBattleScenario.Expectation e in _s.Expectations) Expectations.Add(Hook(new FormExpectation(e)));

            Species = HgEngineBattleTests.Names(rel, "SPECIES_");
            MoveNames = HgEngineBattleTests.Names(rel, "MOVE_").Where(n => !n.StartsWith("MOVE_EFFECT")).ToList();
            Abilities = HgEngineBattleTests.Names(rel, "ABILITY_");
            Items = HgEngineBattleTests.Names(rel, "ITEM_");
            BattleTypes = HgEngineBattleTests.Names(rel, "BATTLE_TYPE_");
            Weathers = HgEngineBattleTests.Names(rel, "WEATHER_").Concat(HgEngineBattleTests.Names(rel, "FIELD_CONDITION_")).ToList();
            Terrains = HgEngineBattleTests.Names(rel, "").Where(n => n.EndsWith("_TERRAIN") || n.StartsWith("TERRAIN_")).ToList();
            Statuses = HgEngineBattleTests.Names(rel, "STATUS_");
            ActionNames = HgEngineBattleTests.Names(rel, "ACTION_");
            Battlers = HgEngineBattleTests.Names(rel, "BATTLER_");
            ExpectationTypes = HgEngineBattleTests.Names(rel, "EXPECTATION_");
        }

        private FormBattler Battler(string title, HgEngineBattleScenario.Action[] actions) =>
            new FormBattler { Title = title, Actions = actions.Select((a, i) => Hook(new FormAction(a, i))).ToList() };

        private T Hook<T>(T part) where T : FormPart
        {
            part.Changed = Push;
            part.Lists = this;
            return part;
        }

        private void Push() { if (_s != null) _textChanged(_s.Write()); }

        public List<string> Species { get; } = new();
        public List<string> MoveNames { get; } = new();
        public List<string> Abilities { get; } = new();
        public List<string> Items { get; } = new();
        public List<string> BattleTypes { get; } = new();
        public List<string> Weathers { get; } = new();
        public List<string> Terrains { get; } = new();
        public List<string> Statuses { get; } = new();
        public List<string> ActionNames { get; } = new();
        public List<string> Battlers { get; } = new();
        public List<string> ExpectationTypes { get; } = new();
        public List<string> ValueFields { get; } = new() { "message", "hpTaken", "hpRecovered", "formID", "currentHP", "types", "sideConditionMask" };

        private string _battleType, _weather, _field, _terrain, _passCount;
        public string BattleType { get => _battleType; set { Field(ref _battleType, value, v => _s.BattleType = v); Push(); } }
        public string Weather { get => _weather; set { Field(ref _weather, value, v => _s.Weather = v); Push(); } }
        public string FieldCondition { get => _field; set { Field(ref _field, value, v => _s.FieldCondition = v); Push(); } }
        public string Terrain { get => _terrain; set { Field(ref _terrain, value, v => _s.Terrain = v); Push(); } }
        public string PassCount { get => _passCount; set { Field(ref _passCount, value, v => _s.PassCount = v); Push(); } }

        private bool _knownFailing;
        public bool KnownFailing
        {
            get => _knownFailing;
            set
            {
                if (_knownFailing == value || _s == null) return;
                _knownFailing = value;
                _s.KnownFailing = value ? "TRUE" : (_s.KnownFailing.Length > 0 ? "FALSE" : "");
                Push();
            }
        }

        public List<FormMon> Player { get; } = new();
        public List<FormMon> Enemy { get; } = new();
        public List<FormBattler> Scripts { get; } = new();
        public ObservableCollection<FormExpectation> Expectations { get; } = new();

        public void AddExpectation()
        {
            if (_s == null) return;
            HgEngineBattleScenario.Expectation e = new HgEngineBattleScenario.Expectation { Type = "EXPECTATION_TYPE_MESSAGE", Field = "message", Value = "\"\"" };
            _s.Expectations.Add(e);
            Expectations.Add(Hook(new FormExpectation(e)));
            Push();
        }

        public void RemoveExpectation(FormExpectation row)
        {
            if (_s == null || row == null) return;
            int at = _s.Expectations.IndexOf(row.Source);
            if (at < 0) return;
            _s.Expectations.RemoveAt(at);
            Expectations.Remove(row);
            Push();
        }
    }
}
