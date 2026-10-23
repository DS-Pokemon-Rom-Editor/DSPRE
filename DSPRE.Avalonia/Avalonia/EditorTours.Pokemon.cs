namespace DSPRE.Avalonia
{
    public static partial class EditorTours
    {
        static partial void RegisterPokemon()
        {
            Add("PokemonEditorView", "Pokémon editor",
                S("name:SpeciesBar", "Pick a Pokémon", "By number or by name. The buttons beside it play its cry or open it in the Audio editor."),
                S("tab:Personal data", "Personal data", "Its base stats, EV yields, types, abilities and held items."),
                S("tab:Other", "More numbers", "Catch rate, base EXP, friendship, gender ratio, growth curve, Pokédex colour, egg groups and hatching. In HeartGold it also sets how its follower looks."),
                S("tab:TM/HM", "TMs and HMs", "Move machines between the Can Learn and Can't Learn lists. TM/HM bulk editor changes many Pokémon at once."),
                S("tab:Pokéathlon", "Pokéathlon", "Its star ranges for each Pokéathlon stat."),
                S("tab:hg-engine", "Extra data", "Hidden ability, baby form, regional Pokédex number, icon colours and its overworld follower."),
                S("tab:Learnset", "Learnset", "The moves it learns by level. Add, remove and reorder them at the bottom; Bulk edit all changes many Pokémon at once."),
                S("tab:Evolutions", "Evolutions", "What it evolves into, and how: level, item, trade, friendship and more. Save says which row is missing a target or level."),
                S("tab:Sprites", "Sprites", "Its battle sprites and colours. Import or export pictures, click a colour to change it, or paint them in All graphics."),
                S("tab:Battle display", "Battle display", "Where it stands in battle, its shadow, its party icon and how it moves when it appears. The preview plays it."),
                S(null, "Other editors", "Moves and items with a link button open in their editor from the right-click menu, or with Ctrl+click."));

            Add("MoveDataEditorView", "Move data editor",
                S("name:MovePicker", "Pick a move", "Every move in the game. Type part of its name to find it."),
                S("name:PropertiesBox", "Type and split", "Its type, whether it is physical or special, and which Pokémon it targets."),
                S("name:StatsBox", "Numbers", "Power, accuracy, PP, priority, and the chance its extra effect happens."),
                S("name:EffectBox", "Effect", "What the move does besides damage. Edit move script in the toolbar opens this move's battle script."),
                S("name:DescriptionBox", "Description", "The move's in-game description, shown for reference."),
                S("name:ContestBox", "Contests", "The contest condition it shows off and its appeal."),
                S("name:FlagsBox", "Flags", "How the move behaves: whether it makes contact, can be blocked by Protect, reflected, snatched or copied."),
                S("name:AbilityBox", "Ability based", "Whether Iron Fist boosts the move or Soundproof blocks it. A full list can grow with a ROM patch toolbox patch."),
                S("toolbar", "Spreadsheets", "Export CSV and Import CSV move every move's numbers through a spreadsheet. Imported moves wait until you save."));

            Add("BattleScriptEditorView", "Move animations & battle scripts",
                S("name:EntryPicker", "Pick a script", "Choose what to edit: move scripts, move effects, shared subroutines or move animations. Then pick an entry."),
                S("tab:Cards", "Cards", "Each command as a card. Click a card to change its command and values; the arrows move it and the minus removes it."),
                S("tab:Script", "Script", "The same commands as lines you can type or paste. Mistakes are underlined, and Cards stays locked until they are fixed."),
                S("tab:Read", "Read", "Move animations laid out step by step, read only. Show as picks a guided, script or raw view; click a line for details."),
                S("name:ScriptButtons", "Commands and help", "Add command adds a new line. Command guide explains what every command does."),
                S("name:PreviewPanel", "Preview", "For move animations, the battle scene plays the move. Change the backdrop, ground and HP bars, or cast it from the enemy side."),
                S("name:EditParticlesButton", "Particles", "Opens the particle effects this move uses."),
                S("name:SaveButton", "Saving", "Save writes this entry and Discard drops your edits. Ctrl+S saves too."));

            Add("TMEditorView", "TM / HM editor",
                S("list", "Machines", "Every TM and HM with the move it teaches. Pick one to change it."),
                S("name:MachineDetail", "The move", "The move this machine teaches. Right-click it and pick Open, or Ctrl+click, to open it in the Move data editor."),
                S("name:MachineDetail", "Disc colour", "The colour of the disc in the bag. Auto Palette matches it to the move's type, and Auto Palette All does every machine."),
                S("toolbar", "Spreadsheets", "Export CSV and Import CSV move the whole list through a spreadsheet."),
                S("toolbar", "Who learns it", "Which Pokémon learn each machine is set in the TM/HM bulk editor."));

            Add("TmHmBulkEditorView", "TM/HM bulk editor",
                S("name:ByPokemonButton", "Two ways to work", "By Pokémon: tick Pokémon on the left and set their machines on the right. By TM/HM: pick one machine and tick who learns it."),
                S("name:SpeciesTree", "Pokémon", "Pokémon grouped by evolution family. Tick a family to tick all of its members."),
                S("name:MachinePanel", "Machines", "Tick or untick a machine for every ticked Pokémon at once. A filled box means only some of them learn it."),
                S("name:SyncUnionButton", "Families", "The family buttons give every member what any of them learns, or keep only what all share. Copy compatibility to copies one Pokémon's machines to others."),
                S("name:FilterBox", "Search", "Type part of a name to narrow the list. The Select and Enable buttons tick or clear the list."),
                S("toolbar", "Saving", "Save all writes every change. Ctrl+S saves, Ctrl+Z undoes and Ctrl+Y redoes."));

            Add("MoveTutorEditorView", "Move tutors",
                S("tab:Moves", "Tutor moves", "Every move the tutors teach, what it costs and where it is taught. Platinum pays in coloured shards, HeartGold in Battle Points."),
                S("tab:Moves>list", "Jump to a move", "Right-click a move and pick Open, or Ctrl+click it, to open it in the Move data editor."),
                S("tab:Moves>list", "Headbutt", "In HeartGold the Ilex Forest tutor always teaches Headbutt, so its row is locked and no other move can be given to that tutor."),
                S("tab:By Pokémon", "By Pokémon", "Pick a Pokémon, then tick the tutor moves it can learn. Filter narrows the list."),
                S("tab:By move", "By move", "Pick a tutor move, then tick every Pokémon that can learn it."),
                S("toolbar", "Before saving", "A red note at the bottom says what to fix before you can save."));

            Add("EggMoveEditorView", "Egg move editor",
                S("name:MonPanel", "Pokémon", "Every Pokémon that has egg moves. Search finds one; double-click a result to jump to it."),
                S("name:MonPanel", "Adding Pokémon", "Pick a Pokémon in the box under the list, then Add, Replace or Delete."),
                S("name:MovePanel", "Egg moves", "The moves the selected Pokémon passes to its eggs. Pick a move under the list, then Add, Replace or Delete."),
                S("name:BulkPanel", "Bulk changes", "Swap one move for another, or remove a move, in every Pokémon at once."),
                S("toolbar", "Space left", "The toolbar shows how full the table is, amber when full and red when over; a table over the limit isn't saved. In HeartGold the game only looks in the first 2045 entries."),
                S("toolbar", "Spreadsheets", "Export CSV and Import CSV move the table through a spreadsheet."));

            Add("TradeEditorView", "Trade editor",
                S("name:TradePicker", "Pick a trade", "Each in-game trade has a number. Change it to load another trade."),
                S("name:TradeDataPanel", "The Pokémon", "What you get and what the trader asks for, with its ability and held item. Ctrl+click a Pokémon or item to open its editor."),
                S("name:TradeDataPanel", "Its details", "Its original trainer's gender, language and ID, its personality value, its IVs and its contest stats."),
                S("name:TextDataPanel", "Names", "The nickname it arrives with, up to 10 letters, and its original trainer's name, up to 7."));

            Add("StarterEditorView", "Starter Pokémon editor",
                S("name:StarterGrid", "The three starters", "The Pokémon you choose from at the start. Changing them also updates the selection scene, the rival's team and the lines that name them."),
                S("name:StarterGrid", "Item and level", "Where the game allows it, set the item they hold and their level. In Diamond and Pearl a note says which script sets them."),
                S("name:CommandBar", "Which script", "Manage… picks the give-Pokémon command the editor treats as the starter, if your scripts have changed."),
                S("toolbar", "Opening a Pokémon", "Right-click a Pokémon and pick Open, or Ctrl+click it."));

            Add("WildEditorDPPtView", "Wild Pokémon editor",
                S("name:FilePicker", "Pick an area", "Each encounter file holds the wild Pokémon of an area. Its name says which place uses it."),
                S("toolbar", "File tools", "Add a file, remove the last one, or import and export one. Repair all resets broken values in every file."),
                S("tab:Walking", "Walking", "The grass Pokémon, a row per slot with its chance and level; slots 3 and 4 change by day and night. Walk rate sets how often you meet them."),
                S("tab:Water", "Water", "Surfing and each rod. Each has a rate for how often something appears, and a species with a level range per slot."),
                S("tab:Special", "Special", "Swarm, Poké Radar and Dual Slot Pokémon, each with the walking slots it replaces."),
                S("tab:Forms>name:FormDataBox", "Forms", "Which Shellos and Gastrodon forms appear here, and which Unown letters."),
                S("name:SlotOddsButton", "Slot odds", "The chance of each slot is shared by every file. This opens the editor that changes it."),
                S(null, "Jump to a Pokémon", "Right-click a Pokémon and pick Open, or Ctrl+click it, to open it in the Pokémon editor."));

            Add("WildEditorHGSSView", "Wild Pokémon editor",
                S("name:FilePicker", "Pick an area", "Each encounter file holds the wild Pokémon of an area. Its name says which place uses it."),
                S("toolbar", "File tools", "Add a file, remove the last one, or import and export one. Repair all resets broken values in every file."),
                S("tab:Walking", "Walking", "The grass Pokémon, a row per slot with its chance and level, then the morning, day and night Pokémon. Walk rate sets how often you meet them."),
                S("tab:Water", "Water", "Surfing and each rod. Each has a rate for how often something appears, and a species with a level range per slot."),
                S("tab:Special", "Special", "Swarm, night fishing and Hoenn and Sinnoh Sound Pokémon, each with the slots it replaces, and Rock Smash with its rate and levels."),
                S("name:SlotOddsButton", "Slot odds", "The chance of each slot is shared by every file. This opens the editor that changes it."),
                S(null, "Jump to a Pokémon", "Right-click a Pokémon and pick Open, or Ctrl+click it, to open it in the Pokémon editor."));

            Add("EncounterSlotOddsView", "Encounter slot odds",
                S("tabs", "Encounter methods", "One tab per way of meeting wild Pokémon. These odds apply to every area."),
                S("type:ProportionBar", "The odds bar", "Each slot's share, drawn to scale. It updates as you type."),
                S("type:NumericUpDown", "Slot chances", "Each slot's chance in percent. The total underneath turns red when it isn't 100."));

            Add("WildIntroEditorView", "Wild Pokémon intro editor",
                S("name:SpeciesList", "Pokémon", "Wild Pokémon that get their own intro. Pick one to change which intro it gets."),
                S("name:ComboList", "Intros and music", "The intros wild battles use. Pick one to change its music."),
                S("toolbar", "Saving", "Save writes your changes and Ctrl+Z undoes; save the ROM to keep them."));

            Add("WildHeldItemOddsView", "Wild held items",
                S("name:NormalOdds", "Held item odds", "How often a wild Pokémon holds its common item, its rare item or nothing. Rare gets whatever is left."),
                S("name:EyesOdds", "Compound Eyes", "The same odds when your lead Pokémon has Compound Eyes."),
                S("type:ProportionBar", "The bars", "Each bar shows the three shares to scale and updates as you type."),
                S("toolbar", "Held items", "Which items each Pokémon can hold is set in the Pokémon editor."));

            Add("GrowthCurveEditorView", "Growth curves",
                S("toolbar", "Pick a curve", "Each Pokémon uses one of these curves. The EXP needed for level 100 shows beside it."),
                S("list", "EXP per level", "Type the total EXP needed to reach each level. To next shows the gap to the following one."),
                S("name:Chart", "The chart", "The curve from level 1 to 100. Point at it to read a level's EXP."),
                S(null, "Existing saves", "Pokémon in a save keep their EXP, so a new curve can change their level. Raise the level 100 total and old level 100s become 99s."),
                S("toolbar", "Before saving", "A red note at the bottom says what to fix before you can save."));

            Add("BreedingItemsView", "Breeding items",
                S("name:BreedingRows", "Incense babies", "Each row names a baby, the item a parent must hold for it to hatch, and what hatches otherwise."),
                S("name:BreedingRows", "Changing a row", "Type or pick a Pokémon or item. Right-click one and pick Open, or Ctrl+click, to go to its editor."),
                S(null, "One row per baby", "The game only uses the first row for each baby. A red note at the bottom says what to fix before saving."));

            Add("FriendshipChangesView", "Friendship changes",
                S("name:FriendshipRows", "Friendship changes", "How much friendship each event adds or takes away. The three columns are for low, middle and high friendship."),
                S("name:FriendshipRows", "Unused events", "Rows marked unused are never triggered by the game, so they are greyed out."),
                S(null, "Too much of a gain", "A note at the bottom warns when a gain plus the game's own bonuses could wrap around into a loss."));

            Add("TypeChartEditorView", "Type chart",
                S("name:ChartScroll", "The chart", "Attacking types run down the side, defending types across the top. Each cell shows how well that attack works."),
                S("name:ChartScroll", "Changing a matchup", "Click a cell to pick it, and again to step through super effective, not very effective, no effect and neutral. Arrow keys and Enter work too."),
                S("name:EditPanel", "Details", "Pick the effect from the list, or Custom for any multiplier. The Foresight box lets Foresight and Scrappy hit anyway."),
                S("name:ChartScroll", "Rows and columns", "Click a type icon to highlight its row or column and list all its matchups beside the chart."),
                S("toolbar", "Room", "The toolbar shows how many of the chart's matchups are used. Make room moves the chart where there's space for more."),
                S("toolbar", "Before saving", "Problems show in red at the bottom and have to be fixed before you can save."));

            Add("SpecialEncountersEditorView", "Special encounters editor",
                S("tabs", "Special encounters", "Wild Pokémon that don't come from grass or water. Each tab is one kind, and only the ones your game has are shown."),
                S("tab:Honey Tree", "Honey trees", "Pick a group, a slot, then its species; each slot shows its chance. Group C holds the rare Munchlax."),
                S("tab:Great Marsh", "Great Marsh", "The Great Marsh Pokémon before and after the National Pokédex. Pick a list, a slot, then its species."),
                S("tab:Trophy Garden", "Trophy Garden", "The Pokémon that can turn up in the Trophy Garden. Pick a slot, then its species."),
                S("tab:Headbutt", "Headbutt", "Pick a file, then set species and levels for normal and special trees. The 3D map shows where the trees are."),
                S("tab:Headbutt>name:GlHost", "The trees", "Green trees are normal, yellow ones special. Turn on Move trees to drag them or nudge them with the arrow keys, or type positions in the tree list."),
                S("tab:Headbutt>name:GlHost", "ID key", "A normal tree's table depends on the player's ID key, (6 × secret ID + trainer ID) mod 10. Hover a tree to see which keys get which table."),
                S("tab:Bug Contest", "Bug Contest", "The Pokémon of the Bug-Catching Contest, with levels, rate and score. Each set covers certain days once you have the National Pokédex."),
                S("tab:Bug Contest opponents", "Contest rivals", "What each rival enters with, on which day, and the score they finish with."),
                S("tab:Safari Zone", "Safari Zone", "Pick an area, then grass, surfing or a rod. Each has Pokémon by time of day, plus ones that need enough objects placed."),
                S("tab:Safari Zone", "Object points", "A bonus slot needs points of an object type. Each object earns 1 to 7 points, more the longer its area has been in the layout."),
                S("tab:Swarms", "Swarms", "Where a swarm can happen and which Pokémon it brings; every place is equally likely. Delete removes the picked one."),
                S(null, "Saving", "Each tab has its own Save, and Ctrl+S saves every tab with changes; Ctrl+Z and Ctrl+Y work on the open tab. Ctrl+click a Pokémon to open it."));

            Add("HgEngineFormEditorView", "Form editor",
                S("name:SpeciesBar", "Pick a Pokémon", "Pick a base Pokémon to see its alternate forms, such as Mega or regional forms."),
                S("name:FormList", "Forms", "Each row is one form and the species entry it uses. Tick Reverts to base form for forms that change back, like Mega Evolution."),
                S("name:AddSlotButton", "Adding forms", "Add form adds a row and the cross removes one. A form's own stats, types and abilities are edited in the Pokémon editor."));

            Add("AbilityFlagsEditorView", "Ability flags",
                S("name:SearchBox", "Abilities", "Type part of a name to find an ability."),
                S("name:AbilityList", "The list", "Pick an ability to see its flags."),
                S("name:FlagList", "Flags", "Each box changes how other moves and abilities treat this one, such as whether Trace can copy it or Mold Breaker ignores it."));

            Add("PokedexListsView", "Pokédex lists",
                S("tabs", "Two kinds of list", "The regional Pokédex, and the lists the Pokédex sorts Pokémon by."),
                S("tab:Regional dex>name:RegionalList", "Regional Pokédex", "The Pokémon in the regional Pokédex, in order."),
                S("tab:Regional dex>name:RegionalButtons", "Changing it", "Pick a Pokémon and Add puts it after the selected entry. Remove, Up and Down change the list."),
                S("tab:Sort lists>name:SortListChoices", "Sort lists", "Each list is one way the Pokédex can sort. Pick one to change its order."),
                S("toolbar", "Rebuilding", "Rebuild sort lists makes every sort list again from the current names and data."),
                S(null, "Saving", "Save writes every list and Discard drops your changes. When the regional Pokédex changes size, a note offers the patch that matches it."));
        }
    }
}
