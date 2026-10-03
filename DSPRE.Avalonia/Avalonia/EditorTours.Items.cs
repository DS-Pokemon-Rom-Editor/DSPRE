namespace DSPRE.Avalonia
{
    public static partial class EditorTours
    {
        static partial void RegisterItems()
        {
            Add("ItemEditorView", "Item Editor",
                S("name:ItemBar", "Pick an item", "Search by name or type its number. Everything below edits this item."),
                S("name:TableEntryBox", "Icon and data", "The icon picture and colours the item shows, and which data it reads. A note appears when other items share that data. If a table row points past the item files, items from that row on are left alone."),
                S("name:HoldPocketColumn", "Hold effect and pocket", "What it does when held and how strong that is, plus the bag pocket and battle pockets it goes in."),
                S("name:UsageColumn", "Use and price", "Whether it can be tossed, registered or used on a party member, its price, and what it does when used outside or in battle."),
                S("name:MoveRelatedBox", "Move effects", "Its Natural Gift type and power, its Fling effect and power, and its Pluck effect."),
                S("tab:Status Heals", "Status heals", "The conditions it cures, and whether it revives, levels up or evolves. These tabs work once Party Use is ticked."),
                S("tab:Stat Stages", "Stat stages", "The battle stats it raises. The game only checks for a value above 0 and always raises that stat one stage. Crit Rate above 0 gives Focus Energy."),
                S("tab:Restore", "Restore", "How much HP or PP it restores, and whether it works as a PP Up or PP Max."),
                S("tab:EVs", "Effort values", "Tick a stat and set how much its effort value changes."),
                S("tab:Friendship", "Friendship", "How much friendship changes when it is low, middling or high."),
                S("name:ExportButton", "More", "Export saves this item's data to a file. Open icon in Graphics lets you paint the icon by hand."),
                S("name:SaveAllButton", "Saving", "Save writes your changes. Ctrl+S saves, Ctrl+Z undoes and Ctrl+Y redoes."));

            Add("MartEditorView", "Mart Editor",
                S("list", "Marts", "The common mart, whose stock grows as the story goes on, and every specialty mart. Pick one to see what it sells."),
                S("name:MartGrid", "Stock", "Each slot and its item. In the common mart, Stock tier sets how far into the story an item appears; tier 0 is always sold. Right-click an item and pick Open, or Ctrl+click."),
                S("toolbar", "Slots and marts", "Add or remove slots, or add a custom mart. These need the expansion patch, which Apply ARM9 expansion adds if it is missing."),
                S("name:MartNotes", "Showing a new mart", "A custom mart gets its own number. Call that number from an event script to open it in game."),
                S("name:SaveButton", "Saving", "Save writes every mart, and Discard drops your changes. Ctrl+S saves too."));

            Add("BerryDataEditorView", "Berry Data",
                S("list", "Berries", "Every berry. Pick one to edit it; a dot marks berries with unsaved changes."),
                S("name:GrowthBox", "Growth", "Hours per growth stage, how fast the soil dries, and how many berries a plant yields."),
                S("name:LookBox", "Look and taste", "Its size, firmness, five flavours and smoothness. The bar shows each flavour's share."),
                S("name:SaveButton", "Saving", "Save writes every berry. Ctrl+S saves, Ctrl+Z undoes and Ctrl+Y redoes."));

            Add("BpShopEditorView", "Battle Point Shop",
                S("name:ItemCounter", "Item counter", "What the item counter sells for Battle Points, and each price. Right-click an item and pick Open, or Ctrl+click."),
                S("name:TmCounter", "TM counter", "The same for the counter that sells TMs."),
                S("name:ItemCounter", "Arranging", "The arrow buttons move the selected entry. In Platinum you can also add and remove entries, or move them to the other counter."),
                S("name:SaveButton", "Saving", "Save writes both counters. Ctrl+S saves, Ctrl+Z undoes and Ctrl+Y redoes."));

            Add("UndergroundMiningView", "Underground Mining",
                S("list", "Treasures", "Everything you can dig up in the Underground. A treasure with more than one shape has a row for each."),
                S("name:MiningColumns", "Four columns", "Which column the game uses depends on whether your trainer ID is odd or even, and whether you have the National Dex."),
                S("list", "Weights", "Each number is a weight: the higher, the more often it appears. The percentage beside it is its chance."),
                S("name:MiningTotals", "Totals", "Each column's total, and why the table can't be saved if something is wrong."),
                S("name:SaveButton", "Saving", "Save writes the table. Ctrl+S saves, Ctrl+Z undoes and Ctrl+Y redoes."));

            Add("ItemTableEditorView", "Item Tables",
                S("tabs", "Item tables", "Where items come from besides shops: Pickup, hidden items and Rock Smash. Only the tables this game has are shown."),
                S("tab:Pickup Table>name:ActivationGrid", "Which slot", "The chance of each slot being picked when Pickup finds something."),
                S("tab:Pickup Table>name:CommonGrid", "Common items", "The nine common items for each level range. Pick an item in any cell."),
                S("tab:Pickup Table>name:DivisorBox", "How often", "Pickup happens on roughly one battle in this many."),
                S("tab:Rare Pickup Items>name:RareGrid", "Rare items", "The two rare items for each level range."),
                S("tab:Hidden Items>list", "Hidden items", "Each hidden item with its amount and script number. Add or Remove below the list."),
                S("tab:Hidden Items>name:HiddenEditPanel", "Setting one up", "Pick the item, amount and script number. A hidden item on the map uses the number shown underneath."),
                S("tab:Rock Smash>name:RockSmashGrid", "Rock Smash odds", "For each header, the chance a smashed rock gives an item, and which drop table it uses."),
                S("tab:Rock Smash>name:RockSmashTables", "Drop tables", "The eight items in each drop table, each slot with its own fixed chance."),
                S("name:SaveButton", "Saving", "Save writes every table, and Discard drops your changes. Ctrl+S saves, Ctrl+Z undoes and Ctrl+Y redoes."));
        }
    }
}
