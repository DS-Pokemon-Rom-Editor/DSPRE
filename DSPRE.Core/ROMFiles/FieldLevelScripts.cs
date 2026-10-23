using System;
using System.Collections.Generic;
using System.Linq;

namespace DSPRE.ROMFiles
{
    /// <summary>The scripts a map runs by itself, not ones started by talking to someone or stepping on a trigger.</summary>
    public static class FieldLevelScripts
    {
        /// <summary>
        /// The order the arrival scripts run: ON_TRANSITION during the warp, then ON_LOAD in graphics setup and
        /// ON_RESUME after the music (Platinum field_map_change.c and fieldmap.c; HGSS field_warp_tasks.c).
        /// </summary>
        public static readonly int[] ArrivalOrder =
        {
            LevelScriptTrigger.MAPCHANGE,     // 2
            LevelScriptTrigger.LOADGAME,      // 4
            LevelScriptTrigger.SCREENRESET,   // 3
        };

        /// <summary>Everything that runs on arriving at the map, in the order the engine runs it.</summary>
        public static List<LevelScriptTrigger> OnArrival(LevelScriptFile file)
        {
            List<LevelScriptTrigger> found = new List<LevelScriptTrigger>();
            if (file?.BufferSet == null) return found;
            foreach (int kind in ArrivalOrder)
                foreach (LevelScriptTrigger t in file.BufferSet)
                    if (t != null && t.TriggerType == kind) found.Add(t);
            return found;
        }

        /// <summary>The entries that sit and watch a variable, checked on every step.</summary>
        public static List<VariableValueTrigger> Watchers(LevelScriptFile file)
        {
            if (file?.BufferSet == null) return new List<VariableValueTrigger>();
            return file.BufferSet.OfType<VariableValueTrigger>()
                       .Where(t => t.TriggerType == LevelScriptTrigger.VARIABLEVALUE)
                       .ToList();
        }

        /// <summary>The watchers whose variable now holds what they are waiting for. </summary>
        public static List<VariableValueTrigger> ReadyToFire(LevelScriptFile file,
                                                             Func<int, int> valueOf)
        {
            List<VariableValueTrigger> ready = new List<VariableValueTrigger>();
            if (valueOf == null) return ready;
            foreach (VariableValueTrigger t in Watchers(file))
                if (IsSatisfied(t, valueOf)) ready.Add(t);
            return ready;
        }

        /// <summary>
        /// Whether a watcher would fire. The game compares VarGet of both halves, so the expected value
        /// is itself a variable when it is 0x4000 or above, and a plain number below that.
        /// </summary>
        public static bool IsSatisfied(VariableValueTrigger t, Func<int, int> valueOf)
            => t != null && valueOf != null
               && Resolve(t.VariableToWatch, valueOf) == Resolve(t.ExpectedValue, valueOf);

        private static int Resolve(int operand, Func<int, int> valueOf)
            => operand >= VariableValueTrigger.FirstVariable ? valueOf(operand) : operand;

        /// <summary>Plain wording for when one of these runs, for showing somebody what the map does.</summary>
        public static string WhenItRuns(LevelScriptTrigger trigger)
        {
            if (trigger == null) return "";
            switch (trigger.TriggerType)
            {
                case LevelScriptTrigger.VARIABLEVALUE:
                    VariableValueTrigger v = trigger as VariableValueTrigger;
                    return v == null
                        ? "Every step, once a variable holds the right value"
                        : $"Every step, once {FieldScriptValues.Describe(v.VariableToWatch)} holds "
                          + (v.ExpectedValue >= VariableValueTrigger.FirstVariable ? $"the value of {FieldScriptValues.Describe(v.ExpectedValue)}" : v.ExpectedValue.ToString());
                case LevelScriptTrigger.MAPCHANGE: return "On warping in, before the map loads";
                case LevelScriptTrigger.SCREENRESET: return "While the map sets up, once the music starts";
                case LevelScriptTrigger.LOADGAME: return "While the map sets up, before its data loads";
                default: return "When a trigger DSPRE doesn't know fires";
            }
        }
    }
}
