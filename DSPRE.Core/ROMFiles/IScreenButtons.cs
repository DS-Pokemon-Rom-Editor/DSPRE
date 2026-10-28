using System.Collections.Generic;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// A touch screen's buttons page by page: each button's touch area and, for the first <see cref="StopsOf"/>, a cursor stop
    /// and where the arrow keys go from it.
    /// </summary>
    public interface IScreenButtons
    {
        IReadOnlyList<string> PageNames { get; }
        string[] ButtonsOf(int page);
        int StopsOf(int page);

        /// <summary>Top, bottom, left and right in pixels, as the game tests them.</summary>
        (int Top, int Bottom, int Left, int Right) AreaOf(int page, int button);

        /// <summary>Null, or why this touch area stays where it is.</summary>
        string MoveLocked(int page, int button) => null;

        /// <summary>Pixels per move step, for areas whose art sits on the tile grid.</summary>
        int MoveStep(int page, int button) => 1;
        /// <summary>Moves the touch area and its cursor without leaving the screen.</summary>
        void Move(int page, int button, int dx, int dy);

        /// <summary>Sets the touch area's size from its top left corner.</summary>
        void Resize(int page, int button, int width, int height);

        string StopName(int page, int stop) => ButtonsOf(page)[stop];
        /// <summary>The middle of the stop's cursor in pixels.</summary>
        (double X, double Y) CursorCentre(int page, int stop);

        /// <summary>The byte the arrow holds.</summary>
        int Target(int page, int stop, CursorDirection d);

        void SetTarget(int page, int stop, CursorDirection d, int target);

        /// <summary>Every value an arrow on the page can hold, with its name.</summary>
        IReadOnlyList<(int Value, string Name)> Choices(int page);

        /// <summary>The value that sends the cursor to a stop.</summary>
        int ValueOf(int page, int stop);

        /// <summary>The stop a value sends the cursor to, or -1 when the game picks one when it happens.</summary>
        int StopOf(int page, int value);

        /// <summary>Null, or why the game doesn't read this arrow.</summary>
        string Fixed(int page, int stop, CursorDirection d);

        /// <summary>False for a stop the cursor never reaches, such as a touch-only arrow.</summary>
        bool HasCursor(int page, int stop) => true;

        byte[] ToBytes();
        void FromBytes(byte[] bytes);

        /// <summary>The bytes the file holds now, in <see cref="ToBytes"/> order.</summary>
        byte[] FileBytes();

        void WriteFile(byte[] bytes);

        /// <summary>Null when the game can use these tables; otherwise the first problem they didn't already have.</summary>
        string WhyNot();

        /// <summary>Null when the bytes can be written.</summary>
        string WhyNotWrite(byte[] bytes);
    }

    /// <summary>Screens whose cursor walks a grid of cells, each holding a button; the arrows follow from the grid.</summary>
    public interface IScreenButtonGrid
    {
        /// <summary>Columns and rows, or (0, 0) when the page has no grid.</summary>
        (int Width, int Height) GridOf(int page);

        int Cell(int page, int x, int y);
        void SetCell(int page, int x, int y, int button);
    }

    /// <summary>Screens with more choices per stop than one arrow holds, edited as rows under the stops.</summary>
    public interface IScreenButtonRows
    {
        string RowsLabel { get; }
        string RowsTip { get; }
        IReadOnlyList<string> RowNames(int page);
        int RowLength(int page);
        /// <summary>A value from <see cref="IScreenButtons.Choices"/>.</summary>
        int RowValue(int page, int row, int i);
        void SetRowValue(int page, int row, int i, int value);
    }
}
