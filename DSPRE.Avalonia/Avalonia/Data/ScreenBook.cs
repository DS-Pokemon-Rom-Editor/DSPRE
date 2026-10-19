using System;
using System.Collections.Generic;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// One set of pages the screen pages editor shows: the archive they are drawn from, the pages themselves and the
    /// sample whose values and pictures fill them in. The Pokédex and the summary screen are each one.
    /// </summary>
    public abstract class ScreenBook
    {
        /// <summary>The editor's name, which titles its window and its messages.</summary>
        public abstract string Title { get; }

        /// <summary>The archive every page is drawn from.</summary>
        public abstract DirNames Archive { get; }

        /// <summary>What the editor's unsaved changes are called when the app asks about them.</summary>
        public virtual string UnsavedChangesDescription => Title;

        public abstract IReadOnlyList<ScreenPage> Pages { get; }

        /// <summary>A composer drawing these pages with the sample's pictures and values.</summary>
        public abstract ScreenPageComposer MakeComposer(Func<int, FieldFont> font);

        /// <summary>Archives other editors save that the pages show, so a save there redraws them.</summary>
        public virtual DirNames[] PictureArchives => Array.Empty<DirNames>();

        /// <summary>The drawing a sprite shows, which can follow the sample (a type icon picks its type's drawing).</summary>
        public virtual int SpriteDrawing(ScreenSprite sprite) => sprite.Drawing;

        /// <summary>The files a sample picture is drawn from, which live in other archives.</summary>
        public virtual IEnumerable<(string Name, string Where, DirNames Dir, int Member)> MonParts(ScreenMon mon)
            => Array.Empty<(string, string, DirNames, int)>();

        /// <summary>Forgets what the sample has read, so an edited file is read again.</summary>
        public virtual void Forget() { }

        /// <summary>The pieces of the pages the game places in its code, which can be moved, or null.</summary>
        public virtual ScreenBookLayout Layout => null;
    }

    /// <summary>A text box or bar, in tiles on its screen.</summary>
    public sealed class ScreenLayoutItem
    {
        public string Name { get; init; }
        public ScreenSide Side { get; init; }
        public int X { get; init; }
        public int Y { get; init; }
        public int Width { get; init; }
        public int Height { get; init; }
        public bool CanResize { get; init; }
        /// <summary>What the layout knows the item by.</summary>
        public object Key { get; init; }
    }

    /// <summary>
    /// Where a book's code places its text boxes and bars. The layout is bytes staged like any other edit; the
    /// book draws its pages from whatever bytes it was last given.
    /// </summary>
    public abstract class ScreenBookLayout
    {
        /// <summary>The bytes the layout lives in, which an edit stages and Save writes.</summary>
        public abstract PendingArchiveWrites.Target Target { get; }

        /// <summary>Draws the pages from these layout bytes from now on.</summary>
        public abstract void Use(byte[] bytes);

        /// <summary>The items shown on a page in a state.</summary>
        public abstract IReadOnlyList<ScreenLayoutItem> Items(int page, string variant);

        /// <summary>The layout bytes with an item moved or resized, or why it can't be.</summary>
        public abstract (byte[] Bytes, string Error) Place(ScreenLayoutItem item, int x, int y, int width, int height);
    }
}
