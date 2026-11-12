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
        private SdatArchive _sdat;
        private bool _sdatLoadTried;
        private string _sdatLoadError;
        private SdatArchive LoadSdat()
        {
            if (_sdat != null || _sdatLoadTried) return _sdat;
            _sdatLoadTried = true;
            try
            {
                // Each version's sound archive has its own filename; DP does not carry Platinum's "pl_" file.
                string fileName = GameFamily switch
                {
                    GameFamilies.HGSS => "gs_sound_data.sdat",
                    GameFamilies.Plat => "pl_sound_data.sdat",
                    GameFamilies.DP => "sound_data.sdat",
                    _ => "sound_data.sdat",
                };
                // The ROM's own files sit one level under the project root: files for ds-rom, data for legacy.
                string romRoot = System.IO.Path.Combine(WorkDir, IsDsRomProject ? "files" : "data");
                string path = System.IO.Path.Combine(romRoot, "data", "sound", fileName);
                if (!PlainFiles.Exists(path)) { _sdatLoadError = $"Sound archive not found at:\n{path}"; return null; }
                _sdat = SdatArchive.Parse(PlainFiles.ReadBytes(path));
                if (_sdat.Sequences.Count == 0) _sdatLoadError = $"The sound archive at {path} has no sequences DSPRE can read.";
            }
            catch (System.Exception ex) { _sdat = null; _sdatLoadError = ex.ToString(); }
            return _sdat;
        }

        private string SoundNameOf(int soundId) => LoadSdat()?.SeqNames.TryGetValue(soundId, out string n) == true ? n : null;

        // The previewed species' cry. PlayPokemonCry's mode, pan and volume are not applied here.
        private void PreviewCry()
        {
            short[] pcm = AtVolume(CryPcm());
            if (pcm == null || pcm.Length == 0) return;
            try { _previewVoices.Add((-1, AudioOutput.Current.Start(pcm, SoundRate))); }
            catch { /* a preview should not put a dialog up because a sound would not play */ }
        }

        private const int SoundRate = AudioOutput.RenderRate;
        // Each sound renders once, on whichever thread asks first; the others wait for that render.
        private readonly System.Collections.Concurrent.ConcurrentDictionary<int, Lazy<short[]>> _soundPcm =
            new System.Collections.Concurrent.ConcurrentDictionary<int, Lazy<short[]>>();
        private int _cryPcmSpecies = -1;
        private short[] _cryPcm;

        private short[] SoundPcm(int soundId)
        {
            SdatArchive sdat = LoadSdat();
            if (sdat == null) return null;
            return _soundPcm.GetOrAdd(soundId, id => new Lazy<short[]>(() =>
            {
                try { return SseqPlayer.Render(sdat, id); }
                catch (Exception ex) { AppLogger.WarnOnce("BattleScriptEditorViewModel.SoundPcm", ex); return null; }
            })).Value;
        }

        private short[] CryPcm()
        {
            int species = _gaugeSpeciesId;
            if (species <= 0) return null;
            if (_cryPcmSpecies == species) return _cryPcm;
            try { _cryPcm = SoundArchive.RenderCry(species); }
            catch (Exception ex) { AppLogger.WarnOnce("BattleScriptEditorViewModel.CryPcm", ex); _cryPcm = null; }
            _cryPcmSpecies = species;
            return _cryPcm;
        }

        private short[] AtVolume(short[] pcm)
        {
            if (pcm == null || _volume >= 100) return pcm;
            short[] scaled = new short[pcm.Length];
            for (int i = 0; i < pcm.Length; i++) scaled[i] = (short)(pcm[i] * _volume / 100);
            return scaled;
        }

        private int PassesOf(short[] pcm) =>
            pcm == null || pcm.Length == 0 ? 0 : (int)Math.Ceiling(pcm.Length / 2.0 / SoundRate * PreviewFps);

        private static readonly HashSet<string> SoundCommands = new HashSet<string>
        {
            "PlaySoundEffect", "PlayPannedSoundEffect", "PlayMovingSoundEffectAtkDef", "PlayMovingSoundEffectAtkDef2",
            "PlayMovingSoundEffectNoCorrection", "PlayLoopedSoundEffect", "PlayDelayedSoundEffect",
        };

        private void RenderScriptSounds(List<WazaSeqCommand> cmds)
        {
            foreach (WazaSeqCommand c in cmds)
                if (c.Args.Length >= 1 && SoundCommands.Contains(BattleAnimCommands.Name(_version, c.OpId) ?? "")) SoundPcm(c.Args[0]);
        }

        // Only this preview's own sounds, so stopping them leaves other editors' sound alone.
        private readonly List<(int Sound, object Handle)> _previewVoices = new List<(int Sound, object Handle)>();

        private void PreviewStopSound(int soundId)
        {
            foreach ((int sound, object handle) in _previewVoices)
                if (sound == soundId) StopVoice(handle);
            _previewVoices.RemoveAll(v => v.Sound == soundId);
        }

        private void StopPreviewSounds()
        {
            foreach ((int _, object handle) in _previewVoices) StopVoice(handle);
            _previewVoices.Clear();
        }

        private static void StopVoice(object handle)
        {
            try { AudioOutput.Current.Stop(handle); }
            catch (Exception caught) when (AppLogger.Noted("BattleScriptEditorViewModel.StopVoice", caught)) { }   // logged, and the other sounds still stop
        }

        private void PreviewSound(int soundId)
        {
            short[] pcm = AtVolume(SoundPcm(soundId));
            if (pcm == null || pcm.Length == 0) return;
            try { _previewVoices.Add((soundId, AudioOutput.Current.Start(pcm, SoundRate))); }
            catch { /* best-effort preview; animation playback must never surface an error dialog */ }
        }

        internal string TryPreviewSound(int soundId)
        {
            SdatArchive sdat = LoadSdat();
            if (sdat == null) return _sdatLoadError ?? "The sound archive couldn't be loaded.";
            try
            {
                if (!sdat.SeqNames.ContainsKey(soundId) && (soundId < 0 || soundId >= sdat.Sequences.Count || sdat.Sequences[soundId] == null))
                    return $"Sound {soundId} isn't in this ROM's sound archive.";
                short[] pcm = SseqPlayer.Render(sdat, soundId);
                if (pcm == null || pcm.Length == 0) return $"Sound {soundId} plays nothing here.";
                AudioOutput.Current.Play(pcm, SoundRate);
                if (AudioOutput.Current is NullAudioOutput) return "No sound output.";
                return null;
            }
            catch (System.Exception ex) { AppLogger.WarnOnce("BattleScriptEditorViewModel.TryPreviewSound", ex); return ex.ToString(); }
        }

        public ObservableCollection<string> OpcodeNames { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> OpcodeDisplayNames { get; } = new ObservableCollection<string>();
    }
}
