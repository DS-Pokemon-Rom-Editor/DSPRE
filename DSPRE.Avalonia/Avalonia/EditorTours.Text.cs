namespace DSPRE.Avalonia
{
    public static partial class EditorTours
    {
        static partial void RegisterText()
        {
            Add("TextEditorView", "Text editor",
                S(null, "Game text", "Every line of text, in numbered archives."),
                S("toolbar", "Archives", "Pick, add or remove an archive, or import and export one."),
                S("name:LinesGrid", "Lines", "Double-click a line to edit it. Scripts use its number."),
                S("name:LineButtons", "Adding lines", "Add, remove or move lines."),
                S("toolbar", "Message box", "Shows the line in the game's box. STRVAR help lists the codes."),
                S("name:ReplacePanel", "Search and replace", "Search this archive or all of them. Double-click a result to go there."),
                S(null, "Shortcuts", "Enter searches. Ctrl+S saves, Ctrl+Z undoes, Ctrl+Y redoes."));

            Add("ScriptEditorView", "Script editor",
                S(null, "Scripts", "A place's scripts, functions and movements, as text."),
                S("toolbar", "Script files", "Open, add, import, export or search a script file."),
                S("name:RotomEditor", "Code", "Point at a command for help. F12 or Ctrl+click goes to its definition."),
                S("tab:Search", "Search", "Search every script file. Double-click a result to open it."),
                S("tab:Diagnostics", "Problems", "Errors found while typing or saving."),
                S(null, "Shortcuts", "Ctrl+S saves and builds. Build errors show under Problems."));

            Add("LevelScriptEditorView", "Level script editor",
                S(null, "Level scripts", "Scripts that run when a map loads, the game resumes, or a variable is set."),
                S("toolbar", "Files", "Pick, import or export a level script file."),
                S("list", "Triggers", "What this file runs and when."),
                S("name:AddTriggerPanel", "Adding a trigger", "When it fires and which script it runs."),
                S(null, "Shortcuts", "Ctrl+S saves, Ctrl+Z undoes, Ctrl+Y redoes."));

            Add("CustomScrcmdManagerView", "Custom script command manager",
                S(null, "Script commands", "The project's list of script commands."),
                S("list", "This project", "The open project's command list."),
                S("name:DatabaseActions", "Refresh", "Reads the list again after editing the files by hand."),
                S("name:DatabaseActions", "Import and export", "Import replaces the list after a backup. Export saves a copy."));

            Add("FontEditorView", "Font editor",
                S(null, "Fonts", "The letters the game writes with."),
                S("toolbar", "Font and pictures", "Pick a font, or export and import letters as PNG."),
                S("list", "Letters", "Every letter. Search, or show only drawn, empty or unassigned ones."),
                S("type:GlyphPainter", "Drawing", "Left paints, right rubs out. Width sets the letter's spacing."),
                S("name:ShadePicker", "Shades", "Nothing, ink, second ink and paper."),
                S("type:FontSampleLine", "Sample", "Type a sentence to see it at game size."),
                S(null, "Shortcuts", "Ctrl+S saves, Ctrl+Z undoes, Ctrl+Y redoes."));

            Add("CharMapManagerView", "Character map manager",
                S(null, "Character map", "The text each character code stands for."),
                S("list", "Characters", "Every code. Double-click one to copy it."),
                S("name:CharSearch", "Search", "Find a character, or a code as 0x and a number."),
                S("name:AliasPanel", "Aliases", "Another name to type for a code."),
                S("name:MapActions", "Your custom map", "Create, delete, reload or update your custom map."),
                S(null, "Shortcuts", "Enter searches. Ctrl+S saves."));
        }
    }
}
