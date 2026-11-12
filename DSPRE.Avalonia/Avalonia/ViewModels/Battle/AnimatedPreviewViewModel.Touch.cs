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
        private bool _touchShowsChoices;
        private int _touchSwapFrames;
        private bool _touchSwapToChoices;
        private int _touchBlink = -1;
        private int _touchLabelShown = -1;

        public bool TouchScreenShowsChoices => _touchShowsChoices;

        public double TouchScreenBrightness
        {
            get
            {
                if (_touchSwapFrames <= 0) return 1;
                double half = FieldScriptRunner.TouchScreenSwapFrames / 2.0;
                return Math.Abs(_touchSwapFrames - half) / half;
            }
        }

        /// <summary>Whether the cursor's red frame shows. It blinks when an entry is picked.</summary>
        public bool TouchCursorShown => HgssTouchScreen.BlinkShows(_touchBlink);

        public bool TouchChoiceIsYesNo => _question?.Kind == ScriptQuestion.QuestionKind.YesNo;

        public IReadOnlyList<string> TouchChoiceItems => HasTouchChoice ? ChoiceAllItems : Array.Empty<string>();

        /// <summary>The A button: NEXT with a message up, TALK facing somebody, else CHECK.</summary>
        public int TouchALabel
        {
            get
            {
                if (MessageVisible) return HgssTouchScreen.NextMessage;
                if (Player == null) return HgssTouchScreen.CheckMessage;
                (int x, int z) = FieldInteraction.TalkTile(Player, _collision);
                return NpcAt(x, z) != null ? HgssTouchScreen.TalkMessage : HgssTouchScreen.CheckMessage;
            }
        }

        private void ResetTouchScreen()
        {
            _touchShowsChoices = false;
            _touchSwapFrames = 0;
            _touchBlink = -1;
            OnPropertyChanged(nameof(TouchScreenShowsChoices));
            OnPropertyChanged(nameof(TouchScreenBrightness));
            OnPropertyChanged(nameof(TouchCursorShown));
        }

        // A touch list writes the entry's description into the top screen's box as the cursor moves.
        private void ShowDescription(string text)
        {
            _printer = null;
            _boxText = ExpandVars(text);
            _boxOpen = true;
            RaiseMessageChanged();
        }

        public void TouchChoice(int index)
        {
            if (_question == null || !_question.OnTouchScreen || _touchBlink >= 0) return;
            if (index < 0 || index >= _question.Options.Count) return;
            _choiceCursor = index;
            foreach (ChoiceEntry entry in ChoiceEntries) entry.IsSelected = entry.Index == index;
            OnPropertyChanged(nameof(ChoiceCursor));
            OnPropertyChanged(nameof(ChoiceCursorRow));
            ConfirmChoice();
        }

        private bool _hasPoketch = true;

        /// <summary>Set by hand. The preview has no save to read it from.</summary>
        public bool HasPoketch { get => _hasPoketch; set => Set(ref _hasPoketch, value); }

        /// <summary>Known once a script has asked, and it picks the Pokétch's casing.</summary>
        public bool PlayerIsFemale => GameState.TryGetFact("CheckPlayerGender", out long gender) && gender == 1;

        public FieldCameraEntry CameraEntry => FieldCamera.Entry(_cameraId, _family);

        public Func<int, int, MoveFacing, (float pitch, float yaw, float roll, int steps)?> CameraAnglesAt;

        public string CameraDescription
        {
            get
            {
                FieldCameraEntry c = CameraEntry;
                string kind = c.Orthographic ? "flat" : $"{c.FieldOfViewDegrees:0.#} degrees";
                string name = LabelStore.GetLabel(LabelStore.CameraKeyFor(_family), c.Id);
                return $"Camera {c.Id} ({name}): {c.DistanceInTiles:0.#} tiles back, "
                     + $"{c.PitchDegrees:0.#} degrees down, {kind}";
            }
        }

        public ObservableCollection<string> ScriptLines { get; } = new ObservableCollection<string>();

        private ScriptQuestion _question;
        public ScriptQuestion Question
        {
            get => _question;
            private set
            {
                Set(ref _question, value);
                AnswerOptions.Clear();
                if (value != null) foreach ((string Label, long Value) o in value.Options) AnswerOptions.Add(o.Label);
                _choiceCursor = value == null ? 0 : Math.Min(Math.Max(0, value.InitialCursor), Math.Max(0, value.Options.Count - 1));
                _choiceScroll = 0;
                KeepCursorOnShow();
                TypedAnswer = "0";
                OnPropertyChanged(nameof(HasQuestion));
                OnPropertyChanged(nameof(HasStatePrompt));
                OnPropertyChanged(nameof(HasChoiceWindow));
                OnPropertyChanged(nameof(HasTouchChoice));
                OnPropertyChanged(nameof(QuestionPrompt));
                OnPropertyChanged(nameof(QuestionTitle));
                OnPropertyChanged(nameof(AcceptsTypedAnswer));
                OnPropertyChanged(nameof(ChoiceItems));
                OnPropertyChanged(nameof(ChoiceAllItems));
                OnPropertyChanged(nameof(TouchChoiceItems));
                OnPropertyChanged(nameof(TouchChoiceIsYesNo));
                OnPropertyChanged(nameof(ChoiceCursor));
                OnPropertyChanged(nameof(ChoiceCursorRow));
                OnPropertyChanged(nameof(ChoiceCursorX));
                OnPropertyChanged(nameof(ChoiceRowOffset));
                OnPropertyChanged(nameof(ChoiceLeft));
                OnPropertyChanged(nameof(ChoiceTop));
                OnPropertyChanged(nameof(ChoiceIndent));
                OnPropertyChanged(nameof(ChoiceWidthTiles));
                OnPropertyChanged(nameof(ChoiceHeightTiles));
                OnPropertyChanged(nameof(ScriptProgressText));
                OnPropertyChanged(nameof(ScriptStatusText));
                RebuildChoiceEntries();
            }
        }
    }
}
