using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using DSPRE.Avalonia.Data;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia.Controls
{
    /// <summary>
    /// The follower or player beside a notice or dialog, facing the viewer and walking in place, with an HGSS speech bubble above
    /// its head. Hidden when the open game's sprites can't be read.
    /// </summary>
    public class NoticeFigure : Grid
    {
        private const int Scale = 2;
        private const int Bubble = 32 * Scale;

        public NoticeFigure(SpeechBubble bubble)
        {
            IsHitTestVisible = false;
            (ushort entry, int frames) = NoticeArt.Who();
            Bitmap[] pictures = NoticeArt.Bubble(bubble);
            IsVisible = frames > 0;
            // Followers come in several sizes, so the box is sized from the sprite.
            OverworldSprites.SpritePixels standing = frames > 0 ? OverworldSprites.Get(entry, (ushort)MoveFacing.Down, ifUnpacked: true) : null;
            double spriteWidth = (standing?.Width ?? 32) * Scale, spriteHeight = (standing?.Height ?? 32) * Scale;
            Width = Math.Max(spriteWidth, pictures != null ? Bubble : 0);
            // The bubble's top sits half its height above the sprite, so it covers the head.
            Height = pictures != null ? spriteHeight + Bubble / 2 : spriteHeight;
            Children.Add(new OverworldSpriteView
            {
                Entry = entry,
                Facing = (int)MoveFacing.Down,
                Scale = Scale,
                WalkInPlace = true,
                IfUnpacked = true,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
            });
            if (pictures != null) Children.Add(new BubbleView(pictures) { VerticalAlignment = VerticalAlignment.Top });
        }

        /// <summary>
        /// <paramref name="content"/> with the figure on its left, or <paramref name="content"/> alone when the open
        /// game's art can't be drawn.
        /// </summary>
        public static Control Beside(Control content, SpeechBubble bubble, VerticalAlignment align, Thickness margin)
        {
            NoticeFigure figure;
            try
            {
                if (!NoticeArt.Available) return content;
                figure = new NoticeFigure(bubble) { VerticalAlignment = align, Margin = margin };
            }
            catch (Exception ex)
            {
                AppLogger.WarnOnce("NoticeFigure.Beside", ex);
                return content;
            }
            if (!figure.IsVisible) return content;
            Grid row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
            Grid.SetColumn(content, 1);
            row.Children.Add(figure);
            row.Children.Add(content);
            return row;
        }

        /// <summary>Cycles the bubble's pictures at sprite scale.</summary>
        private sealed class BubbleView : Control
        {
            private const int FramesPerPicture = 8;
            private readonly DispatcherTimer _timer;
            private readonly Bitmap[] _pictures;
            private int _tick;

            public BubbleView(Bitmap[] pictures)
            {
                _pictures = pictures;
                Height = Bubble;
                _timer = SpritePictures.Clock(() => { _tick++; InvalidateVisual(); });
            }

            protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
            {
                base.OnAttachedToVisualTree(e);
                if (_pictures.Length > 1) _timer.Start();
            }

            protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
            {
                base.OnDetachedFromVisualTree(e);
                _timer.Stop();
            }

            public override void Render(DrawingContext context)
            {
                if (_pictures.Length == 0) return;
                Bitmap shown = _pictures[_tick / FramesPerPicture % _pictures.Length];
                double w = shown.PixelSize.Width * Scale, h = shown.PixelSize.Height * Scale;
                SpritePictures.Draw(context, shown, new Rect((Bounds.Width - w) / 2, 0, w, h));
            }
        }
    }
}
