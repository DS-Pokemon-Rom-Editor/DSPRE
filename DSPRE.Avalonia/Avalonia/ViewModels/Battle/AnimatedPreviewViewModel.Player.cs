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
        public (float x, float y, float z) PlayerWorldPosition()
        {
            if (Player == null || _tileToWorld == null) return (0f, 0f, 0f);
            return _tileToWorld(Player.DrawX, Player.DrawZ);
        }

        private (float x, float y, float z) FollowedPosition()
        {
            if (_cameraObject != null && _tileToWorld != null)
                return _tileToWorld(_cameraObject.TileX + _cameraObject.Motion.DrawOffsetX,
                                    _cameraObject.TileZ + _cameraObject.Motion.DrawOffsetZ);
            return PlayerWorldPosition();
        }

        // Only the camera's height lags, six frames back, so steps don't make the whole view bob.
        private readonly Queue<float> _cameraTrail = new Queue<float>();

        public (float x, float y, float z) CameraTarget()
        {
            (float x, float y, float z) now = FollowedPosition();
            if (_cameraTrail.Count == 0) return now;
            return (now.x, _cameraTrail.Peek(), now.z);
        }

        private void RememberCameraTrail()
        {
            if (Player == null || _tileToWorld == null) return;
            _cameraTrail.Enqueue(FollowedPosition().y);
            // The games keep one more than the delay, so the oldest one held is six frames old.
            while (_cameraTrail.Count > FieldCamera.TrailFrames + 1) _cameraTrail.Dequeue();
        }

        public string Move(MoveFacing dir)
        {
            if (Player == null || _question != null) return null;
            if (PlayerHeld?.Invoke() == true) return null;

            if (ScriptRunning)
            {
                // WaitButton also carries on at a direction, and turns the player to face it.
                if (_runner.ButtonTakesPad)
                {
                    if (_runner.ButtonTurnsPlayer) Player.Face(dir);
                    _runner.Pressed(pad: true);
                    Rebuild();
                }
                return null;
            }

            CloseLeftoverBox();
            StepResult result = Player.Go(dir);
            Rebuild();

            if (result == StepResult.Walked) CheckLevelScriptWatchers();
            if (result == StepResult.Blocked && FieldInteraction.DoorAhead(_events, Player, _collision) is Warp door)
            {
                (int doorX, int doorZ) = Player.TileAhead;
                OpenDoorAt(doorX, doorZ);
                return WarpLine(door);
            }

            switch (result)
            {
                case StepResult.Blocked: return "There is something in the way.";
                case StepResult.BlockedByEvent: return "Somebody is standing there.";
                default: return null;
            }
        }

        public Action<int, int, int> PlayerArrivedOn;

        public Func<int, int, float> PlayerRollAt;

        public Func<bool> PlayerHeld;

        public Func<(float x, float y, float z)> PlayerSpriteShift;

        public Func<int, float?> BuildingOpacity;

        public void SwapScene(NsbmdRenderModel scene, TextureSrtAnimation terrain, EventFile events, bool indoor,
                              MapCollisionGrid collision)
        {
            Load(scene, terrain, events, indoor, collision, _seed, _tileToWorld, _walkerFor, _walkerStartId,
                 _scriptHome, _actionsFor);
            PlaceNpcs(_footFinder);
        }

        public void PutPlayerOn(int tileX, int tileZ, MoveFacing facing)
        {
            if (Player == null) return;
            Player.PlaceAt(tileX, tileZ, facing);
            Rebuild();
        }

        private static string WarpLine(Warp warp) =>
            $"A warp to header {warp.Header}, warp {warp.Anchor}. The preview doesn't follow it.";

        private void ArriveOnTile()
        {
            PlayerArrivedOn?.Invoke(Player.TileX, Player.TileZ, _frame);

            Warp warp = FieldInteraction.WarpAt(_events, Player.TileX, Player.TileZ);
            if (warp != null)
            {
                ScriptLines.Add(WarpLine(warp));
                OpenDoorAt(Player.TileX, Player.TileZ);
            }

            Trigger waiting = FieldInteraction.TriggerAt(_events, Player.TileX, Player.TileZ, null);
            if (waiting == null) return;

            if (GameState.TryGetVar(waiting.VariableWatched, out long held))
            {
                if (held == waiting.ExpectedVarValue) { ScriptLines.Clear(); RunScript(waiting.ScriptNumber, "The trigger goes off."); }
                return;
            }

            _pendingTrigger = waiting;
            Question = new ScriptQuestion
            {
                Kind = ScriptQuestion.QuestionKind.YesNo,
                FromPreview = true,
                Subject = "the trigger on this tile",
                Prompt = (waiting.ScriptNumber == EventFile.NoScript
                            ? "There is a trigger here with no script. It goes off when "
                            : $"There is a trigger here. It runs script {waiting.ScriptNumber} when ")
                       + $"{FieldScriptValues.Describe(waiting.VariableWatched)} is {waiting.ExpectedVarValue}.",
                Options = new[] { ("Set it off", 1L), ("Leave it", 0L) },
            };
            ScriptLines.Add(Question.Prompt);
        }

        private Npc _talkTarget;

        private Npc NpcAt(int x, int z) => _npcs.FirstOrDefault(n =>
            IsOnMap(n) && n.Motion.Visible
            && FieldInteraction.TileX(n.Event) + n.Motion.OffsetX == x
            && FieldInteraction.TileZ(n.Event) + n.Motion.OffsetZ == z);

        public void Interact()
        {
            if (Player == null || _question != null) return;
            if (ScriptRunning) { PressA(); return; }
            if (CloseLeftoverBox()) return;

            ScriptLines.Clear();

            (int x, int z) = FieldInteraction.TalkTile(Player, _collision);
            Npc npc = NpcAt(x, z);
            if (npc != null)
            {
                _talkTarget = npc;
                RunScript(npc.Event.ScriptNumber, $"Talking to overworld {npc.Event.OwID}.");
                return;
            }

            Spawnable sign = FieldInteraction.SpawnableAt(_events, x, z, Player.Facing);
            if (sign != null)
            {
                _talkTarget = null;
                string what = (SpawnableKind)sign.Type == SpawnableKind.Signboard ? "Reading the sign"
                            : (SpawnableKind)sign.Type == SpawnableKind.HiddenItem ? "Finding something hidden"
                            : "Looking at it";
                RunScript(sign.ScriptNumber, $"{what}.");
                return;
            }

            ScriptLines.Add("There is nothing there to talk to.");
        }

        // The games leave a finished script's box drawn until something draws over it.
        private bool CloseLeftoverBox()
        {
            if (ScriptRunning || !_boxOpen) return false;
            ClearMessage();
            return true;
        }

        private void ConfigureWalker(ScriptWalker w)
        {
            w.State = GameState;
            w.CommonScripts = CommonScripts;
            w.ArchiveText = ArchiveText;
            w.SharedArchive = SharedArchive;
            w.MenuText = MenuText;
            w.PlayerPosition = () => Player == null ? ((int, int)?)null : (Player.TileX, Player.TileZ);
        }

        private void RunScript(int scriptNumber, string opening)
        {
            if (!string.IsNullOrEmpty(opening)) ScriptLines.Add(opening);

            int? trainer = TrainerScripts.TrainerIdFor(scriptNumber);
            if (trainer != null)
                ScriptLines.Add($"Script {scriptNumber} is the trainer script for "
                    + $"trainer {trainer}{(TrainerScripts.IsDouble(scriptNumber) ? ", a double battle" : "")}.");

            // Null for the map's own scripts.
            string home = _scriptHome?.Invoke(scriptNumber);
            if (home != null) ScriptLines.Add(home);

            if (_walkerFor == null) { _walker = null; Question = null; return; }

            _walker = _walkerFor(scriptNumber);
            if (_walker == null) { ScriptLines.Add("That script couldn't be read."); return; }

            ConfigureWalker(_walker);
            _walker.Begin(_walkerStartId?.Invoke(scriptNumber) ?? scriptNumber);
            // The engine notes who was spoken to before the script's first command runs.
            if (_talkTarget != null) GameState.SetVar(ScriptWalker.LastTalkedVar, _talkTarget.Event.OwID);

            Question = null;
            _shake = null;
            _cameraMove = null;
            Runner.Play(_walker);
            OnPropertyChanged(nameof(ScriptRunning));
            OnPropertyChanged(nameof(ScriptProgressText));
            OnPropertyChanged(nameof(ScriptStatusText));
        }

        // Runs straight through, as a map's setup scripts run before anything is on screen.
        private void RunWhileLoading(int scriptNumber, string opening)
        {
            ScriptLines.Add(opening);
            ScriptWalker w = _walkerFor?.Invoke(scriptNumber);
            if (w == null) return;

            ConfigureWalker(w);
            w.GuessUnknowns = true;
            w.Begin(_walkerStartId?.Invoke(scriptNumber) ?? scriptNumber);
            while (w.Next()) { }
            // What still stops to ask is a yes/no or a menu, which cannot come up while loading.
            while (w.Pending != null && !w.Finished) { w.Answer(0); while (w.Next()) { } }

            foreach (ScriptStep s in w.Steps)
            {
                ScriptLines.Add(s.Text);
                if (s.Effect?.Kind == ScriptEffectKind.ShowObject) ApplyEffect(s.Effect);
            }
            Rebuild();
        }

        private void OpenDoorAt(int tileX, int tileZ)
        {
            if (_scene == null) return;

            foreach (NsbmdRenderModel.BuildingMaterials b in _scene.Buildings)
            {
                if (Math.Abs(b.TileX - tileX) > 1 || Math.Abs(b.TileZ - tileZ) > 1) continue;

                (IReadOnlyList<JointAnimation> joints, IReadOnlyList<TexturePatternAnimation> patterns) = BuildingAnimationSet.DoorAnimations(b.ModelId, _indoor);
                if (joints.Count == 0 && patterns.Count == 0) continue;

                foreach (JointAnimation j in joints) _playingOnce.Add(new OneShot { Building = b, Joint = j, Frame = 0 });
                foreach (TexturePatternAnimation t in patterns)
                    for (int k = b.FirstKey; k < b.FirstKey + b.Count; k++)
                    {
                        if (!_scene.MaterialNameByKey.TryGetValue(k, out string name)) continue;
                        int m = t.IndexOf(name);
                        if (m < 0 || t.IsStatic(m)) continue;
                        _playingOnce.Add(new OneShot { Building = b, Pattern = t, MaterialKey = k, Material = m, Frame = 0 });
                    }

                string sound = BuildingAnimationSet.DoorSound(b.ModelId, _indoor, opening: true);
                if (sound != null) ScriptLines.Add($"The door opens, with the sound of {sound}.");
                return;
            }
        }

        private sealed class OneShot
        {
            public NsbmdRenderModel.BuildingMaterials Building;
            public JointAnimation Joint;
            public TexturePatternAnimation Pattern;
            public int MaterialKey, Material;
            public int Frame;
            public bool Done;
        }

        private readonly List<OneShot> _playingOnce = new List<OneShot>();
    }
}
