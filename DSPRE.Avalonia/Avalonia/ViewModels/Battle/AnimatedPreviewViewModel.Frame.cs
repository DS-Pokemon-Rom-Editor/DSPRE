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
        public void Advance(int frames)
        {
            if (!_playing || frames <= 0) return;
            bool wasRunning = ScriptRunning;
            for (int i = 0; i < frames; i++) FieldFrame();
            Frame = _frame + frames;
            Rebuild();
            if (wasRunning || ScriptRunning || _boxOpen)
            {
                RaiseMessageChanged();
                OnPropertyChanged(nameof(ScriptProgressText));
                OnPropertyChanged(nameof(ScriptStatusText));
            }
        }

        // One field pass: keys, the script until it waits, everybody moves, then the printer twice.
        private void FieldFrame()
        {
            _scriptFrame++;
            bool press = _aPressed;
            _aPressed = false;

            if (_runner != null && _runner.Running)
            {
                if (press) _runner.Pressed();
                _runner.Advance(1);
                if (!_runner.Running) ScriptFinished();
            }

            foreach (Npc npc in _npcs) npc.Motion?.Advance(1);
            _cameraObject?.Motion.Advance(1);

            if (_touchSwapFrames > 0)
            {
                _touchSwapFrames--;
                if (_touchSwapFrames == FieldScriptRunner.TouchScreenSwapFrames / 2)
                {
                    _touchShowsChoices = _touchSwapToChoices;
                    OnPropertyChanged(nameof(TouchScreenShowsChoices));
                }
                OnPropertyChanged(nameof(TouchScreenBrightness));
            }
            if (_touchBlink >= 0)
            {
                _touchBlink++;
                OnPropertyChanged(nameof(TouchCursorShown));
                if (_touchBlink >= HgssTouchScreen.BlinkFrames)
                {
                    _touchBlink = -1;
                    OnPropertyChanged(nameof(TouchCursorShown));
                    AnswerOption(_choiceCursor);
                }
            }
            int touchLabel = TouchALabel;
            if (touchLabel != _touchLabelShown) { _touchLabelShown = touchLabel; OnPropertyChanged(nameof(TouchALabel)); }

            if (_shake != null)
            {
                _shake.Advance(1);
                if (!_shake.Running) _shake = null;
            }
            _cameraMove?.Advance(1);

            if (Player != null)
            {
                bool walking = Player.IsWalking && !Player.IsScripted;
                Player.Advance(1);
                RememberCameraTrail();
                // Whatever is under the tile only happens once the player has arrived on it.
                if (walking && !Player.IsWalking) ArriveOnTile();
            }

            if (_printer != null)
            {
                _printer.Frame(press, _aHeld);
                if (_printer.Finished) _boxText = _printer.Text;
            }

            foreach (OneShot shot in _playingOnce)
            {
                shot.Frame++;
                int length = shot.Joint?.FrameCount ?? shot.Pattern?.FrameCount ?? 1;
                if (shot.Frame >= length) { shot.Frame = length - 1; shot.Done = true; }
            }
            _playingOnce.RemoveAll(x => x.Done && x.Frame <= 0);
        }

        private void ScriptFinished()
        {
            if (_printer != null) { _boxText = _printer.Text; _printer = null; }
            OnPropertyChanged(nameof(ScriptRunning));
            OnPropertyChanged(nameof(ScriptProgressText));
            OnPropertyChanged(nameof(ScriptStatusText));
            RaiseMessageChanged();
            CheckLevelScriptWatchers();
        }

        public void Restart()
        {
            Load(_scene, _terrain, _events, _indoor, _collision, _seed, _tileToWorld, _walkerFor,
                 _walkerStartId, _scriptHome);
            PlaceNpcs(_footFinder);
        }

        // No save to read, so start on the picked tile or an open one amid the people.
        private FieldPlayer MakePlayer()
        {
            bool haveEvents = _events?.Overworlds != null && _events.Overworlds.Count > 0;
            if (!haveEvents && StartTile == null) return null;

            int cx, cz;
            if (StartTile != null) { cx = StartTile.Value.x; cz = StartTile.Value.z; }
            else
            {
                cx = (int)_events.Overworlds.Average(o => o.XMatrixPosition * MapFile.MapSize + o.XMapPosition);
                cz = (int)_events.Overworlds.Average(o => o.YMatrixPosition * MapFile.MapSize + o.YMapPosition);
            }

            bool Free(int x, int z) => CanStand(x, z);

            // Spiral out from the middle until an open tile turns up.
            for (int r = 0; r < MapFile.MapSize; r++)
                for (int dz = -r; dz <= r; dz++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dz)) != r) continue;
                        if (Free(cx + dx, cz + dz))
                            return new FieldPlayer(cx + dx, cz + dz, _startFacing, _collision,
                                                   (x, z) => SomebodyOn(x, z, null, false));
                    }
            return null;
        }

        // An emote's mark: two tiles over the head, bouncing up and settling before it holds.
        private void AddEmote(List<NsbmdGlControl.SpriteInstance> sprites, float x, float y, float z, int frame, string name)
        {
            OverworldSprites.SpritePixels pix = FieldEmoteMarks.For(name);
            if (pix == null) return;
            int bounce = frame >= 1 && frame - 1 < FieldMovementScript.EmoteBounce.Length ? FieldMovementScript.EmoteBounce[frame - 1] : 0;
            if (frame < 1) return;          // the mark appears on the frame after the action starts
            float unit = _tileX / 16f;
            float halfW = _tileX * pix.Width / (OverworldSprites.PixelsPerTile * 2f);
            float halfH = _tileX * pix.Height / (OverworldSprites.PixelsPerTile * 2f);
            sprites.Add(new NsbmdGlControl.SpriteInstance
            {
                Cx = x,
                Cy = y + (32 + bounce) * unit + halfH,
                Cz = z + unit,
                HalfW = halfW,
                HalfH = halfH,
                Rgba = pix.Rgba,
                Width = pix.Width,
                Height = pix.Height,
            });
        }

        private void Rebuild()
        {
            if (_scene == null) return;

            SceneBuildingAnimator.Frame built = _animateTerrain && _buildings != null ? _buildings.At(_frame) : null;
            TextureSwaps = built?.TextureSwaps;

            TextureMatrices = null;
            if (_animateTerrain && (_animatedMaterials.Count > 0 || built?.TextureMatrices != null))
            {
                Dictionary<int, float[]> mats = built?.TextureMatrices != null ? new Dictionary<int, float[]>(built.TextureMatrices) : new Dictionary<int, float[]>();
                foreach (KeyValuePair<int, (TextureSrtAnimation anim, int material)> kv in _animatedMaterials)
                    mats[kv.Key] = kv.Value.anim.Evaluate(kv.Value.material, _frame).ToMatrix3();
                TextureMatrices = mats;
            }

            MaterialFades = built?.MaterialFades;

            ApplyBuildingOpacity();

            MovedParts = built?.MovedParts;
            ApplyOpeningDoors();

            List<NsbmdGlControl.SpriteInstance> sprites = new List<NsbmdGlControl.SpriteInstance>();
            AddPeople(sprites);
            AddStartMarker(sprites);
            AddPlayer(sprites);
            AddEmotes(sprites);
            Sprites = sprites;

            FrameAdvanced?.Invoke(this, EventArgs.Empty);
        }

        private void ApplyBuildingOpacity()
        {
            if (BuildingOpacity != null)
            {
                Dictionary<int, float> fades = null;
                foreach (NsbmdRenderModel.BuildingMaterials b in _scene.Buildings)
                {
                    float? opacity = BuildingOpacity(b.ModelId);
                    if (opacity == null) continue;
                    fades ??= MaterialFades != null ? new Dictionary<int, float>(MaterialFades) : new Dictionary<int, float>();
                    for (int k = b.FirstKey; k < b.FirstKey + b.Count; k++) fades[k] = opacity.Value;
                }
                if (fades != null) MaterialFades = fades;
            }
        }

        private void ApplyOpeningDoors()
        {
            // A door that is part-way through opening overrides whatever else drives its parts.
            if (_playingOnce.Count > 0)
            {
                Dictionary<int, float[]> moved = MovedParts != null ? new Dictionary<int, float[]>(MovedParts) : new Dictionary<int, float[]>();
                Dictionary<int, string> swaps = TextureSwaps != null ? new Dictionary<int, string>(TextureSwaps) : new Dictionary<int, string>();

                foreach (OneShot shot in _playingOnce)
                {
                    if (shot.Joint != null)
                        foreach (KeyValuePair<int, float[]> kv in NsbmdGeometry.RebuildBuilding(_scene, shot.Building,
                                     (id, part) => shot.Joint.MatrixFor(id, shot.Frame, part, shot.Building.Model?.ModelScale ?? 1f)))
                            moved[kv.Key] = kv.Value;

                    if (shot.Pattern != null)
                    {
                        TexturePatternAnimation.Swap swap = shot.Pattern.Evaluate(shot.Material, shot.Frame);
                        if (swap.IsSet) swaps[shot.MaterialKey] = swap.TextureName;
                    }
                }
                if (moved.Count > 0) MovedParts = moved;
                if (swaps.Count > 0) TextureSwaps = swaps;
            }
        }

        private void AddPeople(List<NsbmdGlControl.SpriteInstance> sprites)
        {
            if (_showPeople)
                foreach (Npc npc in _npcs)
                {
                    if (!IsOnMap(npc) || !npc.Motion.Visible) continue;
                    // Signposts, the book and the like are models, already in the scene the editor built.
                    if (DSPRE.ROMFiles.Overworld3DObjects.IsModel(npc.Event.OverlayTableEntry)) continue;
                    OverworldSprites.SpritePixels pix = OverworldSprites.Get(npc.Event.OverlayTableEntry, (ushort)npc.Motion.Facing,
                                                   PictureFor(npc.Event.OverlayTableEntry, npc.Motion.Facing, npc.Motion.Cycle));
                    if (pix == null || pix.Width <= 0 || pix.Height <= 0) continue;
                    float halfW = HalfWidthOf(pix), halfH = HalfHeightOf(pix);
                    sprites.Add(new NsbmdGlControl.SpriteInstance
                    {
                        // Drawn at its real position, between tiles while walking.
                        Cx = npc.FootX + npc.Motion.DrawOffsetX * _tileX,
                        Cy = npc.FootY + halfH + npc.Motion.HopHeight * _tileX,
                        Cz = npc.FootZ + npc.Motion.DrawOffsetZ * _tileZ,
                        HalfW = halfW,
                        HalfH = halfH,
                        Rgba = pix.Rgba,
                        Width = pix.Width,
                        Height = pix.Height,
                    });
                }
        }

        private void AddStartMarker(List<NsbmdGlControl.SpriteInstance> sprites)
        {
            // Before stepping in, the player stands on the start point to show where the walk begins.
            if (!_stepInto && _startTile != null && _tileToWorld != null)
            {
                OverworldSprites.SpritePixels pix = OverworldSprites.Get(PlayerSpriteEntry, (ushort)_startFacing,
                                               FieldSpriteAnimation.PictureFor(
                                                   OverworldSprites.FrameCount(PlayerSpriteEntry),
                                                   (int)_startFacing, null));
                if (pix != null && pix.Width > 0 && pix.Height > 0)
                {
                    (float x, float y, float z) foot = _tileToWorld(_startTile.Value.x, _startTile.Value.z);
                    float halfH = HalfHeightOf(pix);
                    sprites.Add(new NsbmdGlControl.SpriteInstance
                    {
                        Cx = foot.x,
                        Cy = foot.y + halfH,
                        Cz = foot.z,
                        HalfW = HalfWidthOf(pix),
                        HalfH = halfH,
                        Rgba = pix.Rgba,
                        Width = pix.Width,
                        Height = pix.Height,
                    });
                }
            }
        }

        private void AddPlayer(List<NsbmdGlControl.SpriteInstance> sprites)
        {
            if (_stepInto && Player != null && _tileToWorld != null && Player.Visible)
            {
                OverworldSprites.SpritePixels pix = OverworldSprites.Get(PlayerSpriteEntry, (ushort)Player.Facing,
                                               PictureFor(PlayerSpriteEntry, Player.Facing, Player.Cycle));
                if (pix != null && pix.Width > 0 && pix.Height > 0)
                {
                    (float x, float y, float z) foot = _tileToWorld(Player.DrawX, Player.DrawZ);
                    float halfH = HalfHeightOf(pix);
                    float roll = PlayerRollAt?.Invoke(Player.TileX, Player.TileZ) ?? 0f;
                    (float shiftX, float shiftY, float shiftZ) = PlayerSpriteShift?.Invoke() ?? (0f, 0f, 0f);
                    sprites.Add(new NsbmdGlControl.SpriteInstance
                    {
                        Cx = foot.x + shiftX * _tileX,
                        Cy = foot.y + halfH + (Player.HopHeight + shiftY) * _tileX,
                        Cz = foot.z + shiftZ * _tileZ,
                        HalfW = HalfWidthOf(pix),
                        HalfH = halfH,
                        Rgba = pix.Rgba,
                        Width = pix.Width,
                        Height = pix.Height,
                        RollDegrees = roll,
                    });
                }
            }
        }

        private void AddEmotes(List<NsbmdGlControl.SpriteInstance> sprites)
        {
            if (_showPeople)
            {
                foreach (Npc npc in _npcs)
                    if (IsOnMap(npc) && npc.Motion.EmoteFrame >= 0)
                        AddEmote(sprites, npc.FootX + npc.Motion.DrawOffsetX * _tileX, npc.FootY,
                                 npc.FootZ + npc.Motion.DrawOffsetZ * _tileZ, npc.Motion.EmoteFrame, npc.Motion.EmoteName);
            }
            if (_stepInto && Player != null && _tileToWorld != null && Player.EmoteFrame >= 0)
            {
                (float x, float y, float z) foot = _tileToWorld(Player.DrawX, Player.DrawZ);
                AddEmote(sprites, foot.x, foot.y, foot.z, Player.EmoteFrame, Player.EmoteName);
            }
        }
    }
}
