using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using DSPRE.ROMFiles;
using DSPRE.Avalonia.ViewModels;   // for TradeOriginLang (enum lives with the trade VM)
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.Data
{
    /// <summary>One customisable dropdown category: its built-in default labels plus the data-type cap.</summary>
    public sealed class LabelCategory
    {
        public string Key { get; init; }            // stable id, e.g. "evolution_methods"
        public string DisplayName { get; init; }    // shown in the editor
        public string Group { get; init; } = "General";   // which editor it belongs to (tab in the Label editor)
        public string Singular { get; init; } = "Entry";   // for generated labels beyond the defaults
        public int Cap { get; init; } = 256;        // max entries the underlying field can hold (u8 = 256)
        public IReadOnlyList<string> Defaults { get; init; } = Array.Empty<string>();

        // Optional secondary per-entry ATTRIBUTE (a small enum choice). e.g. evolution methods carry a
        // "param meaning" (Level / Item / Move / Species / Beauty) so the editor knows how to interpret the
        // method's parameter. Null AttrName = no attribute. AttrDefaults is value-indexed like Defaults.
        public string AttrName { get; init; }
        public IReadOnlyList<string> AttrOptions { get; init; }
        public IReadOnlyList<int> AttrDefaults { get; init; }
        public int AttrDefaultForNew { get; init; }   // attr value applied to a freshly-added entry
        public bool HasAttr => !string.IsNullOrEmpty(AttrName) && AttrOptions != null && AttrOptions.Count > 0;
    }

    /// <summary>
    /// Customisable labels for hardcoded enums/dropdowns. The game stores fixed-width numeric values;
    /// DSPRE only relabels them — useful when a ROM hack repurposes or adds values (e.g. a new evolution
    /// method). Resolution order per index: PROJECT override → GLOBAL override → built-in default →
    /// generated "Singular N". Project overrides live in <c>workDir/dspre_labels.json</c> (travels with
    /// the extracted ROM); global overrides in <c>%AppData%/DSPRE/databases/labels.global.json</c>.
    /// </summary>
    public static class LabelStore
    {
        private static readonly Dictionary<string, LabelCategory> _cats = new();
        private static readonly Dictionary<string, Dictionary<int, string>> _global = new();
        private static readonly Dictionary<string, Dictionary<int, string>> _project = new();
        private static readonly Dictionary<string, Dictionary<int, int>> _globalAttr = new();
        private static readonly Dictionary<string, Dictionary<int, int>> _projectAttr = new();
        private static bool _builtinsRegistered;
        private static bool _globalLoaded;
        private static string _loadedProjectDir;

        public static IReadOnlyCollection<LabelCategory> Categories { get { Ensure(); return _cats.Values; } }
        public static LabelCategory GetCategory(string key) { Ensure(); return _cats.TryGetValue(key, out var c) ? c : null; }

        private static string GlobalPath => Path.Combine(AppPaths.DatabasePath, "labels.global.json");
        private static string ProjectPath => string.IsNullOrEmpty(workDir) ? null : Path.Combine(workDir, "dspre_labels.json");

        /// <summary>Lazily registers built-ins, loads the global file once, and (re)loads the project file
        /// whenever the open ROM's working directory changes.</summary>
        private static void Ensure()
        {
            if (!_builtinsRegistered) { RegisterBuiltins(); _builtinsRegistered = true; }
            if (!_globalLoaded) { Load(GlobalPath, _global, _globalAttr); _globalLoaded = true; }
            string pdir = string.IsNullOrEmpty(workDir) ? null : workDir;
            if (pdir != _loadedProjectDir) { Load(ProjectPath, _project, _projectAttr); _loadedProjectDir = pdir; }
        }

        private static void Register(LabelCategory c) => _cats[c.Key] = c;

        // Built-in categories. Add a line here (+ swap the editor VM's source to LabelStore.Get) to make
        // another hardcoded dropdown customisable.
        private static void RegisterBuiltins()
        {
            void Reg(string key, string name, string group, string singular, string[] defaults)
                => Register(new LabelCategory { Key = key, DisplayName = name, Group = group, Singular = singular, Cap = 256, Defaults = defaults });

            // Pokémon — combos bind by SelectedIndex == enum position (these enums are sequential).
            // Evolution methods also carry a per-method "param meaning" attribute (what the parameter is).
            Register(new LabelCategory
            {
                Key = "evolution_methods", DisplayName = "Evolution Methods", Group = "Pokémon", Singular = "Method",
                Cap = 256, Defaults = Enum.GetNames<EvolutionMethod>(),
                AttrName = "Parameter", AttrOptions = Enum.GetNames<EvolutionParamMeaning>(),
                AttrDefaults = EvoParamDefaults(),
                AttrDefaultForNew = (int)EvolutionParamMeaning.CustomNumber,   // new methods default to a raw number
            });
            Reg("pokemon_growth_curves",  "Growth Curves",     "Pokémon", "Curve",  Enum.GetNames<PokemonGrowthCurve>());
            Reg("pokemon_egg_groups",     "Egg Groups",        "Pokémon", "Group",  Enum.GetNames<PokemonEggGroup>());
            Reg("pokemon_dex_colors",     "Pokédex Colors",    "Pokémon", "Color",  Enum.GetNames<PokemonDexColor>());
            // Items — combos bind by SelectedIndex == raw byte VALUE, so register VALUE-indexed defaults
            // (NaturalGiftType is non-sequential: gaps become generated "Type N" labels you can rename).
            Reg("item_hold_effects",      "Item Hold Effects",       "Items", "Effect", ByValue<HoldEffect>());
            Reg("item_field_pockets",     "Item Field Pockets",      "Items", "Pocket", ByValue<FieldPocket>());
            Reg("item_field_use",         "Item Field Use Funcs",    "Items", "Func",   ByValue<FieldUseFunc>());
            Reg("item_battle_use",        "Item Battle Use Funcs",   "Items", "Func",   ByValue<BattleUseFunc>());
            Reg("item_natural_gift",      "Item Natural Gift Types", "Items", "Type",   ByValue<NaturalGiftType>());
            // Moves / trades
            Reg("move_split",             "Move Split (Phys/Spec/Status)", "Moves", "Split", Enum.GetNames<MoveData.MoveSplit>());
            Reg("move_contest_conditions","Move Contest Conditions", "Moves", "Condition", Enum.GetNames<MoveData.ContestCondition>());
            Reg("trade_languages",        "Trade Origin Languages", "Trades", "Language", Enum.GetNames<TradeOriginLang>());
        }

        /// <summary>Builds a VALUE-indexed default-label array for an enum (slot i = the member whose value
        /// is i, or null for a gap → resolved to a generated "Singular i"). Needed for combos bound by the
        /// raw byte value rather than the declaration position.</summary>
        /// <summary>Default "param meaning" per evolution method (method index → EvolutionParamMeaning value),
        /// from EvolutionFile.evoDescriptions — the attribute defaults for the evolution_methods category.</summary>
        private static int[] EvoParamDefaults()
        {
            var names = Enum.GetNames<EvolutionMethod>();
            var arr = new int[names.Length];
            for (int i = 0; i < names.Length; i++)
                arr[i] = EvolutionFile.evoDescriptions.TryGetValue((EvolutionMethod)i, out var meaning) ? (int)meaning : 0;
            return arr;
        }

        private static string[] ByValue<TEnum>() where TEnum : struct, Enum
        {
            var vals = Enum.GetValues<TEnum>();
            int max = 0;
            foreach (var v in vals) max = Math.Max(max, Convert.ToInt32(v));
            var arr = new string[max + 1];
            foreach (var v in vals)
            {
                int i = Convert.ToInt32(v);
                if (i >= 0 && i < arr.Length) arr[i] = v.ToString();
            }
            return arr;
        }

        /// <summary>The resolved labels for a category (defaults overlaid with global then project overrides,
        /// extended to cover any added indices, capped at the data-type max).</summary>
        public static IReadOnlyList<string> Get(string key)
        {
            Ensure();
            if (!_cats.TryGetValue(key, out var cat)) return Array.Empty<string>();
            int count = cat.Defaults.Count;
            if (_global.TryGetValue(key, out var gl)) count = Math.Max(count, MaxIndex(gl) + 1);
            if (_project.TryGetValue(key, out var pj)) count = Math.Max(count, MaxIndex(pj) + 1);
            count = Math.Min(Math.Max(count, 0), cat.Cap);
            var list = new List<string>(count);
            for (int i = 0; i < count; i++) list.Add(Resolve(cat, key, i));
            return list;
        }

        /// <summary>The label at a single index (same resolution as <see cref="Get"/>).</summary>
        public static string GetLabel(string key, int index)
        {
            Ensure();
            return _cats.TryGetValue(key, out var cat) ? Resolve(cat, key, index) : index.ToString();
        }

        /// <summary>The built-in default at an index (ignores overrides) — shown as a hint in the editor.</summary>
        public static string GetDefault(string key, int index)
        {
            var cat = GetCategory(key);
            if (cat == null) return "";
            return index < cat.Defaults.Count ? cat.Defaults[index] : $"{cat.Singular} {index}";
        }

        private static string Resolve(LabelCategory cat, string key, int i)
        {
            if (_project.TryGetValue(key, out var pj) && pj.TryGetValue(i, out var pv) && !string.IsNullOrEmpty(pv)) return pv;
            if (_global.TryGetValue(key, out var gl) && gl.TryGetValue(i, out var gv) && !string.IsNullOrEmpty(gv)) return gv;
            string d = i < cat.Defaults.Count ? cat.Defaults[i] : null;   // null = value-gap or beyond defaults
            return string.IsNullOrEmpty(d) ? $"{cat.Singular} {i}" : d;
        }

        /// <summary>Current entry count for a category (defaults + any added indices).</summary>
        public static int Count(string key) => Get(key).Count;

        /// <summary>Refreshes a bound combo collection to a category's current labels, in place (keeps selection).</summary>
        public static void Sync(ObservableCollection<string> target, string key) => ListSync.Apply(target, Get(key));

        // ── Editing (call Save + AppEvents.RaiseLabelsChanged when done) ──────────────────
        public static void SetLabel(string key, int index, string value, bool global)
        {
            Ensure();
            var map = global ? _global : _project;
            if (!map.TryGetValue(key, out var d)) { d = new Dictionary<int, string>(); map[key] = d; }
            if (string.IsNullOrWhiteSpace(value)) d.Remove(index);   // blank = fall back to the lower layer/default
            else d[index] = value.Trim();
        }

        public static void ResetCategory(string key, bool global)
        {
            (global ? _global : _project).Remove(key);
            (global ? _globalAttr : _projectAttr).Remove(key);
        }

        // ── Per-entry attribute (e.g. evolution param meaning) ────────────────────────────
        /// <summary>The attribute value (an index into <see cref="LabelCategory.AttrOptions"/>) for an entry,
        /// resolved project → global → built-in default.</summary>
        public static int GetAttr(string key, int index)
        {
            Ensure();
            if (_projectAttr.TryGetValue(key, out var pj) && pj.TryGetValue(index, out var pv)) return pv;
            if (_globalAttr.TryGetValue(key, out var gl) && gl.TryGetValue(index, out var gv)) return gv;
            var cat = GetCategory(key);
            if (cat?.AttrDefaults != null && index >= 0 && index < cat.AttrDefaults.Count) return cat.AttrDefaults[index];
            return 0;
        }

        public static void SetAttr(string key, int index, int value, bool global)
        {
            Ensure();
            var map = global ? _globalAttr : _projectAttr;
            if (!map.TryGetValue(key, out var d)) { d = new Dictionary<int, int>(); map[key] = d; }
            d[index] = value;
        }

        // ── Draft layer (Label editor) ────────────────────────────────────────────────────
        // The Label editor edits a DRAFT that other editors never see; only Commit (= Save) promotes it to
        // the real store + persists + raises LabelsChanged. This stops unsaved edits leaking into editors
        // that read GetAttr/Get live (e.g. the evolution param meaning).
        private static readonly Dictionary<(bool g, string k, int i), string> _draftLabels = new();
        private static readonly Dictionary<(bool g, string k, int i), int> _draftAttrs = new();
        private static readonly HashSet<(bool g, string k)> _draftResets = new();

        public static bool HasDraft => _draftLabels.Count > 0 || _draftAttrs.Count > 0 || _draftResets.Count > 0;

        public static void DraftSetLabel(string key, int idx, string value, bool global) { Ensure(); _draftLabels[(global, key, idx)] = value ?? ""; }
        public static void DraftSetAttr(string key, int idx, int value, bool global) { Ensure(); _draftAttrs[(global, key, idx)] = value; }
        public static void DraftReset(string key, bool global)
        {
            Ensure();
            _draftResets.Add((global, key));
            foreach (var k in _draftLabels.Keys.Where(k => k.g == global && k.k == key).ToList()) _draftLabels.Remove(k);
            foreach (var k in _draftAttrs.Keys.Where(k => k.g == global && k.k == key).ToList()) _draftAttrs.Remove(k);
        }

        /// <summary>A label as the Label editor should DISPLAY it (committed value overlaid with the draft).</summary>
        public static string GetDraftLabel(string key, int idx, bool global)
        {
            if (_draftLabels.TryGetValue((global, key, idx), out var v)) return string.IsNullOrEmpty(v) ? GetDefault(key, idx) : v;
            if (_draftResets.Contains((global, key))) return GetDefault(key, idx);
            return GetLabel(key, idx);
        }

        public static int GetDraftAttr(string key, int idx, bool global)
        {
            if (_draftAttrs.TryGetValue((global, key, idx), out var v)) return v;
            if (_draftResets.Contains((global, key)))
            {
                var c = GetCategory(key);
                return c?.AttrDefaults != null && idx >= 0 && idx < c.AttrDefaults.Count ? c.AttrDefaults[idx] : 0;
            }
            return GetAttr(key, idx);
        }

        /// <summary>Entry count for the editor including any draft-added indices.</summary>
        public static int DraftCount(string key, bool global)
        {
            int n = _draftResets.Contains((global, key)) ? (GetCategory(key)?.Defaults.Count ?? 0) : Count(key);
            foreach (var k in _draftLabels.Keys) if (k.g == global && k.k == key) n = Math.Max(n, k.i + 1);
            return Math.Min(n, GetCategory(key)?.Cap ?? 256);
        }

        /// <summary>Promotes the draft to the real store, persists the touched scopes, and signals editors.</summary>
        public static void CommitDraft()
        {
            Ensure();
            foreach (var r in _draftResets) ResetCategory(r.k, r.g);
            foreach (var kv in _draftLabels) SetLabel(kv.Key.k, kv.Key.i, kv.Value, kv.Key.g);
            foreach (var kv in _draftAttrs) SetAttr(kv.Key.k, kv.Key.i, kv.Value, kv.Key.g);
            bool g = false, p = false;
            void Note(bool global) { if (global) g = true; else p = true; }
            foreach (var k in _draftLabels.Keys) Note(k.g);
            foreach (var k in _draftAttrs.Keys) Note(k.g);
            foreach (var k in _draftResets) Note(k.g);
            DiscardDraft();
            if (p) Save(false);
            if (g) Save(true);
        }

        public static void DiscardDraft() { _draftLabels.Clear(); _draftAttrs.Clear(); _draftResets.Clear(); }

        public static void Save(bool global)
        {
            Ensure();
            string path = global ? GlobalPath : ProjectPath;
            if (path == null) return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                var file = new LabelFile
                {
                    labels = StringKeyed(global ? _global : _project),
                    attrs  = StringKeyed(global ? _globalAttr : _projectAttr),
                };
                File.WriteAllText(path, JsonSerializer.Serialize(file, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex) { AppLogger.Error("LabelStore.Save: " + ex.Message); }
        }

        private static Dictionary<string, Dictionary<string, T>> StringKeyed<T>(Dictionary<string, Dictionary<int, T>> src)
        {
            var outObj = new Dictionary<string, Dictionary<string, T>>();
            foreach (var kv in src)
                if (kv.Value.Count > 0)
                    outObj[kv.Key] = kv.Value.OrderBy(e => e.Key).ToDictionary(e => e.Key.ToString(), e => e.Value);
            return outObj;
        }

        private static int MaxIndex(Dictionary<int, string> d) => d.Count == 0 ? -1 : d.Keys.Max();

        // JSON schema: { "labels": {cat:{idx:label}}, "attrs": {cat:{idx:int}} }. Old flat files
        // (cat:{idx:label} at the root) are still read as labels for backward compatibility.
        private sealed class LabelFile
        {
            public Dictionary<string, Dictionary<string, string>> labels { get; set; }
            public Dictionary<string, Dictionary<string, int>> attrs { get; set; }
        }

        private static void Load(string path, Dictionary<string, Dictionary<int, string>> labels, Dictionary<string, Dictionary<int, int>> attrs)
        {
            labels.Clear(); attrs.Clear();
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
                string json = File.ReadAllText(path);
                var file = JsonSerializer.Deserialize<LabelFile>(json);
                if (file?.labels != null)
                {
                    foreach (var kv in file.labels) labels[kv.Key] = IntKeyed(kv.Value);
                    if (file.attrs != null) foreach (var kv in file.attrs) attrs[kv.Key] = IntKeyed(kv.Value);
                }
                else   // old flat format (root = cat → idx → label)
                {
                    var flat = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(json);
                    if (flat != null) foreach (var kv in flat) labels[kv.Key] = IntKeyed(kv.Value);
                }
            }
            catch (Exception ex) { AppLogger.Error("LabelStore.Load: " + ex.Message); }
        }

        private static Dictionary<int, T> IntKeyed<T>(Dictionary<string, T> src)
        {
            var d = new Dictionary<int, T>();
            foreach (var e in src) if (int.TryParse(e.Key, out int idx)) d[idx] = e.Value;
            return d;
        }
    }
}
