using System;
using System.Collections.Generic;
using System.Linq;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// The scripts a map runs by itself, rather than because you talked to somebody or walked onto a
    /// trigger.
    /// </summary>
    public static class FieldLevelScripts
    {
        /// <summary>The order the engine runs the arrival scripts in: field setup first, then the map change.</summary>
        public static readonly int[] ArrivalOrder =
        {
            LevelScriptTrigger.LOADGAME,      // 4, SP_SCRID_INIT_CHANGE
            LevelScriptTrigger.SCREENRESET,   // 3, SP_SCRID_OBJ_CHANGE
            LevelScriptTrigger.MAPCHANGE,     // 2, SP_SCRID_FLAG_CHANGE, ev_mapchange.c:391
        };

        /// <summary>Everything that runs on arriving at the map, in the order the engine runs it.</summary>
        public static List<LevelScriptTrigger> OnArrival(LevelScriptFile file)
        {
            var found = new List<LevelScriptTrigger>();
            if (file?.bufferSet == null) return found;
            foreach (int kind in ArrivalOrder)
                foreach (var t in file.bufferSet)
                    if (t != null && t.triggerType == kind) found.Add(t);
            return found;
        }

        /// <summary>The entries that sit and watch a variable, checked on every step.</summary>
        public static List<VariableValueTrigger> Watchers(LevelScriptFile file)
        {
            if (file?.bufferSet == null) return new List<VariableValueTrigger>();
            return file.bufferSet.OfType<VariableValueTrigger>()
                       .Where(t => t.triggerType == LevelScriptTrigger.VARIABLEVALUE)
                       .ToList();
        }

        /// <summary>The watchers whose variable now holds what they are waiting for. </summary>
        public static List<VariableValueTrigger> ReadyToFire(LevelScriptFile file,
                                                             Func<int, int> valueOf)
        {
            var ready = new List<VariableValueTrigger>();
            if (valueOf == null) return ready;
            foreach (var t in Watchers(file))
                if (valueOf(t.variableToWatch) == t.expectedValue) ready.Add(t);
            return ready;
        }

        /// <summary>Plain wording for when one of these runs, for showing somebody what the map does.</summary>
        public static string WhenItRuns(LevelScriptTrigger trigger)
        {
            if (trigger == null) return "";
            switch (trigger.triggerType)
            {
                case LevelScriptTrigger.VARIABLEVALUE:
                    var v = trigger as VariableValueTrigger;
                    return v == null
                        ? "Every step, once a variable holds the right value"
                        : $"Every step, once {FieldScriptValues.Describe(v.variableToWatch)} holds {v.expectedValue}";
                case LevelScriptTrigger.MAPCHANGE: return "As you arrive on the map";
                case LevelScriptTrigger.SCREENRESET: return "While the map sets up, once the music starts";
                case LevelScriptTrigger.LOADGAME: return "While the map sets up, before anything else";
                default: return "Under something this editor does not recognise";
            }
        }
    }
}
