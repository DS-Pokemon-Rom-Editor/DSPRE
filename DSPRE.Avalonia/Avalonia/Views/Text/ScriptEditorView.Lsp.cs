using Avalonia.Controls;
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
            _inlineTexts = new InlineTextGenerator { Clicked = ShowReferences };
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

        // One use opens straight away; more are listed in the Search tab.
        private async void ShowReferences(RotomLspInlineText lens)
        {
            ScriptEditorViewModel vm = VM;
            if (vm == null || lens.Locations == null) return;
            List<ScriptSearchResult> uses = vm.ShowReferences(lens.Line, lens.Locations);
            if (uses.Count != 1) return;
            try
            {
                if (await vm.OpenSearchResultAsync(uses[0])) SelectEditorRange(uses[0].Line, uses[0].Column, uses[0].SelectionLength);
            }
            catch (Exception ex)
            {
                CrashReporter.LogHandled(ex);
                await DialogHelper.ShowErrorAsync("The use could not be opened:\n" + ex.Message);
            }
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
            Caret caret = RotomEditor.TextArea.Caret;
            List<RotomLspCompletion> items = await VM.CompletionAsync(caret.Line, caret.Column);
            if (items.Count == 0 || _completion != null) return;

            _completion = new CompletionWindow(RotomEditor.TextArea) { StartOffset = WordStart() };
            foreach (RotomLspCompletion item in items) _completion.CompletionList.CompletionData.Add(new CompletionItem(item));
            _completion.Closed += (_, _) => _completion = null;
            _completion.Show();
        }

        private async Task ShowSignatureAsync()
        {
            Caret caret = RotomEditor.TextArea.Caret;
            RotomLspSignature signature = await VM.SignatureHelpAsync(caret.Line, caret.Column);
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
            if (OutlineList.SelectedItem is not OutlineEntry entry) return;
            JumpToLine(entry.Line);
        }

        /// <summary>Places the caret at the start of a line and scrolls it into view.</summary>
        private void JumpToLine(int line)
        {
            if (line > RotomEditor.Document.LineCount) return;
            RotomEditor.TextArea.Caret.Line = line;
            RotomEditor.TextArea.Caret.Column = 1;
            RotomEditor.ScrollToLine(line);
            RotomEditor.TextArea.Focus();
        }

        // Ctrl+Space asks for completion wherever the caret is.
        private async Task<bool> HandleCompletionShortcutAsync(KeyEventArgs e)
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
                TextBlock block = new global::Avalonia.Controls.TextBlock();
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
            private List<(int Offset, string Text, RotomLspInlineText Source)> _items = new();

            public Action<RotomLspInlineText> Clicked { get; set; }

            public void Set(TextDocument document, IReadOnlyList<RotomLspInlineText> texts)
            {
                List<(int, string, RotomLspInlineText)> items = new List<(int, string, RotomLspInlineText)>();
                if (document != null && texts != null)
                {
                    foreach (RotomLspInlineText t in texts)
                    {
                        if (t.Line < 1 || t.Line > document.LineCount) continue;
                        DocumentLine line = document.GetLineByNumber(t.Line);
                        int offset = t.Column < 0 ? line.EndOffset : Math.Min(line.Offset + t.Column - 1, line.EndOffset);
                        items.Add((offset, t.Column < 0 ? "   " + t.Text : t.Text, t));
                    }
                }
                _items = items.OrderBy(i => i.Item1).ToList();
            }

            public override int GetFirstInterestedOffset(int startOffset)
            {
                int end = CurrentContext.VisualLine.LastDocumentLine.EndOffset;
                foreach ((int offset, string _, RotomLspInlineText _) in _items)
                    if (offset >= startOffset && offset <= end) return offset;
                return -1;
            }

            public override VisualLineElement ConstructElement(int offset)
            {
                List<(int Offset, string Text, RotomLspInlineText Source)> here = _items.Where(i => i.Offset == offset).ToList();
                string text = string.Concat(here.Select(i => i.Text));
                if (text.Length == 0) return null;
                RotomLspInlineText uses = here.Select(i => i.Source).FirstOrDefault(s => s.Locations?.Count > 0);
                return uses != null ? new ReferencesElement(text, () => Clicked?.Invoke(uses)) : new HintElement(text);
            }
        }

        // A reference count reads as a link and lists the uses when clicked.
        private sealed class ReferencesElement : FormattedTextElement
        {
            private static readonly IBrush Hint = new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80));
            private readonly Action _click;
            public ReferencesElement(string text, Action click) : base(text, 0) { _click = click; }

            public override global::Avalonia.Media.TextFormatting.TextRun CreateTextRun(int startVisualColumn, ITextRunConstructionContext context)
            {
                TextRunProperties?.SetForegroundBrush(Hint);
                TextRunProperties?.SetTextDecorations(TextDecorations.Underline);
                return base.CreateTextRun(startVisualColumn, context);
            }

            protected override void OnQueryCursor(global::Avalonia.Input.PointerEventArgs e)
            {
                if (e.Source is global::Avalonia.Input.InputElement element) element.Cursor = new global::Avalonia.Input.Cursor(global::Avalonia.Input.StandardCursorType.Hand);
                e.Handled = true;
            }

            protected override void OnPointerPressed(global::Avalonia.Input.PointerPressedEventArgs e)
            {
                if (e.Handled) return;
                _click();
                e.Handled = true;
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
