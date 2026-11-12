using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using DSPRE.Avalonia.BattleAnim;
using DSPRE.HgEngine;
using DSPRE.ROMFiles;
using global::Avalonia.Media;
using global::Avalonia.Media.Imaging;
using global::Avalonia.Threading;
using DSPRE.Avalonia.Data;
using static DSPRE.RomInfo;
using Avalonia.Platform;
using System.Runtime.InteropServices;

namespace DSPRE.Avalonia.ViewModels.Battle
{
    public sealed partial class BattleScriptEditorViewModel
    {
        private int _previewFrame = -1;
        public int PreviewFrame { get => _previewFrame; private set { if (_previewFrame != value) { _previewFrame = value; OnPropertyChanged(); OnPropertyChanged(nameof(FrameText)); } } }
        public string FrameText => _timeline == null ? "" : $"Frame {Math.Max(0, _previewFrame)} of {_timeline.TotalPasses}, {_timeline.TotalPasses / 30.0:0.00} s";

        /// <summary>Shows one frame, paused. An earlier frame replays from the start, same seed.</summary>
        public void SeekTo(int frame)
        {
            if (!HasPreview) return;
            if (IsCellPlaying) Pause();
            frame = Math.Max(0, frame);
            if (!_holding || _animPlayer == null || _animPlayer.Pass > frame)
            {
                List<WazaSeqCommand> cmds = BuildCommands();
                LoadCellResourcesForCommands(cmds, _fileIndex);
                _animPlayer = NewPlayer(cmds, _fileIndex);
                _cellFrameIdx = 0; _cellTick = 0; _cellLoops = 0; _previewFrames = 0;
                CellPreview = HasCellAnimation && _cellFrames.Count > 0 ? _cellFrames[0].Bitmap : null;
                _holding = true;
            }
            SetSoundsOn(_animPlayer, false);
            while (!_animPlayer.Finished && _animPlayer.Pass < frame && _animPlayer.Pass < MaxPreviewFrames) StepPass();
            ShowPass();
            RaiseNotes();
            RaisePreviewProps();
        }

        public void StepFrame(int delta) => SeekTo(Math.Max(0, PreviewFrame) + delta);
        public void SeekToEnd() => SeekTo(_timeline?.TotalPasses ?? MaxPreviewFrames);

        public void Pause()
        {
            if (!IsCellPlaying) return;
            _previewTimer.Stop();
            StopPreviewSounds();
            _holding = true;
            RaisePreviewProps();
        }

        private bool _loop;
        public bool Loop { get => _loop; set => Set(ref _loop, value); }

        private static readonly double[] Speeds = { 1, 0.5, 0.25 };
        public string[] SpeedNames { get; } = { "Normal speed", "Half speed", "Quarter speed" };
        private int _speedIndex;
        /// <summary>Slower playback is silent, since the sounds can't slow down with it.</summary>
        public int SpeedIndex
        {
            get => _speedIndex;
            set
            {
                if (value < 0 || value >= Speeds.Length || !Set(ref _speedIndex, value)) return;
                if (!IsCellPlaying) return;
                _passesRun = 0; _clock.Restart();
                if (Speeds[value] != 1) StopPreviewSounds();
                SetSoundsOn(_animPlayer, Speeds[value] == 1);
            }
        }

        private void SetSoundsOn(BattleAnimPlayer player, bool on)
        {
            if (player == null) return;
            on &= _soundsOn;
            player.PlaySound = on ? PreviewSound : null;
            player.PlayCry = on ? PreviewCry : null;
            player.StopSound = on ? PreviewStopSound : null;
        }

        public void PickCommand(int command)
        {
            if (command < 0 || command >= Rows.Count) return;
            for (int i = 0; i < Rows.Count; i++) Rows[i].IsExpanded = i == command;
            PickedCommand = command;
        }

        private int _pickedCommand = -1;
        public int PickedCommand { get => _pickedCommand; private set { _pickedCommand = value; OnPropertyChanged(); } }

        private BattleAnimTrace _timeline;
        /// <summary>The move's run recorded silently, as labelled bars. Null for other scripts.</summary>
        public BattleAnimTrace Timeline { get => _timeline; private set { _timeline = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasTimeline)); OnPropertyChanged(nameof(FrameText)); OnPropertyChanged(nameof(ShowStoryboardText)); } }
        public bool ShowStoryboardText => ShowStoryboard && _timeline == null;
        public bool HasTimeline => _timeline != null;

        private short[] RenderedSoundPcm(int soundId) =>
            _soundPcm.TryGetValue(soundId, out Lazy<short[]> pcm) && pcm.IsValueCreated ? pcm.Value : null;

        private void RecordTimeline(List<WazaSeqCommand> cmds)
        {
            if (!IsAnimation || cmds.Count == 0 || _fileIndex < 0) { Timeline = null; return; }
            BattleAnimTrace trace = RecordTrace(cmds, null);
            if (trace == null) { Timeline = null; return; }
            foreach (BattleAnimTrace.Item item in trace.Items) LabelTimelineItem(item, cmds);
            Timeline = trace;
        }

        // With stopAfter, ends once that bar has finished.
        private BattleAnimTrace RecordTrace(List<WazaSeqCommand> cmds, Func<BattleAnimTrace, bool> stopAfter)
        {
            try
            {
                BattleAnimPlayer player = NewPlayer(cmds, _fileIndex, live: false);
                while (!player.Finished && player.Pass < MaxPreviewFrames && (stopAfter == null || !stopAfter(player.Trace))) player.Step();
                return player.Trace;
            }
            catch (Exception ex) { AppLogger.WarnOnce("BattleScriptEditorViewModel.RecordTrace", ex); return null; }
        }

        /// <summary>The value of a bar's command that moves its end or the whole bar, or null.</summary>
        public BattleAnimTrace.Knob FindKnob(BattleAnimTrace.Item item, bool movesBar)
        {
            if (_timeline == null || IsHgEngineSource || item == null || item.Command < 0 || item.Command >= Rows.Count) return null;
            ScriptCmdRow row = Rows[item.Command];
            if (row.Raw) return null;
            List<WazaSeqCommand> cmds = BuildCommands();
            WazaSeqCommand c = cmds[item.Command];
            string op = BattleAnimCommands.Name(_version, c.OpId) ?? "";
            int ordinal = OrdinalOf(_timeline, item);
            // A routine's first two values pick the routine and count its values.
            int first = op == "CallFunc" ? 2 : 0;
            BattleAnimTrace.Knob best = null;
            // The finest value wins: a fade's hold moves the end a pass at a time, its step length many.
            for (int a = first; a < c.Args.Length; a++)
            {
                ParamVM param = row.ParamAt(a);
                if (param != null && (param.IsEnum || param.IsSound)) continue;
                int v = c.Args[a];
                if (v < 0) continue;
                int d1 = Nudged(cmds, item, ordinal, a, v + 1, movesBar);
                if (d1 <= 0 || (best != null && d1 >= best.FramesPerStep)) continue;
                if (Nudged(cmds, item, ordinal, a, v + 2, movesBar) != 2 * d1) continue;
                if (Nudged(cmds, item, ordinal, a, v + 3, movesBar) != 3 * d1) continue;
                bool byteValue = op != "CallFunc";
                best = new BattleAnimTrace.Knob
                {
                    Command = item.Command, Arg = a, Value = v, Min = 0, Max = byteValue ? 255 : Math.Max(v, 999),
                    FramesPerStep = d1, MovesBar = movesBar,
                    Name = BattleAnimSchema.ParamLabel(row.OpName, a, row.Args),
                };
                if (d1 == 1) break;
            }
            return best;
        }

        // How far the bar's end, or the whole bar, moved with one value changed. 0 for any other change.
        private int Nudged(List<WazaSeqCommand> cmds, BattleAnimTrace.Item item, int ordinal, int arg, int value, bool movesBar)
        {
            WazaSeqCommand c = cmds[item.Command];
            int[] args = (int[])c.Args.Clone();
            args[arg] = value;
            List<WazaSeqCommand> nudged = new List<WazaSeqCommand>(cmds);
            nudged[item.Command] = new WazaSeqCommand(c.OpId, args) { WordPos = c.WordPos, Raw = c.Raw, Tail = c.Tail };
            BattleAnimTrace.Item found = null;
            BattleAnimTrace trace = RecordTrace(nudged, t =>
            {
                found ??= Match(t, item, ordinal);
                return found != null && found.End < t.TotalPasses;
            });
            found ??= trace == null ? null : Match(trace, item, ordinal);
            if (found == null) return 0;
            int dStart = found.Start - item.Start, dEnd = found.End - item.End;
            if (movesBar) return dStart == dEnd ? dStart : 0;
            return dStart == 0 ? dEnd : 0;
        }

        private static int OrdinalOf(BattleAnimTrace trace, BattleAnimTrace.Item item)
        {
            int n = 0;
            foreach (BattleAnimTrace.Item i in trace.Items)
            {
                if (ReferenceEquals(i, item)) return n;
                if (i.Lane == item.Lane && i.Command == item.Command) n++;
            }
            return -1;
        }

        private static BattleAnimTrace.Item Match(BattleAnimTrace trace, BattleAnimTrace.Item like, int ordinal)
        {
            int n = 0;
            foreach (BattleAnimTrace.Item i in trace.Items)
                if (i.Lane == like.Lane && i.Command == like.Command && n++ == ordinal) return i;
            return null;
        }

        public void SetKnob(BattleAnimTrace.Knob knob, int value)
        {
            if (knob == null || knob.Command < 0 || knob.Command >= Rows.Count) return;
            value = Math.Clamp(value, knob.Min, knob.Max);
            if (value == knob.Value) return;
            _lastStep = default;
            Rows[knob.Command].SetArg(knob.Arg, value);
        }

        private void LabelTimelineItem(BattleAnimTrace.Item item, List<WazaSeqCommand> cmds)
        {
            WazaSeqCommand c = item.Command >= 0 && item.Command < cmds.Count ? cmds[item.Command] : null;
            string op = c == null ? "" : BattleAnimCommands.Name(_version, c.OpId) ?? "";
            string command = item.Command >= 0 && item.Command < Rows.Count ? Rows[item.Command].OpDisplay : op;
            string routine = op == "CallFunc" && c.Args.Length > 0 ? BattleAnimScriptDisplay.RoutineName(c.Args[0]) : command;
            item.Label = item.Lane switch
            {
                BattleAnimTrace.Lane.Script => op == "Delay" && c.Args.Length > 0 ? $"{command} {c.Args[0]}" : command,
                BattleAnimTrace.Lane.Particles => $"Slot {item.A}, emitter {item.B}",
                BattleAnimTrace.Lane.Sound => ShortSoundName(item.A),
                _ => routine,
            };
            int frames = item.End - item.Start;
            item.Tip = $"{item.Label}\nFrame {item.Start}, {Plural.Of(frames, "frame")}" + (command != item.Label ? $"\n{command}" : "");
        }

        private string ShortSoundName(int soundId)
        {
            string name = SoundNameOf(soundId);
            if (string.IsNullOrEmpty(name)) return $"Sound {soundId}";
            foreach (string prefix in new[] { "SEQ_SE_DP_", "SEQ_SE_PL_", "SEQ_SE_", "SEQ_" })
                if (name.StartsWith(prefix, StringComparison.Ordinal)) return name.Substring(prefix.Length);
            return name;
        }
    }
}
