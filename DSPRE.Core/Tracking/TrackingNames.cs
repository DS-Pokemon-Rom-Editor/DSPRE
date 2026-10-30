using System;
using System.Collections.Generic;
using DSPRE.ROMFiles;

namespace DSPRE.Tracking
{
    /// <summary>The project's names for numbered records, read once per export run.</summary>
    public sealed class TrackingNames
    {
        private readonly Dictionary<string, string[]> _lists = new Dictionary<string, string[]>();

        public string Species(int id) => From("species", RomInfo.GetPokemonNames, id);
        public string Move(int id) => From("moves", RomInfo.GetAttackNames, id);
        public string Item(int id) => From("items", RomInfo.GetItemNames, id);
        public string Ability(int id) => From("abilities", RomInfo.GetAbilityNames, id);
        public string TrainerClass(int id) => From("classes", RomInfo.GetTrainerClassNames, id);
        public string Trainer(int id) => From("trainers", RomInfo.GetSimpleTrainerNames, id);
        public string Type(int id) => From("types", RomInfo.GetTypeNames, id);
        public string Location(int id) => From("locations", () => new TextArchive(RomInfo.LocationNamesTextNumber).Messages.ToArray(), id);

        public string[] TypeList => List("types", RomInfo.GetTypeNames);

        private string From(string key, Func<string[]> load, int id)
        {
            string[] list = List(key, load);
            return id >= 0 && id < list.Length ? list[id] : null;
        }

        private string[] List(string key, Func<string[]> load)
        {
            lock (_lists)
            {
                if (_lists.TryGetValue(key, out string[] list)) return list;
                try { list = load() ?? Array.Empty<string>(); }
                catch (Exception ex)
                {
                    AppLogger.WarnOnce("TrackingNames." + key, ex);
                    list = Array.Empty<string>();
                }
                _lists[key] = list;
                return list;
            }
        }
    }
}
