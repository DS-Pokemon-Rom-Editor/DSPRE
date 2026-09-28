namespace DSPRE.Avalonia
{
    public static partial class EditorTours
    {
        static partial void RegisterTrainers()
        {
            Add("TrainerEditorView", "Trainer Editor",
                S("tab:Trainers>name:TrainerList", "Trainers", "Every trainer in the game. Pick one to edit it."),
                S("tab:Trainers>name:PartyBox", "The party", "Each Pokémon's species, level, moves and held item. Right-click a species, move or item to open it in its own editor, or Ctrl+click it."),
                S("tab:Trainers>name:TrainerToolbar", "Tools", "Search, reorder the party, edit battle messages and the trainer's class, or move trainers in and out as files. Ctrl+Z undoes, Ctrl+S saves."),
                S("tab:Classes", "Classes", "Each trainer class: its name, gender, prize money and the music that plays when its trainers spot you."));

            // Serves both the class sprite editor and the player back sprite editor.
            Add("TrainerSpriteEditorView", "Trainer Sprite Editor",
                S("name:SpritePicker", "Pick a sprite", "Choose a trainer class, or a player back sprite in the back sprite editor. Everything below shows that sprite."),
                S("tab:Sprite>name:CanvasImage", "Paint", "Click or drag on the sprite to paint with the selected colour."),
                S("tab:Sprite>name:PaletteColumn", "Tools and colours", "Switch between Pencil and Eyedropper and pick a colour. Double-click a colour, or press Edit colour, to change it."),
                S("tab:Sprite>name:FrameStrip", "Frames", "Click a frame to paint on it. Frames that share parts change together."),
                S("name:SpriteButtons", "Pictures in and out", "Export saves the sprite as a picture, and Import brings one back if its colours match. Open in Graphics shows it in the Graphics window."),
                S("tab:Animations", "Animations", "Pick a sequence to see each frame's pose and how long it shows. Play once previews it."),
                S("tab:Editor", "Changing an animation", "Where editing is allowed, add or remove sequences and frames, change poses and delays, and move frames with the arrows."),
                S("tab:JSON", "As text", "The same animation written out as text, for editing by hand. Save Animation JSON keeps it."),
                S("name:SaveButton", "Saving", "Save writes the sprite and its colours. Ctrl+S saves too, and closing with unsaved changes asks first."));

            Add("VsSeekerRematchView", "Vs. Seeker Rematch Editor",
                S("list", "Encounters", "Each row is a trainer you meet on your journey. Pick one to set who you face when you rematch them with the Vs. Seeker."),
                S("toolbar", "Filter", "Type part of a trainer name to narrow the list."),
                S("name:EncounterBox", "First battle", "The trainer you fight the first time. Right-click it and pick Open, or Ctrl+click, to open them in the Trainer Editor."),
                S("name:RematchPanel", "Rematches", "Rematch A to E are the trainers you face on each later rematch, in order. Skip this level leaves one out, and end of chain stops there."),
                S("name:SaveButton", "Saving", "Save writes every changed row, and Discard drops them. Ctrl+S saves too."));

            Add("PokegearRematchView", "Pokégear Rematch Editor",
                S("list", "Rematch rows", "Each row is a trainer you can rematch after they call you. Pick one to choose who you face at each level."),
                S("name:NotesPanel", "Notes", "Which rows the phone book actually reaches, so you know which ones matter."),
                S("name:BaseTrainerBox", "Base trainer", "The trainer you first battle. Right-click and pick Open, or Ctrl+click, to open them; Open in Phone Book jumps to the contact who calls."),
                S("name:RowPanel", "Rematch levels", "Rematches 1 to 5 are the trainers faced as the story moves on, and each label says what unlocks it. Warnings point out rows the game handles badly."),
                S("toolbar", "Filter", "Type part of a trainer name to narrow the list."),
                S("name:SaveAllButton", "Saving", "Save Row writes the row you are on, and Save All writes every changed row. Ctrl+S saves them all."));

            Add("PokegearPhoneBookView", "Pokégear Phone Book",
                S("list", "Contacts", "Everyone who can be in your Pokégear. Pick one to edit how and when they call, or filter the list above."),
                S("name:ContactGrid", "Who they are", "Their call type, the title under their name, the trainer they are and the map they live on. Point at an i for details."),
                S("name:RematchDayBox", "Calls and gifts", "Set the day and time a rematch can be arranged, plus the gift, greeting and how often they ring at random, in the rows around it."),
                S("name:SortPanel", "Sort order", "The phone lists contacts by title, name or location. Drag a contact, use Alt+Up and Alt+Down, or press Auto Sort."),
                S("name:SaveButton", "Saving", "Save writes the phone book. Ctrl+S saves too. A contact's name is edited in the Text Editor."));

            Add("VsIntroEditorView", "VS Intro Editor",
                S("tab:Mugshots>name:MugshotList", "Mugshots", "Every intro with its own art. Pick one to edit it."),
                S("tab:Mugshots>name:MugshotDetails", "Editing a mugshot", "Pick the name on the banner, the face and the banner art. Paint and Animation open the art in the graphics editors."),
                S("tab:Mugshots>name:MugshotPreview", "Preview", "The intro put together from its art. Animate plays a close copy of it."),
                S("tab:Trainer intros and music>name:IntroList", "Intros and music", "Every intro trainer battles use. Pick one to change its music or see its classes."),
                S("tab:Trainer intros and music>name:ClassList", "Classes", "Every trainer class and the intro it gets. Pick one to change it."),
                S("toolbar", "Saving", "Save or Ctrl+S writes your changes, Ctrl+Z undoes. Save the ROM to keep them."));

            Add("TrainerFlagBulkEditorView", "Trainer Flag Bulk Editor",
                S("list", "Trainers", "Every trainer, grouped by class. Tick trainers, or a whole class, to choose who you change."),
                S("toolbar", "Two ways to work", "By Trainer sets the flags of every ticked trainer at once. By Flag picks one flag, and ticking a trainer turns it on for them."),
                S("name:FlagChecklist", "Flags", "The AI flags and the double battle flag. Tick one to set it for every ticked trainer; a mixed box means only some have it."),
                S("name:FilterBox", "Filter and select", "Narrow the list by trainer name. Select All and Select None, or Enable All and Disable All in By Flag mode, act on the trainers listed."),
                S("name:SaveButton", "Saving", "Save writes every changed trainer, and Discard drops the changes. Ctrl+S saves too."));

            Add("BattleTowerEditorView", "Battle Tower Editor",
                S("tab:Trainers>list", "Tower trainers", "The trainers you can meet in the Battle Tower. Pick one, or add one with + New Trainer."),
                S("tab:Trainers>name:TowerTrainerPanel", "Trainer details", "Their class, name and the three things they say."),
                S("tab:Trainers>name:SetIdList", "Their Pokémon", "The sets this trainer draws a team from. Add one by number, remove one, or double-click one to open it."),
                S("tab:Pokémon Sets>list", "Pokémon sets", "The shared pool of Pokémon the tower trainers use. Pick one, or add one with + New Set."),
                S("tab:Pokémon Sets>name:SetPanel", "Set details", "Species, moves, nature, held item, form and which stats are maxed. Right-click a species, move or item to open it, or Ctrl+click."),
                S("toolbar", "Files", "Export and Import move the open tab's data to and from a file, and Locate shows where it is kept."),
                S("name:SaveButton", "Saving", "Save writes both tabs, and Discard drops your changes. Ctrl+S saves too."));
        }
    }
}
