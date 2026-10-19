using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DSPRE.Avalonia
{
    internal sealed class RotomLanguageServerClient : IDisposable
    {
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly SemaphoreSlim _writeLock = new SemaphoreSlim(1, 1);
        private readonly Dictionary<int, TaskCompletionSource<JsonElement>> _pendingRequests = new Dictionary<int, TaskCompletionSource<JsonElement>>();
        private readonly object _pendingLock = new object();
        private Process _process;
        private Task _readLoop;
        private int _nextRequestId;
        private bool _initialized;
        private volatile bool _disposed;

        private const int RequestTimeoutSeconds = 5;

        public event EventHandler<RotomLspDiagnosticsEventArgs> DiagnosticsPublished;

        public bool IsRunning => _process != null && !_process.HasExited && _initialized;

        // The server runs inside the project folder, which keeps that folder from being deleted while it lives.
        private static readonly List<RotomLanguageServerClient> Running = new List<RotomLanguageServerClient>();

        /// <summary>Stops every language server, for when the project folder is about to be deleted.</summary>
        public static void StopAll()
        {
            RotomLanguageServerClient[] all;
            lock (Running) all = Running.ToArray();
            foreach (RotomLanguageServerClient client in all) client.Dispose();
        }

        public async Task StartAsync()
        {
            if (!RotomTool.IsLspAvailable)
                throw new FileNotFoundException("rotom-lsp was not found in DSPRE's Tools folder.", RotomTool.LspPath);

            _process = new Process
            {
                StartInfo =
                {
                    WorkingDirectory = RotomTool.ProjectRoot,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                },
                EnableRaisingEvents = true
            };
            if (!DSUtils.ConfigureToolStartInfo(_process.StartInfo, "rotom-lsp"))
            {
                _process.Dispose();
                _process = null;
                throw new FileNotFoundException(
                    DSUtils.ToolAvailabilityError("rotom-lsp"), RotomTool.LspPath);
            }
            _process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                    AppLogger.Debug("rotom-lsp: " + e.Data);
            };

            AppLogger.Info("Starting rotom-lsp: " + _process.StartInfo.FileName + " "
                + _process.StartInfo.Arguments);
            _process.Start();
            lock (Running) Running.Add(this);
            _process.BeginErrorReadLine();
            _readLoop = Task.Run(ReadLoopAsync);

            var initParams = new
            {
                processId = Environment.ProcessId,
                rootPath = RotomTool.ProjectRoot,
                rootUri = FileUri(RotomTool.ProjectRoot),
                clientInfo = new { name = "DSPRE" },
                capabilities = new
                {
                    workspace = new
                    {
                        didChangeWatchedFiles = new { dynamicRegistration = false }
                    },
                    textDocument = new
                    {
                        synchronization = new { didSave = true, dynamicRegistration = false },
                        publishDiagnostics = new { relatedInformation = false },
                        completion = new { completionItem = new { snippetSupport = false } },
                        signatureHelp = new { signatureInformation = new { parameterInformation = new { labelOffsetSupport = true } } },
                        documentSymbol = new { hierarchicalDocumentSymbolSupport = true },
                        codeLens = new { },
                        inlayHint = new { }
                    }
                },
                workspaceFolders = new[]
                {
                    new
                    {
                        uri = FileUri(RotomTool.ProjectRoot),
                        name = string.IsNullOrWhiteSpace(RotomTool.ProjectRoot)
                            ? "DSPRE"
                            : Path.GetFileName(RotomTool.ProjectRoot)
                    }
                }
            };

            await SendRequestAsync("initialize", initParams);

            await SendNotificationAsync("initialized", new { });
            _initialized = true;
        }

        public Task DidOpenAsync(string path, string languageId, int version, string text)
            => SendNotificationAsync("textDocument/didOpen", new
            {
                textDocument = new
                {
                    uri = FileUri(path),
                    languageId,
                    version,
                    text = text ?? ""
                }
            });

        public Task DidChangeAsync(string path, int version, string text)
            => SendNotificationAsync("textDocument/didChange", new
            {
                textDocument = new
                {
                    uri = FileUri(path),
                    version
                },
                contentChanges = new[]
                {
                    new { text = text ?? "" }
                }
            });

        public Task DidSaveAsync(string path)
            => SendNotificationAsync("textDocument/didSave", new
            {
                textDocument = new { uri = FileUri(path) }
            });

        /// <summary>Files changed outside the editor; rotom-lsp drops its cached project state for them.</summary>
        public Task DidChangeWatchedFilesAsync(System.Collections.Generic.IEnumerable<string> paths)
            => SendNotificationAsync("workspace/didChangeWatchedFiles", new
            {
                changes = paths.Select(p => new { uri = FileUri(p), type = 2 }).ToArray()
            });

        public Task DidCloseAsync(string path)
            => SendNotificationAsync("textDocument/didClose", new
            {
                textDocument = new { uri = FileUri(path) }
            });

        public async Task<RotomLspLocation> DefinitionAsync(string path, int line, int column)
        {
            JsonElement response = await SendRequestAsync("textDocument/definition", PositionParams(path, line, column));
            if (!response.TryGetProperty("result", out JsonElement result) || result.ValueKind == JsonValueKind.Null)
                return null;

            if (result.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in result.EnumerateArray())
                {
                    RotomLspLocation location = ReadLocation(item);
                    if (location != null) return location;
                }
                return null;
            }

            return ReadLocation(result);
        }

        public async Task<string> HoverAsync(string path, int line, int column)
        {
            JsonElement response = await SendRequestAsync("textDocument/hover", PositionParams(path, line, column));
            if (!response.TryGetProperty("result", out JsonElement result) || result.ValueKind == JsonValueKind.Null)
                return null;
            if (!result.TryGetProperty("contents", out JsonElement contents))
                return null;

            return ReadHoverText(contents)?.Trim();
        }

        public async Task<List<RotomLspCompletion>> CompletionAsync(string path, int line, int column)
        {
            List<RotomLspCompletion> items = new List<RotomLspCompletion>();
            JsonElement response = await SendRequestAsync("textDocument/completion", PositionParams(path, line, column));
            if (!response.TryGetProperty("result", out JsonElement result) || result.ValueKind == JsonValueKind.Null) return items;
            if (result.ValueKind == JsonValueKind.Object && result.TryGetProperty("items", out JsonElement list)) result = list;
            if (result.ValueKind != JsonValueKind.Array) return items;
            foreach (JsonElement item in result.EnumerateArray())
            {
                string label = item.ReadString("label");
                if (string.IsNullOrEmpty(label)) continue;
                string insert = item.TryGetProperty("textEdit", out JsonElement edit) ? edit.ReadString("newText") : null;
                items.Add(new RotomLspCompletion(label, insert ?? item.ReadString("insertText") ?? label,
                    item.ReadString("detail"), ReadMarkup(item, "documentation")));
            }
            return items;
        }

        public async Task<RotomLspSignature> SignatureHelpAsync(string path, int line, int column)
        {
            JsonElement response = await SendRequestAsync("textDocument/signatureHelp", PositionParams(path, line, column));
            if (!response.TryGetProperty("result", out JsonElement result) || result.ValueKind != JsonValueKind.Object) return null;
            if (!result.TryGetProperty("signatures", out JsonElement signatures) || signatures.GetArrayLength() == 0) return null;
            int active = Math.Clamp(result.ReadInt("activeSignature"), 0, signatures.GetArrayLength() - 1);
            JsonElement signature = signatures[active];
            string label = signature.ReadString("label") ?? "";
            int parameter = signature.TryGetProperty("activeParameter", out _) ? signature.ReadInt("activeParameter") : result.ReadInt("activeParameter");
            int start = -1, end = -1;
            if (signature.TryGetProperty("parameters", out JsonElement parameters) && parameter >= 0 && parameter < parameters.GetArrayLength()
                && parameters[parameter].TryGetProperty("label", out JsonElement p))
            {
                if (p.ValueKind == JsonValueKind.Array && p.GetArrayLength() == 2) { start = p[0].GetInt32(); end = p[1].GetInt32(); }
                else if (p.ValueKind == JsonValueKind.String)
                {
                    start = label.IndexOf(p.GetString(), StringComparison.Ordinal);
                    end = start < 0 ? -1 : start + p.GetString().Length;
                }
            }
            return new RotomLspSignature(label, ReadMarkup(signature, "documentation"), start, end);
        }

        public async Task<List<RotomLspSymbol>> DocumentSymbolsAsync(string path)
        {
            List<RotomLspSymbol> symbols = new List<RotomLspSymbol>();
            JsonElement response = await SendRequestAsync("textDocument/documentSymbol", new { textDocument = new { uri = FileUri(path) } });
            if (!response.TryGetProperty("result", out JsonElement result) || result.ValueKind != JsonValueKind.Array) return symbols;
            void Walk(JsonElement list, int depth)
            {
                foreach (JsonElement symbol in list.EnumerateArray())
                {
                    JsonElement range = default;
                    if (symbol.TryGetProperty("selectionRange", out JsonElement sel)) range = sel;
                    else if (symbol.TryGetProperty("range", out JsonElement r)) range = r;
                    else if (symbol.TryGetProperty("location", out JsonElement loc) && loc.TryGetProperty("range", out JsonElement lr)) range = lr;
                    int line = range.ValueKind == JsonValueKind.Object && range.TryGetProperty("start", out JsonElement st) ? st.ReadInt("line") + 1 : 1;
                    symbols.Add(new RotomLspSymbol(symbol.ReadString("name") ?? "", symbol.ReadInt("kind"), symbol.ReadString("detail"), line, depth));
                    if (symbol.TryGetProperty("children", out JsonElement children) && children.ValueKind == JsonValueKind.Array) Walk(children, depth + 1);
                }
            }
            Walk(result, 0);
            return symbols;
        }

        public async Task<List<RotomLspInlineText>> CodeLensAsync(string path)
        {
            List<RotomLspInlineText> lenses = new List<RotomLspInlineText>();
            JsonElement response = await SendRequestAsync("textDocument/codeLens", new { textDocument = new { uri = FileUri(path) } });
            if (!response.TryGetProperty("result", out JsonElement result) || result.ValueKind != JsonValueKind.Array) return lenses;
            foreach (JsonElement lens in result.EnumerateArray())
            {
                if (!lens.TryGetProperty("command", out JsonElement command) || !lens.TryGetProperty("range", out JsonElement range)) continue;
                string title = command.ReadString("title");
                if (string.IsNullOrWhiteSpace(title) || !range.TryGetProperty("start", out JsonElement start)) continue;
                lenses.Add(new RotomLspInlineText(start.ReadInt("line") + 1, -1, title));
            }
            return lenses;
        }

        public async Task<List<RotomLspInlineText>> InlayHintsAsync(string path, int lastLine)
        {
            List<RotomLspInlineText> hints = new List<RotomLspInlineText>();
            JsonElement response = await SendRequestAsync("textDocument/inlayHint", new
            {
                textDocument = new { uri = FileUri(path) },
                range = new { start = new { line = 0, character = 0 }, end = new { line = Math.Max(0, lastLine), character = 0 } }
            });
            if (!response.TryGetProperty("result", out JsonElement result) || result.ValueKind != JsonValueKind.Array) return hints;
            foreach (JsonElement hint in result.EnumerateArray())
            {
                if (!hint.TryGetProperty("position", out JsonElement position) || !hint.TryGetProperty("label", out JsonElement label)) continue;
                string text = label.ValueKind == JsonValueKind.String ? label.GetString()
                            : label.ValueKind == JsonValueKind.Array ? string.Concat(label.EnumerateArray().Select(part => part.ReadString("value")))
                            : null;
                if (string.IsNullOrEmpty(text)) continue;
                if (hint.TryGetProperty("paddingLeft", out JsonElement pl) && pl.ValueKind == JsonValueKind.True) text = " " + text;
                if (hint.TryGetProperty("paddingRight", out JsonElement pr) && pr.ValueKind == JsonValueKind.True) text += " ";
                hints.Add(new RotomLspInlineText(position.ReadInt("line") + 1, position.ReadInt("character") + 1, text));
            }
            return hints;
        }

        private static string ReadMarkup(JsonElement element, string property)
        {
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(property, out JsonElement value)) return null;
            return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ReadString("value");
        }

        private static object PositionParams(string path, int line, int column)
            => new
            {
                textDocument = new { uri = FileUri(path) },
                position = new
                {
                    line = Math.Max(0, line - 1),
                    character = Math.Max(0, column - 1)
                }
            };

        private async Task<JsonElement> SendRequestAsync(string method, object parameters)
        {
            int id = Interlocked.Increment(ref _nextRequestId);
            TaskCompletionSource<JsonElement> completion = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_pendingLock) _pendingRequests[id] = completion;

            await SendPayloadAsync(new { jsonrpc = "2.0", id, method, @params = parameters });

            if (await Task.WhenAny(completion.Task, Task.Delay(TimeSpan.FromSeconds(RequestTimeoutSeconds), _cts.Token)) != completion.Task)
            {
                lock (_pendingLock) _pendingRequests.Remove(id);
                throw new TimeoutException("rotom-lsp did not answer " + method + " within " + RequestTimeoutSeconds + " seconds.");
            }

            return await completion.Task;
        }

        private Task SendNotificationAsync(string method, object parameters)
            => SendPayloadAsync(new { jsonrpc = "2.0", method, @params = parameters });

        private async Task SendPayloadAsync(object payload)
        {
            if (_disposed || _process == null || _process.HasExited) return;

            string json = JsonSerializer.Serialize(payload);
            byte[] body = Encoding.UTF8.GetBytes(json);
            byte[] header = Encoding.ASCII.GetBytes("Content-Length: " + body.Length + "\r\n\r\n");

            await _writeLock.WaitAsync(_cts.Token);
            try
            {
                // A caller (e.g. a fire-and-forget document-open notification) can still be mid-write
                // when Dispose() runs from elsewhere (a ROM switch restarting the LSP); bail before
                // touching the now-torn-down process/stream rather than throwing into the caller.
                if (_disposed) return;
                Stream stream = _process.StandardInput.BaseStream;
                await stream.WriteAsync(header, 0, header.Length, _cts.Token);
                await stream.WriteAsync(body, 0, body.Length, _cts.Token);
                await stream.FlushAsync(_cts.Token);
            }
            finally
            {
                // Dispose() may have torn down _writeLock while this write was in flight (see above):
                // Release() on an already-disposed SemaphoreSlim throws ObjectDisposedException, which
                // would otherwise surface as a confusing "rotom-lsp open failed" warning on every ROM switch.
                if (!_disposed)
                {
                    try { _writeLock.Release(); } catch (ObjectDisposedException) { }
                }
            }
        }

        private async Task ReadLoopAsync()
        {
            try
            {
                Stream stream = _process.StandardOutput.BaseStream;
                while (!_cts.IsCancellationRequested)
                {
                    using JsonDocument message = await ReadMessageAsync(stream, _cts.Token);
                    if (message == null) break;
                    await HandleMessageAsync(message.RootElement);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (!_cts.IsCancellationRequested)
                    AppLogger.Warn("rotom-lsp read loop stopped: " + ex.Message);
            }
        }

        private async Task<JsonDocument> ReadMessageAsync(Stream stream, CancellationToken token)
        {
            List<byte> headerBytes = new List<byte>();
            byte[] single = new byte[1];

            while (true)
            {
                int read = await stream.ReadAsync(single, 0, 1, token);
                if (read == 0) return null;
                headerBytes.Add(single[0]);

                int count = headerBytes.Count;
                if (count >= 4
                    && headerBytes[count - 4] == (byte)'\r'
                    && headerBytes[count - 3] == (byte)'\n'
                    && headerBytes[count - 2] == (byte)'\r'
                    && headerBytes[count - 1] == (byte)'\n')
                {
                    break;
                }
            }

            string header = Encoding.ASCII.GetString(headerBytes.ToArray());
            int length = 0;
            foreach (string line in header.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                int colon = line.IndexOf(':');
                if (colon <= 0) continue;
                if (line.Substring(0, colon).Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
                    int.TryParse(line.Substring(colon + 1).Trim(), out length);
            }

            if (length <= 0) return null;

            byte[] body = new byte[length];
            int offset = 0;
            while (offset < length)
            {
                int read = await stream.ReadAsync(body, offset, length - offset, token);
                if (read == 0) return null;
                offset += read;
            }

            return JsonDocument.Parse(body);
        }

        private async Task HandleMessageAsync(JsonElement root)
        {
            if (root.TryGetProperty("method", out JsonElement methodElement))
            {
                string method = methodElement.GetString();
                if (method == "textDocument/publishDiagnostics" && root.TryGetProperty("params", out JsonElement diagnosticsParams))
                {
                    PublishDiagnostics(diagnosticsParams);
                    return;
                }

                if (root.TryGetProperty("id", out JsonElement requestId))
                    await SendEmptyResponseAsync(requestId);
                return;
            }

            if (!root.TryGetProperty("id", out JsonElement idElement) || !idElement.TryGetInt32(out int id))
                return;

            TaskCompletionSource<JsonElement> completion;
            lock (_pendingLock)
            {
                if (!_pendingRequests.TryGetValue(id, out completion)) return;
                _pendingRequests.Remove(id);
            }
            completion.TrySetResult(root.Clone());
        }

        private Task SendEmptyResponseAsync(JsonElement idElement)
        {
            if (idElement.ValueKind == JsonValueKind.String)
                return SendPayloadAsync(new { jsonrpc = "2.0", id = idElement.GetString(), result = (object)null });

            if (idElement.TryGetInt32(out int id))
                return SendPayloadAsync(new { jsonrpc = "2.0", id, result = (object)null });

            return Task.CompletedTask;
        }

        private void PublishDiagnostics(JsonElement parameters)
        {
            string uri = parameters.ReadString("uri");
            string path = LocalPath(uri);
            List<RotomLspDiagnostic> diagnostics = new List<RotomLspDiagnostic>();

            if (parameters.TryGetProperty("diagnostics", out JsonElement items) && items.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in items.EnumerateArray())
                {
                    JsonElement range = item.TryGetProperty("range", out JsonElement rangeValue) ? rangeValue : default;
                    JsonElement start = range.ValueKind == JsonValueKind.Object && range.TryGetProperty("start", out JsonElement startValue) ? startValue : default;
                    JsonElement end = range.ValueKind == JsonValueKind.Object && range.TryGetProperty("end", out JsonElement endValue) ? endValue : default;

                    int startLine = start.ReadInt("line");
                    int startColumn = start.ReadInt("character");
                    int endLine = end.ReadInt("line");
                    int endColumn = end.ReadInt("character");
                    string severity = SeverityName(item.ReadInt("severity"));
                    string source = item.ReadString("source");
                    string code = item.ReadString("code");
                    string kind = !string.IsNullOrWhiteSpace(code) ? code : source;
                    if (string.IsNullOrWhiteSpace(kind)) kind = "LSP";

                    diagnostics.Add(new RotomLspDiagnostic(
                        severity,
                        kind,
                        item.ReadString("message") ?? severity,
                        startLine + 1,
                        startColumn + 1,
                        Math.Max(1, endLine == startLine ? endColumn - startColumn : 1)));
                }
            }

            DiagnosticsPublished?.Invoke(this, new RotomLspDiagnosticsEventArgs(path, diagnostics));
        }

        private static RotomLspLocation ReadLocation(JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.Object) return null;

            string uri = element.ReadString("targetUri") ?? element.ReadString("uri");
            if (string.IsNullOrWhiteSpace(uri)) return null;

            JsonElement range = default;
            if (element.TryGetProperty("targetSelectionRange", out JsonElement targetSelectionRange))
                range = targetSelectionRange;
            else if (element.TryGetProperty("range", out JsonElement locationRange))
                range = locationRange;

            JsonElement start = range.ValueKind == JsonValueKind.Object && range.TryGetProperty("start", out JsonElement startValue) ? startValue : default;
            JsonElement end = range.ValueKind == JsonValueKind.Object && range.TryGetProperty("end", out JsonElement endValue) ? endValue : default;

            int startLine = start.ReadInt("line");
            int startColumn = start.ReadInt("character");
            int endLine = end.ReadInt("line");
            int endColumn = end.ReadInt("character");

            return new RotomLspLocation(
                LocalPath(uri),
                startLine + 1,
                startColumn + 1,
                Math.Max(1, endLine == startLine ? endColumn - startColumn : 1));
        }

        private static string ReadHoverText(JsonElement contents)
        {
            if (contents.ValueKind == JsonValueKind.String)
                return contents.GetString();

            if (contents.ValueKind == JsonValueKind.Array)
            {
                List<string> parts = new List<string>();
                foreach (JsonElement item in contents.EnumerateArray())
                {
                    string text = ReadHoverText(item);
                    if (!string.IsNullOrWhiteSpace(text)) parts.Add(text);
                }
                return string.Join("\n\n", parts);
            }

            if (contents.ValueKind != JsonValueKind.Object)
                return contents.ToString();

            string value = contents.ReadString("value");
            if (!string.IsNullOrWhiteSpace(value)) return value;

            return contents.ToString();
        }

        private static string FileUri(string path)
            => string.IsNullOrWhiteSpace(path) ? "" : new Uri(Path.GetFullPath(path)).AbsoluteUri;

        private static string LocalPath(string uri)
        {
            if (string.IsNullOrWhiteSpace(uri)) return "";
            try { return new Uri(uri).LocalPath; }
            catch { return uri; }
        }

        private static string SeverityName(int severity)
            => severity switch
            {
                1 => "Error",
                2 => "Warning",
                3 => "Info",
                4 => "Hint",
                _ => "Info"
            };

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            lock (Running) Running.Remove(this);
            _cts.Cancel();

            lock (_pendingLock)
            {
                foreach (TaskCompletionSource<JsonElement> pending in _pendingRequests.Values)
                    pending.TrySetCanceled();
                _pendingRequests.Clear();
            }

            try
            {
                if (_process != null && !_process.HasExited)
                {
                    _process.Kill(true);
                    _process.WaitForExit(3000);
                }
            }
            catch { }

            _process?.Dispose();
            _writeLock.Dispose();
            _cts.Dispose();
        }
    }

    internal sealed class RotomLspDiagnosticsEventArgs : EventArgs
    {
        public RotomLspDiagnosticsEventArgs(string path, IReadOnlyList<RotomLspDiagnostic> diagnostics)
        {
            Path = path;
            Diagnostics = diagnostics;
        }

        public string Path { get; }
        public IReadOnlyList<RotomLspDiagnostic> Diagnostics { get; }
    }

    internal sealed class RotomLspDiagnostic
    {
        public RotomLspDiagnostic(string severity, string kind, string message, int line, int column, int selectionLength)
        {
            Severity = severity;
            Kind = kind;
            Message = message;
            Line = line;
            Column = column;
            SelectionLength = selectionLength;
        }

        public string Severity { get; }
        public string Kind { get; }
        public string Message { get; }
        public int Line { get; }
        public int Column { get; }
        public int SelectionLength { get; }
    }

    internal sealed class RotomLspLocation
    {
        public RotomLspLocation(string path, int line, int column, int selectionLength)
        {
            Path = path;
            Line = line;
            Column = column;
            SelectionLength = selectionLength;
        }

        public string Path { get; }
        public int Line { get; }
        public int Column { get; }
        public int SelectionLength { get; }
    }

    internal sealed record RotomLspCompletion(string Label, string InsertText, string Detail, string Documentation);

    /// <summary>A signature with the active parameter's span in <see cref="Label"/>, or -1 when none.</summary>
    internal sealed record RotomLspSignature(string Label, string Documentation, int ActiveStart, int ActiveEnd);

    /// <summary>One outline symbol. rotom-lsp puts a script's jump-table slot in
    /// <see cref="Detail"/> as "#N"; other symbols leave it empty.</summary>
    internal sealed record RotomLspSymbol(string Name, int Kind, string Detail, int Line, int Depth);

    /// <summary>Text drawn in the editor at a 1-based line and column, or at the end of the line when Column is -1.</summary>
    internal sealed record RotomLspInlineText(int Line, int Column, string Text);

    internal static class JsonElementExtensions
    {
        public static string ReadString(this JsonElement element, string property)
        {
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(property, out JsonElement value))
                return null;
            return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
        }

        public static int ReadInt(this JsonElement element, string property)
        {
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(property, out JsonElement value))
                return 0;
            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int number))
                return number;
            return 0;
        }
    }
}
