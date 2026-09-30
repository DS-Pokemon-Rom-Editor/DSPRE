using Avalonia.Input;
using Avalonia.Media;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;
using DSPRE.Avalonia.ViewModels.Text;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace DSPRE.Avalonia.Views.Text
{
    // Completion, signature help, code lenses and inlay hints from rotom-lsp.
    public partial class ScriptEditorView
    {
        private CompletionWindow _completion;
        private OverloadInsightWindow _signature;
        private InlineTextGenerator _inlineTexts;
        private ScriptEditorViewModel _lspVm;

        private void SetupLspFeatures()
        {
            _inlineTexts = new InlineTextGenerator();
            RotomEditor.TextArea.TextView.ElementGenerators.Add(_inlineTexts);
            RotomEditor.TextArea.TextEntered += OnTextEntered;
            DataContextChanged += (_, _) => HookLspViewModel();
            HookLspViewModel();
        }

        private void HookLspViewModel()
        {
            if (_lspVm != null) _lspVm.InlineTextsChanged -= OnInlineTextsChanged;
            _lspVm = VM;
            if (_lspVm != null) _lspVm.InlineTextsChanged += OnInlineTextsChanged;
        }

        private void OnInlineTextsChanged(object sender, EventArgs e)
        {
            _inlineTexts.Set(RotomEditor.Document, VM?.InlineTexts);
            RotomEditor.TextArea.TextView.Redraw();
        }

        private async void OnTextEntered(object sender, TextInputEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Text) || VM == null || RotomEditor.IsReadOnly) return;
            char typed = e.Text[^1];
            // The server's own trigger characters.
            if (typed is '(' or ',' or ' ') await ShowSignatureAsync();
            if (_completion == null && (typed is ' ' or '.' or '>' || (IsWordChar(typed) && WordStartsAtCaret())))
                await ShowCompletionAsync();
        }

        private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';

        private bool WordStartsAtCaret()
        {
            int offset = RotomEditor.CaretOffset;
            return offset < 2 || !IsWordChar(RotomEditor.Document.GetCharAt(offset - 2));
        }

        private int WordStart()
        {
            int offset = RotomEditor.CaretOffset;
            while (offset > 0 && IsWordChar(RotomEditor.Document.GetCharAt(offset - 1))) offset--;
            return offset;
        }

        private async Task ShowCompletionAsync()
        {
            var caret = RotomEditor.TextArea.Caret;
            var items = await VM.CompletionAsync(caret.Line, caret.Column);
            if (items.Count == 0 || _completion != null) return;

            _completion = new CompletionWindow(RotomEditor.TextArea) { StartOffset = WordStart() };
            foreach (var item in items) _completion.CompletionList.CompletionData.Add(new CompletionItem(item));
            _completion.Closed += (_, _) => _completion = null;
            _completion.Show();
        }

        private async Task ShowSignatureAsync()
        {
            var caret = RotomEditor.TextArea.Caret;
            var signature = await VM.SignatureHelpAsync(caret.Line, caret.Column);
            if (signature == null)
            {
                _signature?.Close();
                return;
            }
            if (_signature == null)
            {
                _signature = new AboveLineInsightWindow(RotomEditor.TextArea);
                _signature.Closed += (_, _) => _signature = null;
                _signature.Provider = new SingleSignature(signature);
                _signature.Show();
            }
            else
            {
                _signature.Provider = new SingleSignature(signature);
            }
        }

        private void Outline_DoubleTapped(object sender, TappedEventArgs e) => JumpToOutlineEntry();

        private void Outline_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) JumpToOutlineEntry();
        }

        private void JumpToOutlineEntry()
        {
            if (OutlineList.SelectedItem is not OutlineEntry entry || entry.Line > RotomEditor.Document.LineCount) return;
            RotomEditor.TextArea.Caret.Line = entry.Line;
            RotomEditor.TextArea.Caret.Column = 1;
            RotomEditor.ScrollToLine(entry.Line);
            RotomEditor.TextArea.Focus();
        }

        // Ctrl+Space asks for completion wherever the caret is.
        private async Task<bool> HandleCompletionShortcut(KeyEventArgs e)
        {
            if (e.Key != Key.Space || (e.KeyModifiers & KeyModifiers.Control) == 0 || VM == null) return false;
            e.Handled = true;
            await ShowCompletionAsync();
            return true;
        }

        private sealed class CompletionItem : ICompletionData
        {
            private readonly RotomLspCompletion _item;
            public CompletionItem(RotomLspCompletion item) => _item = item;
            public IImage Image => null;
            public string Text => _item.Label;
            public object Content => _item.Label;
            public object Description => string.IsNullOrWhiteSpace(_item.Documentation) ? _item.Detail : (_item.Detail + "\n" + _item.Documentation).Trim();
            public double Priority => 0;

            public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
                => textArea.Document.Replace(completionSegment, _item.InsertText);
        }

        // Opens above the caret line, so the value list that opens below it does not hide the signature.
        private sealed class AboveLineInsightWindow : OverloadInsightWindow
        {
            private bool _adjusting;

            public AboveLineInsightWindow(TextArea textArea) : base(textArea)
            {
                PlacementGravity = global::Avalonia.Controls.Primitives.PopupPositioning.PopupGravity.TopRight;
            }

            // The base places it at the bottom of the caret line on every caret move.
            protected override void OnPropertyChanged(global::Avalonia.AvaloniaPropertyChangedEventArgs change)
            {
                base.OnPropertyChanged(change);
                if (change.Property != VerticalOffsetProperty || _adjusting) return;
                _adjusting = true;
                VerticalOffset = (double)change.NewValue - TextArea.TextView.DefaultLineHeight;
                _adjusting = false;
            }
        }

        private sealed class SingleSignature : IOverloadProvider
        {
            public SingleSignature(RotomLspSignature signature)
            {
                CurrentHeader = Header(signature);
                CurrentContent = signature.Documentation;
            }

            private static object Header(RotomLspSignature signature)
            {
                string label = signature.Label ?? "";
                int start = signature.ActiveStart, end = signature.ActiveEnd;
                if (start < 0 || end <= start || end > label.Length) return label;
                var block = new global::Avalonia.Controls.TextBlock();
                block.Inlines.Add(new global::Avalonia.Controls.Documents.Run(label[..start]));
                block.Inlines.Add(new global::Avalonia.Controls.Documents.Run(label[start..end]) { FontWeight = FontWeight.Bold });
                block.Inlines.Add(new global::Avalonia.Controls.Documents.Run(label[end..]));
                return block;
            }
            public int SelectedIndex { get => 0; set { } }
            public int Count => 1;
            public string CurrentIndexText => null;
            public object CurrentHeader { get; }
            public object CurrentContent { get; }
            public event PropertyChangedEventHandler PropertyChanged { add { } remove { } }
        }

        /// <summary>Draws code lens titles at line ends and inlay hints inside lines, taking no document space.</summary>
        private sealed class InlineTextGenerator : VisualLineElementGenerator
        {
            private List<(int Offset, string Text)> _items = new();

            public void Set(TextDocument document, IReadOnlyList<RotomLspInlineText> texts)
            {
                var items = new List<(int, string)>();
                if (document != null && texts != null)
                {
                    foreach (var t in texts)
                    {
                        if (t.Line < 1 || t.Line > document.LineCount) continue;
                        var line = document.GetLineByNumber(t.Line);
                        int offset = t.Column < 0 ? line.EndOffset : Math.Min(line.Offset + t.Column - 1, line.EndOffset);
                        items.Add((offset, t.Column < 0 ? "   " + t.Text : t.Text));
                    }
                }
                _items = items.OrderBy(i => i.Item1).ToList();
            }

            public override int GetFirstInterestedOffset(int startOffset)
            {
                int end = CurrentContext.VisualLine.LastDocumentLine.EndOffset;
                foreach (var (offset, _) in _items)
                    if (offset >= startOffset && offset <= end) return offset;
                return -1;
            }

            public override VisualLineElement ConstructElement(int offset)
            {
                string text = string.Concat(_items.Where(i => i.Offset == offset).Select(i => i.Text));
                if (text.Length == 0) return null;
                return new HintElement(text);
            }
        }

        // The line assigns text properties only after ConstructElement returns, so the colour is set here.
        private sealed class HintElement : FormattedTextElement
        {
            private static readonly IBrush Hint = new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80));
            public HintElement(string text) : base(text, 0) { }

            public override global::Avalonia.Media.TextFormatting.TextRun CreateTextRun(int startVisualColumn, ITextRunConstructionContext context)
            {
                TextRunProperties?.SetForegroundBrush(Hint);
                return base.CreateTextRun(startVisualColumn, context);
            }
        }
    }
}
