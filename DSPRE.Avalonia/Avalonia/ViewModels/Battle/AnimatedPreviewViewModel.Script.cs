using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using DSPRE.Avalonia.Gl;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia.ViewModels.Battle
{
    public sealed partial class AnimatedPreviewViewModel
    {
        private FieldScriptRunner _runner;
        private FieldCameraShake _shake;
        private Func<int, IReadOnlyList<ScriptAction>> _actionsFor;

        // Counts field frames whether or not the scene is animating, so script waits keep time.
        private int _scriptFrame;

        public Action<ScriptEffectKind, int> PlaySound { get; set; }

        private bool _playSounds = true;

        public bool PlaySounds
        {
            get => _playSounds;
            set { if (Set(ref _playSounds, value) && !value) PlaySound?.Invoke(ScriptEffectKind.MusicStop, 0); }
        }

        private bool _playMapMusic;

        public bool PlayMapMusic
        {
            get => _playMapMusic;
            set { if (Set(ref _playMapMusic, value)) MapMusicChanged?.Invoke(this, EventArgs.Empty); }
        }

        public event EventHandler MapMusicChanged;

        public int MapMusicId => FieldTimeOfDay.IsNight(_timeOfDay) ? MusicNightId : MusicDayId;

        public int MusicDayId { get; set; }
        public int MusicNightId { get; set; }

        private FieldCameraMove _cameraMove;

        public float CameraPitchDegrees => _cameraMove?.PitchDegrees ?? CameraEntry.PitchDegrees;

        public float CameraShiftX => _cameraMove?.ShiftXInTiles ?? 0f;
        public float CameraShiftY => _cameraMove?.ShiftYInTiles ?? 0f;
        public float CameraShiftZ => _cameraMove?.ShiftZInTiles ?? 0f;

        public float ShakeOffsetX => _shake?.OffsetX ?? 0f;
        public float ShakeOffsetY => _shake?.OffsetY ?? 0f;

        public bool ScriptRunning => _runner != null && _runner.Running;

        public string ScriptProgressText
        {
            get
            {
                if (!ScriptRunning) return "";
                switch (_runner.Waiting)
                {
                    case FieldScriptRunner.WaitKind.Message: return MessageHasMore ? "Waiting for A to turn the page" : "Printing a message";
                    case FieldScriptRunner.WaitKind.Button: return "Waiting for A or B";
                    case FieldScriptRunner.WaitKind.Movement: return "Waiting for everyone to finish moving";
                    case FieldScriptRunner.WaitKind.Frames: return $"Waiting {_runner.HoldingFrames} frames";
                    case FieldScriptRunner.WaitKind.Question: return "Waiting for an answer";
                    case FieldScriptRunner.WaitKind.Sound: return "Waiting for a sound to finish";
                    default: return "Running";
                }
            }
        }

        public ScriptGameState GameState { get; } = new ScriptGameState();

        public Func<int, ScriptSource> CommonScripts { get; set; }

        public Func<int, int, string> ArchiveText { get; set; }

        public Func<int, int> SharedArchive { get; set; }

        public Func<int, string> MenuText { get; set; }

        private FieldScriptRunner Runner => _runner ??= new FieldScriptRunner(new FieldScriptRunner.Hooks
        {
            StartMovement = StartMovement,
            MovementsRunning = () => (Player?.IsScripted ?? false)
                                     || _npcs.Any(n => n.Motion.IsScripted)
                                     || (_cameraObject?.Motion.IsScripted ?? false),
            PlaySound = StartSound,
            SoundPlaying = kind => _soundEnds.TryGetValue(kind, out int end) && end > _scriptFrame,
            ShakeCamera = (x, y, count, frames) => _shake = new FieldCameraShake(x, y, count, frames),
            MoveCamera = row =>
            {
                if (!FieldCameraMove.Exists(row)) return 0;
                _cameraMove = new FieldCameraMove(row, CameraEntry.PitchDegrees);
                return _cameraMove.TotalFrames;
            },
            ShowMessage = StartMessage,
            MessagePrinting = () => _printer != null && !_printer.Finished,
            OpenMessage = () => { _printer = null; _boxText = ""; _boxOpen = true; RaiseMessageChanged(); },
            CloseMessage = keepWords =>
            {
                if (_printer != null) _boxText = _printer.Text;
                _printer = null;
                // The frozen close leaves the tiles drawn until something draws over them.
                if (!keepWords) { _boxOpen = false; _boxText = null; }
                RaiseMessageChanged();
            },
            Ask = q => Question = q,
            Apply = ApplyEffect,
            Report = step => ScriptLines.Add(step.Text),
        });

        // When each kind of sound started, and when it ends once its length is known.
        private readonly Dictionary<ScriptEffectKind, int> _soundEnds = new Dictionary<ScriptEffectKind, int>();
        private readonly Dictionary<ScriptEffectKind, int> _soundStarts = new Dictionary<ScriptEffectKind, int>();

        private void StartSound(ScriptEffectKind kind, int id)
        {
            if (!_playSounds || PlaySound == null) { _soundEnds.Remove(kind); return; }
            _soundStarts[kind] = _scriptFrame;
            _soundEnds[kind] = int.MaxValue;
            PlaySound(kind, id);
        }

        public void SoundLength(ScriptEffectKind kind, int frames)
        {
            if (_soundStarts.TryGetValue(kind, out int start)) _soundEnds[kind] = start + Math.Max(0, frames);
        }

        private int ResolveObject(int number)
        {
            if (!FieldScriptValues.IsVariable(number)) return number;
            return GameState.TryGetVar(number, out long v) ? (int)v : -1;
        }

        private Npc NpcById(int id) => _npcs.FirstOrDefault(n => n.Event.OwID == id && IsOnMap(n));

        // Returns how long the movement takes. The script only waits for it when it asks to.
        private int StartMovement(int overworldId, int movementNumber)
        {
            IReadOnlyList<ScriptAction> actions = _walker?.ActionsFor(movementNumber) ?? _actionsFor?.Invoke(movementNumber);
            List<FieldMovementStep> steps = FieldMovementScript.Parse(actions);
            if (steps.Count == 0) return 0;

            int who = ResolveObject(overworldId);
            if (who == ScriptWalker.PlayerObject)
            {
                if (Player == null) return 0;
                Player.PlayScript(steps);
            }
            else if (who == ScriptWalker.CameraObject)
            {
                if (_cameraObject == null) return 0;
                _cameraObject.Motion.PlayScript(steps);
            }
            else
            {
                Npc npc = NpcById(who);
                if (npc == null) return 0;
                npc.Motion.PlayScript(steps);
            }
            return FieldMovementScript.TotalFrames(steps);
        }

        private static MoveFacing Opposite(MoveFacing f) => f switch
        {
            MoveFacing.Up => MoveFacing.Down,
            MoveFacing.Down => MoveFacing.Up,
            MoveFacing.Left => MoveFacing.Right,
            _ => MoveFacing.Left,
        };

        private void ApplyEffect(ScriptEffect e)
        {
            switch (e.Kind)
            {
                case ScriptEffectKind.FacePlayer:
                    // It turns against the way the player faces, not towards where the player stands.
                    if (_talkTarget != null && Player != null) _talkTarget.Motion.Face(Opposite(Player.Facing));
                    break;

                case ScriptEffectKind.Lock:
                case ScriptEffectKind.Release:
                {
                    bool paused = e.Kind == ScriptEffectKind.Lock;
                    if (e.A < 0) foreach (Npc n in _npcs) n.Motion.Paused = paused;
                    else if (NpcById(ResolveObject(e.A)) is Npc one) one.Motion.Paused = paused;
                    break;
                }

                case ScriptEffectKind.ShowObject:
                {
                    int id = ResolveObject(e.A);
                        Npc npc = _npcs.FirstOrDefault(n => n.Event.OwID == id);
                    if (npc == null) break;
                    if (e.B == 1)
                    {
                        // AddObject only brings somebody back while their flag is clear.
                        bool hidden = npc.Event.Flag != 0 && (GameState.TryGetFlag(npc.Event.Flag, out bool set) ? set : FlagIsSet(npc.Event.Flag));
                        if (!hidden) npc.OnMap = true;
                    }
                    else
                    {
                        npc.OnMap = false;
                        if (npc.Event.Flag != 0) GameState.SetFlag(npc.Event.Flag, true);
                    }
                    break;
                }

                case ScriptEffectKind.TouchScreen:
                    _touchSwapFrames = FieldScriptRunner.TouchScreenSwapFrames;
                    _touchSwapToChoices = e.A == 1;
                    OnPropertyChanged(nameof(TouchScreenBrightness));
                    break;

                case ScriptEffectKind.CameraObject:
                    _cameraObject = e.C == 1
                        ? new CameraObjectState { TileX = e.A, TileZ = e.B, Motion = new OverworldAnimator(null, MoveFacing.Down) }
                        : null;
                    break;
            }
        }

        // The invisible object a script hands the camera to and walks about to pan it.
        private sealed class CameraObjectState
        {
            public int TileX, TileZ;
            public OverworldAnimator Motion;
        }

        private CameraObjectState _cameraObject;

        private bool _showFlags;
        public bool ShowFlags { get => _showFlags; set => Set(ref _showFlags, value); }

        private void BuildFlagList(EventFile events)
        {
            EventFlags.Clear();
            _flagsSet.Clear();
            if (events?.Overworlds == null) return;

            // Nothing sets flag 0, so an overworld with it always shows.
            foreach (IGrouping<ushort, Overworld> g in events.Overworlds.Where(o => o.Flag != 0)
                                               .GroupBy(o => o.Flag)
                                               .OrderBy(g => g.Key))
                EventFlags.Add(new EventFlagSwitch(g.Key, g.Count(), FlagsChanged));
        }

        private int _cameraId;

        public int CameraId
        {
            get => _cameraId;
            set { if (Set(ref _cameraId, value)) { OnPropertyChanged(nameof(CameraEntry)); OnPropertyChanged(nameof(CameraDescription)); } }
        }

        private RomInfo.GameFamilies _family = RomInfo.GameFamily;

        public RomInfo.GameFamilies Family
        {
            get => _family;
            set
            {
                if (!Set(ref _family, value)) return;
                OnPropertyChanged(nameof(CameraEntry));
                OnPropertyChanged(nameof(CameraDescription));
                OnPropertyChanged(nameof(IsHeartGold));
                OnPropertyChanged(nameof(IsPlatinum));
                OnPropertyChanged(nameof(ShowTouchScreen));
            }
        }

        public bool IsHeartGold => _family == RomInfo.GameFamilies.HGSS;

        public bool IsPlatinum => _family == RomInfo.GameFamilies.Plat;
    }
}
