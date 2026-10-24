namespace DSPRE.Avalonia
{
    public static partial class EditorTours
    {
        static partial void RegisterTrainers()
        {
            Add("TrainerEditorView", "Trainer editor",
                S("tab:Trainers>name:TrainerList", "Trainers", "Every trainer in the game."),
                S("tab:Trainers>name:PartyBox", "Party", "Species, level, moves and item. The DV sets IVs and nature."),
                S("tab:Trainers>name:PartyBox", "Linked editors", "Ctrl+click a species, move or item to edit it in its editor."),
                S("tab:Trainers>name:TrainerToolbar", "Tools", "Search, or export and import a trainer, its properties or party."),
                S("tab:Classes", "Classes", "Each class's name, gender, prize money and spotting music."),
                S(null, "Shortcuts", "Ctrl+S saves, Ctrl+Z undoes, Ctrl+Y redoes."));

            // Serves both the class sprite editor and the player back sprite editor.
            Add("TrainerSpriteEditorView", "Trainer sprite editor",
                S("name:SpritePicker", "Sprite", "A trainer class, or a player back sprite."),
                S("tab:Sprite>name:CanvasImage", "Paint", "Click or drag to paint with the picked colour."),
                S("tab:Sprite>name:PaletteColumn", "Tools and colours", "Pencil, eyedropper and the palette. Double-click a colour to change it."),
                S("tab:Sprite>name:FrameStrip", "Frames", "Click a frame to paint it. Frames that share parts change together."),
                S("name:SpriteButtons", "Pictures in and out", "Export or import the sprite, or every frame as a sheet."),
                S("tab:Animations", "Animations", "Each sequence's frames and timing. Play once previews it."),
                S("tab:Editor", "Changing an animation", "Add or remove sequences and frames, and change poses and delays."),
                S("tab:JSON", "As text", "The animation as text, to edit by hand."),
                S(null, "Shortcuts", "Ctrl+S saves, Ctrl+Z undoes, Ctrl+Y redoes."));

            Add("VsSeekerRematchView", "Vs. Seeker rematch editor",
                S("list", "Encounters", "Each trainer you can rematch with the Vs. Seeker."),
                S("toolbar", "Search", "Type part of a name to narrow the list."),
                S("name:EncounterBox", "First battle", "The trainer you fight first. Ctrl+click edits them in the Trainer editor."),
                S("name:RematchPanel", "Rematches", "Who you face on each later rematch, A to E."),
                S(null, "Shortcuts", "Ctrl+S saves, Ctrl+Z undoes, Ctrl+Y redoes."));

            Add("PokegearRematchView", "Pokégear rematch editor",
                S("list", "Rematch rows", "Each trainer you can rematch after they call."),
                S("name:BaseTrainerBox", "Base trainer", "The trainer you first battle. Ctrl+click edits them in the Trainer editor."),
                S("name:RowPanel", "Rematch levels", "Who you face at each level, and what unlocks it."),
                S("toolbar", "Search", "Type part of a name to narrow the list."),
                S(null, "Shortcuts", "Ctrl+S saves, Ctrl+Z undoes, Ctrl+Y redoes."));

            Add("PokegearPhoneBookView", "Pokégear phone book",
                S("list", "Contacts", "Everyone who can be in your Pokégear."),
                S("name:ContactGrid", "Contact", "Call type, title, trainer and home map."),
                S("name:RematchDayBox", "Calls and gifts", "Rematch day and time, gift, greeting and call rate."),
                S("name:SortPanel", "Sort order", "Drag contacts, use Alt+Up and Alt+Down, or Auto sort."),
                S(null, "Names", "Contact names are in the Text editor."),
                S(null, "Shortcuts", "Ctrl+S saves, Ctrl+Z undoes, Ctrl+Y redoes."));

            Add("VsIntroEditorView", "VS intro editor",
                S("tab:Mugshots>name:MugshotList", "Mugshots", "Every intro with its own art."),
                S("tab:Mugshots>name:MugshotDetails", "Mugshot", "The banner name, face and banner art."),
                S("tab:Mugshots>name:MugshotPreview", "Preview", "The intro built from its art. Animate plays it."),
                S("tab:Intros and music>name:IntroList", "Intros and music", "Every intro trainer battles use, and its music."),
                S("tab:Classes>name:ClassList", "Classes", "Every class under its current intro."),
                S("tab:Classes>name:ClassDetails", "Class intro", "The intro and its music, shared by every class on it."),
                S("tab:Classes>name:ClassList", "Several at once", "Tick classes to give them one intro."),
                S("tab:Trainer class intros>name:CiClassList", "Class intros", "With the class intros patch, each class has its own."),
                S("tab:Trainer class intros>name:CiClassDetails", "Class intro", "The style and battle music."),
                S("tab:Trainer class intros>name:CiClassPreview", "Preview", "How the class's intro looks."),
                S(null, "Shortcuts", "Ctrl+S saves, Ctrl+Z undoes, Ctrl+Y redoes."));

            Add("TrainerFlagBulkEditorView", "Trainer flag bulk editor",
                S("list", "Trainers", "Every trainer, by class. Tick the ones to change."),
                S("toolbar", "Two ways to work", "By trainer sets every ticked trainer. By flag sets one flag."),
                S("name:FlagChecklist", "Flags", "AI and double battle flags. A filled box means only some."),
                S("name:FilterBox", "Filter and select", "Narrow the list by name, then select or set them all."),
                S(null, "Shortcuts", "Ctrl+S saves, Ctrl+Z undoes, Ctrl+Y redoes."));

            Add("BattleTowerEditorView", "Battle Tower editor",
                S("tab:Trainers>list", "Tower trainers", "The Battle Tower's trainers."),
                S("tab:Trainers>name:TowerTrainerPanel", "Trainer", "Their class, name and three lines."),
                S("tab:Trainers>name:SetIdList", "Their Pokémon", "The sets this trainer picks a team from."),
                S("tab:Pokémon sets>list", "Pokémon sets", "The shared pool of tower Pokémon."),
                S("tab:Pokémon sets>name:SetPanel", "Set", "Species, moves, nature, item, form and maxed stats. Ctrl+click edits them."),
                S("toolbar", "Files", "Export or import the open tab, or show where it is kept."),
                S(null, "Shortcuts", "Ctrl+S saves, Ctrl+Z undoes, Ctrl+Y redoes."));
        }
    }
}
