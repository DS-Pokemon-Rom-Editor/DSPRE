namespace DSPRE.Avalonia
{
    public static partial class EditorTours
    {
        static partial void RegisterTools()
        {
            Add("ProjectChecksView", "Validation and where used",
                S(null, "Project checks", "Broken links in the project, and what uses a file."),
                S("tab:Validation", "Validation", "Checks every header for links to missing files."),
                S("tab:Validation>list", "Findings", "The file kind, the header and what is wrong."),
                S("tab:Where used", "Where used", "Every header that uses a file."));

            Add("PatchToolboxView", "ROM patch toolbox",
                S(null, "Patches", "Ready-made changes to the game's code and data."),
                S("name:CautionBanner", "Back up first", "Some patches can't be undone."),
                S("name:PatchList", "Patches", "What each one does, and whether it is applied or fits this game."),
                S("name:PatchList", "Applying", "Applies after you confirm, with nothing to save."));

            Add("OverlayEditorView", "Overlay editor",
                S(null, "Code overlays", "Code the game loads as it needs it, and whether it is compressed."),
                S("list", "Overlays", "A highlighted cell means the file and the game disagree."),
                S("toolbar", "Changing them", "Decompress all or toggle every row. Some projects show it read only."),
                S(null, "Shortcuts", "Ctrl+S saves, Ctrl+Z undoes, Ctrl+Y redoes."));

            Add("TableEditorView", "Music & battle tables",
                S(null, "Music and battle tables", "Tables that pick music and battle intros."),
                S("tab:Conditional music", "Conditional music", "Places whose music changes while a flag is set."),
                S("tab:Effect combos", "Effect combos", "Battle intro animations paired with battle themes."),
                S("tab:VS trainer", "VS trainer", "The effect combo each trainer class uses."),
                S("tab:VS Pokémon", "VS Pokémon", "Wild Pokémon with their own intro and theme. Read only."),
                S(null, "Shortcuts", "Ctrl+S saves, Ctrl+Z undoes, Ctrl+Y redoes."));

            Add("AddressHelperView", "Address helper",
                S(null, "Address helper", "Finds which file a debugger address is in."),
                S("name:AddressBox", "Address", "Type it in hex and press Search."),
                S("list", "Results", "The code loaded at that address, and the offset into it."),
                S(null, "No match", "A note says when no code covers the address."));

            Add("ResearchHelperView", "Research helper",
                S(null, "Research helper", "What uses a flag, variable, trainer or script, across the project."),
                S("tab:Scripts", "Scripts", "Each script file's scripts, functions and movements."),
                S("tab:Level scripts", "Level scripts", "Each level script file's triggers."),
                S("tab:Variable watcher", "Variable watcher", "Every file that uses a variable."),
                S("tab:Flag watcher", "Flag watcher", "Every script and person that uses a flag."),
                S("tab:Overworld watcher", "Overworld watcher", "Everyone on a map with a given sprite."),
                S("tab:Trainer watcher", "Trainer watcher", "Every script, person and rematch that names a trainer."),
                S("tab:File watcher", "File watcher", "Which headers use a script file."),
                S("tab:ID watcher", "ID watcher", "The events that run a script."),
                S("tab:Header watcher", "Header watcher", "A header's settings and the warps in and out."));

            Add("LabelEditorView", "Dropdown labels",
                S(null, "Dropdown labels", "Rename the choices in dropdowns across the editors."),
                S("toolbar", "Where it applies", "This project only, or every ROM you open."),
                S("name:GroupStrip", "Editors", "One tab per editor."),
                S("list", "Categories", "The dropdown to rename."),
                S("name:EntryRows", "Entries", "A new name beside each number and its default."),
                S("toolbar", "Choices", "Add a choice, or reset to the defaults."),
                S(null, "Shortcuts", "Ctrl+S saves, Ctrl+Z undoes, Ctrl+Y redoes."));

            Add("AudioEditorView", "Audio editor",
                S(null, "Audio", "Every sound in the game, to play, export or replace."),
                S("name:SearchBox", "Search", "Filters every tab by name or number."),
                S("tab:Cries", "Cries", "Each Pokémon's cry. Import your own."),
                S("tab:Music", "Music", "Every song. Double-click to play."),
                S("tab:Fanfares", "Fanfares", "The short jingles."),
                S("tab:Sound effects", "Sound effects", "Every sound effect."),
                S("tab:Sounds", "Sounds", "The sounds music and effects are made of."),
                S("name:PlayBar", "Playing", "Space plays or stops, Enter restarts, Up and Down change row."),
                S("name:AudioActions", "Export and import", "Export a sound, song or instruments, or import a cry or effect."),
                S(null, "Shortcuts", "Ctrl+S saves, Ctrl+Z undoes, Ctrl+Y redoes."));

            Add("HgEnginePatchesView", "hg-engine patches",
                S(null, "Code patches", "The patches hg-engine applies on each build."),
                S("toolbar", "Filter", "Which part of the code to list."),
                S("list", "Patches", "Where each lands, what it does and what it overlaps."),
                S("toolbar", "Built only", "Untick to see lines your #ifdefs leave out."),
                S("name:AddPatchPanel", "Adding a patch", "Pick a list, the place and the change, then Add."),
                S(null, "Shortcuts", "Ctrl+S saves, Ctrl+Z undoes, Ctrl+Y redoes."));

            Add("BattleTestsView", "Battle tests",
                S(null, "Battle tests", "hg-engine's battle test scenarios."),
                S("list", "Tests", "Pick a test to open it."),
                S("tab:Code", "Code", "The test as written. Undefined names are underlined."),
                S("tab:Form", "Form", "The same test as fields."),
                S("toolbar", "New tests", "Name it, pick a test to copy, then Add test."),
                S(null, "Shortcuts", "Ctrl+S saves, Ctrl+Z undoes, Ctrl+Y redoes."));

            Add("HgeRomReviewView", "hg-engine ROM review",
                S(null, "ROM review", "What the game shows from this build."),
                S("tab:Pokémon graphics>list", "Pokémon", "Pick a Pokémon."),
                S("tab:Pokémon graphics", "Pokémon graphics", "Icon, follower and battle sprites. Problems show in orange."),
                S("tab:Archive check", "Archive check", "Finds leftover pieces from earlier builds, and repairs them."),
                S(null, "Shortcuts", "Ctrl+S saves, Ctrl+Z undoes, Ctrl+Y redoes."));

            Add("ChangesPanel", "Changes panel",
                S("name:ChangesList", "Your changes", "Everything you've edited. Double-click a row to open it."),
                S("name:GitButtons", "Commit, pull and push", "Commit saves a version. Push uploads your versions. Pull downloads new ones."),
                S("name:FolderButtons", "The changes folder", "Where your edits are kept. Help me set it up walks you through git."),
                S("name:ChangelogButton", "Changelog", "Changelog writes a list of your changes for players."));
        }
    }
}
