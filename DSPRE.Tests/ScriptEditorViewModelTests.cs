using System.Collections.Generic;
using AvaloniaEdit.Document;
using DSPRE.Avalonia.ViewModels;
using Xunit;

namespace DSPRE.Tests
{
    public class ScriptEditorViewModelTests
    {
        [Fact]
        public void EditorDocumentChange_MarksDirtyWithoutEchoingScriptText()
        {
            var vm = new ScriptEditorViewModel(true);
            var document = new TextDocument("script Main #0:\n\tEnd\n");
            var changedProperties = new List<string>();
            vm.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName);
            vm.AttachEditorDocument(document);

            document.Insert(document.TextLength, "// changed\n");
            vm.NotifyEditorDocumentChanged();

            Assert.True(vm.HasUnsavedChanges);
            Assert.Equal(document.Text, vm.ScriptText);
            Assert.Contains(nameof(ScriptEditorViewModel.HasUnsavedChanges), changedProperties);
            Assert.DoesNotContain(nameof(ScriptEditorViewModel.ScriptText), changedProperties);

            vm.DetachEditorDocument(document);
            Assert.Equal(document.Text, vm.ScriptText);
        }
    }
}
