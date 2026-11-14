using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using DSPRE.Avalonia.Data;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Graphics
{
    public sealed class ScreenArchiveChoice
    {
        public ScreenArchiveChoice(int bank, string first) { Bank = bank; _first = first; }
        private readonly string _first;
        public int Bank { get; }
        public string Label => string.IsNullOrEmpty(_first) ? $"Text archive {Bank}" : $"{Bank}  {_first}";
    }

    public sealed class ScreenLineRow
    {
        public ScreenLineRow(int index, string text) { Index = index; Text = text ?? ""; }
        public int Index { get; }
        public string Text { get; }
        /// <summary>The line as the archive holds it, line breaks as spaces.</summary>
        public string Label => $"{Index}  {Text.Replace("\\n", " ").Replace("\\r", " ").Replace("\\f", " ").Trim()}";
        public string Full => Text.Replace("\\n", "\n").Replace("\\r", "\n").Replace("\\f", "\n");

        private static string Words(string text) =>
            global::System.Text.RegularExpressions.Regex.Replace(FieldStringVars.Expand(text ?? "", GameTextScreens.SampleWord), @"\{[^}]*\}", "");

        internal static string Readable(string text) => Words(text).Replace("\\n", " ").Replace("\\r", " ").Replace("\\f", " ").Trim();
    }

    /// <summary>An archive file the screen is drawn from, opened in All graphics.</summary>
    public sealed class ScreenPieceRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private bool _lit;
        /// <summary>True when it draws the picked spot or the picked sample Pokémon.</summary>
        public bool IsLit
        {
            get => _lit;
            set
            {
                if (_lit == value) return;
                _lit = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsLit)));
            }
        }

        public ScreenPieceRow(DirNames dir, int member, string kind, bool nameArchive)
        {
            Dir = dir;
            Member = member;
            GraphicAssets.Archive archive = GraphicAssets.All.FirstOrDefault(a => a.Dir == dir);
            string name = null;
            try { name = archive?.NameOf?.Invoke(member); } catch (Exception ex) { AppLogger.WarnOnce("ScreensEditor.PieceName", ex); }
            Name = string.IsNullOrEmpty(name) ? kind ?? "File" : name;
            Where = nameArchive ? $"{archive?.Title ?? dir.ToString()}, file {member}" : $"File {member}";
        }

        public DirNames Dir { get; }
        public int Member { get; }
        public string Name { get; }
        public string Where { get; }

        public void Open() => AvaloniaEditorLauncher.OpenGraphicAt(Dir, Member);
    }
}
