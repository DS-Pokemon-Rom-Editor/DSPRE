using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using DSPRE.Avalonia.Data;
using DSPRE.Editors;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Graphics
{
    public sealed partial class BottomScreenEditorViewModel
    {
        private void LoadScreens()
        {
            try
            {
                using IDisposable pending = Pending.Reading();
                _hgss = IsHgss ? HgssTouchScreen.Load() : null;
                _poketch = HasPoketch ? PoketchScreen.Load() : null;
                if (HasPoketch) LoadAnimation();
                if (RomInfo.FieldTouchMenuTextArchive >= 0)
                    try { _words = new TextArchive(RomInfo.FieldTouchMenuTextArchive); } catch (Exception ex) { AppLogger.WarnOnce("BottomScreenEditorViewModel.LoadScreens", ex); _words = null; }
            }
            catch (Exception ex) { AppLogger.Error("Bottom screen load: " + ex.Message); }
        }

        // The words carry script variable tags. Unexpanded, the name reads "STRVAR_1, 3, 0, 0".
        private string Word(int message)
        {
            if (_words?.Messages == null || message < 0 || message >= _words.Messages.Count) return null;
            string line = _words.Messages[message];
            return line == null ? null
                 : FieldStringVars.Expand(line, (_, kind, buffer) => FieldStringVars.SuggestFor(kind, buffer, null));
        }

        public void Refresh()
        {
            if (_loading) return;
            string keep = Selected?.Name;

            _menuExtras = null;
            _pieces = BuildPieces();
            NoteSharing(_pieces);

            Pieces.Clear();
            foreach (BottomScreenPiece p in _pieces) Pieces.Add(new BottomScreenPieceRow { Piece = p });

            // Keeps what was picked, or else starts on the first piece so the buttons aren't greyed out.
            int at = keep == null ? -1 : _pieces.FindIndex(p => p.Name == keep);
            _selectedIndex = at >= 0 ? at : (_pieces.Count > 0 ? 0 : -1);
            OnPropertyChanged(nameof(SelectedIndex));
            RaiseSelection();
            Draw();
        }

        private void Draw()
        {
            if (_loading) return;
            try
            {
                byte[] rgba = null;
                if (IsMenuTab && _hgss != null)
                    rgba = _hgss.RenderMenu(_font, _iconFont, Word, MenuLook());
                else if (IsChoicesTab && _hgss != null)
                {
                    (string[] labels, bool yesNo) = ChoiceShown();
                    rgba = _hgss.RenderChoices(_font, labels, yesNo, 0, labels != null);
                }
                else if (HasPoketch && _poketch != null)
                    rgba = PoketchShot();

                Screen = rgba == null ? null : ImageConverter.FromRgba(rgba, DsBgScreen.Width, DsBgScreen.Height);
                if (rgba == null) StatusText = "This screen couldn't be read from the ROM.";
            }
            catch (Exception ex)
            {
                AppLogger.Error("Bottom screen draw: " + ex.Message);
                StatusText = "This screen couldn't be drawn. " + ex.Message;
            }
        }

        private HgssTouchScreen.MenuLook MenuLook()
        {
            // With the menu open, the icon picked in the list is the one the cursor is on.
            int kind = Selected?.IconKind ?? -1;
            int cursor = MenuOpen ? HgssTouchScreen.SlotOf(kind < 0 ? 0 : kind, ModeIndex) : -1;
            int label = ALabelIndex >= 0 && ALabelIndex < HgssTouchScreen.ALabelMessages.Length
                ? HgssTouchScreen.ALabelMessages[ALabelIndex] : HgssTouchScreen.CheckMessage;
            return new HgssTouchScreen.MenuLook
            {
                Busy = ScriptRunning || MenuOpen, Cursor = cursor, AHeld = AHeld,
                Shoes = ShoesShown, ShoesOn = ShoesOn, RegisteredItems = RegisteredItems,
                Mode = ModeIndex, Girl = Female, ALabel = label, Extras = _menuExtras ??= FieldTouchMenuButtons.Read()?.Extras,
            };
        }

        // The frame and the Digital Watch draw the watch with its digits. Other apps get the casing.
        private byte[] PoketchShot()
        {
            if (NoPoketchYet) { BankCount = 0; return _poketch.RenderUnavailable(); }

            PoketchApps.App app = _app >= 1 && _app <= PoketchApps.All.Length ? PoketchApps.All[_app - 1] : null;
            if (app == null || app.Name == "Digital Watch")
            {
                BankCount = 0;
                return _poketch.RenderWatch(Female, Theme, Backlight, Hour, Minute,
                                            PoketchScreen.Look.Free, PoketchScreen.Look.Free);
            }

            BankCount = app.Cells >= 0 ? _poketch.CellBankCount(app.Cells) : 0;
            HasContents = app.Fills != null || app.SpriteSlots != null;
            return _poketch.RenderApp(Female, Theme, Backlight, app.Tiles, app.Arrangement,
                                      ShowSprites ? app.Sprites : -1,
                                      ShowSprites ? app.Cells : -1,
                                      CellBank,
                                      FillContents ? app.SpriteSlots : null,
                                      FillContents ? app.Fills : null,
                                      _motion, app.BgLayer);
        }
    }
}
