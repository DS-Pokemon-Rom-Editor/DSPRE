using System;
using System.Collections.Generic;
using System.Linq;
using DSPRE.Resources;

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
        /// <summary>Play a movement: A is the overworld, B the movement number. The script carries straight on.</summary>
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
        /// <summary>Waits for the sound of kind A to finish.</summary>
        Wait,
        /// <summary>Prints Text into the message box. A is 1 when it prints all at once, B is 1 when A cannot hurry it.</summary>
        Message,
        /// <summary>Waits for A or B. A is 1 when the d-pad also carries on, 2 when it turns the player too.</summary>
        WaitButton,
        /// <summary>Opens the message box without writing in it.</summary>
        OpenMessage,
        /// <summary>Takes the message box away. A is 1 when the words stay on screen.</summary>
        CloseMessage,
        /// <summary>Waits until every movement the script started has finished.</summary>
        WaitMovement,
        /// <summary>Waits A frames.</summary>
        WaitFrames,
        /// <summary>Turns whoever the player is talking to round to face them.</summary>
        FacePlayer,
        /// <summary>Stops an overworld (A, or everyone when A is -1) wandering about.</summary>
        Lock,
        /// <summary>Lets an overworld (A, or everyone when A is -1) wander again.</summary>
        Release,
        /// <summary>Puts overworld A on the map (B is 1) or takes it off (B is 0).</summary>
        ShowObject,
        /// <summary>Puts the camera on an invisible object at tile A, B (C is 1), or back on the player (C is 0).</summary>
        CameraObject,
        /// <summary>HeartGold and SoulSilver's touch screen: A is 1 for the Poké Ball screen touch questions use, 0 for the touch menu.</summary>
        TouchScreen,
    }

    /// <summary>What a step asks for, with its numbers.</summary>
    public sealed class ScriptEffect
    {
        public ScriptEffectKind Kind;
        public int A, B, C, D;
        /// <summary>The words, for a message.</summary>
        public string Text;
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
        public enum QuestionKind
        {
            /// <summary>What a variable holds. Only asked when nothing has said yet.</summary>
            Variable,
            /// <summary>Whether a flag is set. Only asked when nothing has said yet.</summary>
            Flag,
            /// <summary>The game's own yes/no box, answered the way the player would.</summary>
            YesNo,
            /// <summary>One of the game's own menus, answered the way the player would.</summary>
            Menu,
            /// <summary>Something the save would say, like whether the bag has room. Asked once.</summary>
            Fact,
        }

        public QuestionKind Kind;
        /// <summary>The variable, flag or trainer the script is asking about.</summary>
        public string Subject;
        public string Prompt;
        /// <summary>Ready-made answers. A variable question also accepts any number.</summary>
        public IReadOnlyList<(string Label, long Value)> Options = Array.Empty<(string, long)>();
        public bool AcceptsAnyNumber => Kind == QuestionKind.Variable || (Kind == QuestionKind.Fact && TypedAllowed);

        /// <summary>Whether a fact can be any number, like how many Pokémon are in the party.</summary>
        public bool TypedAllowed;

        /// <summary>What a fact is remembered by, so the same question is not asked again.</summary>
        public string FactKey;

        /// <summary>Set when the preview is asking on its own account rather than a script asking.</summary>
        public bool FromPreview;

        /// <summary>Whether the player answers this in game, as opposed to the preview needing to know.</summary>
        public bool IsInGame => !FromPreview && (Kind == QuestionKind.YesNo || Kind == QuestionKind.Menu);

        /// <summary>Where a menu window sits, in tiles, and which entry the cursor starts on.</summary>
        public int X, Y, InitialCursor;

        /// <summary>Whether B backs out of a menu.</summary>
        public bool Cancellable;

        /// <summary>Set for a list menu, which shows eight rows at a time and pages with left and right.</summary>
        public bool IsList;

        /// <summary>A line for each entry, which a touch list writes on the top screen while the cursor is on it.</summary>
        public IReadOnlyList<string> Descriptions;

        /// <summary>Set for a touch screen menu, which the games draw on the bottom screen.</summary>
        public bool OnTouchScreen;

        /// <summary>The variable or flag number the answer is about.</summary>
        public int Number;
    }

    /// <summary>What the preview has been told about the game's flags and variables.</summary>
    public sealed class ScriptGameState
    {
        private readonly Dictionary<int, long> _vars = new Dictionary<int, long>();
        private readonly Dictionary<int, bool> _flags = new Dictionary<int, bool>();
        private readonly Dictionary<string, long> _facts = new Dictionary<string, long>();

        public event EventHandler Changed;

        /// <summary>Things the save would know, by the question that was asked.</summary>
        public IReadOnlyDictionary<string, long> Facts => _facts;

        public bool TryGetFact(string question, out long value) => _facts.TryGetValue(question, out value);

        public void SetFact(string question, long value)
        {
            _facts[question] = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public void ForgetFact(string question) { if (_facts.Remove(question)) Changed?.Invoke(this, EventArgs.Empty); }

        public bool TryGetVar(int number, out long value) => _vars.TryGetValue(number, out value);
        public bool TryGetFlag(int number, out bool set) => _flags.TryGetValue(number, out set);

        public void SetVar(int number, long value)
        {
            _vars[number] = value & 0xFFFF;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public void SetFlag(int number, bool set)
        {
            _flags[number] = set;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public void ForgetVar(int number) { if (_vars.Remove(number)) Changed?.Invoke(this, EventArgs.Empty); }
        public void ForgetFlag(int number) { if (_flags.Remove(number)) Changed?.Invoke(this, EventArgs.Empty); }

        public IReadOnlyDictionary<int, long> Vars => _vars;
        public IReadOnlyDictionary<int, bool> Flags => _flags;

        /// <summary>
        /// The script's own slots (SCWK) only live as long as one script, so what one script was told about
        /// them says nothing about the next.
        /// </summary>
        public void ForgetScriptSlots()
        {
            var slots = _vars.Keys.Where(k => k >= FieldScriptValues.ScriptFirst).ToList();
            foreach (int k in slots) _vars.Remove(k);
            if (slots.Count > 0) Changed?.Invoke(this, EventArgs.Empty);
        }

        public void Clear()
        {
            _vars.Clear();
            _flags.Clear();
            _facts.Clear();
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Where a script's commands, functions, words and movements come from.</summary>
    public sealed class ScriptSource
    {
        public IReadOnlyList<ScriptCommandContainer> Scripts = Array.Empty<ScriptCommandContainer>();
        public IReadOnlyList<ScriptCommandContainer> Functions = Array.Empty<ScriptCommandContainer>();
        public Func<int, string> Messages;
        public Func<int, IReadOnlyList<ScriptAction>> Actions;
        /// <summary>Which script of the file to start at, when it is not the number asked for.</summary>
        public int StartId = -1;
    }

    /// <summary>
    /// Runs an event's script one command at a time and says what each does, without the game. It stops
    /// wherever only the watcher can say what happens next.
    /// </summary>
    public sealed class ScriptWalker
    {
        /// <summary>How the game orders two values. The script stores this, then a later jump tests it.</summary>
        private enum Relation { Less = 0, Equal = 1, Greater = 2 }

        /// <summary>The object numbers scripts use for someone other than an event on the map.</summary>
        public const int PlayerObject = 0xFF;
        public const int CameraObject = 0xF1;
        public const int PartnerObject = 0xF2;
        public const int FollowerObject = 0xFD;

        /// <summary>VAR_LAST_TALKED: the engine leaves the local id of whoever was talked to here.</summary>
        public const int LastTalkedVar = 0x800D;

        private ScriptSource _source;
        private readonly List<ScriptStep> _steps = new List<ScriptStep>();
        private readonly Stack<(ScriptSource source, ScriptCommandContainer container, int index)> _returns
            = new Stack<(ScriptSource, ScriptCommandContainer, int)>();

        private ScriptCommandContainer _current;
        private int _index;
        private Relation? _relation;
        private ScriptCommand _waitingOn;
        private long _pendingLeft;              // the value being compared, once known
        private int _pendingVar = -1;            // where an answer goes
        private int _guard;

        // A menu is built up an entry at a time before it is shown.
        private ScriptQuestion _menu;
        private List<(string, long)> _menuOptions;
        private List<string> _menuDescriptions;
        private bool _menuGlobalText;

        /// <summary>Stops a script that jumps back on itself from running away.</summary>
        public const int MaxSteps = 2000;

        public IReadOnlyList<ScriptStep> Steps => _steps;
        public ScriptQuestion Pending { get; private set; }
        public bool Finished { get; private set; }

        /// <summary>What the watcher has said, and what the script has set, so nothing is asked twice.</summary>
        public ScriptGameState State { get; set; } = new ScriptGameState();

        /// <summary>Finds the file a shared script lives in, by its number. Null leaves them unfollowed.</summary>
        public Func<int, ScriptSource> CommonScripts { get; set; }

        /// <summary>Reads one of the shared menu entries. Null names the entry by number instead.</summary>
        public Func<int, string> MenuText { get; set; }

        /// <summary>Reads a message out of another archive: archive, then message.</summary>
        public Func<int, int, string> ArchiveText { get; set; }

        /// <summary>Which archive each of the four shared message archives is.</summary>
        public Func<int, int> SharedArchive { get; set; }

        /// <summary>Where the player stands, in whole-matrix tiles. Null when there is no player.</summary>
        public Func<(int x, int z)?> PlayerPosition { get; set; }

        /// <summary>
        /// Takes anything not known yet as 0 or not set instead of asking, for scripts that run while a map
        /// loads and so have nobody to ask.
        /// </summary>
        public bool GuessUnknowns { get; set; }

        /// <summary>Looks up the old database name for a command number, which is what the steps match on.</summary>
        public Func<ushort, string> LegacyNameOf { get; set; } = DefaultLegacyName;

        /// <param name="messageLookup">Turns a message id into its text. Null just names the id instead.</param>
        public ScriptWalker(ScriptFile file, Func<int, string> messageLookup = null)
            : this(file?.allScripts, file?.allFunctions, messageLookup, ActionsFrom(file)) { }

        /// <summary>Walks a set of scripts and functions directly, without needing the file they live in.</summary>
        public ScriptWalker(IReadOnlyList<ScriptCommandContainer> scripts,
                            IReadOnlyList<ScriptCommandContainer> functions,
                            Func<int, string> messageLookup = null,
                            Func<int, IReadOnlyList<ScriptAction>> actionLookup = null)
        {
            _source = new ScriptSource
            {
                Scripts = scripts ?? Array.Empty<ScriptCommandContainer>(),
                Functions = functions ?? Array.Empty<ScriptCommandContainer>(),
                Messages = messageLookup,
                Actions = actionLookup,
            };
        }

        private static Func<int, IReadOnlyList<ScriptAction>> ActionsFrom(ScriptFile file) => ActionsById(file?.allActions);

        /// <summary>
        /// Finds a movement by the number a Movement command carries. That is the movement's own id, which
        /// the reader and the writer both match on, not its position in the list.
        /// </summary>
        public static Func<int, IReadOnlyList<ScriptAction>> ActionsById(IReadOnlyList<ScriptActionContainer> actions)
        {
            if (actions == null) return null;
            return number => number < 0 ? null : actions.FirstOrDefault(a => a.manualUserID == (uint)number)?.commands;
        }

        /// <summary>The movement a number names in the file the script is running from now.</summary>
        public IReadOnlyList<ScriptAction> ActionsFor(int movementNumber) => _source?.Actions?.Invoke(movementNumber);

        // Started with Start, an answer carries the walk on to the next question; started with Begin, the
        // caller takes each command itself.
        private bool _eager;

        /// <summary>Starts at one of the file's scripts. Walks until it ends or needs an answer.</summary>
        public void Start(int scriptNumber)
        {
            Begin(scriptNumber);
            _eager = true;
            while (Next()) { }
        }

        /// <summary>Gets ready to run one of the file's scripts, without running any of it yet.</summary>
        public void Begin(int scriptNumber)
        {
            _steps.Clear();
            _returns.Clear();
            Pending = null;
            Finished = false;
            _eager = false;
            _relation = null;
            _waitingOn = null;
            _menu = null;
            _guard = 0;
            State.ForgetScriptSlots();

            _current = FindScript(_source, scriptNumber);
            _index = 0;
            if (_current == null)
            {
                Add(ScriptStepKind.Ended, $"There is no script {scriptNumber} in this file.", null);
                Finished = true;
            }
        }

        /// <summary>
        /// Runs one command. False once the script has ended or is waiting on an answer.
        /// </summary>
        public bool Next()
        {
            if (Finished || Pending != null) return false;

            if (++_guard > MaxSteps)
            {
                Add(ScriptStepKind.Ended, "Stopped: this script keeps going round and does not finish.", null);
                Finished = true;
                return false;
            }

            if (_current == null || _index < 0 || _index >= (_current.commands?.Count ?? 0))
            {
                if (!PopReturn()) { Add(ScriptStepKind.Ended, "The script ends here.", null); Finished = true; }
                return !Finished;
            }

            Step(_current.commands[_index]);
            return !Finished && Pending == null;
        }

        /// <summary>Answers the question it stopped on, and carries on the way it was started.</summary>
        public void Answer(long value)
        {
            if (Pending == null) return;

            var q = Pending;
            Pending = null;

            switch (q.Kind)
            {
                case ScriptQuestion.QuestionKind.Variable:
                    State.SetVar(q.Number, value);
                    _relation = Compare(value, _pendingLeft);
                    Add(ScriptStepKind.Branch,
                        $"You said {q.Subject} is {value}, so the script treats it as {Describe(_relation.Value)} {_pendingLeft}.",
                        CommandName(_waitingOn));
                    break;

                case ScriptQuestion.QuestionKind.Flag:
                    // A flag test leaves the script "equal" when the flag is set, which is what the
                    // following JumpIf EQUAL / JumpIf DIFFERENT pair is written against.
                    if (q.Number >= 0) State.SetFlag(q.Number, value != 0);
                    _relation = value != 0 ? Relation.Equal : Relation.Less;
                    Add(ScriptStepKind.Branch,
                        $"You said {q.Subject} is {(value != 0 ? "set" : "not set")}.", CommandName(_waitingOn));
                    break;

                case ScriptQuestion.QuestionKind.YesNo:
                    if (_pendingVar >= 0) State.SetVar(_pendingVar, value);
                    Add(ScriptStepKind.Branch, $"You answered {(value == YesValue ? "yes" : "no")}.", CommandName(_waitingOn));
                    break;

                case ScriptQuestion.QuestionKind.Fact:
                    if (_pendingVar >= 0) State.SetVar(_pendingVar, value);
                    if (q.FactKey != null) State.SetFact(q.FactKey, value);
                    string said = q.Options.FirstOrDefault(o => o.Value == value).Label ?? value.ToString();
                    Add(ScriptStepKind.Branch, $"You said: {said}.", CommandName(_waitingOn));
                    break;

                case ScriptQuestion.QuestionKind.Menu:
                    if (_pendingVar >= 0) State.SetVar(_pendingVar, value);
                    string label = q.Options.FirstOrDefault(o => o.Value == value).Label ?? value.ToString();
                    Add(ScriptStepKind.Branch, $"You picked {label}.", CommandName(_waitingOn));
                    break;
            }

            _waitingOn = null;
            _pendingVar = -1;
            _index++;
            if (_eager) while (Next()) { }
        }

        /// <summary>What a yes/no box writes for yes. No is one.</summary>
        public const long YesValue = 0;

        /// <summary>The menu value written when B backs out.</summary>
        public const long MenuCancelled = 0xFFFE;

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

                case "CommonScript":
                    CallCommon(cmd, name);
                    return;

                case "JumpIf":
                case "CallIf":
                {
                    var op = Operator(cmd, 0);
                    if (!_relation.HasValue)
                    {
                        Add(ScriptStepKind.Command,
                            $"{name} with nothing tested before it, so it carries straight on.", name);
                        _index++;
                        return;
                    }
                    bool take = Matches(op, _relation.Value);
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
                    int number = (int)Value(cmd, 0);
                    string variable = Named(cmd, 0);
                    _pendingLeft = Value(cmd, 1);
                    if (TryVar(number, out long known))
                    {
                        _relation = Compare(known, _pendingLeft);
                        Add(ScriptStepKind.Branch,
                            $"{variable} holds {known}, which is {Describe(_relation.Value)} {_pendingLeft}.", name);
                        _index++;
                        return;
                    }
                    if (GuessUnknowns)
                    {
                        _relation = Compare(0, _pendingLeft);
                        Add(ScriptStepKind.Branch, $"Nothing says what {variable} holds, so it is taken as 0.", name);
                        _index++;
                        return;
                    }
                    AskVariable(cmd, number, variable,
                        $"The script is checking {variable} against {_pendingLeft}. What is it?");
                    return;
                }

                case "CompareVars":
                {
                    int leftNumber = (int)Value(cmd, 0), rightNumber = (int)Value(cmd, 1);
                    string left = Named(cmd, 0), right = Named(cmd, 1);
                    bool knowRight = TryVar(rightNumber, out long rightValue);
                    _pendingLeft = knowRight ? rightValue : 0;
                    if (knowRight && TryVar(leftNumber, out long leftValue))
                    {
                        _relation = Compare(leftValue, rightValue);
                        Add(ScriptStepKind.Branch,
                            $"{left} holds {leftValue} and {right} holds {rightValue}.", name);
                        _index++;
                        return;
                    }
                    if (GuessUnknowns)
                    {
                        _relation = Compare(TryVar(leftNumber, out long guessLeft) ? guessLeft : 0, _pendingLeft);
                        Add(ScriptStepKind.Branch, $"Nothing says what {left} or {right} hold, so they are taken as 0.", name);
                        _index++;
                        return;
                    }
                    AskVariable(cmd, leftNumber, left, knowRight
                        ? $"The script is checking {left} against {right}, which holds {rightValue}. What is {left}?"
                        : $"The script is checking {left} against {right}. Give a value for {left}, taking {right} as 0.");
                    return;
                }

                case "CheckFlag":
                case "CheckTrainerFlag":
                {
                    int number = (int)Value(cmd, 0);
                    bool trainer = name == "CheckTrainerFlag";
                    string subject = (trainer ? "trainer flag " : "flag ") + Display(cmd, 0);
                    // Trainer flags live in a range of their own, so they are remembered apart.
                    int key = trainer ? TrainerFlagKey(number) : number;
                    if (FieldScriptValues.IsVariable(number) && !trainer)
                    {
                        if (TryVar(number, out long flagNumber)) key = (int)flagNumber;
                        else key = -1;
                    }
                    if (key >= 0 && State.TryGetFlag(key, out bool set))
                    {
                        _relation = set ? Relation.Equal : Relation.Less;
                        Add(ScriptStepKind.Branch, $"{Capital(subject)} is {(set ? "set" : "not set")}.", name);
                        _index++;
                        return;
                    }
                    if (GuessUnknowns)
                    {
                        _relation = Relation.Less;
                        Add(ScriptStepKind.Branch, $"Nothing says whether {subject} is set, so it is taken as not set.", name);
                        _index++;
                        return;
                    }
                    _waitingOn = cmd;
                    Pending = new ScriptQuestion
                    {
                        Kind = ScriptQuestion.QuestionKind.Flag,
                        Subject = subject,
                        Number = key,
                        Prompt = $"The script is checking {subject}. Is it set?",
                        Options = new[] { ("Set", 1L), ("Not set", 0L) },
                    };
                    Add(ScriptStepKind.Question, Pending.Prompt, name);
                    return;
                }

                case "SetFlag":
                case "ClearFlag":
                case "SetTrainerFlag":
                case "ClearTrainerFlag":
                {
                    int number = (int)Value(cmd, 0);
                    bool trainer = name.Contains("Trainer");
                    bool set = name.StartsWith("Set", StringComparison.Ordinal);
                    int key = trainer ? TrainerFlagKey(number) : number;
                    if (!trainer && FieldScriptValues.IsVariable(number))
                        key = TryVar(number, out long flagNumber) ? (int)flagNumber : -1;
                    if (key >= 0) State.SetFlag(key, set);
                    Add(ScriptStepKind.Command,
                        $"{(set ? "Sets" : "Clears")} {(trainer ? "trainer flag" : "flag")} {Display(cmd, 0)}.", name);
                    _index++;
                    return;
                }

                case "SetVar":
                case "SetVarFromVariable":
                case "SetVarFromFlexible":
                case "IncrementVar":
                case "DecrementVar":
                {
                    int target = (int)Value(cmd, 0);
                    int raw = (int)Value(cmd, 1);
                    bool known = name == "SetVarFromVariable"
                        ? State.TryGetVar(raw, out long source)
                        : TryValue(raw, out source);
                    string what = Named(cmd, 0);

                    if (name == "IncrementVar" || name == "DecrementVar")
                    {
                        bool add = name == "IncrementVar";
                        if (known && TryVar(target, out long before))
                            State.SetVar(target, add ? before + source : before - source);
                        else State.ForgetVar(target);
                        Add(ScriptStepKind.Command, $"{(add ? "Adds" : "Takes")} {Display(cmd, 1)} {(add ? "to" : "from")} {what}.", name);
                    }
                    else
                    {
                        if (known) State.SetVar(target, source);
                        else State.ForgetVar(target);
                        Add(ScriptStepKind.Command,
                            known ? $"Puts {source} in {what}." : $"Puts {Named(cmd, 1)} in {what}.", name);
                    }
                    _index++;
                    return;
                }

                case "GetPlayerPosition":
                {
                    var at = PlayerPosition?.Invoke();
                    int xVar = (int)Value(cmd, 0), zVar = (int)Value(cmd, 1);
                    if (at != null)
                    {
                        State.SetVar(xVar, at.Value.x);
                        State.SetVar(zVar, at.Value.z);
                        Add(ScriptStepKind.Command, $"Notes the player is at {at.Value.x}, {at.Value.z}.", name);
                    }
                    else
                    {
                        State.ForgetVar(xVar);
                        State.ForgetVar(zVar);
                        Add(ScriptStepKind.Command, "Notes where the player is.", name);
                    }
                    _index++;
                    return;
                }

                case "LockCamera":
                {
                    TryValue((int)Value(cmd, 0), out long x);
                    TryValue((int)Value(cmd, 1), out long z);
                    Add(ScriptStepKind.Command, $"Puts the camera on its own at {x}, {z}.", name,
                        new ScriptEffect(ScriptEffectKind.CameraObject, (int)x, (int)z, 1));
                    _index++;
                    return;
                }

                case "ReleaseCamera":
                    Add(ScriptStepKind.Command, "Hands the camera back to the player.", name,
                        new ScriptEffect(ScriptEffectKind.CameraObject, 0, 0, 0));
                    _index++;
                    return;

                case "CheckItemSpace":
                case "CheckItem":
                {
                    int item = (int)Value(cmd, 0);
                    string count = TryValue((int)Value(cmd, 1), out long n) ? n.ToString() : Named(cmd, 1);
                    bool space = name == "CheckItemSpace";
                    AskFact(cmd, name, varParam: 2,
                        key: $"{name} {item} {count}",
                        prompt: space ? $"Is there room in the bag for {count} {ItemName(item)}?"
                                      : $"Does the player have {count} {ItemName(item)}?",
                        options: space ? new[] { ("Room", 1L), ("No room", 0L) } : new[] { ("Has it", 1L), ("Does not", 0L) });
                    return;
                }

                case "CheckBadge":
                {
                    string badge = TryValue((int)Value(cmd, 0), out long b) ? b.ToString() : Named(cmd, 0);
                    AskFact(cmd, name, varParam: 1, key: $"{name} {badge}",
                        prompt: $"Does the player have badge {badge}?",
                        options: new[] { ("Has it", 1L), ("Does not", 0L) });
                    return;
                }

                case "CheckPlayerGender":
                    AskFact(cmd, name, varParam: 0, key: name,
                        prompt: "Is the player a boy or a girl?",
                        options: new[] { ("Boy", 0L), ("Girl", 1L) });
                    return;

                case "GetPartyCount":
                    AskFact(cmd, name, varParam: 0, key: name,
                        prompt: "How many Pokémon are in the party?",
                        options: new[] { ("1", 1L), ("2", 2L), ("3", 3L), ("6", 6L) }, typed: true);
                    return;

                case "YesNoBox":
                case "YesNoTouchScreen":
                {
                    _waitingOn = cmd;
                    _pendingVar = (int)Value(cmd, 0);
                    Pending = new ScriptQuestion
                    {
                        Kind = ScriptQuestion.QuestionKind.YesNo,
                        Subject = "the yes/no box",
                        Number = _pendingVar,
                        OnTouchScreen = name == "YesNoTouchScreen",
                        Prompt = "Yes or no?",
                        Options = new[] { ("YES", YesValue), ("NO", 1L) },
                    };
                    Add(ScriptStepKind.Question, "Asks the player yes or no.", name);
                    return;
                }

                case "MultiStandardText":
                case "MultiLocalText":
                case "ListStandardText":
                case "ListLocalText":
                case "MultiTouchStandardText":
                case "MultiTouchLocalText":
                {
                    _menu = new ScriptQuestion
                    {
                        Kind = ScriptQuestion.QuestionKind.Menu,
                        X = (int)Value(cmd, 0),
                        Y = (int)Value(cmd, 1),
                        // The touch lists always start on their first entry.
                        InitialCursor = name.StartsWith("MultiTouch", StringComparison.Ordinal) ? 0 : (int)Value(cmd, 2),
                        Cancellable = Value(cmd, 3) != 0,
                        IsList = name.StartsWith("List", StringComparison.Ordinal),
                        OnTouchScreen = name.StartsWith("MultiTouch", StringComparison.Ordinal),
                        Number = (int)Value(cmd, 4),
                        Subject = "the menu",
                        Prompt = "Pick one.",
                    };
                    _menuOptions = new List<(string, long)>();
                    _menuDescriptions = new List<string>();
                    _menuGlobalText = name.Contains("Standard");
                    Add(ScriptStepKind.Command, "Gets a menu ready.", name);
                    _index++;
                    return;
                }

                case "AddMultiOption":
                case "AddListOption":
                case "CreateMultiTouchBox":
                {
                    int message = (int)Value(cmd, 0);
                    // A list or touch entry carries a second message for its description before its value.
                    bool described = name != "AddMultiOption";
                    long value = Value(cmd, described ? 2 : 1);
                    string text = _menuGlobalText ? MenuText?.Invoke(message) : _source.Messages?.Invoke(message);
                    string label = string.IsNullOrEmpty(text) ? $"entry {message}" : FirstLine(text);
                    _menuOptions?.Add((label, value));
                    string about = null;
                    int aboutMessage = described ? (int)Value(cmd, 1) : 0xFF;
                    if (aboutMessage != 0xFF && aboutMessage != 0xFFFF)
                        about = _menuGlobalText ? MenuText?.Invoke(aboutMessage) : _source.Messages?.Invoke(aboutMessage);
                    _menuDescriptions?.Add(about);
                    Add(ScriptStepKind.Command, $"Adds “{label}” to the menu.", name);
                    _index++;
                    return;
                }

                case "ShowMulti":
                case "ShowList":
                case "MultiColumn":
                case "CloseMultiTouch":
                {
                    if (_menu == null || _menuOptions == null || _menuOptions.Count == 0)
                    {
                        Add(ScriptStepKind.Command, "Shows a menu that has nothing in it.", name);
                        _index++;
                        return;
                    }
                    _waitingOn = cmd;
                    _pendingVar = _menu.Number;
                    _menu.Options = _menuOptions;
                    _menu.Descriptions = _menuDescriptions;
                    Pending = _menu;
                    _menu = null;
                    Add(ScriptStepKind.Question,
                        $"Shows a menu: {string.Join(", ", _menuOptions.Select(o => o.Item1))}.", name);
                    return;
                }
            }

            // One of two lines depending on the player's gender. The preview has no save to ask, so it
            // shows the one a male player would get and says what the other one is.
            if (name == "GenderMessage")
            {
                int male = (int)Value(cmd, 0), female = (int)Value(cmd, 1);
                string text = _source.Messages?.Invoke(male);
                Add(ScriptStepKind.Message,
                    string.IsNullOrEmpty(text)
                        ? $"Shows message {male} to a male player, or {female} to a female one."
                        : $"Shows to a male player (a female one gets message {female}): “{text}”",
                    name, MessageEffect(text));
                _index++;
                return;
            }

            // Picks one of the four shared message archives and leaves its number in a variable for a
            // later message command. Nothing is shown by this on its own.
            if (name == "GetCommonMessageArchive")
            {
                int which = (int)Value(cmd, 0), into = (int)Value(cmd, 1);
                string archive = FieldSharedMessageArchives.NameOf(which);
                int? archiveId = SharedArchive?.Invoke(which);
                if (archiveId != null && archiveId >= 0) State.SetVar(into, archiveId.Value);
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
                string text = null;
                if (TryValue(archive, out long archiveId) && TryValue(id, out long messageId))
                    text = ArchiveText?.Invoke((int)archiveId, (int)messageId);
                Add(ScriptStepKind.Message,
                    string.IsNullOrEmpty(text)
                        ? $"Shows message {FieldScriptValues.Describe(id)} from archive {FieldScriptValues.Describe(archive)}."
                        : $"Shows: “{text}”",
                    name, MessageEffect(text));
                _index++;
                return;
            }

            if (IsMessageCommand(name))
            {
                int id = (int)Value(cmd, 0);

                // A flexible slot may be a variable rather than a message number, and then there is no
                // way to know which line it lands on without running the game.
                if (FieldScriptValues.IsVariable(id))
                {
                    string held = TryVar(id, out long heldId) ? _source.Messages?.Invoke((int)heldId) : null;
                    Add(ScriptStepKind.Message,
                        held == null ? $"Shows whichever message {FieldScriptValues.Describe(id)} is holding."
                                     : $"Shows: “{held}”",
                        name, MessageEffect(held));
                    _index++;
                    return;
                }

                string text = _source.Messages?.Invoke(id);
                string where = name == "BoardMessage" ? "on the board" : null;
                var effect = MessageEffect(text);
                // MessageAll prints the whole thing at once; MessageNoSkip cannot be hurried.
                if (effect != null && name == "MessageAll") effect.A = 1;
                if (effect != null && name == "MessageNoSkip") effect.B = 1;
                Add(ScriptStepKind.Message,
                    string.IsNullOrEmpty(text)
                        ? $"Shows message {id}{(where == null ? "" : " " + where)}."
                        : where == null ? $"Shows: “{text}”" : $"Shows {where}: “{text}”",
                    name, effect);
                _index++;
                return;
            }

            if (name == "Movement")
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
                    Add(ScriptStepKind.Command, "Takes the sign down.", name,
                        new ScriptEffect(ScriptEffectKind.CloseMessage));
                    _index++; return;
                case "WaitBoard":
                    Add(ScriptStepKind.Command, "Waits for the sign.", name);
                    _index++; return;
            }

            var effectFor = EffectFor(name, cmd);
            if (effectFor != null)
            {
                Add(ScriptStepKind.Command, DescribeEffect(name, cmd, effectFor), name, effectFor);
                _index++;
                return;
            }

            Add(ScriptStepKind.Command, $"Runs {cmd.name}.", name);
            _index++;
        }

        private static bool IsMessageCommand(string name) =>
            name == "Message" || name == "MessageAll" || name == "MessageFlex" || name == "MessageNoSkip"
            || name == "BoardMessage" || name == "TrainerMessage";

        private static ScriptEffect MessageEffect(string text) =>
            string.IsNullOrEmpty(text) ? null : new ScriptEffect(ScriptEffectKind.Message) { Text = text };

        /// <summary>The commands the preview plays out rather than just naming.</summary>
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
                case "WaitFanfare": return new ScriptEffect(ScriptEffectKind.Wait, (int)ScriptEffectKind.SoundEffect);
                case "WaitSound": return new ScriptEffect(ScriptEffectKind.Wait, (int)ScriptEffectKind.Fanfare);
                case "WaitCry": return new ScriptEffect(ScriptEffectKind.Wait, (int)ScriptEffectKind.Cry);
                // DSPRE has no name for this one, so its raw number is matched as well.
                case "MoveSeamlessCamera":
                case "CMD_610": return new ScriptEffect(ScriptEffectKind.CameraChange, (int)Value(cmd, 0));
                case "ShakeCamera":
                    // Platinum's command of that name takes one number and is not the HGSS screen shake.
                    if ((cmd?.cmdParams?.Count ?? 0) < 4) return null;
                    return new ScriptEffect(ScriptEffectKind.CameraShake,
                        (int)Value(cmd, 0), (int)Value(cmd, 1), (int)Value(cmd, 2), (int)Value(cmd, 3));

                case "WaitAB": return new ScriptEffect(ScriptEffectKind.WaitButton);
                case "WaitButton": return new ScriptEffect(ScriptEffectKind.WaitButton, 2);
                case "WaitABPad": return new ScriptEffect(ScriptEffectKind.WaitButton, 1);
                case "OpenMessage": return new ScriptEffect(ScriptEffectKind.OpenMessage);
                case "CloseMessage": return new ScriptEffect(ScriptEffectKind.CloseMessage);
                case "FreezeMessage": return new ScriptEffect(ScriptEffectKind.CloseMessage, 1);
                case "WaitMovement": return new ScriptEffect(ScriptEffectKind.WaitMovement);
                case "WaitTime": return new ScriptEffect(ScriptEffectKind.WaitFrames, (int)Value(cmd, 0));
                case "FacePlayer": return new ScriptEffect(ScriptEffectKind.FacePlayer);
                case "LockAll": return new ScriptEffect(ScriptEffectKind.Lock, -1);
                case "ReleaseAll": return new ScriptEffect(ScriptEffectKind.Release, -1);
                case "Lock": return new ScriptEffect(ScriptEffectKind.Lock, (int)Value(cmd, 0));
                case "Release": return new ScriptEffect(ScriptEffectKind.Release, (int)Value(cmd, 0));
                case "AddOW": return new ScriptEffect(ScriptEffectKind.ShowObject, (int)Value(cmd, 0), 1);
                case "RemoveOW": return new ScriptEffect(ScriptEffectKind.ShowObject, (int)Value(cmd, 0), 0);
                case "OpenTouchScreen": return new ScriptEffect(ScriptEffectKind.TouchScreen, 1);
                case "CloseTouchScreen": return new ScriptEffect(ScriptEffectKind.TouchScreen, 0);
                default:
                    return null;
            }
        }

        /// <summary>A sound by name where the ROM knows one, so a line says what is actually playing.</summary>
        private static string SoundName(int id)
        {
            try
            {
                var names = ScriptDatabase.soundNames;
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
                case ScriptEffectKind.WaitButton: return "Waits for the player to press a button.";
                case ScriptEffectKind.OpenMessage: return "Opens the message box.";
                case ScriptEffectKind.CloseMessage: return e.A == 1 ? "Stops the message box, leaving the words up." : "Closes the message box.";
                case ScriptEffectKind.WaitMovement: return "Waits for everyone to finish moving.";
                case ScriptEffectKind.WaitFrames: return $"Waits {e.A} frames.";
                case ScriptEffectKind.FacePlayer: return "Turns to face the player.";
                case ScriptEffectKind.TouchScreen: return e.A == 1 ? "Swaps the touch menu for the Poké Ball screen." : "Puts the touch menu back.";
                case ScriptEffectKind.Lock: return e.A < 0 ? "Stops everyone on the map." : $"Stops {Display(cmd, 0)}.";
                case ScriptEffectKind.Release: return e.A < 0 ? "Lets everyone move again." : $"Lets {Display(cmd, 0)} move again.";
                case ScriptEffectKind.ShowObject:
                    return e.B == 1 ? $"Puts {Display(cmd, 0)} on the map." : $"Takes {Display(cmd, 0)} off the map.";
                case ScriptEffectKind.CameraObject:
                    return e.C == 1 ? "Puts the camera on its own." : "Hands the camera back to the player.";
                default: return $"Runs {cmd.name}.";
            }
        }

        /// <summary>
        /// Spells out what a movement actually does, rather than just naming its number.
        /// </summary>
        private string DescribeMovement(int movementNumber)
        {
            var actions = _source.Actions?.Invoke(movementNumber);
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

        /// <summary>
        /// Asks for something the save would know and writes it where the command writes it. An answer given
        /// once is used again the next time the same thing is asked.
        /// </summary>
        private void AskFact(ScriptCommand cmd, string name, int varParam, string key, string prompt,
                             (string, long)[] options, bool typed = false)
        {
            int into = (int)Value(cmd, varParam);
            if (State.TryGetFact(key, out long known))
            {
                State.SetVar(into, known);
                string label = options.FirstOrDefault(o => o.Item2 == known).Item1 ?? known.ToString();
                Add(ScriptStepKind.Branch, $"{prompt} You said: {label}.", name);
                _index++;
                return;
            }
            if (GuessUnknowns)
            {
                State.SetVar(into, options[0].Item2);
                Add(ScriptStepKind.Branch, $"{prompt} Nobody can say while the map loads, so it is taken as {options[0].Item1}.", name);
                _index++;
                return;
            }

            _waitingOn = cmd;
            _pendingVar = into;
            Pending = new ScriptQuestion
            {
                Kind = ScriptQuestion.QuestionKind.Fact,
                Subject = key,
                FactKey = key,
                Number = into,
                Prompt = prompt,
                Options = options,
                TypedAllowed = typed,
            };
            Add(ScriptStepKind.Question, prompt, name);
        }

        /// <summary>An item by name where the ROM knows one.</summary>
        private static string ItemName(int item)
        {
            try
            {
                var names = ScriptDatabase.itemNames;
                return names != null && names.TryGetValue((ushort)item, out string n) && !string.IsNullOrWhiteSpace(n)
                    ? n : $"item {item}";
            }
            catch { return $"item {item}"; }
        }

        private void AskVariable(ScriptCommand cmd, int number, string subject, string prompt)
        {
            _waitingOn = cmd;
            Pending = new ScriptQuestion
            {
                Kind = ScriptQuestion.QuestionKind.Variable,
                Subject = subject,
                Number = number,
                Prompt = prompt,
                Options = new[] { ("0", 0L), ("1", 1L) },
            };
            Add(ScriptStepKind.Question, prompt, CommandName(cmd));
        }

        private void GoTo(ScriptCommand cmd, string name, bool call, string why, int targetParam = 0)
        {
            int target = (int)Value(cmd, targetParam);
            var container = FindFunction(_source, target);
            string where = container != null ? $"function {target}" : $"function {target}, which isn't in this file";

            Add(ScriptStepKind.Branch,
                $"{(call ? "Calls" : "Goes to")} {where}{(why == null ? "" : ", " + why)}.", name);

            if (container == null) { _index++; return; }
            if (call) _returns.Push((_source, _current, _index + 1));
            _current = container;
            _index = 0;
        }

        /// <summary>Follows a call into one of the shared script files, and comes back afterwards.</summary>
        private void CallCommon(ScriptCommand cmd, string name)
        {
            int id = (int)Value(cmd, 0);
            var source = CommonScripts?.Invoke(id);
            var container = source == null ? null : FindScript(source, source.StartId >= 0 ? source.StartId : id);
            if (container == null)
            {
                Add(ScriptStepKind.Command, $"Runs shared script {id}.", name);
                _index++;
                return;
            }

            Add(ScriptStepKind.Branch, $"Runs shared script {id}.", name);
            _returns.Push((_source, _current, _index + 1));
            _source = source;
            _current = container;
            _index = 0;
        }

        private bool PopReturn()
        {
            if (_returns.Count == 0) return false;
            var (source, container, index) = _returns.Pop();
            _source = source;
            _current = container;
            _index = index;
            return true;
        }

        // ── reading a command ────────────────────────────────────────────────────────────

        /// <summary>
        /// The name the steps are matched on. Scripts can be written with the old database names or the
        /// newer ones, and many of the newer ones are only numbers, so the command number decides.
        /// </summary>
        private string CommandName(ScriptCommand cmd)
        {
            if (cmd == null) return null;
            if (cmd.id != null)
            {
                string legacy = LegacyNameOf?.Invoke(cmd.id.Value);
                if (!string.IsNullOrEmpty(legacy)) return legacy;
            }
            // ScriptCommand.name is the command plus its formatted parameters; the command is the first word.
            string full = cmd.name ?? "";
            int space = full.IndexOf(' ');
            return space < 0 ? full : full.Substring(0, space);
        }

        /// <summary>The old database name for a command number in the ROM that is open.</summary>
        public static string DefaultLegacyName(ushort id)
        {
            try
            {
                var info = RomInfo.GetScriptCommandInfoDict();
                return info != null && info.TryGetValue(id, out var c) ? c?.LegacyName : null;
            }
            catch { return null; }
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

        /// <summary>A number that may name a variable, as its value when the value is known.</summary>
        private bool TryValue(int raw, out long value)
        {
            if (!FieldScriptValues.IsVariable(raw)) { value = raw; return true; }
            return State.TryGetVar(raw, out value);
        }

        private bool TryVar(int number, out long value) => State.TryGetVar(number, out value);

        // Trainer flags sit in a range of their own in the save; this keeps them clear of ordinary flags.
        private static int TrainerFlagKey(int trainer) => 0x10000 + trainer;

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
            return param + 1 < parts.Length ? parts[param + 1].TrimEnd(',') : Value(cmd, param).ToString();
        }

        private static string FirstLine(string text)
        {
            int cut = text.IndexOfAny(new[] { '\n', '\r', '\f' });
            string line = cut >= 0 ? text.Substring(0, cut) : text;
            return line.Replace("\\n", " ").Trim();
        }

        private static string Capital(string s) =>
            string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

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

        // A file's own numbering starts at 1, so a raw 0 names the first script rather than nothing. The
        // event editor has always read it that way; matching on the id alone reported "there is no script 0"
        // for a script that is there.
        private static ScriptCommandContainer FindScript(ScriptSource source, int number)
        {
            var scripts = source?.Scripts;
            if (scripts == null || scripts.Count == 0) return null;
            return number == 0 ? scripts[0]
                               : scripts.FirstOrDefault(s => s.manualUserID == (uint)number);
        }

        private static ScriptCommandContainer FindFunction(ScriptSource source, int number)
        {
            var functions = source?.Functions;
            if (functions == null || functions.Count == 0) return null;
            return number == 0 ? functions[0]
                               : functions.FirstOrDefault(f => f.manualUserID == (uint)number);
        }

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
