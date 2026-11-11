using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;
using global::Avalonia.Controls;
using global::Avalonia.Media;

namespace DSPRE.Avalonia.ViewModels.Text
{
    public sealed class StrVarHelpExample
    {
        public string Title { get; }
        public string Script { get; }
        public string Message { get; }

        public StrVarHelpExample(string title, string script, string message)
        {
            Title = title;
            Script = script;
            Message = message;
        }
    }

    public sealed class StrVarTypeReference
    {
        public string TypeLabel { get; }
        public string Pattern { get; }
        public string Description { get; }
        public IReadOnlyList<string> Commands { get; }
        public string CommandsText => string.Join(", ", Commands);

        public StrVarTypeReference(string typeLabel, string pattern, string description, params string[] commands)
        {
            TypeLabel = typeLabel;
            Pattern = pattern;
            Description = description;
            Commands = Array.AsReadOnly(commands ?? Array.Empty<string>());
        }
    }

    public sealed class StrVarHelpSection
    {
        public string Title { get; }
        public string Code { get; }
        public string Body { get; }
        public IReadOnlyList<StrVarHelpExample> Examples { get; }
        public IReadOnlyList<StrVarTypeReference> TypeReferences { get; }
        public bool HasCode => !string.IsNullOrWhiteSpace(Code);
        public bool HasBody => !string.IsNullOrWhiteSpace(Body);

        public StrVarHelpSection(
            string title,
            string code = null,
            string body = null,
            IEnumerable<StrVarHelpExample> examples = null,
            IEnumerable<StrVarTypeReference> typeReferences = null)
        {
            Title = title;
            Code = code;
            Body = body;
            Examples = Array.AsReadOnly((examples ?? Enumerable.Empty<StrVarHelpExample>()).ToArray());
            TypeReferences = Array.AsReadOnly((typeReferences ?? Enumerable.Empty<StrVarTypeReference>()).ToArray());
        }
    }

    /// <summary>A text code the game's printer handles, in DSPRE's spelling, with an example line from the game.</summary>
    public sealed class SpecialCode
    {
        public string Code { get; }
        public string What { get; }
        public string Example { get; }
        public bool HasExample => !string.IsNullOrEmpty(Example);
        public string Note { get; }
        public bool HasNote => !string.IsNullOrEmpty(Note);

        public SpecialCode(string code, string what, string example, string note = null)
        {
            Code = code;
            What = what;
            Example = example;
            Note = note;
        }
    }

    /// <summary>One {COLOR} value with the ink and shadow it gets in the speech box and in menus.</summary>
    public sealed class TextColourRow
    {
        public string Code { get; init; }
        public IBrush SpeechInk { get; init; }
        public IBrush SpeechShadow { get; init; }
        public string SpeechNote { get; init; }
        public IBrush MenuInk { get; init; }
        public IBrush MenuShadow { get; init; }
    }

    public sealed class StrVarHelpViewModel : INotifyPropertyChanged, IDetachable
    {
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>Codes 0 to 4 in this game's colours.</summary>
        public IReadOnlyList<TextColourRow> ColourRows { get; private set; } = Array.Empty<TextColourRow>();
        public bool HasColourRows => ColourRows.Count > 0;

        /// <summary>Colours 3 and 4 are still empty in the speech box row, so they print black.</summary>
        public bool SpeechColoursEmpty { get; private set; }

        public int SpeechColoursMember => ROMFiles.SpeechBoxColoursPatch.Member;

        private void ReadColours()
        {
            if (Design.IsDesignMode || !AvaloniaEditorLauncher.IsRomLoaded) return;
            ushort[] speech = null, menus = null;
            try { speech = GameTextScreens.TalkColours; menus = GameTextScreens.SystemColours; }
            catch (Exception ex) { AppLogger.WarnOnce("StrVarHelpViewModel.ReadColours", ex); }
            if (speech == null || menus == null || speech.Length < 16 || menus.Length < 16) return;

            static IBrush Brush(ushort c)
            {
                (byte r, byte g, byte b) = ROMFiles.NitroBgCodec.Colour(c);
                return new SolidColorBrush(Color.FromRgb(r, g, b));
            }
            List<TextColourRow> rows = new List<TextColourRow>();
            for (int n = 0; n <= 4; n++)
            {
                (int ink, int shadow) = ROMFiles.TextColours.Entries(n);
                bool empty = speech[ink] == 0 && speech[shadow] == 0;
                rows.Add(new TextColourRow
                {
                    Code = $"{{COLOR, 0, {n}}}",
                    SpeechInk = Brush(speech[ink]), SpeechShadow = Brush(speech[shadow]), SpeechNote = empty ? "black" : null,
                    MenuInk = Brush(menus[ink]), MenuShadow = Brush(menus[shadow]),
                });
            }
            ColourRows = rows;
            SpeechColoursEmpty = rows[3].SpeechNote != null || rows[4].SpeechNote != null;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ColourRows)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasColourRows)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SpeechColoursEmpty)));
        }

        private void OnPatchesChanged(object sender, EventArgs e) => ReadColours();
        public void Detach() => AppEvents.RomPatchStateChanged -= OnPatchesChanged;

        public string Title => "Special variables";
        public string Subtitle => "For Diamond, Pearl, Platinum, HeartGold, and SoulSilver";
        public IReadOnlyList<StrVarHelpSection> Sections { get; }
        public IReadOnlyList<SpecialCode> LineAndBox { get; }
        public IReadOnlyList<SpecialCode> Look { get; }
        public IReadOnlyList<SpecialCode> Placement { get; }
        public IReadOnlyList<SpecialCode> TimingAndIcons { get; }

        public StrVarHelpViewModel()
        {
            ReadColours();
            if (!Design.IsDesignMode) AppEvents.RomPatchStateChanged += OnPatchesChanged;

            // pokeplatinum and pokeheartgold render_text.c: Diamond, Pearl and Platinum handle the same codes. HGSS adds the
            // alignment codes and two box breaks.
            bool hgss = RomInfo.GameFamily == RomInfo.GameFamilies.HGSS;
            string HgssOnly() => hgss ? null : "HeartGold and SoulSilver only. Other games ignore it.";
            LineAndBox = Array.AsReadOnly(new[]
            {
                new SpecialCode("\\n", "Next line.", "OUR FRIENDS LOOK\\n"),
                new SpecialCode("\\r", "Waits for A, then clears the box and starts again at the top.", "{SIZE, 0, 200}“Gyaaah!!”{SIZE, 0, 100}\\r"),
                new SpecialCode("\\f", "Waits for A, then scrolls the box up one line.", "TURTWIG,{CURSOR_X, 0, 70}GROTLE,{CURSOR_X, 0, 140}TORTERRA,\\f"),
                new SpecialCode("{UNK_207, 0}", "Like \\r: waits for A and clears the box. A \\n right after it is skipped.", null, HgssOnly() ?? "Not used in the retail games."),
                new SpecialCode("{UNK_208, 0}", "Like \\f: waits for A and scrolls one line. A \\n right after it is skipped.", null, HgssOnly() ?? "Not used in the retail games."),
            });
            Look = Array.AsReadOnly(new[]
            {
                new SpecialCode("{COLOR, 0, n}", "Text after it uses colour pair n of the box's palette row (colours 2n+1 and 2n+2). 0 is the normal colour.",
                                "{COLOR, 0, 1}LINK WITH POKéMON RANGER{COLOR, 0, 0}.",
                                "The same number can be a different colour in another box. The table below shows this game's."),
                new SpecialCode("{COLOR, 0, 255}", "Switches to the colour stored with 100 to 106.",
                                "a {COLOR, 0, 255}White Fluff{COLOR, 0, 0}",
                                "The game uses it in names that are put into other messages."),
                new SpecialCode("{COLOR, 0, 100}", "100 to 106 store colours 0 to 6 for a later 255.",
                                "{COLOR, 0, 101}{STRVAR_1, 0, 1, 0}{COLOR, 0, 0}!"),
                new SpecialCode("{SIZE, 0, n}", "200 doubles the letters. 100 sets them back to normal.", "{SIZE, 0, 200}“Gyaaah!!”{SIZE, 0, 100}"),
            });
            Placement = Array.AsReadOnly(new[]
            {
                new SpecialCode("{CURSOR_X, 0, n}", "The next letter starts n pixels from the box's left edge.", "TURTWIG,{CURSOR_X, 0, 70}GROTLE,{CURSOR_X, 0, 140}TORTERRA,"),
                new SpecialCode("{CURSOR_Y, 0, n}", "The next letter starts n pixels from the box's top edge.", "{CURSOR_Y, 0, 24}The characters found scratched"),
                new SpecialCode("{ALN_CENTER, 0}", "Centres the rest of the line in the box.", "{ALN_CENTER, 0}UNOWN Report", HgssOnly()),
                new SpecialCode("{ALN_RIGHT, 0}", "Puts the rest of the line against the box's right edge.", "{ALN_RIGHT, 0}Lv. {STRVAR_1, 52, 1, 0}", HgssOnly()),
            });
            TimingAndIcons = Array.AsReadOnly(new[]
            {
                new SpecialCode("{PAUSE, 0, n}", "Waits n frames before the next letter. 60 frames is a second.", "1,{PAUSE, 0, 20} 2,{PAUSE, 0, 20} and...{PAUSE, 0, 20} Poof!"),
                new SpecialCode("{YESNO, 0, n}", "Draws the icon that tells the player where to look: 0 touch the bottom screen, 1 look at the bottom screen, 2 answer on the top screen, 3 look at the top screen.",
                                "Use next Pokémon?{YESNO, 0, 0}"),
                new SpecialCode("{WAIT, 0, n}", "Plays sound or animation n on the screen showing the text. To wait, use PAUSE.",
                                "Booted up a TM.{WAIT, 0, 3}{WAIT, 0, 4}"),
            });

            Sections = Array.AsReadOnly(new[]
            {
                new StrVarHelpSection(
                    "Syntax and tips",
                    "{STRVAR_1, Type, BufferNumber, 0}",
                    "Type refers to the associated script command category.\n" +
                    "BufferNumber is the number specified in the script command's buffer parameter.\n" +
                    "The final 0 can be ignored.\n\n" +
                    "Tips\n" +
                    "• Replace ? with your buffer number (0, 1, 2, etc.). Buffer numbers must match between the script command and message.\n" +
                    "• See the script command database for how each command is used.\n" +
                    "• Check vanilla scripts for real-world examples."),
                new StrVarHelpSection("Examples", examples: BuildExamples()),
                new StrVarHelpSection(
                    "Type reference",
                    body: "Use the type associated with the command that filled the selected buffer.",
                    typeReferences: BuildTypeReferences()),
            });
        }

        private static IReadOnlyList<StrVarHelpExample> BuildExamples() => Array.AsReadOnly(new[]
        {
            new StrVarHelpExample(
                "Example 1: simple string buffer",
                "TextPlayerName 0\nMessage 0",
                "Hello, {STRVAR_1, 3, 0, 0}!"),
            new StrVarHelpExample(
                "Example 2: several buffers in one message",
                "CountSinnohDexSeen 0x8005\nTextNumber 0 0x8005\nTextNumber 1 210\nMessage 0",
                "Ah, so you've seen {STRVAR_1, 50, 0, 0} Pokémon? \\r\n" +
                "Current research indicates that there are \\n\n" +
                "about {STRVAR_1, 50, 1, 0} species of Pokémon living \\f\n" +
                "in the region of Sinnoh!"),
            new StrVarHelpExample(
                "Example 3: different buffer types",
                "TextPlayerName 0\nTextPokeNickname 1\nMessage 0",
                "{STRVAR_1, 3, 0, 0} fed an Oran Berry\\nto {STRVAR_1, 0, 1, 0}!"),
        });

        private static IReadOnlyList<StrVarTypeReference> BuildTypeReferences() => Array.AsReadOnly(new[]
        {
            Ref("0, 1", "{STRVAR_1, 0, ?, 0}", "Pokémon species or nickname",
                "TextPokemon", "TextPokeNickname", "TextPokemonStored", "TextStarterPokemon",
                "TextRivalStarter", "TextCounterpartStarter", "TextPartyPokemonDefault (HGSS)",
                "TextBugContestPokeNickname (HGSS)"),
            Ref("3", "{STRVAR_1, 3, ?, 0}", "Player or character name",
                "TextPlayerName", "TextRivalName", "TextCounterpart"),
            Ref("4", "{STRVAR_1, 4, ?, 0}", "Map name", "TextMapName"),
            Ref("6", "{STRVAR_1, 6, ?, 0}", "Move name",
                "TextMove", "TextMachineMove", "TextPartyPokemonMove", "TextAttackItem (HGSS)"),
            Ref("7", "{STRVAR_1, 7, ?, 0}", "Nature", "TextNature"),
            Ref("8", "{STRVAR_1, 8, ?, 0}", "Item name",
                "TextItem", "TextBerry", "TextItemLowercase", "TextItemPlural", "TextTrap",
                "TextTreasure", "TextAccessory (also type 31)", "TextApricorn (HGSS)",
                "TextBackgroundName (HGSS)"),
            Ref("10", "{STRVAR_1, 10, ?, 0}", "Seal", "TextSeal", "TextSealPlural", "TextSealSingular"),
            Ref("14", "{STRVAR_1, 14, ?, 0}", "Trainer class", "TextPlayerTrainerType", "TextTrainerClass"),
            Ref("15", "{STRVAR_1, 15, ?, 0}", "Type name", "CMD_765 (Platinum)", "TextTypeName (HGSS)"),
            Ref("18", "{STRVAR_1, 18, ?, 0}", "Pocket name", "TextPocket (also type 31)"),
            Ref("24", "{STRVAR_1, 24, ?, 0}", "Pokétch app", "TextPoketch"),
            Ref("25", "{STRVAR_1, 25, ?, 0}", "Goods or decoration", "TextGoods"),
            Ref("28", "{STRVAR_1, 28, ?, 0}", "Stone name", "CMD_581", "TextStoneName"),
            Ref("31", "{STRVAR_1, 31, ?, 0}", "Miscellaneous",
                "TextPocket (also type 18)", "TextAccessory (also type 8)"),
            Ref("39", "{STRVAR_1, 39, ?, 0}", "Ribbon", "TextRibbon"),
            Ref("50-55", "{STRVAR_1, 50, ?, 0}", "Number",
                "TextNumber", "TextPartyPokemonSize", "TextPokemonSizeRecord", "TextNumberSp",
                "TextBugContestRemainingTime (HGSS)", "TextBattleHallStreak (HGSS)"),
            Ref("1 (STRVAR_4)", "{STRVAR_4, 1, ?, 0}", "Pokéathlon course name",
                "TextPokeathlonCourseName (HGSS)"),
        });

        private static StrVarTypeReference Ref(
            string typeLabel,
            string pattern,
            string description,
            params string[] commands) => new(typeLabel, pattern, description, commands);
    }
}
