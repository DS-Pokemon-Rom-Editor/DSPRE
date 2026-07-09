using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using AvaloniaEdit.Search;
using AvaloniaEdit.TextMate;
using DSPRE.Avalonia.Gl;
using DSPRE.Avalonia.ViewModels;
using NSMBe4.DSFileSystem;
using System;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using TextMateSharp.Grammars;
using static MKDS_Course_Editor.NSBTP.NSBTP.NSBTP_File;

namespace DSPRE.Avalonia.Views
{
    /// <summary>Authored as a <see cref="UserControl"/> so it can be embedded as the Scripts tab in the
    /// Maps workspace; standalone launches host it in an <see cref="EditorHostWindow"/>.</summary>
    public partial class ScriptEditorView : UserControl
    {
        private ScriptEditorViewModel VM => DataContext as ScriptEditorViewModel;
        private RegistryOptions _registryOptions;
        private AvaloniaEdit.TextMate.TextMate.Installation _textMate;
        private SearchPanel _fileSearchPanel;
        private CtrlHoverUnderlineColorizer _ctrlHoverUnderline;
        private bool _setupDone;
        private bool _syncing;
        private int _hoverRequestId;
        private bool _hasLastPointerTextPosition;
        private int _lastPointerLine;
        private int _lastPointerColumn;

        public ScriptEditorView()
        {
            InitializeComponent();

            RotomEditor.Options.ConvertTabsToSpaces = false;
            RotomEditor.Options.IndentationSize = 4;
            _fileSearchPanel = SearchPanel.Install(RotomEditor);

            _registryOptions = new RegistryOptions(ThemeName.OneDark);
            _textMate = RotomEditor.InstallTextMate(_registryOptions);
            _ctrlHoverUnderline = new CtrlHoverUnderlineColorizer();
            RotomEditor.TextArea.TextView.LineTransformers.Add(_ctrlHoverUnderline);
            ApplyTheme();
            ApplyGrammar();

            RotomEditor.TextChanged += (_, _) =>
            {
                if (!_syncing && VM != null) VM.ScriptText = RotomEditor.Text;
            };
            RotomEditor.KeyDown += RotomEditor_KeyDown;
            RotomEditor.KeyUp += RotomEditor_KeyUp;
            RotomEditor.PointerMoved += RotomEditor_PointerMoved;
            RotomEditor.PointerHover += RotomEditor_PointerHover;
            RotomEditor.PointerHoverStopped += (_, _) =>
            {
                CloseHoverTip();
                ClearCtrlHoverUnderline();
            };
            RotomEditor.PointerExited += (_, _) =>
            {
                _hasLastPointerTextPosition = false;
                CloseHoverTip();
                ClearCtrlHoverUnderline();
            };
            AddHandler(InputElement.KeyDownEvent, ScriptEditor_KeyDown, RoutingStrategies.Tunnel, true);
            RotomEditor.AddHandler(InputElement.PointerPressedEvent, RotomEditor_PointerPressed, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, true);

            Loaded += OnLoadedSetup;
        }

        public ScriptEditorView(ScriptEditorViewModel vm) : this()
        {
            DataContext = vm;
        }

        private async void OnLoadedSetup(object sender, RoutedEventArgs e) => await EnsureSetupAsync();

        /// <summary>
        /// One-time VM setup. No-ops until a ROM is loaded — the embedded Maps-workspace instance is
        /// created at app boot, before any ROM; <see cref="MapsWorkspaceView"/> re-invokes this after a load.
        /// </summary>
        public async Task EnsureSetupAsync()
        {
            if (_setupDone || Design.IsDesignMode) return;
            var vm = VM;
            if (vm == null || !AvaloniaEditorLauncher.IsRomLoaded) return;
            var owner = TopLevel.GetTopLevel(this) as Window;
            if (owner == null) return;

            _setupDone = true;
            // Hook the owning Window's Closed (not this UserControl's DetachedFromVisualTree — that
            // fires on every tab switch when embedded in the Maps workspace, which would tear down the
            // LSP mid-session). For the embedded case the owner is the main window, whose Closed only
            // fires at app exit — exactly when this cleanup should happen there too.
            owner.Closed += (_, _) =>
            {
                RotomEditor.TextArea.TextView.LineTransformers.Remove(_ctrlHoverUnderline);
                VM?.ShutdownLsp();
                _textMate?.Dispose();
            };
            vm.PropertyChanged += OnVmPropertyChanged;
            await vm.SetupAsync(owner);
            PushToEditor(vm.ScriptText);
            UpdateReadOnly();
            ApplyGrammar();
        }

        private void OnVmPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(ScriptEditorViewModel.ScriptText):
                    PushToEditor(VM.ScriptText);
                    break;
                case nameof(ScriptEditorViewModel.IsReadOnly):
                case nameof(ScriptEditorViewModel.IsBusy):
                case nameof(ScriptEditorViewModel.IsEditable):
                    UpdateReadOnly();
                    break;
                case nameof(ScriptEditorViewModel.SelectedScriptPath):
                    ApplyGrammar();
                    break;
                case nameof(ScriptEditorViewModel.SelectedEditorThemeName):
                    ApplyTheme();
                    break;
            }
        }

        private void PushToEditor(string text)
        {
            text ??= "";
            if (RotomEditor.Text == text) return;
            _syncing = true;
            RotomEditor.Text = text;
            _syncing = false;
        }

        private void UpdateReadOnly()
        {
            var vm = VM;
            RotomEditor.IsReadOnly = vm == null || !vm.IsEditable;
        }

        private void ApplyGrammar()
        {
            if (_textMate == null) return;

            try
            {
                string path = VM?.SelectedScriptPath;
                if (path != null && Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase))
                {
                    string scope = _registryOptions.GetScopeByExtension(".json") ?? "source.json";
                    _textMate.SetGrammar(scope);
                    return;
                }

                string grammarPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Avalonia", "TextMate", "rotom.tmLanguage.json");
                if (!System.IO.File.Exists(grammarPath))
                    grammarPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "rotom.tmLanguage.json");
                if (System.IO.File.Exists(grammarPath))
                    _textMate.SetGrammarFile(grammarPath);
            }
            catch (Exception ex)
            {
                AppLogger.Error("Failed to apply Rotom TextMate grammar: " + ex.Message);
            }
        }

        private void ApplyTheme()
        {
            if (_textMate == null || _registryOptions == null) return;

            try
            {
                ThemeName theme = ThemeName.OneDark;
                string selected = VM?.SelectedEditorThemeName;
                if (string.IsNullOrEmpty(selected) || !Enum.TryParse(selected, out theme))
                    theme = ThemeName.OneDark;

                var textMateTheme = _registryOptions.LoadTheme(theme);
                if (textMateTheme != null)
                    _textMate.SetTheme(textMateTheme);
            }
            catch (Exception ex)
            {
                AppLogger.Error("Failed to apply Rotom editor theme: " + ex.Message);
            }
        }

        private async void Save_Click(object sender, RoutedEventArgs e) => await Safe(VM?.SaveAsync());
        private async void Import_Click(object sender, RoutedEventArgs e) => await Safe(VM?.ImportAsync());
        private async void Export_Click(object sender, RoutedEventArgs e) => await Safe(VM?.ExportAsync());
        private void Add_Click(object sender, RoutedEventArgs e) => VM?.AddScriptFile();

        private void Find_Click(object sender, RoutedEventArgs e)
        {
            RotomEditor.Focus();
            _fileSearchPanel?.Open();
        }

        private void ProjectSearch_Click(object sender, RoutedEventArgs e) => VM?.SearchProject();

        private void ProjectSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) VM?.SearchProject();
        }

        private void ProjectSearchResult_DoubleTapped(object sender, TappedEventArgs e) => OpenSelectedSearchResult();

        private void ProjectSearchResult_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) OpenSelectedSearchResult();
        }

        private void Diagnostic_DoubleTapped(object sender, TappedEventArgs e) => OpenSelectedDiagnostic();

        private void Diagnostic_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) OpenSelectedDiagnostic();
        }

        private void ClearDiagnostics_Click(object sender, RoutedEventArgs e) => VM?.ClearDiagnostics();

        private async void ScriptEditor_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.S || (e.KeyModifiers & KeyModifiers.Control) == 0) return;

            e.Handled = true;
            await Safe(VM?.SaveAsync());
        }

        private async void RotomEditor_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
            {
                UpdateCtrlHoverUnderline();
                return;
            }

            if (e.Key == Key.F12)
            {
                e.Handled = true;
                await GoToDefinitionAtCaret();
            }
        }

        private void RotomEditor_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
                ClearCtrlHoverUnderline();
        }

        private void RotomEditor_PointerMoved(object sender, PointerEventArgs e)
        {
            if (TryGetEditorPosition(e, out int line, out int column))
            {
                _hasLastPointerTextPosition = true;
                _lastPointerLine = line;
                _lastPointerColumn = column;
            }
            else
            {
                _hasLastPointerTextPosition = false;
            }

            if ((e.KeyModifiers & KeyModifiers.Control) == 0)
            {
                ClearCtrlHoverUnderline();
                return;
            }

            UpdateCtrlHoverUnderline(e);
        }

        private async void RotomEditor_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            if ((e.KeyModifiers & KeyModifiers.Control) == 0) return;
            if (!e.GetCurrentPoint(RotomEditor).Properties.IsLeftButtonPressed) return;
            if (!TryGetEditorPosition(e, out int line, out int column)) return;

            e.Handled = true;
            CloseHoverTip();
            await GoToDefinitionAt(line, column);
        }

        private async void RotomEditor_PointerHover(object sender, EventArgs e)
        {
            if (e is not PointerEventArgs pointerEvent) return;
            if (!TryGetEditorPosition(pointerEvent, out int line, out int column)) return;
            _hasLastPointerTextPosition = true;
            _lastPointerLine = line;
            _lastPointerColumn = column;
            if ((pointerEvent.KeyModifiers & KeyModifiers.Control) != 0)
                UpdateCtrlHoverUnderline(pointerEvent);

            int requestId = ++_hoverRequestId;
            string text = await (VM?.HoverAsync(line, column) ?? Task.FromResult<string>(null));
            if (requestId != _hoverRequestId) return;

            if (string.IsNullOrWhiteSpace(text))
            {
                CloseHoverTip();
                return;
            }

            ToolTip.SetTip(RotomEditor, BuildHoverContent(text));
            ToolTip.SetIsOpen(RotomEditor, true);
        }

        private Control BuildHoverContent(string markdown)
        {
            var panel = new StackPanel
            {
                MaxWidth = 520,
                Spacing = 4
            };

            bool inCode = false;
            var code = new StringBuilder();

            foreach (string rawLine in (markdown ?? "").Replace("\r\n", "\n").Split('\n'))
            {
                string line = rawLine.TrimEnd('\r');
                if (line.StartsWith("```", StringComparison.Ordinal))
                {
                    if (inCode)
                    {
                        panel.Children.Add(new SelectableTextBlock
                        {
                            Text = code.ToString().TrimEnd('\n'),
                            FontFamily = new FontFamily("Consolas, Cascadia Mono, Menlo, monospace"),
                            FontSize = 12,
                            TextWrapping = TextWrapping.Wrap,
                            Background = new SolidColorBrush(Color.FromArgb(0x22, 0x80, 0x80, 0x80)),
                            Padding = new Thickness(6, 4),
                            MaxWidth = 500
                        });
                        code.Clear();
                        inCode = false;
                    }
                    else
                    {
                        inCode = true;
                    }
                    continue;
                }

                if (inCode)
                {
                    code.AppendLine(line);
                    continue;
                }

                if (line.Length == 0)
                {
                    panel.Children.Add(new Border { Height = 2 });
                    continue;
                }

                var textBlock = new TextBlock
                {
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 500
                };

                if (line.StartsWith("# ", StringComparison.Ordinal))
                {
                    textBlock.FontWeight = FontWeight.Bold;
                    textBlock.FontSize = 14;
                    AddMarkdownInlines(textBlock, line.Substring(2));
                }
                else if (line.StartsWith("## ", StringComparison.Ordinal))
                {
                    textBlock.FontWeight = FontWeight.Bold;
                    AddMarkdownInlines(textBlock, line.Substring(3));
                }
                else if (line.StartsWith("- ", StringComparison.Ordinal))
                {
                    textBlock.Inlines.Add(new Run("• "));
                    AddMarkdownInlines(textBlock, line.Substring(2));
                }
                else if (line.StartsWith("> ", StringComparison.Ordinal))
                {
                    textBlock.FontStyle = FontStyle.Italic;
                    textBlock.Foreground = new SolidColorBrush(Color.FromRgb(130, 130, 130));
                    AddMarkdownInlines(textBlock, line.Substring(2));
                }
                else
                {
                    AddMarkdownInlines(textBlock, line);
                }

                panel.Children.Add(textBlock);
            }

            if (code.Length > 0)
            {
                panel.Children.Add(new SelectableTextBlock
                {
                    Text = code.ToString().TrimEnd('\n'),
                    FontFamily = new FontFamily("Consolas, Cascadia Mono, Menlo, monospace"),
                    FontSize = 12,
                    TextWrapping = TextWrapping.Wrap,
                    Background = new SolidColorBrush(Color.FromArgb(0x22, 0x80, 0x80, 0x80)),
                    Padding = new Thickness(6, 4),
                    MaxWidth = 500
                });
            }

            return panel;
        }

        private static void AddMarkdownInlines(TextBlock textBlock, string text)
        {
            for (int i = 0; i < text.Length;)
            {
                if (i + 1 < text.Length && text[i] == '*' && text[i + 1] == '*')
                {
                    int end = text.IndexOf("**", i + 2, StringComparison.Ordinal);
                    if (end > i)
                    {
                        var bold = new Bold();
                        bold.Inlines.Add(new Run(text.Substring(i + 2, end - i - 2)));
                        textBlock.Inlines.Add(bold);
                        i = end + 2;
                        continue;
                    }
                }

                if (text[i] == '`')
                {
                    int end = text.IndexOf('`', i + 1);
                    if (end > i)
                    {
                        textBlock.Inlines.Add(new Run(text.Substring(i + 1, end - i - 1))
                        {
                            FontFamily = new FontFamily("Consolas, Cascadia Mono, Menlo, monospace")
                        });
                        i = end + 1;
                        continue;
                    }
                }

                int nextBold = text.IndexOf("**", i, StringComparison.Ordinal);
                int nextCode = text.IndexOf('`', i);
                int next = nextBold < 0 ? nextCode : nextCode < 0 ? nextBold : Math.Min(nextBold, nextCode);
                if (next < 0) next = text.Length;
                textBlock.Inlines.Add(new Run(text.Substring(i, next - i)));
                i = next;
            }
        }

        private async Task GoToDefinitionAtCaret()
        {
            var document = RotomEditor.Document;
            if (document == null) return;

            var location = document.GetLocation(RotomEditor.CaretOffset);
            await GoToDefinitionAt(location.Line, location.Column);
        }

        private async Task GoToDefinitionAt(int line, int column)
        {
            var vm = VM;
            if (vm == null) return;

            ScriptNavigationTarget target = await vm.GoToDefinitionAsync(line, column);
            if (target == null) return;

            SelectEditorRange(target.Line, target.Column, target.SelectionLength);
        }

        private bool TryGetEditorPosition(PointerEventArgs e, out int line, out int column)
        {
            line = 0;
            column = 0;

            var document = RotomEditor.Document;
            var textView = RotomEditor.TextArea?.TextView;
            if (document == null || textView == null) return false;

            try
            {
                var point = e.GetPosition(textView) + new Vector(RotomEditor.HorizontalOffset, RotomEditor.VerticalOffset);
                var position = textView.GetPositionFloor(point);
                if (position == null) return false;

                line = Math.Max(1, Math.Min(position.Value.Line, document.LineCount));
                var documentLine = document.GetLineByNumber(line);
                column = Math.Max(1, Math.Min(position.Value.Column, documentLine.Length + 1));
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void CloseHoverTip()
        {
            _hoverRequestId++;
            ToolTip.SetIsOpen(RotomEditor, false);
        }

        private void UpdateCtrlHoverUnderline(PointerEventArgs e = null)
        {
            int line;
            int column;
            if (e != null)
            {
                if (!TryGetEditorPosition(e, out line, out column))
                {
                    ClearCtrlHoverUnderline();
                    return;
                }
            }
            else if (_hasLastPointerTextPosition)
            {
                line = _lastPointerLine;
                column = _lastPointerColumn;
            }
            else
            {
                ClearCtrlHoverUnderline();
                return;
            }

            if (!TryGetWordRange(line, column, out int startOffset, out int endOffset))
            {
                ClearCtrlHoverUnderline();
                return;
            }

            if (_ctrlHoverUnderline.SetRange(startOffset, endOffset))
                RotomEditor.TextArea.TextView.Redraw();
        }

        private void ClearCtrlHoverUnderline()
        {
            if (_ctrlHoverUnderline.Clear())
                RotomEditor.TextArea.TextView.Redraw();
        }

        private bool TryGetWordRange(int line, int column, out int startOffset, out int endOffset)
        {
            startOffset = 0;
            endOffset = 0;

            var document = RotomEditor.Document;
            if (document == null || line < 1 || line > document.LineCount) return false;

            var documentLine = document.GetLineByNumber(line);
            int offset = document.GetOffset(line, Math.Max(1, Math.Min(column, documentLine.Length + 1)));
            int lineStart = documentLine.Offset;
            int lineEnd = documentLine.EndOffset;
            string text = RotomEditor.Text ?? "";
            if (text.Length == 0 || offset < 0) return false;

            if (offset >= lineEnd && offset > lineStart) offset--;
            if (offset >= text.Length) offset = text.Length - 1;
            if (offset > lineStart && offset < lineEnd && !IsIdentifierChar(text[offset]) && IsIdentifierChar(text[offset - 1]))
                offset--;
            if (offset < lineStart || offset >= lineEnd || !IsIdentifierChar(text[offset])) return false;

            int start = offset;
            while (start > lineStart && IsIdentifierChar(text[start - 1])) start--;

            int end = offset + 1;
            while (end < lineEnd && IsIdentifierChar(text[end])) end++;

            startOffset = start;
            endOffset = end;
            return endOffset > startOffset;
        }

        private static bool IsIdentifierChar(char c)
            => char.IsLetterOrDigit(c) || c == '_' || c == '.' || c == '’' || c == '?';

        private sealed class CtrlHoverUnderlineColorizer : DocumentColorizingTransformer
        {
            private int _startOffset = -1;
            private int _endOffset = -1;

            public bool SetRange(int startOffset, int endOffset)
            {
                if (_startOffset == startOffset && _endOffset == endOffset) return false;
                _startOffset = startOffset;
                _endOffset = endOffset;
                return true;
            }

            public bool Clear()
            {
                if (_startOffset < 0 && _endOffset < 0) return false;
                _startOffset = -1;
                _endOffset = -1;
                return true;
            }

            protected override void ColorizeLine(DocumentLine line)
            {
                if (_startOffset < 0 || _endOffset <= _startOffset) return;

                int start = Math.Max(_startOffset, line.Offset);
                int end = Math.Min(_endOffset, line.EndOffset);
                if (start >= end) return;

                ChangeLinePart(start, end, element =>
                    element.TextRunProperties.SetTextDecorations(TextDecorations.Underline));
            }
        }

        private void OpenSelectedSearchResult()
        {
            if (ProjectSearchResultsList.SelectedItem is not ScriptSearchResult result) return;
            if (VM?.OpenSearchResult(result) != true) return;

            SelectEditorRange(result.Line, result.Column, result.SelectionLength);
        }

        private void OpenSelectedDiagnostic()
        {
            if (DiagnosticsList.SelectedItem is not ScriptDiagnostic diagnostic) return;
            if (VM?.OpenDiagnostic(diagnostic) != true) return;

            SelectEditorRange(diagnostic.Line, diagnostic.Column, diagnostic.SelectionLength);
        }

        private void SelectEditorRange(int line, int column, int length)
        {
            var document = RotomEditor.Document;
            if (document == null) return;

            line = Math.Max(1, line);
            line = Math.Min(line, Math.Max(1, document.LineCount));

            var documentLine = document.GetLineByNumber(line);
            column = Math.Max(1, column);
            column = Math.Min(column, documentLine.Length + 1);

            int start = document.GetOffset(line, column);
            int maxLength = Math.Max(0, document.TextLength - start);
            length = Math.Min(Math.Max(1, length), maxLength);

            RotomEditor.Select(start, length);
            RotomEditor.TextArea.Caret.BringCaretToView();
            RotomEditor.ScrollToLine(line);
            RotomEditor.TextArea.Focus();
            RotomEditor.Focus();
        }

        private static async Task Safe(Task task)
        {
            if (task == null) return;
            try { await task; }
            catch (Exception ex) { AppLogger.Warn("Script editor async handler failed: " + ex.Message); }
        }
    }
}
