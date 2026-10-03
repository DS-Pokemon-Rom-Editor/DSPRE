namespace DSPRE.Avalonia
{
    public static partial class EditorTours
    {
        static partial void RegisterText()
        {
            Add("TextEditorView", "Text Editor",
                S(null, "Game text", "Every line of dialogue, name and menu text lives in a numbered archive. Pick one and edit its lines here."),
                S("toolbar", "Archives", "Pick an archive, add a new one or remove the last one. Import and Export move a whole archive in and out as a file."),
                S("name:LinesGrid", "The lines", "Each row is one line of text. Double-click the text to change it; the number on the left is what scripts use to show it."),
                S("name:LineButtons", "Adding lines", "Add or remove a line at the end, or move the selected line up and down."),
                S("toolbar", "Message box", "Tick Message box to see the selected line in the game's own box, one stop at a time. STRVAR Help lists the codes that insert names and numbers."),
                S("name:ReplacePanel", "Search and replace", "Find text in this archive or in all of them, and replace it. Double-click a result to jump to that line."),
                S("toolbar", "Saving", "Nothing is written until you press Save. Ctrl+Z undoes and Ctrl+Y redoes. Closing with unsaved edits asks first."));

            Add("ScriptEditorView", "Script Editor",
                S(null, "Scripts", "Each script file holds the scripts, functions and movements for a place in the game, written as text."),
                S("toolbar", "Script files", "Pick a file to open, or Add a new empty one. Import and Export move the text in and out, Find in File searches the open file, and Theme changes the colours."),
                S("name:RotomEditor", "Writing code", "Point at a command to see what it does and what it takes. Ctrl+click a name, or press F12, to jump to where it is defined."),
                S("tab:Search", "Search all scripts", "Search every script file at once, exactly or loosely with Fuzzy. Double-click a result to open it at that line."),
                S("tab:Diagnostics", "Problems", "Mistakes found while you type or when you save. Double-click one to go to its line."),
                S("toolbar", "Saving", "Save (Ctrl+S) writes the text and builds it into the game. If it fails to build, the problems show under Diagnostics."));

            Add("LevelScriptEditorView", "Level Script Editor",
                S(null, "Level scripts", "Level scripts run a script by themselves: when you enter a map, load the game, return to the map screen, or when a variable holds a value."),
                S("toolbar", "Files", "Pick the level script file to edit. Import and Export move it in and out as a file."),
                S("list", "Triggers", "Everything this file runs and when. Select one and press Remove selected to take it out."),
                S("name:AddTriggerPanel", "Adding a trigger", "Pick when it fires and which script it runs. For a variable trigger, also give the variable and the value it must hold."),
                S("toolbar", "Saving", "Nothing is written until you press Save. Ctrl+Z undoes and Ctrl+Y redoes."));

            Add("CustomScrcmdManagerView", "Custom Script Command Manager",
                S(null, "Script commands", "Each project can have its own list of script commands, for hacks that add new ones."),
                S("list", "Projects", "Every project that has its own command list. Pick one to work on it."),
                S("name:DatabaseActions", "Refresh", "Refresh reads the list again after you change the files by hand."),
                S("name:DatabaseActions", "Import and export", "Import replaces the picked project's list with yours and can reload every script right away. Export saves a copy, and Open Folder shows where they live."));

            Add("FontEditorView", "Font Editor",
                S(null, "Fonts", "Draw the letters the game writes text with, and see a sentence come out in the result."),
                S("toolbar", "Font and pictures", "Pick a font. Save a picture and Read a picture move one letter, or the whole font, out to an image and back."),
                S("list", "Letters", "Every letter in the font. Search by character or number, or show only drawn, empty or unassigned slots."),
                S("type:GlyphPainter", "Drawing", "Left button paints the shade picked on the right, right button rubs out. Width sets where the next letter starts."),
                S("name:ShadePicker", "Shades", "The four shades a letter is drawn with: nothing, ink, second ink and paper."),
                S("type:FontSampleLine", "Trying it out", "Type a sentence above to see it written at game size and bigger."),
                S("toolbar", "Saving", "Nothing is written until you save this font (Ctrl+S). Ctrl+Z undoes and Ctrl+Y redoes."));

            Add("CharMapManagerView", "Character Map Manager",
                S(null, "Character map", "How each character code turns into the text you type. A custom map lets you change or add names for codes."),
                S("list", "Characters", "Every code and the text it stands for. Double-click one to copy it."),
                S("name:CharSearch", "Search", "Find a character, or type 0x and a number to find a code."),
                S("name:AliasPanel", "Aliases", "Give a code another name you can type. Pick the code, write the alias and press Add Alias."),
                S("name:MapActions", "Your custom map", "Create or delete your custom map, reload it or open its file. Rebase merges it with the latest built-in map."),
                S("name:MapActions", "Saving", "Edits stay here until you press Save. Closing with unsaved edits asks first."));
        }
    }
}
