namespace DSPRE.Avalonia
{
    public static partial class EditorTours
    {
        static partial void RegisterItems()
        {
            Add("ItemEditorView", "Item editor",
                S("name:ItemBar", "Item", "Search by name or number."),
                S("name:TableEntryBox", "Icon and data", "Its icon, colours and data, and which items share them."),
                S("name:HoldPocketColumn", "Hold effect and pocket", "Its held effect, and its bag and battle pockets."),
                S("name:UsageColumn", "Price and use", "Its price, and its effect outside and in battle."),
                S("name:MoveRelatedBox", "Move effects", "Natural Gift, Fling and Pluck."),
                S("name:UsageChecksBox", "Checks", "Whether it can be tossed or registered."),
                S("name:PartyUseBox", "Party use", "Tick it to use the item on a party Pokémon."),
                S("tab:Status heals", "Status heals", "Conditions it cures, and whether it revives, levels up or evolves."),
                S("tab:Stat stages", "Stat stages", "Battle stats it raises by one stage. Crit rate gives Focus Energy."),
                S("tab:Restore", "Restore", "HP or PP restored, and PP Up."),
                S("tab:EVs", "Effort values", "How much each effort value changes."),
                S("tab:Friendship", "Friendship", "Friendship change at low, middling and high friendship."),
                S("name:ExportButton", "More", "Export or import the item's data, or paint its icon."),
                S(null, "Shortcuts", "Ctrl+S saves, Ctrl+Z undoes, Ctrl+Y redoes."));

            Add("MartEditorView", "Mart editor",
                S("list", "Marts", "The common mart and every specialty mart."),
                S("name:MartGrid", "Stock", "One item per slot. Ctrl+click edits it in the Item editor."),
                S("name:MartGrid", "Stock tier", "When the item goes on sale in the common mart."),
                S("toolbar", "Slots and marts", "Adding slots or marts needs the mart expansion patch."),
                S("name:MartNotes", "New marts", "Open a new mart by its number from an event script."),
                S(null, "Shortcuts", "Ctrl+S saves, Ctrl+Z undoes, Ctrl+Y redoes."));

            Add("BerryDataEditorView", "Berry data",
                S("list", "Berries", "Every berry. A dot marks unsaved changes."),
                S("name:GrowthBox", "Growth", "Hours per stage, soil drying and yield."),
                S("name:LookBox", "Look and taste", "Size, firmness, flavours and smoothness."),
                S(null, "Shortcuts", "Ctrl+S saves, Ctrl+Z undoes, Ctrl+Y redoes."));

            Add("BpShopEditorView", "Battle Point shop",
                S("name:ItemCounter", "Item counter", "Items sold for Battle Points. Ctrl+click edits an item."),
                S("name:TmCounter", "TM counter", "The same for the TM counter."),
                S("name:ItemCounter", "Arranging", "The arrows move an entry. Platinum can also add and remove them."),
                S(null, "Shortcuts", "Ctrl+S saves, Ctrl+Z undoes, Ctrl+Y redoes."));

            Add("UndergroundMiningView", "Underground mining",
                S("name:WallSettings", "Wall settings", "Treasures per wall, the first dig and rock placement tries."),
                S("list", "Treasures and rocks", "Every treasure, then the rocks, one row per shape."),
                S("name:MiningColumns", "Four columns", "Diamond, Pearl, and Platinum by odd or even trainer ID."),
                S("list", "Weights", "A higher weight comes up more often."),
                S("name:MiningDetail", "Art and shape", "The row's picture and shape. Click cells to toggle them."),
                S("name:MiningTotals", "Totals", "Each column's total, and anything that blocks saving."),
                S(null, "Shortcuts", "Ctrl+S saves, Ctrl+Z undoes, Ctrl+Y redoes."));

            Add("ItemTableEditorView", "Item tables",
                S("tabs", "Item tables", "Pickup, hidden items and Rock Smash."),
                S("tab:Pickup>name:PickupRate", "Pickup rate", "One battle in this many, when holding nothing."),
                S("tab:Pickup>name:PickupGrid", "Slots and items", "Nine slots and two rare ones per level range."),
                S("tab:Hidden items>list", "Hidden items", "Each hidden item's amount, script number and place. Ctrl+click edits the item."),
                S("tab:Hidden items>name:HiddenEditPanel", "Setup", "The item, amount and the script number the map uses."),
                S("tab:Rock Smash>name:RockSmashGrid", "Rock Smash odds", "Each header's item chance and drop table."),
                S("tab:Rock Smash>name:RockSmashTables", "Drop tables", "Eight items per table, each with a fixed chance."),
                S(null, "Shortcuts", "Ctrl+S saves, Ctrl+Z undoes, Ctrl+Y redoes."));
        }
    }
}
