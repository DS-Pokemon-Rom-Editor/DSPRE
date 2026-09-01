using System;
using System.Collections.Generic;
using System.Linq;

namespace DSPRE.ROMFiles
{
    /// <summary>What one line of a walked script is telling you.</summary>
    public enum ScriptStepKind
    {
        Message,     // the script shows text, and the text is quoted
        Question,    // the script wants to know something before it can carry on
        Branch,      // a decision was taken, and this says which way and why
        Movement,    // an event is told to move
        Command,     // anything else the script does
        Ended,       // the script finished
    }

    /// <summary>Something a step asks the preview to actually do, rather than just describe.</summary>
    public enum ScriptEffectKind
    {
        None,
        /// <summary>Play a movement: A is the overworld, B the movement number.</summary>
        Movement,
        /// <summary>A sound effect, Snd_SePlay. A is the sequence.</summary>
        SoundEffect,
        /// <summary>A fanfare, Snd_MePlay, which pauses the music while it plays. A is the sequence.</summary>
        Fanfare,
        /// <summary>Background music, Snd_BgmPlay. A is the sequence.</summary>
        Music,
        /// <summary>Stops the music.</summary>
        MusicStop,
        /// <summary>A Pokémon's cry, Snd_PMVoicePlayEx. A is the species.</summary>
        Cry,
        /// <summary>Shakes the view: A and B are how far, C how many times, D over how many frames.</summary>
        CameraShake,
        /// <summary>Moves to one of the alternative camera settings. A is which.</summary>
        CameraChange,
        /// <summary>Waits for whatever was started to finish.</summary>
        Wait,
    }

    /// <summary>What a step asks for, with its numbers.</summary>
    public sealed class ScriptEffect
    {
        public ScriptEffectKind Kind;
        public int A, B, C, D;
        public ScriptEffect(ScriptEffectKind kind, int a = 0, int b = 0, int c = 0, int d = 0)
        { Kind = kind; A = a; B = b; C = c; D = d; }
    }

    public sealed class ScriptStep
    {
        public ScriptStepKind Kind;

        /// <summary>Set when the step is something the preview can play out. Null otherwise.</summary>
        public ScriptEffect Effect;

        /// <summary>The line to show. Plain words, already made readable.</summary>
        public string Text;
        public string CommandName;
        /// <summary>Which script or function this came from, and where in it.</summary>
        public string Location;

        public override string ToString() => Text;
    }

    /// <summary>Something only the person watching can answer, because the game state isn't here.</summary>
    public sealed class ScriptQuestion
    {
        public enum QuestionKind { Variable, Flag, YesNo }

        public QuestionKind Kind;
        /// <summary>The variable, flag or trainer the script is asking about.</summary>
        public string Subject;
        public string Prompt;
        /// <summary>Ready-made answers. A variable question also accepts any number.</summary>
        public IReadOnlyList<(string Label, long Value)> Options = Array.Empty<(string, long)>();
        public bool AcceptsAnyNumber => Kind == QuestionKind.Variable;
    }

    /// <summary>Walks an event's script and says what would happen, without running anything. </summary>
    public sealed class ScriptWalker
    {
        /// <summary>How the game orders two values. The script stores this, then a later jump tests it.</summary>
        private enum Relation { Less = 0, Equal = 1, Greater = 2 }

        private readonly IReadOnlyList<ScriptCommandContainer> _scripts;
        private readonly IReadOnlyList<ScriptCommandContainer> _functions;
        private readonly Func<int, string> _messageLookup;
        private readonly Func<int, IReadOnlyList<ScriptAction>> _actionLookup;
        private readonly List<ScriptStep> _steps = new List<ScriptStep>();
        private readonly Stack<(ScriptCommandContainer container, int index)> _returns
            = new Stack<(ScriptCommandContainer, int)>();

        private ScriptCommandContainer _current;
        private int _index;
        private Relation? _relation;
        private ScriptCommand _waitingOn;
        private long _pendingLeft;              // the value being compared, once known

        /// <summary>Stops a script that jumps back on itself from running away.</summary>
        public const int MaxSteps = 2000;

        public IReadOnlyList<ScriptStep> Steps => _steps;
        public ScriptQuestion Pending { get; private set; }
        public bool Finished { get; private set; }

        /// <param name="messageLookup">Turns a message id into its text. Null just names the id instead.</param>
        public ScriptWalker(ScriptFile file, Func<int, string> messageLookup = null)
            : this(file?.allScripts, file?.allFunctions, messageLookup, ActionsFrom(file)) { }

        /// <summary>Walks a set of scripts and functions directly, without needing the file they live in.</summary>
        public ScriptWalker(IReadOnlyList<ScriptCommandContainer> scripts,
                            IReadOnlyList<ScriptCommandContainer> functions,
                            Func<int, string> messageLookup = null,
                            Func<int, IReadOnlyList<ScriptAction>> actionLookup = null)
        {
            _scripts = scripts ?? Array.Empty<ScriptCommandContainer>();
            _functions = functions ?? Array.Empty<ScriptCommandContainer>();
            _messageLookup = messageLookup;
            _actionLookup = actionLookup;
        }

        // A script file keeps its movements alongside its scripts, but the two are numbered differently:
        // scripts count from one and movements count from zero, so a movement number is already the
        // position in the list.
        private static Func<int, IReadOnlyList<ScriptAction>> ActionsFrom(ScriptFile file)
        {
            var actions = file?.allActions;
            if (actions == null) return null;
            return number => number >= 0 && number < actions.Count ? actions[number].commands : null;
        }

        /// <summary>Starts at one of the file's scripts. Walks until it ends or needs an answer.</summary>
        public void Start(int scriptNumber)
        {
            _steps.Clear();
            _returns.Clear();
            Pending = null;
            Finished = false;
            _relation = null;
            _waitingOn = null;

            _current = FindScript(scriptNumber);
            _index = 0;
            if (_current == null)
            {
                Add(ScriptStepKind.Ended, $"There is no script {scriptNumber} in this file.", null);
                Finished = true;
                return;
            }
            Run();
        }

        /// <summary>Answers the question it stopped on, and carries on.</summary>
        public void Answer(long value)
        {
            if (Pending == null) return;

            var q = Pending;
            Pending = null;

            switch (q.Kind)
            {
                case ScriptQuestion.QuestionKind.Variable:
                    _relation = Compare(value, _pendingLeft);
                    Add(ScriptStepKind.Branch,
                        $"You said {q.Subject} is {value}, so the script treats it as {Describe(_relation.Value)} {_pendingLeft}.",
                        _waitingOn?.name);
                    break;

                case ScriptQuestion.QuestionKind.Flag:
                    // A flag test leaves the script "equal" when the flag is set, which is what the
                    // following JumpIf EQUAL / JumpIf DIFFERENT pair is written against.
                    _relation = value != 0 ? Relation.Equal : Relation.Less;
                    Add(ScriptStepKind.Branch,
                        $"You said {q.Subject} is {(value != 0 ? "set" : "not set")}.", _waitingOn?.name);
                    break;

                case ScriptQuestion.QuestionKind.YesNo:
                    _relation = value != 0 ? Relation.Equal : Relation.Less;
                    Add(ScriptStepKind.Branch,
                        $"You answered {(value != 0 ? "yes" : "no")}.", _waitingOn?.name);
                    break;
            }

            _waitingOn = null;
            _index++;
            Run();
        }

        /// <summary>Walks forward until the script ends or something has to be asked.</summary>
        private void Run()
        {
            int guard = 0;
            while (!Finished && Pending == null)
            {
                if (++guard > MaxSteps)
                {
                    Add(ScriptStepKind.Ended, "Stopped: this script keeps going round and does not finish.", null);
                    Finished = true;
                    return;
                }

                if (_current == null || _index < 0 || _index >= (_current.commands?.Count ?? 0))
                {
                    if (!PopReturn()) { Add(ScriptStepKind.Ended, "The script ends here.", null); Finished = true; }
                    continue;
                }

                Step(_current.commands[_index]);
            }
        }

        private void Step(ScriptCommand cmd)
        {
            string name = CommandName(cmd);

            switch (name)
            {
                case "End":
                    Add(ScriptStepKind.Ended, "The script ends here.", name);
                    Finished = true;
                    return;

                case "Return":
                    Add(ScriptStepKind.Command, "Goes back to whatever called this.", name);
                    if (!PopReturn()) { Finished = true; }
                    return;

                case "Jump":
                    GoTo(cmd, name, call: false, why: null);
                    return;

                case "Call":
                    GoTo(cmd, name, call: true, why: null);
                    return;

                case "JumpIf":
                case "CallIf":
                {
                    var op = Operator(cmd, 0);
                    bool take = _relation.HasValue && Matches(op, _relation.Value);
                    if (!_relation.HasValue)
                    {
                        Add(ScriptStepKind.Command,
                            $"{name} with nothing tested before it, so it carries straight on.", name);
                        _index++;
                        return;
                    }
                    if (take) GoTo(cmd, name, call: name == "CallIf", why: $"because the check came out {OperatorName(op)}", targetParam: 1);
                    else
                    {
                        Add(ScriptStepKind.Branch,
                            $"Skips the {(name == "CallIf" ? "call" : "jump")}, because the check did not come out {OperatorName(op)}.", name);
                        _index++;
                    }
                    return;
                }

                case "CompareVarValue":
                {
                    string variable = Named(cmd, 0);
                    _pendingLeft = Value(cmd, 1);
                    AskVariable(cmd, variable,
                        $"The script is checking {variable} against {_pendingLeft}. What is it?");
                    return;
                }

                case "CompareVars":
                {
                    string left = Named(cmd, 0), right = Named(cmd, 1);
                    _pendingLeft = 0;
                    AskVariable(cmd, left,
                        $"The script is checking {left} against {right}. Give a value for {left}, taking {right} as 0.");
                    return;
                }

                case "CheckFlag":
                case "CheckTrainerFlag":
                {
                    string subject = (name == "CheckTrainerFlag" ? "trainer flag " : "flag ") + Display(cmd, 0);
                    _waitingOn = cmd;
                    Pending = new ScriptQuestion
                    {
                        Kind = ScriptQuestion.QuestionKind.Flag,
                        Subject = subject,
                        Prompt = $"The script is checking {subject}. Is it set?",
                        Options = new[] { ("Set", 1L), ("Not set", 0L) },
                    };
                    Add(ScriptStepKind.Question, Pending.Prompt, name);
                    return;
                }

                case "YesNoBox":
                    _waitingOn = cmd;
                    Pending = new ScriptQuestion
                    {
                        Kind = ScriptQuestion.QuestionKind.YesNo,
                        Subject = "the yes/no box",
                        Prompt = "The script asks the player yes or no. What do you answer?",
                        Options = new[] { ("Yes", 1L), ("No", 0L) },
                    };
                    Add(ScriptStepKind.Question, Pending.Prompt, name);
                    return;
            }

            // One of two lines depending on the player's gender. The preview has no save to ask, so it
            // shows the one a male player would get and says what the other one is.
            if (name == "GenderMessage")
            {
                int male = (int)Value(cmd, 0), female = (int)Value(cmd, 1);
                string text = _messageLookup?.Invoke(male);
                Add(ScriptStepKind.Message,
                    string.IsNullOrEmpty(text)
                        ? $"Shows message {male} to a male player, or {female} to a female one."
                        : $"Shows to a male player (a female one gets message {female}): “{text}”",
                    name);
                _index++;
                return;
            }

            // Picks one of the four shared message archives and leaves its number in a variable for a
            // later message command. Nothing is shown by this on its own.
            if (name == "GetCommonMessageArchive")
            {
                int which = (int)Value(cmd, 0), into = (int)Value(cmd, 1);
                string archive = FieldSharedMessageArchives.NameOf(which);
                Add(ScriptStepKind.Command,
                    archive == null
                        ? $"Looks up shared message archive {FieldScriptValues.Describe(which)} and puts it in {FieldScriptValues.Describe(into)}."
                        : $"Takes the shared messages for {archive} and puts that archive in {FieldScriptValues.Describe(into)}.",
                    name);
                _index++;
                return;
            }

            // A message read out of whichever archive a variable is pointing at.
            if (name == "MessageFromArchive")
            {
                int archive = (int)Value(cmd, 0), id = (int)Value(cmd, 1);
                Add(ScriptStepKind.Message,
                    $"Shows message {FieldScriptValues.Describe(id)} from archive {FieldScriptValues.Describe(archive)}.",
                    name);
                _index++;
                return;
            }

            if (name.StartsWith("Message", StringComparison.Ordinal) || name == "BoardMessage")
            {
                int id = (int)Value(cmd, 0);

                // A flexible slot may be a variable rather than a message number, and then there is no
                // way to know which line it lands on without running the game.
                if (FieldScriptValues.IsVariable(id))
                {
                    Add(ScriptStepKind.Message,
                        $"Shows whichever message {FieldScriptValues.Describe(id)} is holding.", name);
                    _index++;
                    return;
                }

                string text = _messageLookup?.Invoke(id);
                string where = name == "BoardMessage" ? "on the board" : null;
                Add(ScriptStepKind.Message,
                    string.IsNullOrEmpty(text)
                        ? $"Shows message {id}{(where == null ? "" : " " + where)}."
                        : where == null ? $"Shows: “{text}”" : $"Shows {where}: “{text}”",
                    name);
                _index++;
                return;
            }

            if (name == "Movement" || name == "SetOWMovement")
            {
                int who = (int)Value(cmd, 0);
                int movement = (int)Value(cmd, 1);
                string what = DescribeMovement(movement);
                Add(ScriptStepKind.Movement,
                    what == null
                        ? $"Tells {Display(cmd, 0)} to move: {Display(cmd, 1)}."
                        : $"Tells {Display(cmd, 0)} to move: {what}.",
                    name,
                    new ScriptEffect(ScriptEffectKind.Movement, who, movement));
                _index++;
                return;
            }

            // A board is built, then asked to show, then written into, then closed. Only the writing
            // puts words on screen; the rest is the sign being put up and taken down.
            switch (name)
            {
                case "SetTextBoard":
                    Add(ScriptStepKind.Command, $"Puts up a sign, type {Value(cmd, 0)}.", name);
                    _index++; return;
                case "SetIconBoard":
                    Add(ScriptStepKind.Command,
                        $"Puts up a sign with an icon, type {Value(cmd, 1)}.", name);
                    _index++; return;
                case "ShowBoard":
                    Add(ScriptStepKind.Command, "Shows the sign.", name);
                    _index++; return;
                case "CloseBoard":
                    Add(ScriptStepKind.Command, "Takes the sign down.", name);
                    _index++; return;
                case "WaitBoard":
                    Add(ScriptStepKind.Command, "Waits for the sign.", name);
                    _index++; return;
            }

            var effect = EffectFor(name, cmd);
            if (effect != null)
            {
                Add(ScriptStepKind.Command, DescribeEffect(name, cmd, effect), name, effect);
                _index++;
                return;
            }

            Add(ScriptStepKind.Command, $"Runs {cmd.name}.", name);
            _index++;
        }

        /// <summary>The sound and camera commands the preview can act on. </summary>
        private static ScriptEffect EffectFor(string name, ScriptCommand cmd)
        {
            switch (name)
            {
                case "PlayFanfare": return new ScriptEffect(ScriptEffectKind.SoundEffect, (int)Value(cmd, 0));
                case "PlaySound": return new ScriptEffect(ScriptEffectKind.Fanfare, (int)Value(cmd, 0));
                case "PlayMusic":
                case "TempMusic":
                case "SetMusic": return new ScriptEffect(ScriptEffectKind.Music, (int)Value(cmd, 0));
                case "StopMusic": return new ScriptEffect(ScriptEffectKind.MusicStop);
                case "PlayCry": return new ScriptEffect(ScriptEffectKind.Cry, (int)Value(cmd, 0));
                case "WaitFanfare":
                case "WaitSound":
                case "WaitCry": return new ScriptEffect(ScriptEffectKind.Wait);
                // DSPRE has no name for this one, so its raw number is matched as well.
                case "MoveSeamlessCamera":
                case "CMD_610": return new ScriptEffect(ScriptEffectKind.CameraChange, (int)Value(cmd, 0));
                case "ShakeCamera":
                    return new ScriptEffect(ScriptEffectKind.CameraShake,
                        (int)Value(cmd, 0), (int)Value(cmd, 1), (int)Value(cmd, 2), (int)Value(cmd, 3));
                default:
                    return null;
            }
        }

        /// <summary>A sound by name where the ROM knows one, so a line says what is actually playing.</summary>
        private static string SoundName(int id)
        {
            try
            {
                var names = DSPRE.Resources.ScriptDatabase.soundNames;
                if (names != null && names.TryGetValue((ushort)id, out string n) && !string.IsNullOrWhiteSpace(n))
                    return $"{id} ({n})";
                return id.ToString();
            }
            catch { return id.ToString(); }
        }

        /// <summary>A species by name where the ROM knows one, so a cry reads as more than a number.</summary>
        private static string PokemonName(int species)
        {
            try
            {
                var names = RomInfo.GetPokemonNames();
                return names != null && species >= 0 && species < names.Length && !string.IsNullOrWhiteSpace(names[species])
                    ? names[species] : $"Pokémon {species}";
            }
            catch { return $"Pokémon {species}"; }
        }

        private static string DescribeEffect(string name, ScriptCommand cmd, ScriptEffect e)
        {
            switch (e.Kind)
            {
                case ScriptEffectKind.SoundEffect: return $"Playing sound effect {SoundName(e.A)}.";
                case ScriptEffectKind.Fanfare: return $"Playing fanfare {SoundName(e.A)}, which pauses the music.";
                case ScriptEffectKind.Music: return $"Playing music {SoundName(e.A)}.";
                case ScriptEffectKind.MusicStop: return "Stops the music.";
                case ScriptEffectKind.Cry: return $"Playing the cry of {PokemonName(e.A)}.";
                case ScriptEffectKind.CameraChange:
                    return $"Moves the camera to setting {e.A}.";
                case ScriptEffectKind.CameraShake:
                    return $"Shakes the view by {e.A} across and {e.B} down, {e.C} times over {e.D} frames each.";
                case ScriptEffectKind.Wait: return "Waits for that to finish.";
                default: return $"Runs {cmd.name}.";
            }
        }

        /// <summary>
        /// Spells out what a movement actually does, rather than just naming its number.
        /// </summary>
        private string DescribeMovement(int movementNumber)
        {
            var actions = _actionLookup?.Invoke(movementNumber);
            if (actions == null || actions.Count == 0) return null;

            var parts = new List<string>();
            foreach (var action in actions)
            {
                if (action == null) continue;
                string step = action.name;
                if (string.IsNullOrEmpty(step)) continue;
                if (step.StartsWith("End", StringComparison.OrdinalIgnoreCase)) break;

                int times = action.repetitionCount ?? 1;
                parts.Add(times > 1 ? $"{step} ×{times}" : step);
                if (parts.Count >= 12) { parts.Add("…"); break; }
            }
            return parts.Count == 0 ? null : $"movement {movementNumber} ({string.Join(", ", parts)})";
        }

        private void AskVariable(ScriptCommand cmd, string subject, string prompt)
        {
            _waitingOn = cmd;
            Pending = new ScriptQuestion
            {
                Kind = ScriptQuestion.QuestionKind.Variable,
                Subject = subject,
                Prompt = prompt,
                Options = new[] { ("0", 0L), ("1", 1L) },
            };
            Add(ScriptStepKind.Question, prompt, CommandName(cmd));
        }

        private void GoTo(ScriptCommand cmd, string name, bool call, string why, int targetParam = 0)
        {
            int target = (int)Value(cmd, targetParam);
            var container = FindFunction(target);
            string where = container != null ? $"function {target}" : $"function {target}, which isn't in this file";

            Add(ScriptStepKind.Branch,
                $"{(call ? "Calls" : "Goes to")} {where}{(why == null ? "" : ", " + why)}.", name);

            if (container == null) { _index++; return; }
            if (call) _returns.Push((_current, _index + 1));
            _current = container;
            _index = 0;
        }

        private bool PopReturn()
        {
            if (_returns.Count == 0) return false;
            var (container, index) = _returns.Pop();
            _current = container;
            _index = index;
            return true;
        }

        // ── reading a command ────────────────────────────────────────────────────────────
        private static string CommandName(ScriptCommand cmd)
        {
            // ScriptCommand.name is the command plus its formatted parameters; the command is the first word.
            string full = cmd?.name ?? "";
            int space = full.IndexOf(' ');
            return space < 0 ? full : full.Substring(0, space);
        }

        private static long Value(ScriptCommand cmd, int param)
        {
            var data = cmd?.cmdParams;
            if (data == null || param < 0 || param >= data.Count) return 0;
            byte[] b = data[param];
            if (b == null) return 0;
            switch (b.Length)
            {
                case 1: return b[0];
                case 2: return BitConverter.ToUInt16(b, 0);
                case 4: return BitConverter.ToUInt32(b, 0);
                default: return 0;
            }
        }

        /// <summary>
        /// The readable form of one parameter, taken from the name the command already carries.
        /// </summary>
        /// <summary>
        /// A parameter that names a variable, written as what it is rather than a raw number. The
        /// command's own text wins when it already has a name for it, since that is the one the person
        /// editing the script sees.
        /// </summary>
        private static string Named(ScriptCommand cmd, int param)
        {
            string shown = Display(cmd, param);
            if (!string.IsNullOrEmpty(shown) && !long.TryParse(shown, out _)) return shown;

            int v = (int)Value(cmd, param);
            return FieldScriptValues.IsVariable(v) ? FieldScriptValues.Describe(v) : shown;
        }

        private static string Display(ScriptCommand cmd, int param)
        {
            string full = cmd?.name ?? "";
            var parts = full.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return param + 1 < parts.Length ? parts[param + 1] : Value(cmd, param).ToString();
        }

        private static int Operator(ScriptCommand cmd, int param) => (int)Value(cmd, param);

        private static string OperatorName(int op) => op switch
        {
            0 => "less", 1 => "equal", 2 => "greater",
            3 => "less or equal", 4 => "greater or equal", 5 => "different",
            _ => "operator " + op,
        };

        /// <summary>Whether a stored ordering satisfies the operator a jump is testing.</summary>
        private static bool Matches(int op, Relation r) => op switch
        {
            0 => r == Relation.Less,
            1 => r == Relation.Equal,
            2 => r == Relation.Greater,
            3 => r == Relation.Less || r == Relation.Equal,
            4 => r == Relation.Greater || r == Relation.Equal,
            5 => r != Relation.Equal,
            _ => false,
        };

        private static Relation Compare(long left, long right) =>
            left < right ? Relation.Less : left > right ? Relation.Greater : Relation.Equal;

        private static string Describe(Relation r) => r switch
        {
            Relation.Less => "less than",
            Relation.Greater => "greater than",
            _ => "equal to",
        };

        private ScriptCommandContainer FindScript(int number) =>
            _scripts.FirstOrDefault(s => s.manualUserID == (uint)number);

        private ScriptCommandContainer FindFunction(int number) =>
            _functions.FirstOrDefault(f => f.manualUserID == (uint)number);

        private void Add(ScriptStepKind kind, string text, string command, ScriptEffect effect = null)
        {
            _steps.Add(new ScriptStep
            {
                Kind = kind,
                Text = text,
                CommandName = command,
                Effect = effect,
                Location = _current == null ? "" : $"{_current.containerType} {_current.manualUserID}, line {_index + 1}",
            });
        }
    }
}
