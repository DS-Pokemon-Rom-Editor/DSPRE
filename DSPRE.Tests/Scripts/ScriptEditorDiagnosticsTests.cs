using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using DSPRE;
using DSPRE.Avalonia;
using DSPRE.Avalonia.ViewModels.Text;
using DSPRE.HgEngine;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>Compiler results and live diagnostics keep their own rows in the script editor.</summary>
    [Collection("rom")]
    public class ScriptEditorDiagnosticsTests : IDisposable
    {
        private static readonly MethodInfo CompileUpdate = typeof(ScriptEditorViewModel)
            .GetMethod("UpdateCompileDiagnostics", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo LiveUpdate = typeof(ScriptEditorViewModel)
            .GetMethod("ApplyLspDiagnostics", BindingFlags.Instance | BindingFlags.NonPublic);
        private readonly ScriptEditorViewModel _vm = new ScriptEditorViewModel(false);
        private readonly string _root = Path.Combine(Path.GetTempPath(), "dspre-diagnostics-" + Guid.NewGuid().ToString("N"));
        private readonly string _path;
        private readonly RotomTool.Result _failure;
        private readonly RotomTool.Result _success = new RotomTool.Result
        {
            ExitCode = 0,
            Stdout = "{\"failures\":[],\"successes\":[]}"
        };

        /// <summary>Creates an editor with temporary source files and a failed compile result.</summary>
        public ScriptEditorDiagnosticsTests()
        {
            Directory.CreateDirectory(_root);
            _path = Path.Combine(_root, "0001.rotom");
            File.WriteAllText(_path, "source line\n");
            typeof(ScriptEditorViewModel).GetField("_currentPath", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_vm, _path);
            typeof(ScriptEditorViewModel).GetField("_scriptText", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_vm, "source line\n");
            _failure = new RotomTool.Result
            {
                ExitCode = 1,
                Stdout = JsonSerializer.Serialize(new
                {
                    successes = Array.Empty<object>(),
                    failures = new[]
                    {
                        new { path = _path, error = new { type = "Lowering", details = new { span = new { start = 0, end = 6 }, message = "Archive mapping is unavailable." } } }
                    }
                })
            };
        }

        /// <summary>An empty live result leaves the compile row and selection unchanged.</summary>
        [Fact]
        public void EmptyLiveUpdateKeepsTheCompileErrorAndSelectionWithoutResettingTheList()
        {
            CompileUpdate.Invoke(_vm, new object[] { _failure });
            ScriptDiagnostic error = Assert.Single(_vm.Diagnostics);
            _vm.SelectedDiagnostic = error;
            int changes = 0;
            _vm.Diagnostics.CollectionChanged += (_, _) => changes++;

            LiveUpdate.Invoke(_vm, new object[] { new RotomLspDiagnosticsEventArgs(_path, Array.Empty<RotomLspDiagnostic>()) });

            Assert.Same(error, Assert.Single(_vm.Diagnostics));
            Assert.Same(error, _vm.SelectedDiagnostic);
            Assert.Equal("Compile", error.Source);
            Assert.Equal(0, changes);
            Assert.Contains("1 error", _vm.DiagnosticsStatusText);
        }

        /// <summary>A new live result removes old live rows and keeps compile rows.</summary>
        [Fact]
        public void LiveUpdatesReplaceOnlyLiveRows()
        {
            CompileUpdate.Invoke(_vm, new object[] { _failure });
            ScriptDiagnostic error = Assert.Single(_vm.Diagnostics);
            _vm.SelectedDiagnostic = error;
            LiveUpdate.Invoke(_vm, new object[] { new RotomLspDiagnosticsEventArgs(_path, new[] { new RotomLspDiagnostic("Warning", "Analysis", "Old warning", 1, 1, 1) }) });
            LiveUpdate.Invoke(_vm, new object[] { new RotomLspDiagnosticsEventArgs(_path, new[] { new RotomLspDiagnostic("Warning", "Analysis", "New warning", 1, 1, 1) }) });

            Assert.Equal(new[] { "Archive mapping is unavailable.", "New warning" }, _vm.Diagnostics.Select(d => d.Message));
            Assert.Equal(new[] { "Compile", "Live" }, _vm.Diagnostics.Select(d => d.Source));
            Assert.Same(error, _vm.SelectedDiagnostic);

            LiveUpdate.Invoke(_vm, new object[] { new RotomLspDiagnosticsEventArgs(_path, Array.Empty<RotomLspDiagnostic>()) });
            Assert.Same(error, Assert.Single(_vm.Diagnostics));
        }

        /// <summary>A new compile preserves live rows and reports only its own diagnostic count.</summary>
        [Fact]
        public void ANewCompileReplacesOnlyCompileRowsAndReturnsOnlyItsOwnCount()
        {
            LiveUpdate.Invoke(_vm, new object[] { new RotomLspDiagnosticsEventArgs(_path, new[] { new RotomLspDiagnostic("Warning", "Analysis", "Live warning", 1, 1, 1) }) });
            ScriptDiagnostic warning = Assert.Single(_vm.Diagnostics);
            _vm.SelectedDiagnostic = warning;

            Assert.Equal(1, (int)CompileUpdate.Invoke(_vm, new object[] { _failure }));
            Assert.Equal(2, _vm.Diagnostics.Count);
            Assert.Same(warning, _vm.SelectedDiagnostic);
            Assert.Equal(0, (int)CompileUpdate.Invoke(_vm, new object[] { _success }));
            Assert.Same(warning, Assert.Single(_vm.Diagnostics));
            Assert.Contains("1 warning", _vm.DiagnosticsStatusText);
        }

        /// <summary>Clear removes rows from both sources and later live updates do not restore compile rows.</summary>
        [Fact]
        public void ClearRemovesBothSourcesWithoutRestoringOldCompileErrors()
        {
            CompileUpdate.Invoke(_vm, new object[] { _failure });
            LiveUpdate.Invoke(_vm, new object[] { new RotomLspDiagnosticsEventArgs(_path, new[] { new RotomLspDiagnostic("Warning", "Analysis", "Live warning", 1, 1, 1) }) });
            Assert.Equal(2, _vm.Diagnostics.Count);
            _vm.SelectedDiagnostic = _vm.Diagnostics[0];

            _vm.ClearDiagnostics();

            Assert.Empty(_vm.Diagnostics);
            Assert.Null(_vm.SelectedDiagnostic);
            Assert.False(_vm.HasDiagnostics);
            LiveUpdate.Invoke(_vm, new object[] { new RotomLspDiagnosticsEventArgs(_path, Array.Empty<RotomLspDiagnostic>()) });
            Assert.Empty(_vm.Diagnostics);
        }

        /// <summary>Switching to a regular or managed file removes old live rows and keeps compile rows.</summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void SwitchingFilesKeepsCompileErrorsAndRemovesPreviousLiveRows(bool managed)
        {
            string next = Path.Combine(_root, "0002.rotom");
            File.WriteAllText(next, "another source line\n");
            List<string> files = (List<string>)typeof(ScriptEditorViewModel).GetField("_sourceFiles", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_vm);
            files.Add(next);
            typeof(ScriptEditorViewModel).GetField("_selectedIndex", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_vm, 0);
            if (managed)
            {
                Dictionary<string, HgEngineOwnedFile> owned = (Dictionary<string, HgEngineOwnedFile>)typeof(ScriptEditorViewModel).GetField("_managedByPath", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_vm);
                owned[next] = new HgEngineOwnedFile { FullPath = next, RelPath = "managed-source.s" };
            }
            CompileUpdate.Invoke(_vm, new object[] { _failure });
            ScriptDiagnostic error = Assert.Single(_vm.Diagnostics);
            _vm.SelectedDiagnostic = error;
            LiveUpdate.Invoke(_vm, new object[] { new RotomLspDiagnosticsEventArgs(_path, new[] { new RotomLspDiagnostic("Warning", "Analysis", "Live warning", 1, 1, 1) }) });

            typeof(ScriptEditorViewModel).GetMethod("LoadSelectedFile", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_vm, null);

            Assert.Same(error, Assert.Single(_vm.Diagnostics));
            Assert.Same(error, _vm.SelectedDiagnostic);
            Assert.Contains("1 error", _vm.DiagnosticsStatusText);
            LiveUpdate.Invoke(_vm, new object[] { new RotomLspDiagnosticsEventArgs(_path, new[] { new RotomLspDiagnostic("Error", "Analysis", "Late old-file result", 1, 1, 1) }) });
            Assert.Same(error, Assert.Single(_vm.Diagnostics));
        }

        /// <summary>Disconnects the editor and removes its temporary source files.</summary>
        public void Dispose()
        {
            _vm.Detach();
            Directory.Delete(_root, true);
        }
    }
}
