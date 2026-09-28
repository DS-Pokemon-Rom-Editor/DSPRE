namespace DSPRE.Avalonia
{
    public static partial class EditorTours
    {
        static partial void RegisterPokemon()
        {
            Add("PokemonEditorView", "Pokémon Editor",
                S("name:SpeciesBar", "Pick a Pokémon", "By number or by name. The buttons beside it play its cry or open it in the Audio Editor."),
                S("tab:Personal Data", "Personal data", "Its base stats, EV yields, types, abilities and held items."),
                S("tab:Misc", "More numbers", "Catch rate, base EXP, friendship, gender ratio, growth curve, Pokédex colour, egg groups and hatching. In HeartGold it also sets how its follower looks."),
                S("tab:TM/HM", "TMs and HMs", "Move machines between the Can Learn and Cannot Learn lists. Bulk Editor changes many Pokémon at once."),
                S("tab:Pokéathlon", "Pokéathlon", "Its star ranges for each Pokéathlon stat."),
                S("tab:hg-engine", "Extra data", "Hidden ability, baby form, regional Pokédex number, icon colours and its overworld follower."),
                S("tab:Learnset", "Learnset", "The moves it learns by level. Add, remove and reorder them at the bottom; Bulk edit all changes many Pokémon at once."),
                S("tab:Evolutions", "Evolutions", "What it evolves into, and how: level, item, trade, friendship and more."),
                S("tab:Sprites", "Sprites", "Its battle sprites and colours. Import or export pictures, click a colour to change it, or open them in the Graphics window to paint by hand."),
                S("tab:Battle Display", "Battle display", "Where it stands in battle, its shadow, its party icon and how it moves when it appears. The preview plays it."),
                S("name:SaveButton", "Saving", "Save or Ctrl+S writes every tab, Ctrl+Z undoes. Moves and items that show a link button open in their own editor from the right-click menu, or with Ctrl+click."));

            Add("MoveDataEditorView", "Move Data Editor",
                S("name:MovePicker", "Pick a move", "Every move in the game. Type part of its name to find it."),
                S("name:PropertiesBox", "Type and split", "Its type, whether it is physical or special, and which Pokémon it targets."),
                S("name:StatsBox", "Numbers", "Power, accuracy, PP, priority, and the chance its extra effect happens."),
                S("name:EffectBox", "Effect", "What the move does besides damage. Edit move script in the toolbar opens this move's battle script."),
                S("name:DescriptionBox", "Description", "The move's in-game description, shown for reference."),
                S("name:ContestBox", "Contests", "The contest condition it shows off and its appeal."),
                S("name:FlagsBox", "Flags", "How the move behaves: whether it makes contact, can be blocked by Protect, reflected, snatched or copied."),
                S("toolbar", "Spreadsheets", "Export CSV and Import CSV move every move's numbers through a spreadsheet. Imported moves wait until you save."),
                S("toolbar", "Saving", "Save or Ctrl+S writes your changes, Ctrl+Z undoes, Discard drops them."));

            Add("BattleScriptEditorView", "Move Animations & Battle Scripts",
                S("name:EntryPicker", "Pick a script", "Choose what to edit: move scripts, move effects, shared subroutines or move animations. Then pick an entry."),
                S("tab:Read", "Read", "Move animations laid out step by step. Show as switches between a guided, script and raw view; click a line for details."),
                S("tab:Cards", "Cards", "Each command as a card. Expand one to change its command and values, or use its arrow and remove buttons to move or remove it."),
                S("tab:Text", "Text", "The same commands as lines you can type. Mistakes are listed below, and Cards stays locked until they are fixed."),
                S("name:ScriptButtons", "Commands and help", "Add command adds a new line. Command guide explains what every command does."),
                S("name:PreviewPanel", "Preview", "For move animations, the battle scene plays the move. Change the backdrop, ground and HP gauges, or cast it from the enemy side."),
                S("name:EditParticlesButton", "Particles", "Opens the particle effects this move uses."),
                S("name:SaveButton", "Saving", "Save writes this entry and Discard drops your edits; Ctrl+S saves too. Closing with unsaved edits asks first."));

            Add("TMEditorView", "TM / HM Editor",
                S("list", "Machines", "Every TM and HM with the move it teaches. Pick one to change it."),
                S("name:MachineDetail", "The move", "The move this machine teaches. Right-click it and pick Open, or Ctrl+click, to open it in the Move Data Editor."),
                S("name:MachineDetail", "Disc colour", "The colour of the disc in the bag. Auto Palette matches it to the move's type, and Auto Palette All does every machine."),
                S("toolbar", "Spreadsheets", "Export CSV and Import CSV move the whole list through a spreadsheet."),
                S("toolbar", "Saving", "Save or Ctrl+S writes your changes, Ctrl+Z undoes. Which Pokémon learn each machine is set in the TM/HM Bulk Editor."));

            Add("TmHmBulkEditorView", "TM/HM Bulk Editor",
                S("name:ByPokemonButton", "Two ways to work", "By Pokémon: tick Pokémon on the left and set their machines on the right. By TM/HM: pick one machine and tick who learns it."),
                S("name:SpeciesTree", "Pokémon", "Pokémon grouped by evolution family. Tick a family to tick all of its members."),
                S("name:MachinePanel", "Machines", "Tick or untick a machine for every ticked Pokémon at once. A filled box means only some of them learn it."),
                S("name:SyncUnionButton", "Families", "Sync Family gives every member what any of them learns, or keeps only what all share. Copy Compatibility To copies one Pokémon's machines to others."),
                S("name:FilterBox", "Filter", "Type part of a name to narrow the list. The Select and Enable buttons tick or clear the list."),
                S("toolbar", "Saving", "Save All writes every change; Ctrl+S does the same."));

            Add("MoveTutorEditorView", "Move Tutors",
                S("tab:Moves", "Tutor moves", "Every move the tutors teach, what it costs and where it is taught. Platinum pays in coloured shards, HeartGold in Battle Points."),
                S("tab:Moves>list", "Jump to a move", "Right-click a move and pick Open, or Ctrl+click it, to open it in the Move Data Editor."),
                S("tab:By Pokémon", "By Pokémon", "Pick a Pokémon, then tick the tutor moves it can learn. Filter narrows the list."),
                S("tab:By Move", "By Move", "Pick a tutor move, then tick every Pokémon that can learn it."),
                S("toolbar", "Saving", "A red note at the bottom says what to fix before saving. Save or Ctrl+S writes, Ctrl+Z undoes."));

            Add("EggMoveEditorView", "Egg Move Editor",
                S("name:MonPanel", "Pokémon", "Every Pokémon that has egg moves. Search finds one; double-click a result to jump to it."),
                S("name:MonPanel", "Adding Pokémon", "Pick a Pokémon in the box under the list, then Add, Replace or Delete."),
                S("name:MovePanel", "Egg moves", "The moves the selected Pokémon passes to its eggs. Pick a move under the list, then Add, Replace or Delete."),
                S("name:BulkPanel", "Bulk changes", "Swap one move for another, or remove a move, in every Pokémon at once."),
                S("toolbar", "Space left", "The toolbar shows how full the table is, amber when full and red when over. The move count under the list does the same for one Pokémon."),
                S("toolbar", "Saving", "Export CSV and Import CSV use a spreadsheet. Save or Ctrl+S writes, Discard drops unsaved edits."));

            Add("TradeEditorView", "Trade Editor",
                S("name:TradePicker", "Pick a trade", "Each in-game trade has a number. Change it to load another trade."),
                S("name:TradeDataPanel", "The Pokémon", "What you receive and what the trader asks for, with its ability and held item. Right-click a Pokémon or item and pick Open, or Ctrl+click, to go to its editor."),
                S("name:TradeDataPanel", "Its details", "Its original trainer's gender, language and ID, its personality value, its IVs and its contest stats."),
                S("name:TextDataPanel", "Names", "The nickname it arrives with, up to 10 letters, and its original trainer's name, up to 7."),
                S("toolbar", "Saving", "Save or Ctrl+S writes your changes, Ctrl+Z undoes."));

            Add("StarterEditorView", "Starter Pokémon Editor",
                S("name:StarterGrid", "The three starters", "The Pokémon you choose from at the start. Changing them also updates the selection scene, the rival's team and the lines that name them."),
                S("name:StarterGrid", "Item and level", "Where the game allows it, set the item they hold and their level. In Diamond and Pearl a note says which script sets them."),
                S("name:CommandBar", "Which script", "Manage… picks the give-Pokémon command the editor treats as the starter, if your scripts have changed."),
                S("toolbar", "Saving", "Save or Ctrl+S writes your changes, Ctrl+Z undoes. Right-click a Pokémon and pick Open, or Ctrl+click, to open it."));

            Add("WildEditorDPPtView", "Wild Pokémon Editor",
                S("name:FilePicker", "Pick an area", "Each encounter file holds the wild Pokémon of an area. Its name says which place uses it."),
                S("toolbar", "File tools", "Add a file, remove the last one, or import and export one. Repair all resets broken values in every file."),
                S("tab:Walking", "Walking", "The grass Pokémon, one row per slot with its chance and level. Walk Rate sets how often you meet them."),
                S("tab:Walking>name:FormDataBox", "Forms", "Which Shellos and Gastrodon forms appear here, and which Unown letters."),
                S("tab:Time / Special", "Time and specials", "Pokémon that take over some grass slots by day, at night, during a swarm or with the Poké Radar."),
                S("tab:Dual Slot", "Dual Slot", "Pokémon that appear while Ruby, Sapphire, Emerald, FireRed or LeafGreen is in the second slot."),
                S("tab:Water", "Water", "Surfing and each rod. Each has a rate for how often something appears, and a species with a level range per slot."),
                S("name:SlotOddsButton", "Slot odds", "The chance of each slot is shared by every file. This opens the editor that changes it."),
                S(null, "Jump to a Pokémon", "Right-click a Pokémon and pick Open, or Ctrl+click it, to open it in the Pokémon Editor."),
                S("toolbar", "Saving", "Save or Ctrl+S writes this file, Ctrl+Z undoes. Switching files with unsaved edits asks first."));

            Add("WildEditorHGSSView", "Wild Pokémon Editor",
                S("name:FilePicker", "Pick an area", "Each encounter file holds the wild Pokémon of an area. Its name says which place uses it."),
                S("toolbar", "File tools", "Add a file, remove the last one, or import and export one. Repair all resets broken values in every file."),
                S("tab:Morning", "Morning", "The grass Pokémon in the morning, one row per slot with its chance. Walk Rate sets how often you meet them. Each slot has one level for the whole day, so changing it here changes it on Day and Night too."),
                S("tab:Day", "Day", "The grass Pokémon during the day, in the same slots."),
                S("tab:Night", "Night", "The grass Pokémon at night, in the same slots."),
                S("tab:Special", "Special", "Swarm and night-fishing Pokémon, those called by the Hoenn and Sinnoh radio sounds, and Rock Smash with its own rate and levels."),
                S("tab:Water", "Water", "Surfing and each rod. Each has a rate for how often something appears, and a species with a level range per slot."),
                S("name:SlotOddsButton", "Slot odds", "The chance of each slot is shared by every file. This opens the editor that changes it."),
                S(null, "Jump to a Pokémon", "Right-click a Pokémon and pick Open, or Ctrl+click it, to open it in the Pokémon Editor."),
                S("toolbar", "Saving", "Save or Ctrl+S writes this file, Ctrl+Z undoes. Switching files with unsaved edits asks first."));

            Add("EncounterSlotOddsView", "Encounter Slot Odds",
                S("tabs", "Encounter methods", "One tab per way of meeting wild Pokémon. These odds apply to every area."),
                S("type:ProportionBar", "The odds bar", "Each slot's share, drawn to scale. It updates as you type."),
                S("type:NumericUpDown", "Slot chances", "Each slot's chance in percent. The total underneath turns red when it is not 100."),
                S("toolbar", "Saving", "Save or Ctrl+S writes the odds, Ctrl+Z undoes."));

            Add("WildIntroEditorView", "Wild Pokémon Intro Editor",
                S("name:SpeciesList", "Pokémon", "Wild Pokémon that get their own intro. Pick one to change which intro it gets."),
                S("name:ComboList", "Intros and music", "The intros wild battles use. Pick one to change its music."),
                S("toolbar", "Saving", "Save or Ctrl+S writes your changes, Ctrl+Z undoes. Save the ROM to keep them."));

            Add("WildHeldItemOddsView", "Wild Held Items",
                S("name:NormalOdds", "Held item odds", "How often a wild Pokémon holds its common item, its rare item or nothing. Rare gets whatever is left."),
                S("name:EyesOdds", "Compound Eyes", "The same odds when your lead Pokémon has Compound Eyes."),
                S("type:ProportionBar", "The bars", "Each bar shows the three shares to scale and updates as you type."),
                S("toolbar", "Saving", "Save or Ctrl+S writes the odds, Ctrl+Z undoes. Which items each Pokémon can hold is set in the Pokémon Editor."));

            Add("GrowthCurveEditorView", "Growth Curves",
                S("toolbar", "Pick a curve", "Each Pokémon uses one of these curves. The EXP needed for level 100 shows beside it."),
                S("list", "EXP per level", "Type the total EXP needed to reach each level. To next shows the gap to the following one."),
                S("name:Chart", "The chart", "The curve from level 1 to 100. Point at it to read a level's EXP."),
                S("toolbar", "Saving", "A red note at the bottom says what to fix before saving. Save or Ctrl+S writes, Ctrl+Z undoes."));

            Add("BreedingItemsView", "Breeding Items",
                S("name:BreedingRows", "Incense babies", "Each row names a baby, the item a parent must hold for it to hatch, and what hatches otherwise."),
                S("name:BreedingRows", "Changing a row", "Type or pick a Pokémon or item. Right-click one and pick Open, or Ctrl+click, to go to its editor."),
                S(null, "One row per baby", "The game only uses the first row for each baby. A red note at the bottom says what to fix before saving."),
                S("toolbar", "Saving", "Save or Ctrl+S writes your changes, Ctrl+Z undoes."));

            Add("FriendshipChangesView", "Friendship Changes",
                S("name:FriendshipRows", "Friendship changes", "How much friendship each event adds or takes away. The three columns are for low, middle and high friendship."),
                S("name:FriendshipRows", "Unused events", "Rows marked unused are never triggered by the game, so they are greyed out."),
                S(null, "Too much of a gain", "A note at the bottom warns when a gain plus the game's own bonuses could wrap around into a loss."),
                S("toolbar", "Saving", "Save or Ctrl+S writes your changes, Ctrl+Z undoes."));

            Add("TypeChartEditorView", "Type Chart",
                S("name:ChartScroll", "The chart", "Attacking types run down the side, defending types across the top. Each cell shows how well that attack works."),
                S("name:ChartScroll", "Changing a matchup", "Click a cell to select it, and click again to step it through super effective, not very effective, no effect and neutral. Arrow keys and Enter do the same."),
                S("name:EditPanel", "Details", "Pick the effect from the list, or Custom for any multiplier. The Foresight box lets Foresight and Scrappy hit anyway."),
                S("name:ChartScroll", "Rows and columns", "Click a type icon to highlight its row or column and list all its matchups beside the chart."),
                S("toolbar", "Room", "The chart holds a limited number of matchups, and the toolbar shows how many are used. Make room moves it where there is space for more."),
                S("toolbar", "Saving", "Problems show in red at the bottom and must be fixed before saving. Save or Ctrl+S writes, Ctrl+Z undoes."));

            Add("SpecialEncountersEditorView", "Special Encounters Editor",
                S("tabs", "Special encounters", "Wild Pokémon that don't come from grass or water. Each tab is one kind, and only the ones your game has are shown."),
                S("tab:Honey Tree", "Honey trees", "Pick a group, a slot, then its species; each slot shows its chance. Group C holds the rare Munchlax."),
                S("tab:Great Marsh", "Great Marsh", "The Great Marsh Pokémon before and after the National Pokédex. Pick a list, a slot, then its species."),
                S("tab:Trophy Garden", "Trophy Garden", "The Pokémon that can turn up in the Trophy Garden. Pick a slot, then its species."),
                S("tab:Headbutt", "Headbutt", "Pick a file, then set species and levels for normal and special trees. The 3D map shows where the trees are."),
                S("tab:Headbutt>name:GlHost", "The trees", "Green trees are normal, yellow ones special. Turn on Move trees to drag them, or type their positions in the tree list."),
                S("tab:Bug Contest", "Bug Contest", "The Pokémon of the Bug-Catching Contest, with levels, rate and score. Each set covers certain days once you have the National Pokédex."),
                S("tab:Bug Contest Opponents", "Contest rivals", "What each rival enters with, on which day, and the score they finish with."),
                S("tab:Safari Zone", "Safari Zone", "Pick an area, then grass, surfing or a rod. Each has Pokémon by time of day, plus ones that only appear when enough objects are placed."),
                S("tab:Swarms", "Swarms", "Where a swarm can happen and which Pokémon it brings. Every destination is equally likely."),
                S(null, "Saving", "Each tab has its own Save, and Ctrl+S saves every tab with changes. Right-click a Pokémon and pick Open, or Ctrl+click, to open it."));

            Add("HgEngineFormEditorView", "Form Editor",
                S("name:SpeciesBar", "Pick a Pokémon", "Pick a base Pokémon to see its alternate forms, such as Mega or regional forms."),
                S("name:FormList", "Its forms", "Each row is one form and the species entry it uses. Tick Needs Reversion for forms that change back, like Mega Evolution."),
                S("name:AddSlotButton", "Adding forms", "Add Form adds a row and the cross removes one. A form's own stats, types and abilities are edited in the Pokémon Editor."),
                S("name:SaveButton", "Saving", "Save writes your changes and Discard drops them; Ctrl+S saves too."));
        }
    }
}
