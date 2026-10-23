namespace DSPRE.Avalonia
{
    public static partial class EditorTours
    {
        static partial void RegisterTrainers()
        {
            Add("TrainerEditorView", "Trainer editor",
                S("tab:Trainers>name:TrainerList", "Trainers", "Every trainer in the game. Pick one to edit it."),
                S("tab:Trainers>name:PartyBox", "The party", "Each Pokémon's species, level, moves and held item. The DV sets its IVs and, with the trainer, its nature; picking a nature sets the DV. Ctrl+click a species, move or item to open it."),
                S("tab:Trainers>name:TrainerToolbar", "Tools", "Search, or move a trainer, its properties or its party in and out as files."),
                S("tab:Classes", "Classes", "Each trainer class: its name, gender, prize money and the music that plays when its trainers spot you."));

            // Serves both the class sprite editor and the player back sprite editor.
            Add("TrainerSpriteEditorView", "Trainer sprite editor",
                S("name:SpritePicker", "Pick a sprite", "Choose a trainer class, or a player back sprite in the back sprite editor. Everything below shows that sprite."),
                S("tab:Sprite>name:CanvasImage", "Paint", "Click or drag on the sprite to paint with the selected colour."),
                S("tab:Sprite>name:PaletteColumn", "Tools and colours", "Switch between Pencil and Eyedropper and pick a colour. Double-click a colour, or press Edit colour, to change it."),
                S("tab:Sprite>name:FrameStrip", "Frames", "Click a frame to paint on it. Frames that share parts change together."),
                S("name:SpriteButtons", "Pictures in and out", "Export saves the sprite as a picture, and Import brings one back if its colours match. Export sheet and Import sheet do every frame, with a JSON file for their order and timing."),
                S("tab:Animations", "Animations", "Pick a sequence to see each frame's pose and how long it shows. Play once previews it."),
                S("tab:Editor", "Changing an animation", "Where editing is allowed, add or remove sequences and frames, change poses and delays, and move frames with the arrows."),
                S("tab:JSON", "As text", "The same animation written out as text, for editing by hand. Save animation JSON saves it."));

            Add("VsSeekerRematchView", "Vs. Seeker rematch editor",
                S("list", "Encounters", "Each row is a trainer from your journey. Pick one to set who you face when you rematch them with the Vs. Seeker."),
                S("toolbar", "Search", "Type part of a trainer name to narrow the list."),
                S("name:EncounterBox", "First battle", "The trainer you fight the first time. Right-click it and pick Open, or Ctrl+click, to open them in the Trainer editor."),
                S("name:RematchPanel", "Rematches", "Rematch A to E are the trainers you face on each later rematch. Skip this level leaves one out, and Ends here stops there."));

            Add("PokegearRematchView", "Pokégear rematch editor",
                S("list", "Rematch rows", "Each row is a trainer you can rematch after they call you. Pick one to choose who you face at each level."),
                S("name:BaseTrainerBox", "Base trainer", "The trainer you first battle. Right-click and pick Open, or Ctrl+click, to open them; Open in Phone Book jumps to the contact who calls."),
                S("name:RowPanel", "Rematch levels", "Rematches 1 to 5 are the trainers you face as the story goes on; each label says what unlocks it. Warnings point out rows the game handles badly."),
                S("toolbar", "Search", "Type part of a trainer name to narrow the list."));

            Add("PokegearPhoneBookView", "Pokégear phone book",
                S("list", "Contacts", "Everyone who can be in your Pokégear. Pick one to edit how and when they call, or filter the list above."),
                S("name:ContactGrid", "Who they are", "Their call type, the title under their name, the trainer they are and the map they live on. Point at an i for details."),
                S("name:RematchDayBox", "Calls and gifts", "The day and time a rematch can be arranged, and in the rows around it the gift, greeting and how often they call."),
                S("name:SortPanel", "Sort order", "The phone lists contacts by title, name or location. Drag a contact, use Alt+Up and Alt+Down, or press Auto sort."),
                S(null, "Names", "A contact's name is edited in the Text editor."));

            Add("VsIntroEditorView", "VS intro editor",
                S("tab:Mugshots>name:MugshotList", "Mugshots", "Every intro with its own art. Pick one to edit it."),
                S("tab:Mugshots>name:MugshotDetails", "Editing a mugshot", "Pick the name on the banner, the face and the banner art. Paint and Animation open the art in the graphics editors."),
                S("tab:Mugshots>name:MugshotPreview", "Preview", "The intro put together from its art. Animate plays a close copy of it."),
                S("tab:Intros and music>name:IntroList", "Intros and music", "Every intro trainer battles use. Pick one to change its music; its classes open on the Classes tab."),
                S("tab:Classes>name:ClassList", "Classes", "Every trainer class under the intro it has now. Search by class or intro name."),
                S("tab:Classes>name:ClassDetails", "A class's intro", "Pick the intro and its music. The music belongs to the intro, so other classes on it hear the change too."),
                S("tab:Classes>name:ClassList", "Several at once", "Tick classes to give them all one intro from the box under the list."),
                S("tab:Trainer class intros>name:CiClassList", "Class intros", "With the class intros patch every trainer class has its own intro. Pick a class to edit it."),
                S("tab:Trainer class intros>name:CiClassDetails", "A class's intro", "The style and the battle music. Gym leader and Elite Four styles also take their own look."),
                S("tab:Trainer class intros>name:CiClassPreview", "Preview", "How the class's intro looks."),
                S("toolbar", "Saving", "Save writes your changes and Ctrl+Z undoes; save the ROM to keep them."));

            Add("TrainerFlagBulkEditorView", "Trainer flag bulk editor",
                S("list", "Trainers", "Every trainer, grouped by class. Tick trainers, or a whole class, to choose who you change."),
                S("toolbar", "Two ways to work", "By trainer sets the flags of every ticked trainer at once. By flag picks one flag, and ticking a trainer turns it on for them."),
                S("name:FlagChecklist", "Flags", "The AI flags and the double battle flag. Tick one to set it for every ticked trainer; a mixed box means only some have it."),
                S("name:FilterBox", "Filter and select", "Narrow the list by trainer name. Select all and Select none, or Enable all and Disable all by flag, work on the trainers listed."));

            Add("BattleTowerEditorView", "Battle Tower editor",
                S("tab:Trainers>list", "Tower trainers", "The trainers you can meet in the Battle Tower. Pick one, or add one with + New Trainer."),
                S("tab:Trainers>name:TowerTrainerPanel", "Trainer details", "Their class, name and the three things they say."),
                S("tab:Trainers>name:SetIdList", "Their Pokémon", "The sets this trainer draws a team from. Add one by number, remove one, or double-click one to open it."),
                S("tab:Pokémon sets>list", "Pokémon sets", "The shared pool of Pokémon the tower trainers use. Pick one, or add one with + New Set."),
                S("tab:Pokémon sets>name:SetPanel", "Set details", "Species, moves, nature, held item, form and which stats are maxed. Right-click a species, move or item to open it, or Ctrl+click."),
                S("toolbar", "Files", "Export and Import move the open tab's data to and from a file, and Locate shows where it is kept."));
        }
    }
}
