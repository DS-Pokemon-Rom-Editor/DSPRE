namespace DSPRE.Avalonia
{
    public static partial class EditorTours
    {
        static partial void RegisterPokemon()
        {
            Add("PokemonEditorView", "Pokémon editor",
                S("name:SpeciesBar", "Pokémon", "Pick by number or name. The buttons play or open its cry."),
                S("tab:Personal data", "Personal data", "Base stats, EV yields, types, abilities and held items."),
                S("tab:Other", "Other", "Catch rate, EXP, friendship, gender, growth, egg groups and hatching."),
                S("tab:TM/HM", "TMs and HMs", "Which TMs and HMs it can learn."),
                S("tab:Pokéathlon", "Pokéathlon", "Its star ranges for each Pokéathlon stat."),
                S("tab:hg-engine", "Extra data", "Hidden ability, baby form, regional number, icon colours and follower."),
                S("tab:Learnset", "Learnset", "The moves it learns by level. Bulk edit all changes many Pokémon."),
                S("tab:Evolutions", "Evolutions", "What it evolves into, and how."),
                S("tab:Sprites", "Sprites", "Battle sprites and colours. Import, export or paint them."),
                S("tab:Battle display", "Battle display", "Its battle position, shadow, party icon and entry animation."),
                S(null, "Linked editors", "Ctrl+click a move or Pokémon to edit it in its editor."),
                S(null, "Shortcuts", "Ctrl+S saves, Ctrl+Z undoes, Ctrl+Y redoes."));

            Add("MoveDataEditorView", "Move data editor",
                S("name:MovePicker", "Move", "Type part of a name to find it."),
                S("name:PropertiesBox", "Type and split", "Type, physical or special, and target."),
                S("name:StatsBox", "Numbers", "Power, accuracy, PP, priority and effect chance."),
                S("name:EffectBox", "Effect", "What the move does besides damage."),
                S("name:DescriptionBox", "Description", "The in-game description."),
                S("name:ContestBox", "Contests", "Contest condition and appeal."),
                S("name:FlagsBox", "Flags", "Contact, Protect, Magic Coat, Snatch and copying."),
                S("name:AbilityBox", "Ability based", "Iron Fist and Soundproof lists. A patch can make them longer."),
                S("toolbar", "Animation and script", "Open the move's animation or its battle script."),
                S("toolbar", "Spreadsheets", "Export or import every move as CSV."),
                S(null, "Shortcuts", "Ctrl+S saves, Ctrl+Z undoes, Ctrl+Y redoes."));

            Add("BattleScriptEditorView", "Move animations & battle scripts",
                S("name:EntryPicker", "Scripts", "Pick the kind of script, then the entry."),
                S("tab:Cards", "Cards", "The commands in order, one card each. Expand a card to edit it."),
                S("tab:Script", "Script", "The commands as text. Errors are underlined."),
                S("tab:Read", "Read", "The animation step by step, read only."),
                S("name:ScriptButtons", "Commands", "Add a command, or look one up in the guide."),
                S("name:PreviewPanel", "Preview", "Preview the move you are editing."),
                S("name:EditParticlesButton", "Particles", "The move's particle files."),
                S("name:SceneOptions", "Scene", "Edit the preview's parameters: user, weather, backdrop and message box."),
                S("name:PlaybackBar", "Playback", "Space plays, Left and Right step a frame, Home and End jump."),
                S("name:Timeline", "Timeline", "Every command over time. Drag a bar's end to change its length."),
                S(null, "Shortcuts", "Ctrl+S saves, Ctrl+Z undoes, Ctrl+Y redoes."));

            Add("TMEditorView", "TM / HM editor",
                S("list", "TMs and HMs", "Every TM and HM and the move it teaches."),
                S("name:MachineDetail", "Move", "The move it teaches. Ctrl+click edits it in the Move data editor."),
                S("name:MachineDetail", "Disc colour", "Its colour in the bag. Auto Palette matches the move's type."),
                S("toolbar", "Spreadsheets", "Export or import the list as CSV."),
                S("toolbar", "Who learns it", "Set in the TM/HM bulk editor."),
                S(null, "Shortcuts", "Ctrl+S saves, Ctrl+Z undoes, Ctrl+Y redoes."));

            Add("TmHmBulkEditorView", "TM/HM bulk editor",
                S("name:ByPokemonButton", "Two ways to work", "By Pokémon, or by one TM or HM and who learns it."),
                S("name:SpeciesTree", "Pokémon", "By evolution family. Tick a family to tick its members."),
                S("name:MachinePanel", "TMs and HMs", "Set a TM or HM for every ticked Pokémon. A filled box means only some."),
                S("name:SyncUnionButton", "Families", "Share TMs and HMs across a family, or copy one Pokémon's to others."),
                S("name:FilterBox", "Search", "Type part of a name to narrow the list."),
                S(null, "Shortcuts", "Ctrl+S saves, Ctrl+Z undoes, Ctrl+Y redoes."));

            Add("MoveTutorEditorView", "Move tutors",
                S("tab:Moves", "Tutor moves", "Every tutor move, its cost and where it's taught."),
                S("tab:Moves>list", "Editing a move", "Ctrl+click a move to edit it in the Move data editor."),
                S("tab:Moves>list", "Headbutt", "HeartGold's Ilex Forest tutor always teaches Headbutt."),
                S("tab:By Pokémon", "By Pokémon", "Tick the tutor moves a Pokémon can learn."),
                S("tab:By move", "By move", "Tick every Pokémon that can learn a move."),
                S("toolbar", "Before saving", "A red note says what to fix first."),
                S(null, "Shortcuts", "Ctrl+S saves, Ctrl+Z undoes, Ctrl+Y redoes."));

            Add("EggMoveEditorView", "Egg move editor",
                S("name:MonPanel", "Pokémon", "Every Pokémon with egg moves."),
                S("name:MonPanel", "Adding Pokémon", "Pick one under the list, then add, replace or delete."),
                S("name:MovePanel", "Egg moves", "The moves this Pokémon passes on."),
                S("name:BulkPanel", "Bulk changes", "Swap or remove a move in every Pokémon."),
                S("toolbar", "Space left", "How full the table is. Over the limit isn't saved."),
                S("toolbar", "Spreadsheets", "Export or import the table as CSV."),
                S(null, "Shortcuts", "Enter searches. Ctrl+S saves, Ctrl+Z undoes, Ctrl+Y redoes."));

            Add("TradeEditorView", "Trade editor",
                S("name:TradePicker", "Trade", "Each in-game trade by number."),
                S("name:TradeDataPanel", "Pokémon", "What you get, what they ask for, its ability and item."),
                S("name:TradeDataPanel", "Details", "Original trainer, personality, IVs and contest stats."),
                S("name:TradeDataPanel", "Linked editors", "Ctrl+click a Pokémon or item to edit it in its editor."),
                S("name:TextDataPanel", "Names", "Nickname, up to 10 letters, and trainer name, up to 7."),
                S(null, "Shortcuts", "Ctrl+S saves, Ctrl+Z undoes, Ctrl+Y redoes."));

            Add("StarterEditorView", "Starter Pokémon editor",
                S("name:StarterGrid", "Starters", "Also updates the selection scene, rival team and text."),
                S("name:StarterGrid", "Item and level", "Their item and level, where the game allows it."),
                S("name:CommandBar", "Script", "The give-Pokémon command treated as the starter."),
                S("toolbar", "Linked editors", "Ctrl+click a Pokémon or item to edit it in its editor."),
                S(null, "Shortcuts", "Ctrl+S saves, Ctrl+Z undoes, Ctrl+Y redoes."));

            Add("WildEditorDPPtView", "Wild Pokémon editor",
                S("name:FilePicker", "Area", "Each encounter file, named by the place using it."),
                S("toolbar", "File tools", "Add, remove, import, export or repair files."),
                S("tab:Walking", "Walking", "Grass slots with chance and level, and the walk rate."),
                S("tab:Water", "Water", "Surfing and each rod, with rates and level ranges."),
                S("tab:Special", "Special", "Swarm, Poké Radar and Dual Slot Pokémon."),
                S("tab:Forms>name:FormDataBox", "Forms", "Shellos, Gastrodon and Unown forms."),
                S("name:SlotOddsButton", "Slot odds", "Slot chances shared by every file."),
                S(null, "Editing a Pokémon", "Ctrl+click a Pokémon to edit it in the Pokémon editor."),
                S(null, "Shortcuts", "Ctrl+S saves, Ctrl+Z undoes, Ctrl+Y redoes."));

            Add("WildEditorHGSSView", "Wild Pokémon editor",
                S("name:FilePicker", "Area", "Each encounter file, named by the place using it."),
                S("toolbar", "File tools", "Add, remove, import, export or repair files."),
                S("tab:Walking", "Walking", "Grass slots by time of day, and the walk rate."),
                S("tab:Water", "Water", "Surfing and each rod, with rates and level ranges."),
                S("tab:Special", "Special", "Swarm, night fishing, radio sounds and Rock Smash."),
                S("name:SlotOddsButton", "Slot odds", "Slot chances shared by every file."),
                S(null, "Editing a Pokémon", "Ctrl+click a Pokémon to edit it in the Pokémon editor."),
                S(null, "Shortcuts", "Ctrl+S saves, Ctrl+Z undoes, Ctrl+Y redoes."));

            Add("EncounterSlotOddsView", "Encounter slot odds",
                S("tabs", "Encounter methods", "One tab per encounter method, for every area."),
                S("type:ProportionBar", "Odds bar", "Each slot's share, to scale."),
                S("type:NumericUpDown", "Slot chances", "In percent. The total turns red when it isn't 100."),
                S(null, "Shortcuts", "Ctrl+S saves, Ctrl+Z undoes, Ctrl+Y redoes."));

            Add("WildIntroEditorView", "Wild Pokémon intro editor",
                S("name:SpeciesList", "Pokémon", "Wild Pokémon with their own intro."),
                S("name:ComboList", "Intros and music", "Wild battle intros and their music."),
                S(null, "Shortcuts", "Ctrl+S saves, Ctrl+Z undoes, Ctrl+Y redoes."));

            Add("WildHeldItemOddsView", "Wild held items",
                S("name:NormalOdds", "Held item odds", "Common item, rare item or nothing."),
                S("name:EyesOdds", "Compound Eyes", "The same with Compound Eyes in the lead."),
                S("type:ProportionBar", "Bars", "The three shares, to scale."),
                S("toolbar", "Held items", "Set the items they hold in the Pokémon editor."),
                S(null, "Shortcuts", "Ctrl+S saves, Ctrl+Z undoes, Ctrl+Y redoes."));

            Add("GrowthCurveEditorView", "Growth curves",
                S("toolbar", "Curve", "Each Pokémon's growth curve, and its level 100 EXP."),
                S("list", "EXP per level", "Total EXP for each level, and the gap to the next."),
                S("name:Chart", "Chart", "The curve from level 1 to 100."),
                S(null, "Existing saves", "Saved Pokémon keep their EXP, so their level can change."),
                S("toolbar", "Before saving", "A red note says what to fix first."),
                S(null, "Shortcuts", "Ctrl+S saves, Ctrl+Z undoes, Ctrl+Y redoes."));

            Add("BreedingItemsView", "Breeding items",
                S("name:BreedingRows", "Incense babies", "The baby, the item a parent holds, and what hatches otherwise."),
                S("name:BreedingRows", "Changing a row", "Pick a Pokémon or item. Ctrl+click edits it in its editor."),
                S(null, "One row per baby", "The game only uses each baby's first row."),
                S(null, "Shortcuts", "Ctrl+S saves, Ctrl+Z undoes, Ctrl+Y redoes."));

            Add("FriendshipChangesView", "Friendship changes",
                S("name:FriendshipRows", "Friendship changes", "Each event's change at low, middle and high friendship."),
                S("name:FriendshipRows", "Unused events", "Greyed rows are never triggered."),
                S(null, "Too much of a gain", "A note warns when a gain could wrap into a loss."),
                S(null, "Shortcuts", "Ctrl+S saves, Ctrl+Z undoes, Ctrl+Y redoes."));

            Add("TypeChartEditorView", "Type chart",
                S("name:ChartScroll", "Chart", "Attacking types down the side, defending types across."),
                S("name:ChartScroll", "Changing a matchup", "Click a cell to step through the effects. Arrows and Enter work too."),
                S("name:EditPanel", "Details", "The effect or a custom multiplier, and the Foresight box."),
                S("name:ChartScroll", "Rows and columns", "Click a type icon to list all its matchups."),
                S("toolbar", "Room", "How many matchups are used. Make room adds space."),
                S("toolbar", "Before saving", "Problems in red must be fixed first."),
                S(null, "Shortcuts", "Ctrl+S saves, Ctrl+Z undoes, Ctrl+Y redoes."));

            Add("SpecialEncountersEditorView", "Special encounters editor",
                S("tabs", "Special encounters", "Wild Pokémon from outside grass and water."),
                S("tab:Honey Tree", "Honey trees", "Groups and slots. Group C holds the rare Munchlax."),
                S("tab:Great Marsh", "Great Marsh", "Before and after the National Pokédex."),
                S("tab:Trophy Garden", "Trophy Garden", "The Pokémon that can turn up there."),
                S("tab:Headbutt", "Headbutt", "Species and levels for normal and special trees."),
                S("tab:Headbutt>name:GlHost", "Trees", "Green are normal, yellow special. Move trees drags them."),
                S("tab:Headbutt>name:GlHost", "ID key", "A normal tree's table depends on the player's ID. Hover to see."),
                S("tab:Bug Contest", "Bug Contest", "Pokémon, levels, rate and score, by day."),
                S("tab:Bug Contest opponents", "Contest rivals", "Each rival's entry, day and final score."),
                S("tab:Safari Zone", "Safari Zone", "Each area's Pokémon by method and time of day."),
                S("tab:Safari Zone", "Object points", "Bonus slots need enough points from placed objects."),
                S("tab:Swarms", "Swarms", "Where swarms happen and what they bring."),
                S(null, "Shortcuts", "Each tab saves on its own. Ctrl+S saves, Ctrl+Z undoes, Ctrl+Y redoes."));

            Add("HgEngineFormEditorView", "Form editor",
                S("name:SpeciesBar", "Pokémon", "A base Pokémon and its alternate forms."),
                S("name:FormList", "Forms", "Each form's species entry, and whether it reverts."),
                S("name:AddSlotButton", "Adding forms", "Add or remove a form. Its data is in the Pokémon editor."),
                S(null, "Shortcuts", "Ctrl+S saves, Ctrl+Z undoes, Ctrl+Y redoes."));

            Add("AbilityFlagsEditorView", "Ability flags",
                S("name:SearchBox", "Abilities", "Type part of a name to find one."),
                S("name:AbilityList", "Abilities", "Pick an ability to see its flags."),
                S("name:FlagList", "Flags", "How other moves and abilities treat this one."),
                S(null, "Shortcuts", "Ctrl+S saves, Ctrl+Z undoes, Ctrl+Y redoes."));

            Add("PokedexListsView", "Pokédex lists",
                S("tabs", "Lists", "The regional Pokédex and the sort lists."),
                S("tab:Regional dex>name:RegionalList", "Regional Pokédex", "Its Pokémon, in order."),
                S("tab:Regional dex>name:RegionalButtons", "Changing it", "Add, remove or move entries."),
                S("tab:Sort lists>name:SortListChoices", "Sort lists", "Each way the Pokédex can sort."),
                S("toolbar", "Rebuilding", "Rebuilds every sort list from the current data."),
                S(null, "Size change", "A new regional Pokédex size offers the matching patch."),
                S(null, "Shortcuts", "Ctrl+S saves, Ctrl+Z undoes, Ctrl+Y redoes."));
        }
    }
}
