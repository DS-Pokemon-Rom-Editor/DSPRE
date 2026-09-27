namespace DSPRE.Avalonia
{
    public static partial class EditorTours
    {
        static partial void RegisterTools()
        {
            Add("ProjectChecksView", "Validation & Where-Used",
                S(null, "Project checks", "Look for broken links in your project, and find what uses a given file."),
                S("tab:Validation", "Validation", "Run validation checks every header for links to files that do not exist."),
                S("tab:Validation>list", "Findings", "Each row gives the kind of file, the header it was found in and what is wrong."),
                S("tab:Where used", "Where used", "Pick a kind of file and its number, then Find headers lists every header that uses it."));

            Add("PatchToolboxView", "ROM Patch Toolbox",
                S(null, "Patches", "Ready-made changes to the game's code and data, applied with one button."),
                S("name:CautionBanner", "Back up first", "Some patches cannot be undone, so keep a copy of your project before applying one."),
                S("name:PatchList", "Each patch", "What it does, and whether it is applied, can be applied or does not fit this game."),
                S("name:PatchList", "Applying", "The button on the right applies the patch after asking you to confirm. It takes effect straight away, with nothing to save."));

            Add("OverlayEditorView", "Overlay Editor",
                S(null, "Code overlays", "The game loads extra pieces of code as it needs them. This lists each one and whether it is packed down."),
                S("list", "The list", "Compressed is how it is now, Marked Compressed is how the game expects it. A highlighted cell means the two disagree."),
                S("toolbar", "Changing them", "Decompress All and Toggle Marked flip every row at once. In some projects the list is read only, since packing is handled when the ROM is built."),
                S("toolbar", "Saving", "Nothing is written until you Save (Ctrl+S). Discard puts everything back."));

            Add("TableEditorView", "Music & Battle Tables",
                S(null, "Music and battle tables", "Small tables that pick music and battle intros. Only the tables this game has are shown."),
                S("tab:Conditional Music", "Conditional Music", "Places whose music changes while a flag is set. Pick a row, then set the place, the flag and the music to play."),
                S("tab:Effect Combos", "Effect Combos", "Each entry pairs a battle intro animation with a battle theme. Trainer classes and wild Pokémon pick one by number."),
                S("tab:VS Trainer", "VS Trainer", "Gives a trainer class an effect combo, so all its trainers get the same intro and theme."),
                S("tab:VS Pokémon", "VS Pokémon", "Which wild Pokémon have their own intro and theme. This one can only be looked at."),
                S("toolbar", "Saving", "Nothing is written until you Save (Ctrl+S). Discard puts everything back."));

            Add("AddressHelperView", "Address Helper",
                S(null, "Address Helper", "Turns a memory address from a debugger into the file and place it comes from."),
                S("name:AddressBox", "The address", "Type the address in hex and press Search."),
                S("list", "Results", "Every piece of game code loaded at that address, and how far into it the address falls."),
                S(null, "No match", "If no code covers the address, a note under the box says so."));

            Add("ResearchHelperView", "Research Helper",
                S(null, "Research Helper", "Answers questions about your project, like what uses a flag or which warps lead somewhere. It reads everything when it opens, so give it a moment."),
                S("tab:Scripts", "Scripts", "Every script file with how many scripts, functions and movements it has. Click a heading to sort."),
                S("tab:Level Scripts", "Level Scripts", "Every level script file and how many triggers of each kind it has."),
                S("tab:Variable Watcher", "Variable Watcher", "Type a variable number and Search to list every file that uses it. Double-click a row to copy its number."),
                S("tab:Flag Watcher", "Flag Watcher", "Every script and person that sets, checks or hides behind a flag. Double-click a row to copy it."),
                S("tab:Overworld Watcher", "Overworld Watcher", "Everyone placed on a map with a given sprite. Double-click a row to open it in the Event Editor."),
                S("tab:Trainer Watcher", "Trainer Watcher", "Every script, person and rematch that names a trainer. Double-click a row to open it."),
                S("tab:File Watcher", "File Watcher", "Which headers use a script file. Double-click a row to copy it."),
                S("tab:ID Watcher", "ID Watcher", "Pick a script file and one of its scripts to list the events that run it."),
                S("tab:Header Watcher", "Header Watcher", "A header's settings, the warps that lead into it and the warps that leave it. Double-click a warp to go to that header."));

            Add("LabelEditorView", "Dropdown Labels",
                S(null, "Dropdown labels", "Rename the choices shown in dropdowns across the editors, for example to match your hack's changes."),
                S("toolbar", "Where it applies", "This project keeps the names with the current ROM. Global applies them to every ROM you open."),
                S("name:GroupStrip", "Editors", "Each tab is one editor's set of dropdowns."),
                S("list", "Categories", "Pick which dropdown to rename."),
                S("name:EntryRows", "Entries", "Type a new name next to each number. The default name is shown beside it for reference."),
                S("toolbar", "Saving", "Add entry adds one more choice, Reset category brings back the defaults, and nothing is written until you press Save."));

            Add("AudioEditorView", "Audio Editor",
                S(null, "Audio", "Listen to every sound in the game, save it out, and put your own cries and sounds in."),
                S("name:SearchBox", "Search", "Type a name or number to filter every tab at once."),
                S("tab:Cries", "Cries", "Each Pokémon's cry. You can import your own sound as a cry."),
                S("tab:Music", "Music", "Every song. Double-click one to play it."),
                S("tab:Fanfares", "Fanfares", "The short jingles for things like healing or getting an item."),
                S("tab:Sound effects", "Sound effects", "Every sound effect the game plays."),
                S("tab:Sounds", "Sounds", "The sounds the music and effects are made of. Each can be heard, saved and replaced."),
                S("name:PlayBar", "Playing", "Play, Stop and Loop. Click the notes or the wave above to play from that point."),
                S("name:AudioActions", "Export and import", "Export saves the selection as a sound file, a song or its instruments. Import puts your own sound in for a cry or a sound."),
                S("name:SaveButtons", "Saving", "Imports stay here until you Save (Ctrl+S). Discard forgets them."));

            Add("HgEnginePatchesView", "hg-engine Patches",
                S(null, "Code patches", "Every patch your hg-engine checkout applies to the game's code on each build."),
                S("toolbar", "Filter", "Show picks which part of the code to list. Reload reads the lists again."),
                S("list", "The patches", "Where each one lands, what it does and which list it comes from. Runs into shows patches that overlap."),
                S("name:AddPatchPanel", "Adding a patch", "Pick a list, where it goes and what it does, then press Add."),
                S("toolbar", "Saving", "Nothing is written until you Save (Ctrl+S). Discard puts everything back."));

            Add("HgeRomReviewView", "hg-engine ROM Review",
                S(null, "ROM review", "See what the game will actually show from this build, without the source."),
                S("tab:Pokémon graphics>list", "Pick a Pokémon", "Search or scroll the list to pick a Pokémon."),
                S("tab:Pokémon graphics", "Pokémon graphics", "Its party icon, follower and battle sprites as the game reads them. Anything wrong is noted in orange."),
                S("tab:Archive check", "Archive check", "Checks the graphics archive for leftover pieces from earlier builds. Repair member order drops them, and Save (Ctrl+S) writes the fix."));
        }
    }
}
