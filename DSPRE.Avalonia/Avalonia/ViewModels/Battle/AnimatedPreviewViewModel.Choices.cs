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
        public sealed class ChoiceEntry : INotifyPropertyChanged
        {
            public int Index { get; init; }
            public string Label { get; init; }
            private bool _selected;
            public bool IsSelected
            {
                get => _selected;
                set { if (_selected == value) return; _selected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected))); }
            }
            public event PropertyChangedEventHandler PropertyChanged;
        }

        public ObservableCollection<ChoiceEntry> ChoiceEntries { get; } = new ObservableCollection<ChoiceEntry>();

        private void RebuildChoiceEntries()
        {
            ChoiceEntries.Clear();
            if (_question == null || !_question.IsInGame) return;
            for (int i = 0; i < _question.Options.Count; i++)
                ChoiceEntries.Add(new ChoiceEntry { Index = i, Label = _question.Options[i].Label, IsSelected = i == _choiceCursor });
        }

        private bool ChoiceIsList => _question?.Kind == ScriptQuestion.QuestionKind.Menu && _question.IsList;

        /// <summary>Pixels from the writing area's left edge to the words, past the cursor.</summary>
        public int ChoiceIndent => _question?.Kind == ScriptQuestion.QuestionKind.YesNo ? 8 : ChoiceIsList ? 12 : 11;

        public int ChoiceCursorX => ChoiceIsList ? 2 : 0;

        public int ChoiceRowOffset => ChoiceIsList ? 1 : 0;

        public const int ListRows = 8;

        /// <summary>The yes/no window is six tiles by four; a menu sizes itself to its entries.</summary>
        public int ChoiceWidthTiles => _question?.Kind == ScriptQuestion.QuestionKind.YesNo ? 6 : 0;
        public int ChoiceHeightTiles => _question?.Kind == ScriptQuestion.QuestionKind.YesNo ? 4
            : ChoiceIsList ? Math.Min(_question.Options.Count, ListRows) * 2 : 0;

        public bool ShowTouchScreen => _stepInto && (IsHeartGold || IsPlatinum);

        public string ScriptStatusText => ScriptRunning ? ScriptProgressText
            : HasStatePrompt ? "Waiting for an answer"
            : "Arrow keys act as the D-pad, Enter as A and X as B.";

        public void StopScript()
        {
            _runner?.Stop();
            _pendingTrigger = null;
            Question = null;
            ClearMessage();
            foreach (Npc npc in _npcs) { npc.Motion?.StopScript(); if (npc.Motion != null) npc.Motion.Paused = false; }
            _cameraObject = null;
            ResetTouchScreen();
            ScriptLines.Add("Stopped.");
            OnPropertyChanged(nameof(ScriptRunning));
            OnPropertyChanged(nameof(ScriptStatusText));
            Rebuild();
        }

        public bool HasQuestion => _question != null;

        public bool HasStatePrompt => _question != null && !_question.IsInGame;

        public bool HasChoiceWindow => _question != null && _question.IsInGame && !_question.OnTouchScreen;

        public bool HasTouchChoice => _question != null && _question.IsInGame && _question.OnTouchScreen;

        public string QuestionPrompt => _question?.Prompt ?? "";

        public string QuestionTitle => _question == null ? ""
            : _question.FromPreview ? "Step on it?"
            : _question.Kind == ScriptQuestion.QuestionKind.Flag ? "Is this flag set?"
            : _question.Kind == ScriptQuestion.QuestionKind.Fact ? "What does the game know here?"
            : "What does this variable hold?";

        public bool AcceptsTypedAnswer => _question?.AcceptsAnyNumber == true;
        public ObservableCollection<string> AnswerOptions { get; } = new ObservableCollection<string>();

        private string _typedAnswer = "0";
        public string TypedAnswer { get => _typedAnswer; set => Set(ref _typedAnswer, value); }

        public IReadOnlyList<string> ChoiceItems =>
            _question == null ? Array.Empty<string>()
            : ChoiceIsList ? _question.Options.Skip(_choiceScroll).Take(ListRows).Select(o => o.Label).ToList()
            : _question.Options.Select(o => o.Label).ToList();

        public IReadOnlyList<string> ChoiceAllItems =>
            _question == null ? Array.Empty<string>() : _question.Options.Select(o => o.Label).ToList();

        private int _choiceCursor;
        private int _choiceScroll;

        public int ChoiceCursorRow => _choiceCursor - _choiceScroll;

        private void KeepCursorOnShow()
        {
            if (!ChoiceIsList) { _choiceScroll = 0; return; }
            if (_choiceCursor < _choiceScroll) _choiceScroll = _choiceCursor;
            if (_choiceCursor >= _choiceScroll + ListRows) _choiceScroll = _choiceCursor - ListRows + 1;
        }

        public int ChoiceCursor
        {
            get => _choiceCursor;
            set
            {
                if (_question == null) return;
                int n = _question.Options.Count;
                if (n == 0) return;
                int clamped = Math.Min(Math.Max(0, value), n - 1);
                if (!Set(ref _choiceCursor, clamped)) return;
                PlaySound?.Invoke(ScriptEffectKind.SoundEffect, MenuSound);
                foreach (ChoiceEntry e in ChoiceEntries) e.IsSelected = e.Index == clamped;
                int scroll = _choiceScroll;
                KeepCursorOnShow();
                if (scroll != _choiceScroll) OnPropertyChanged(nameof(ChoiceItems));
                OnPropertyChanged(nameof(ChoiceCursorRow));
                string about = _question.Descriptions != null && clamped < _question.Descriptions.Count ? _question.Descriptions[clamped] : null;
                if (_question.OnTouchScreen && !string.IsNullOrEmpty(about)) ShowDescription(about);
            }
        }

        /// <summary>SEQ_SE_CONFIRM in Platinum and SEQ_SE_DP_SELECT in HGSS: the same sequence.</summary>
        public const int MenuSound = 1500;

        public int ChoiceLeft => _question?.Kind == ScriptQuestion.QuestionKind.YesNo ? YesNoLeft : _question?.X ?? 0;
        public int ChoiceTop => _question?.Kind == ScriptQuestion.QuestionKind.YesNo ? YesNoTop : _question?.Y ?? 0;

        /// <summary>The yes/no window's writing area starts at tile 25, 13 in both games.</summary>
        public const int YesNoLeft = 25, YesNoTop = 13;

        public void MoveChoiceCursor(int delta)
        {
            if (_question == null || !_question.IsInGame) return;
            int n = _question.Options.Count;
            if (n == 0 || _touchBlink >= 0) return;
            if (_question.OnTouchScreen)
            {
                int to = HgssTouchScreen.Neighbour(n, TouchChoiceIsYesNo, _choiceCursor, 0, Math.Sign(delta));
                if (to >= 0) ChoiceCursor = to;
                return;
            }
            int next = _choiceCursor + delta;
            if (n >= 4 && !ChoiceIsList) next = ((next % n) + n) % n;
            ChoiceCursor = next;
        }

        public void PageChoice(int pages)
        {
            if (_question != null && _question.OnTouchScreen)
            {
                if (_touchBlink >= 0) return;
                int to = HgssTouchScreen.Neighbour(_question.Options.Count, TouchChoiceIsYesNo, _choiceCursor, Math.Sign(pages), 0);
                if (to >= 0) ChoiceCursor = to;
                return;
            }
            if (!ChoiceIsList) return;
            ChoiceCursor = _choiceCursor + pages * ListRows;
        }

        public void ConfirmChoice()
        {
            if (_question == null || !_question.IsInGame || _touchBlink >= 0) return;
            PlaySound?.Invoke(ScriptEffectKind.SoundEffect, MenuSound);
            if (_question.OnTouchScreen) { _touchBlink = 0; OnPropertyChanged(nameof(TouchCursorShown)); return; }
            AnswerOption(_choiceCursor);
        }

        public void CancelChoice()
        {
            if (_question == null || !_question.IsInGame || _touchBlink >= 0) return;
            if (_question.OnTouchScreen)
            {
                // B picks NO, or a menu's last entry when the menu can be backed out of.
                if (_question.Kind == ScriptQuestion.QuestionKind.Menu && !_question.Cancellable) return;
                TouchChoice(_question.Options.Count - 1);
                return;
            }
            if (_question.Kind == ScriptQuestion.QuestionKind.YesNo)
            {
                PlaySound?.Invoke(ScriptEffectKind.SoundEffect, MenuSound);
                // 1 is NO.
                AnswerQuestion(1);
            }
            else if (_question.Cancellable)
            {
                PlaySound?.Invoke(ScriptEffectKind.SoundEffect, MenuSound);
                AnswerQuestion(ScriptWalker.MenuCancelled);
            }
            else if (!ChoiceIsList)
            {
                // Still beeps. The script never hears about it.
                PlaySound?.Invoke(ScriptEffectKind.SoundEffect, MenuSound);
            }
        }

        public void AnswerQuestion(long value)
        {
            if (_question == null) return;

            if (_pendingTrigger != null)
            {
                Trigger trigger = _pendingTrigger;
                _pendingTrigger = null;
                Question = null;

                if (value != 0)
                {
                    GameState.SetVar(trigger.VariableWatched, trigger.ExpectedVarValue);
                    ScriptLines.Clear();
                    RunScript(trigger.ScriptNumber, "The trigger goes off.");
                }
                else
                {
                    ScriptLines.Add("The trigger is left alone.");
                }
                return;
            }

            if (_walker == null) { Question = null; return; }
            _walker.Answer(value);
            Question = null;
        }

        public void AnswerTyped()
        {
            if (long.TryParse(TypedAnswer, out long v)) AnswerQuestion(v);
        }

        public void AnswerOption(int index)
        {
            if (_question != null && index >= 0 && index < _question.Options.Count)
                AnswerQuestion(_question.Options[index].Value);
        }
    }
}
