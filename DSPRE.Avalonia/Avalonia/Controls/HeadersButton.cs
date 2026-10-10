using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia.Controls
{
    /// <summary>Lists the map headers using the open file, each opening the Header editor.</summary>
    public sealed class HeadersButton : Button
    {
        public static readonly StyledProperty<HeaderLinks.Kind> SourceProperty = AvaloniaProperty.Register<HeadersButton, HeaderLinks.Kind>(nameof(Source));
        public HeaderLinks.Kind Source { get => GetValue(SourceProperty); set => SetValue(SourceProperty, value); }

        public static readonly StyledProperty<int> FileIdProperty = AvaloniaProperty.Register<HeadersButton, int>(nameof(FileId), -1);
        public int FileId { get => GetValue(FileIdProperty); set => SetValue(FileIdProperty, value); }

        protected override Type StyleKeyOverride => typeof(Button);

        public HeadersButton()
        {
            Content = "Headers";
            Icon.SetKey(this, "goto");
            ToolTip.SetTip(this, "Map headers that use this file");
        }

        protected override async void OnClick()
        {
            base.OnClick();
            int id = FileId;
            HeaderLinks.Kind source = Source;
            if (id < 0) return;
            List<(ushort Header, string Name)> headers;
            try { headers = await Task.Run(() => HeaderLinks.HeadersLinking(source, id)); }
            catch (Exception ex) { AppLogger.Warn($"Header lookup for {source} {id} failed: {ex.Message}"); headers = new List<(ushort, string)>(); }

            IReadOnlyList<string> labels = HeaderLabels.Friendly();
            MenuFlyout menu = new MenuFlyout();
            foreach ((ushort h, string name) in headers)
            {
                MenuItem item = new MenuItem { Header = h < labels.Count ? labels[h] : $"{h}: {name}" };
                ushort header = h;
                item.Click += (_, _) => AvaloniaEditorLauncher.OpenHeaderEditor(header);
                menu.Items.Add(item);
            }
            if (headers.Count == 0) menu.Items.Add(new MenuItem { Header = "No header uses it", IsEnabled = false });
            menu.Items.Add(new Separator());
            MenuItem search = new MenuItem { Header = "Advanced header search" };
            search.Click += (_, _) => AvaloniaEditorLauncher.OpenHeaderSearch();
            menu.Items.Add(search);
            menu.ShowAt(this);
        }
    }
}
