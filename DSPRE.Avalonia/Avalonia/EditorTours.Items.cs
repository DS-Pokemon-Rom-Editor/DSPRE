namespace DSPRE.Avalonia
{
    public static partial class EditorTours
    {
        static partial void RegisterItems()
        {
            Add("ItemEditorView", "Item editor",
                S("name:ItemBar", "Pick an item", "Search by name or type its number. Everything below edits this item."),
                S("name:TableEntryBox", "Icon and data", "Its icon, colours and data. A note says when other items share that data."),
                S("name:HoldPocketColumn", "Hold effect and pocket", "What it does when held and how strongly, and its bag and battle pockets."),
                S("name:UsageColumn", "Price and use", "Its price, and what it does when used outside or in battle."),
                S("name:MoveRelatedBox", "Move effects", "Its Natural Gift type and power, its Fling effect and power, and its Pluck effect."),
                S("name:UsageChecksBox", "Checks", "Whether it can be tossed and registered to SELECT."),
                S("name:PartyUseBox", "Party use", "Tick Party use so it can be used on a party Pokémon, then set what it does in the tabs."),
                S("tab:Status heals", "Status heals", "The conditions it cures, and whether it revives, levels up or evolves."),
                S("tab:Stat stages", "Stat stages", "The battle stats it raises. Any value above 0 raises that stat one stage, and Crit rate gives Focus Energy."),
                S("tab:Restore", "Restore", "How much HP or PP it restores, and whether it raises a move's max PP like PP Up."),
                S("tab:EVs", "Effort values", "Tick a stat and set how much its effort value changes."),
                S("tab:Friendship", "Friendship", "How much friendship changes when it is low, middling or high."),
                S("name:ExportButton", "More", "Export and Import move this item's data in and out as a file. Open in All graphics lets you paint the icon by hand."));

            Add("MartEditorView", "Mart editor",
                S("list", "Marts", "The common mart, whose stock grows with the story, and every specialty mart."),
                S("name:MartGrid", "Stock", "Each slot holds one item. In the common mart, Stock tier sets when it goes on sale; tier 0 is always on sale. Ctrl+click an item to open it."),
                S("toolbar", "Slots and marts", "Add or remove slots, or add a mart. These need the mart expansion patch from the ROM patch toolbox."),
                S("name:MartNotes", "Showing a new mart", "A new mart gets a number; call it from an event script to open the mart in game."));

            Add("BerryDataEditorView", "Berry data",
                S("list", "Berries", "Every berry. Pick one to edit it; a dot marks berries with unsaved changes."),
                S("name:GrowthBox", "Growth", "Hours per growth stage, how fast the soil dries, and how many berries a plant yields."),
                S("name:LookBox", "Look and taste", "Its size, firmness, five flavours and smoothness. The bar shows each flavour's share."));

            Add("BpShopEditorView", "Battle Point shop",
                S("name:ItemCounter", "Item counter", "What the item counter sells for Battle Points, and at what price. Ctrl+click an item to open it."),
                S("name:TmCounter", "TM counter", "The same for the counter that sells TMs."),
                S("name:ItemCounter", "Arranging", "The arrows move the picked entry. In Platinum you can also add, remove or move entries to the other counter."));

            Add("UndergroundMiningView", "Underground mining",
                S("name:WallSettings", "Wall settings", "Treasures per wall, the first dig, and how hard the game tries to fit rocks in."),
                S("list", "Treasures and rocks", "Every treasure, then the rocks. A treasure with several shapes has a row for each."),
                S("name:MiningColumns", "Four columns", "Diamond and Pearl each use their own column, Platinum picks by odd or even trainer ID; the later two count once you have the National Dex."),
                S("list", "Weights", "The higher a weight, the more often it comes up; the percentage beside it is its chance."),
                S("name:MiningDetail", "Art and shape", "The row's picture, its ug_parts files and its shape. Click cells to make them solid or empty."),
                S("name:MiningTotals", "Totals", "Each column's total, and why the table can't be saved if something is wrong."));

            Add("ItemTableEditorView", "Item tables",
                S("tabs", "Item tables", "Where items come from besides shops: Pickup, hidden items and Rock Smash, for the tables this game has."),
                S("tab:Pickup>name:PickupRate", "How often", "Pickup happens on one battle in this many, when the Pokémon holds nothing."),
                S("tab:Pickup>name:PickupGrid", "Slots and items", "The Chance row is how often each slot comes up; each level range has nine slots and two rare ones."),
                S("tab:Hidden items>list", "Hidden items", "Each hidden item's amount, script number and place. Add or Remove below the list."),
                S("tab:Hidden items>name:HiddenEditPanel", "Setting one up", "Pick the item, amount and script number; the hidden item on the map uses the number underneath."),
                S("tab:Rock Smash>name:RockSmashGrid", "Rock Smash odds", "Each header's chance of an item from a smashed rock, and its drop table."),
                S("tab:Rock Smash>name:RockSmashTables", "Drop tables", "The eight items in each drop table, each slot with its own fixed chance."));
        }
    }
}
