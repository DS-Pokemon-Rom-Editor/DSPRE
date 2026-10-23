namespace DSPRE.Avalonia
{
    public static partial class EditorTours
    {
        static partial void RegisterTools()
        {
            Add("ProjectChecksView", "Validation and where used",
                S(null, "Project checks", "Look for broken links in your project, and find what uses a given file."),
                S("tab:Validation", "Validation", "Run validation to check every header for links to files that don't exist."),
                S("tab:Validation>list", "Findings", "Each row gives the kind of file, the header it was found in and what is wrong."),
                S("tab:Where used", "Where used", "Pick a kind of file and its number, then Find headers lists every header that uses it."));

            Add("PatchToolboxView", "ROM patch toolbox",
                S(null, "Patches", "Ready-made changes to the game's code and data, applied with one button."),
                S("name:CautionBanner", "Back up first", "Some patches can't be undone, so keep a copy of your project before applying one."),
                S("name:PatchList", "Each patch", "What it does, and whether it is applied, can be applied or doesn't fit this game."),
                S("name:PatchList", "Applying", "The button on the right applies the patch after asking you to confirm. It takes effect straight away, with nothing to save."));

            Add("OverlayEditorView", "Overlay editor",
                S(null, "Code overlays", "The game loads extra pieces of code as it needs them. This lists each one and whether it is packed down."),
                S("list", "The list", "Compressed shows how each file is now and Marked compressed shows how the game expects it. A highlighted cell means the two disagree."),
                S("toolbar", "Changing them", "Decompress all and Toggle marked change every row at once. Some projects show the list read only, as the ROM build handles packing."));

            Add("TableEditorView", "Music & battle tables",
                S(null, "Music and battle tables", "Small tables that pick music and battle intros. Only the tables this game has are shown."),
                S("tab:Conditional music", "Conditional music", "Places whose music changes while a flag is set. Pick a row, then set the place, the flag and the music to play."),
                S("tab:Effect combos", "Effect combos", "Each entry pairs a battle intro animation with a battle theme. Trainer classes and wild Pokémon pick one by number."),
                S("tab:VS trainer", "VS trainer", "Gives a trainer class an effect combo, so all its trainers get the same intro and theme."),
                S("tab:VS Pokémon", "VS Pokémon", "Which wild Pokémon have their own intro and theme. This one can only be looked at."));

            Add("AddressHelperView", "Address helper",
                S(null, "Address helper", "Turns a memory address from a debugger into the file and place it comes from."),
                S("name:AddressBox", "The address", "Type the address in hex and press Search."),
                S("list", "Results", "Every piece of game code loaded at that address, and how far into it the address falls."),
                S(null, "No match", "If no code covers the address, a note under the box says so."));

            Add("ResearchHelperView", "Research helper",
                S(null, "Research helper", "Answers questions about your project, like what uses a flag or which warps lead somewhere. It reads everything as it opens, so give it a moment."),
                S("tab:Scripts", "Scripts", "Every script file with how many scripts, functions and movements it has. Click a heading to sort."),
                S("tab:Level scripts", "Level scripts", "Every level script file and how many triggers of each kind it has."),
                S("tab:Variable watcher", "Variable watcher", "Type a variable number and press Search to list every file that uses it. Double-click a row to copy its number."),
                S("tab:Flag watcher", "Flag watcher", "Every script that sets or checks a flag, and every person a flag hides. Double-click a row to copy it."),
                S("tab:Overworld watcher", "Overworld watcher", "Everyone placed on a map with a given sprite. Double-click a row to open it in the Event editor."),
                S("tab:Trainer watcher", "Trainer watcher", "Every script, person and rematch that names a trainer. Double-click a row to open it."),
                S("tab:File watcher", "File watcher", "Which headers use a script file. Double-click a row to copy it."),
                S("tab:ID watcher", "ID watcher", "Pick a script file and one of its scripts to list the events that run it."),
                S("tab:Header watcher", "Header watcher", "A header's settings, the warps that lead into it and the warps that leave it. Double-click a warp to go to that header."));

            Add("LabelEditorView", "Dropdown labels",
                S(null, "Dropdown labels", "Rename the choices shown in dropdowns across the editors, for example to match your hack's changes."),
                S("toolbar", "Where it applies", "This project keeps the names with the current ROM. Global applies them to every ROM you open."),
                S("name:GroupStrip", "Editors", "Each tab is one editor's set of dropdowns."),
                S("list", "Categories", "Pick which dropdown to rename."),
                S("name:EntryRows", "Entries", "Type a new name next to each number. The default name is shown beside it for reference."),
                S("toolbar", "Choices", "Add entry adds a choice, and Reset category brings back the defaults."));

            Add("AudioEditorView", "Audio editor",
                S(null, "Audio", "Listen to every sound in the game, save it out, and put your own cries and sounds in."),
                S("name:SearchBox", "Search", "Type a name or number to filter every tab at once."),
                S("tab:Cries", "Cries", "Each Pokémon's cry. You can import your own sound as a cry."),
                S("tab:Music", "Music", "Every song. Double-click one to play it."),
                S("tab:Fanfares", "Fanfares", "The short jingles for things like healing or getting an item."),
                S("tab:Sound effects", "Sound effects", "Every sound effect the game plays."),
                S("tab:Sounds", "Sounds", "The sounds the music and effects are made of. Each can be heard, saved and replaced."),
                S("name:PlayBar", "Playing", "Click the notes or the wave to play from there. Space plays or stops, Enter plays from the start, and Up and Down pick another row."),
                S("name:AudioActions", "Export and import", "Export saves the selection as a sound file, a song or its instruments. Import puts your own sound in for a cry or sound effect."),
                S("name:SaveButtons", "Saving", "Imports wait until you press Save, and Discard drops them. Ctrl+S saves too."));

            Add("HgEnginePatchesView", "hg-engine patches",
                S(null, "Code patches", "Every patch your hg-engine checkout applies to the game's code on each build."),
                S("toolbar", "Filter", "Show picks which part of the code to list. Reload reads the lists again."),
                S("list", "The patches", "Where each one lands, what it does and which list it comes from. Runs into shows patches that overlap."),
                S("toolbar", "Built only", "Lines your config's #ifdefs leave out are hidden. Untick it to see them greyed, with Built and When saying why."),
                S("name:AddPatchPanel", "Adding a patch", "Pick a list, where it goes and what it does, then press Add."));

            Add("BattleTestsView", "Battle tests",
                S(null, "Battle tests", "hg-engine's battle test scenarios, one file each under data/battle_tests."),
                S("list", "The tests", "Pick a test to open it. Search narrows the list."),
                S("tab:Code", "Code", "The test as written. Underlined names aren't defined in the headers the build uses; the line below says why."),
                S("tab:Form", "Form", "The same test as fields: battle, both parties, turns and what to expect. Changes go straight into the code."),
                S("toolbar", "New tests", "Type a name, pick a test to start from, then press Add test. Save (Ctrl+S) writes to the checkout."));

            Add("HgeRomReviewView", "hg-engine ROM review",
                S(null, "ROM review", "See what the game will actually show from this build, without the source."),
                S("tab:Pokémon graphics>list", "Pick a Pokémon", "Search or scroll the list to pick a Pokémon."),
                S("tab:Pokémon graphics", "Pokémon graphics", "Its party icon, follower and battle sprites as the game reads them. Anything wrong is noted in orange."),
                S("tab:Archive check", "Archive check", "Checks the graphics archive for leftover pieces from earlier builds. Repair member order drops them, and Save (Ctrl+S) writes the fix."));
        }
    }
}
