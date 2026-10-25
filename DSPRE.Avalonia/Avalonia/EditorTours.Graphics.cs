namespace DSPRE.Avalonia
{
    public static partial class EditorTours
    {
        static partial void RegisterGraphics()
        {
            Add("GraphicsBrowserView", "All graphics",
                S("name:SearchBox", "Every graphic", "Every picture in the game. Search by name or number."),
                S("name:CategoryList", "Kinds", "Show one kind of graphic, or Everything."),
                S("name:ItemList", "Entries", "Pick an entry to show it."),
                S("name:PartsList", "Parts", "The files an entry is made of, like front and back."),
                S("name:PreviewPanel", "Preview", "The picture as the game draws it, enlarged."),
                S("name:ActionsBar", "Import and export", "Export a PNG, edit it and import it back."),
                S("name:ActionsBar", "Painting and more", "Paint it, or edit the rest in its editor."),
                S(null, "Shortcuts", "Ctrl+S saves, Ctrl+Z undoes, Ctrl+Y redoes."));

            Add("ModelBrowserView", "All models and textures",
                S("name:SearchBox", "Every model", "Every model, texture set and animation. Search by name or number."),
                S("name:CategoryList", "Kinds", "Show one kind, or Everything."),
                S("name:ItemList", "Entries", "Pick an entry to view it."),
                S("name:GlHost", "Preview", "Drag to turn it, wheel to zoom."),
                S("name:SidePanel", "Textures and animation", "Pick the textures, colours and animation to show."),
                S("name:PlayBar", "Movement", "Play the animation or drag to a frame."),
                S("name:FileButtons", "Import and export", "Export as DAE, glTF or the raw file, or import one."),
                S(null, "Shortcuts", "Ctrl+S saves, Ctrl+Z undoes, Ctrl+Y redoes."));

            Add("BattleSceneBrowserView", "Battle scenes",
                S("name:SceneList", "Battle scenery", "Every set of battle scenery, and where it is used."),
                S("name:SceneChoices", "Ground and time", "The ground and time of day."),
                S("name:ScenePreview", "Preview", "The scenery as a battle draws it."),
                S("name:SceneButtons", "Editing", "Paint the drawing or change its colours."),
                S("name:PlacesList", "Places", "Every place that uses this scenery."),
                S(null, "Keeping changes", "Edits go into the project. Save the ROM to keep them."));

            Add("BattleScreenEditorView", "Battle screen",
                S(null, "Battle screen", "Both screens of a battle. Every piece can be edited."),
                S("toolbar", "Scene", "Ground, time of day, text box, backdrop and touch screen menu."),
                S("name:PieceList", "Pieces", "Click a piece here or on a screen to pick it."),
                S("name:BothScreens", "Screens", "Both screens as a battle draws them."),
                S("name:PieceDetails", "Piece", "Paint, export or import it."),
                S("name:SampleSection", "Sample", "A name, level, HP and message to try. Not saved."),
                S(null, "Keeping changes", "Edits go into the project. Save the ROM to keep them."),
                S(null, "Shortcuts", "Ctrl+S saves, Ctrl+Z undoes, Ctrl+Y redoes."));

            Add("BottomScreenEditorView", "Bottom screen",
                S("name:ScreenTabs", "Screens", "The touch screen outside battle."),
                S("name:ScreenOptions", "Screen options", "App, casing, backlight, clock or busy menu. Play runs an app."),
                S("name:PieceList", "Pieces", "Every piece the screen is built from."),
                S("name:ScreenPreview", "Preview", "The screen as the game draws it."),
                S("name:PieceDetails", "Piece", "Paint, export or import it, or edit its animation."),
                S("name:ColourSwatches", "Colours", "Click a colour to change it."),
                S(null, "Shortcuts", "Ctrl+S saves, Ctrl+Z undoes, Ctrl+Y redoes."));

            Add("ScreensEditorView", "Screens editor",
                S("name:ScreenList", "Screens", "Pick a screen."),
                S("name:ScreenViews", "Views", "Preview shows the screen. Layout moves its text."),
                S(null, "Shortcuts", "Ctrl+S saves, Ctrl+Z undoes, Ctrl+Y redoes."));

            Add("TitleScreenEditorView", "Title screen",
                S("name:PieceList", "Pieces", "Import or export a piece."),
                S("name:SaveButtons", "Version", "HeartGold's or SoulSilver's title screen."),
                S(null, "Shortcuts", "Ctrl+S saves, Ctrl+Z undoes, Ctrl+Y redoes."));

            Add("DungeonCutinEditorView", "Dungeon cut-in editor",
                S("name:CutinList", "Splash screens", "The cut-ins shown on entering some places."),
                S("name:LocationCard", "Location", "The place that shows it, and its name line."),
                S("name:TimesGrid", "Times of day", "A picture for each time of day."),
                S("name:CutinToolbar", "Table and graphics", "Export or import the table as CSV."),
                S(null, "Shortcuts", "Ctrl+S saves, Ctrl+Z undoes, Ctrl+Y redoes."));

            Add("TrainerCardEditorView", "Trainer card",
                S("name:RankBar", "Rank", "The card's colours for each rank."),
                S("name:PieceList", "Pieces", "Import or export a piece."),
                S(null, "Shortcuts", "Ctrl+S saves, Ctrl+Z undoes, Ctrl+Y redoes."));

            Add("BannerEditorView", "Game icon & banner",
                S(null, "Game icon and banner", "The icon and titles on the DS menu."),
                S("name:IconRow", "Icon", "32 by 32, up to 15 colours. Import or export it."),
                S("name:TitlesList", "Menu titles", "The text under the icon, per language."),
                S(null, "Shortcuts", "Ctrl+S saves, Ctrl+Z undoes, Ctrl+Y redoes."));

            Add("BtxEditorView", "Overworld editor",
                S("list", "Overworlds", "Every overworld sprite."),
                S("name:SpritePreview", "Preview", "The sheet, and each facing standing, walking and running."),
                S("name:SpriteButtons", "Pictures", "Import or export the sheet. Shiny palette shows shiny colours."),
                S("name:PropertiesPanel", "Drawing", "Flat or model, shadow, footprints and reflection."),
                S("name:EntryButtons", "New overworlds", "Add or delete entries with the overworld expansion patch."),
                S(null, "Shortcuts", "Ctrl+S saves, Ctrl+Z undoes, Ctrl+Y redoes."));

            Add("NsbtxEditorView", "Map & building textures",
                S("toolbar", "Map or building", "Map texture packs or building ones."),
                S("name:PackList", "Packs", "Every pack. Add copies the first, Remove last deletes the last."),
                S("name:TextureList", "Textures", "The pictures in the pack."),
                S("name:PaletteList", "Colours", "The pack's colour sets."),
                S("name:TexturePreview", "Preview", "The texture in the picked colours."),
                S("toolbar", "Import and export", "Export or import the whole pack."),
                S(null, "Shortcuts", "Ctrl+S saves, Ctrl+Z undoes, Ctrl+Y redoes."));

            Add("CellAnimationEditorView", "Cell animation editor",
                S("toolbar", "Cell animation", "Which drawing each frame shows and for how long."),
                S("name:SequenceList", "Sequences", "Each animation in the file."),
                S("name:SequenceButtons", "Adding sequences", "Add one at the end or remove the last."),
                S("name:AnimationPreview", "Preview", "Play runs it at game speed."),
                S("name:FramesPanel", "Sequence", "Once, looping or backwards, and where a loop returns."),
                S("name:FramesPanel", "Frames", "Each frame's drawing and time, in sixtieths of a second."),
                S("name:PiecesPanel", "Pieces", "Move a drawing's pieces."),
                S(null, "Shortcuts", "Ctrl+S saves, Ctrl+Z undoes, Ctrl+Y redoes."));

            Add("ParticleLibraryView", "Particles",
                S("name:ParticleList", "Particles", "Every particle effect in the game."),
                S("name:CategoryPicker", "Kinds", "Show one kind of effect."),
                S("name:ParticleSearch", "Search", "By name or file number."),
                S("name:OpenButton", "Open", "Opens the effect in the Particle editor."));

            Add("ParticleEditorView", "Particle editor",
                S(null, "Particle editor", "One particle effect, with a live preview."),
                S("name:EmitterPicker", "Emitters", "Pick an emitter. Tick Only this emitter to hide the rest."),
                S("name:FieldsPanel", "Settings", "The emitter's settings. Filter to find one."),
                S("name:ParticlePreview", "Preview", "The effect, updated as you edit. Replay restarts it."),
                S("name:TextureList", "Textures", "Export or replace a particle's picture."),
                S(null, "Shortcuts", "Ctrl+S saves, Ctrl+Z undoes, Ctrl+Y redoes."));

            Add("BallCapsuleEditorView", "Ball capsules",
                S("name:CapsulePicker", "Capsules", "The seals on a trainer's Poké Ball, and who uses it."),
                S("name:SealList", "Seals", "Drag a seal onto the capsule, or double-click it."),
                S("name:BoardCanvas", "Capsule", "Drag seals to move them. Right-click removes one."),
                S("name:SealButtons", "Seal looks", "Edit the seal's sticker or its particles."),
                S("name:SendOutPanel", "Send-out preview", "Watch the capsule play as a Pokémon is sent out."),
                S(null, "Shortcuts", "Ctrl+S saves, Ctrl+Z undoes, Ctrl+Y redoes."));

            Add("TilesetBuilderView", "Picture to background",
                S("toolbar", "Picture to background", "Turns a PNG into a background the game can draw."),
                S("name:PictureArea", "Before and after", "Your picture, and how the game would draw it."),
                S("name:OptionsPanel", "Colours", "Sixteen banks of sixteen colours, or one list of 256."),
                S("name:OptionsPanel", "Result", "The colours and tiles it needs, and why it won't fit."),
                S("name:SavePanel", "Files", "Saves the three files, ready for All graphics."));

            Add("GraphicPainterView", "Painter",
                S(null, "Painter", "Paint a graphic pixel by pixel."),
                S("name:CanvasHost", "Picture", "Click or drag to paint. Ctrl+wheel zooms."),
                S("name:ColourList", "Colours", "Pick a colour, or change it everywhere it's used."),
                S("name:ViewBar", "View", "Zoom, the pixel grid, and the frame."),
                S(null, "Shortcuts", "Ctrl+S saves, Ctrl+Z undoes, Ctrl+Y redoes."));

            Add("PokedexGraphicsEditorView", "Pokédex graphics",
                S(null, "Pokédex graphics", "Both screens of a Pokédex page. Every piece can be edited."),
                S("toolbar", "Page", "The page and its state."),
                S("name:LayerList", "Layers", "Everything on the page, front first."),
                S("name:Screens", "Screens", "Both screens as the game draws them."),
                S("pick:LayerList>name:PartPanel", "Files", "The layer's files. Paint, export or import one."),
                S(null, "Shortcuts", "Ctrl+S saves, Ctrl+Z undoes, Ctrl+Y redoes."));

            Add("SummaryScreenEditorView", "Summary screen",
                S(null, "Summary screen", "Both screens of a summary page, with a sample Pokémon."),
                S("toolbar", "Page", "The page and its state."),
                S("name:LayerList", "Layers", "Everything on the page, front first."),
                S("pick:LayerList>name:PartPanel", "Files", "The layer's files. Paint, export or import one."),
                S("name:BoxPanel", "Text boxes and bars", "Where each text box and bar sits, in tiles."),
                S(null, "Shortcuts", "Ctrl+S saves, Ctrl+Z undoes, Ctrl+Y redoes."));

            Add("HpBarLayoutEditorView", "HP bars",
                S(null, "HP bars", "Where each part of a battle HP bar is drawn."),
                S("toolbar", "Bar", "The bar to lay out. Import PNG turns a picture into its art."),
                S("name:Board", "Bar", "Drag a part's box to move it."),
                S("name:PartList", "Parts", "Where each part starts, how long the gauges are, and which font the name uses."),
                S("name:BattlePreview", "In battle", "The bar on the battle screen."),
                S(null, "Shortcuts", "Ctrl+S saves, Ctrl+Z undoes, Ctrl+Y redoes."));

            Add("MoveBackgroundEditorView", "Move backgrounds",
                S("name:BackgroundList", "Backgrounds", "The backgrounds some moves show in battle."),
                S("name:FileNumbers", "Files", "Its drawing, colours and three screens."),
                S("name:PreviewPanel", "Preview", "Which of its screens to show."),
                S("toolbar", "Adding backgrounds", "In hg-engine projects, add or remove the last one."),
                S(null, "Shortcuts", "Ctrl+S saves, Ctrl+Z undoes, Ctrl+Y redoes."));
        }
    }
}
