using System;
using System.Collections.Generic;
using System.Linq;

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

    public sealed class StrVarHelpViewModel
    {
        public string Title => "String Buffer (STRVAR) Reference";
        public string Subtitle => "For Diamond, Pearl, Platinum, HeartGold, and SoulSilver";
        public IReadOnlyList<StrVarHelpSection> Sections { get; }

        public StrVarHelpViewModel()
        {
            Sections = Array.AsReadOnly(new[]
            {
                new StrVarHelpSection(
                    "Syntax and Tips",
                    "{STRVAR_1, Type, BufferNumber, 0}",
                    "Type refers to the associated script command category.\n" +
                    "BufferNumber is the number specified in the script command's buffer parameter.\n" +
                    "The final 0 can be ignored.\n\n" +
                    "Tips\n" +
                    "• Replace ? with your buffer number (0, 1, 2, etc.). Buffer numbers must match between the script command and message.\n" +
                    "• Refer to the SCRCMD Database for detailed command usage.\n" +
                    "• Check vanilla scripts for real-world examples."),
                new StrVarHelpSection("Examples", examples: BuildExamples()),
                new StrVarHelpSection(
                    "Type Reference",
                    body: "Use the type associated with the command that filled the selected buffer.",
                    typeReferences: BuildTypeReferences()),
            });
        }

        private static IReadOnlyList<StrVarHelpExample> BuildExamples() => Array.AsReadOnly(new[]
        {
            new StrVarHelpExample(
                "Example 1: Simple String Buffer",
                "TextPlayerName 0\nMessage 0",
                "Hello, {STRVAR_1, 3, 0, 0}!"),
            new StrVarHelpExample(
                "Example 2: Multiple Buffers in One Message",
                "CountSinnohDexSeen 0x8005\nTextNumber 0 0x8005\nTextNumber 1 210\nMessage 0",
                "Ah, so you've seen {STRVAR_1, 50, 0, 0} Pokémon? \\r\n" +
                "Current research indicates that there are \\n\n" +
                "about {STRVAR_1, 50, 1, 0} species of Pokémon living \\f\n" +
                "in the region of Sinnoh!"),
            new StrVarHelpExample(
                "Example 3: Different Buffer Types",
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
