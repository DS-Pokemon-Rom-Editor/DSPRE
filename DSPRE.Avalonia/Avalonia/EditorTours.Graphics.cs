namespace DSPRE.Avalonia
{
    public static partial class EditorTours
    {
        static partial void RegisterGraphics()
        {
            Add("GraphicsBrowserView", "All graphics",
                S("name:SearchBox", "Every graphic", "Every picture in the game in one place. Type a name or number, like trainer or icon, to narrow the list."),
                S("name:CategoryList", "Kinds", "Pick a kind of graphic to see only those, or Everything to see them all."),
                S("name:ItemList", "The list", "Pick an entry to show it on the right."),
                S("name:PartsList", "Parts", "Some entries are made of several files, like a Pokémon's front and back. Pick the part to look at."),
                S("name:PreviewPanel", "Preview", "The picture as the game draws it, blown up so small ones are easy to read."),
                S("name:ActionsBar", "Out and back in", "Export PNG writes a PNG that keeps its colours in order, so it can be imported back. Import PNG replaces the picture."),
                S("name:ActionsBar", "Painting and more", "Paint opens it in the painter, and the Edit button opens the editor for everything else about it. Shiny shows shiny colours."));

            Add("ModelBrowserView", "All models and textures",
                S("name:SearchBox", "Every model", "Every 3D model, texture set and animation in the game. Type a name or number, like building or map, to narrow the list."),
                S("name:CategoryList", "Kinds", "Pick a kind to see only those, or Everything to see them all."),
                S("name:ItemList", "The list", "Pick an entry to view it."),
                S("name:GlHost", "Preview", "Drag to turn the model round and use the wheel to come closer. Texture sets show their pictures here instead."),
                S("name:SidePanel", "What it wears", "Pick the pictures, colours and movement to show it with. Buildings and scenery share pictures with a whole map, so you pick the set."),
                S("name:PlayBar", "Movement", "Play the chosen movement, or drag the slider to one frame."),
                S("name:FileButtons", "Import and export", "Export a model as DAE or glTF for other tools, or Export file for the entry exactly as it is. Import swaps in a file of the same kind or an OBJ mesh."));

            Add("BattleSceneBrowserView", "Battle scenes",
                S("name:SceneList", "Battle scenery", "Every set of battle scenery, with where it is used. Pick one to see it."),
                S("name:SceneChoices", "Ground and time", "Pick what the Pokémon stand on and the time of day. Each set is painted for day, evening and night."),
                S("name:ScenePreview", "Preview", "The scenery as a battle draws it."),
                S("name:SceneButtons", "Editing", "Paint the drawing opens it in All graphics. Change the colours opens the colours used at the time shown."),
                S("name:PlacesList", "Where it is used", "Open this to list every place that uses the picked scenery."),
                S(null, "Keeping changes", "Edits made from here go straight into the project. Save the ROM to keep them."));

            Add("BattleScreenEditorView", "Battle screen",
                S(null, "Battle screen", "Both screens of a battle. Every piece can be edited."),
                S("toolbar", "What is shown", "Pick the ground, the time of day, the text box style, the backdrop and which menu the touch screen shows."),
                S("name:PieceList", "Pieces", "Every piece of both screens. Click one here, or click it on either screen, to select it."),
                S("name:BothScreens", "The screens", "The top screen and the touch screen as a battle draws them. The selected piece is outlined."),
                S("name:PieceDetails", "Editing a piece", "Paint it, export it as a PNG or import one in its place. If other things share the piece, you are asked first."),
                S("name:SampleSection", "Sample", "Try a name, level, health and message to check your bars and text box. Nothing here is written to the ROM."),
                S(null, "Keeping changes", "Paints and imports go straight into the project. Save the ROM to keep them."));

            Add("BottomScreenEditorView", "Bottom screen",
                S("name:ScreenTabs", "Screens", "The touch screen outside battle. Pick which screen to edit here."),
                S("name:ScreenOptions", "What it shows", "Set the screen up as a player would see it: the Pokétch app, casing, backlight and clock, or the menu's busy look. Play runs an app's animation."),
                S("name:PieceList", "Pieces", "Every piece the screen is built from. Pick one to edit it."),
                S("name:ScreenPreview", "Preview", "The screen as the game draws it, with your changes."),
                S("name:PieceDetails", "Editing a piece", "Paint it, export it as a PNG or import one. Edit animation opens a moving piece in the Cell Animation editor."),
                S("name:ColourSwatches", "Colours", "Click a colour to change it. A note warns you when other pieces share these colours."));

            Add("ScreensEditorView", "Screens editor",
                S("name:ScreenList", "Screens", "Pick a screen."),
                S("name:ScreenViews", "Views", "Preview shows a line on the screen and its pieces. Layout moves its text."));

            Add("TitleScreenEditorView", "Title screen",
                S("name:PieceList", "Pieces", "Pick a piece to import or export it."),
                S("name:SaveButtons", "Version", "Version picks whose title screen you edit, HeartGold's or SoulSilver's."));

            Add("DungeonCutinEditorView", "Dungeon cut-in editor",
                S("name:CutinList", "Splash screens", "The cut-ins that flash up when you enter certain places. Pick one to edit it."),
                S("name:LocationCard", "Location", "Zone picks the place that shows this cut-in, and Name line picks the text drawn as its name. The game never uses Wipe type."),
                S("name:TimesGrid", "Times of day", "A picture each for morning, noon, evening and night, imported or exported as a PNG. The numbers under it pick its colours, picture and layout."),
                S("name:CutinToolbar", "Table and graphics", "Export CSV and Import CSV move the whole table in and out as a spreadsheet. Open in All graphics shows all four drawings together."));

            Add("TrainerCardEditorView", "Trainer card",
                S("name:RankBar", "Rank", "The card's colours for each rank."),
                S("name:PieceList", "Pieces", "Pick a piece to import or export it."));

            Add("BannerEditorView", "Game icon & banner",
                S(null, "Game icon and banner", "The icon and titles the DS system menu shows for your game."),
                S("name:IconRow", "Icon", "A 32 by 32 picture with up to 15 colours plus see-through. Import a PNG to replace it, or export the current one."),
                S("name:TitlesList", "Menu titles", "The text under the icon, one box per language. Usually the game's name, then the publisher, each on its own line."));

            Add("BtxEditorView", "Overworld editor",
                S("list", "Overworlds", "Every overworld sprite: the people, Pokémon and objects seen on the maps. Pick one to see it."),
                S("name:SpritePreview", "Preview", "The whole sprite sheet, and beside it each facing standing, walking and running."),
                S("name:SpriteButtons", "Pictures", "Import PNG replaces the sprite sheet and Export PNG saves it. Shiny palette shows the shiny colours when there are any."),
                S("name:PropertiesPanel", "How it is drawn", "In Diamond, Pearl and Platinum, pick whether it is drawn flat or as a model, and its shadow, footprints and reflection."),
                S("name:EntryButtons", "New overworlds", "With the overworld expansion patch applied, add new entries here, or delete ones you added."),
                S("name:SpriteButtons", "Saving", "Save selected writes the one you're on and Save all writes every changed one; Discard drops them. Ctrl+S saves them all, Ctrl+Z undoes and Ctrl+Y redoes."));

            Add("NsbtxEditorView", "Map & building textures",
                S("toolbar", "Map or building", "Switch between the texture packs maps use and the ones buildings use."),
                S("name:PackList", "Packs", "Every texture pack. Add copies the first pack onto the end, and Remove last deletes the last one."),
                S("name:TextureList", "Textures", "The pictures inside the chosen pack. Pick one to preview it."),
                S("name:PaletteList", "Colours", "The colour sets in the pack. The one matching the texture's name is picked for you, but you can pick another."),
                S("name:TexturePreview", "Preview", "The chosen texture drawn in the chosen colours."),
                S("toolbar", "Import and export", "Export saves the whole chosen pack as a file, and Import replaces the chosen pack with one."));

            Add("CellAnimationEditorView", "Cell animation editor",
                S("toolbar", "Cell Animation", "Edit a moving sprite: which drawing each frame shows and for how long. The top line says which animation this is."),
                S("name:SequenceList", "Sequences", "Each sequence is one animation in the file. Go to jumps to one by its number."),
                S("name:SequenceButtons", "Adding sequences", "Add one on the end or remove the last. They can't be reordered, since the game finds them by number."),
                S("name:AnimationPreview", "Preview", "Play runs the animation at game speed. For a Pokétch app you can show it inside the app's own screen."),
                S("name:FramesPanel", "This sequence", "Plays sets once, looping or backwards, and Back to picks the frame a loop returns to."),
                S("name:FramesPanel", "Frames", "Each frame picks a drawing and how long it holds, in sixtieths of a second; some also turn, stretch or move it. Add after and Remove change the frames."),
                S("name:PiecesPanel", "Pieces", "Move the pieces a drawing is built from, across and down. The preview follows straight away."));

            Add("ParticleLibraryView", "Particles",
                S("name:ParticleList", "Particles", "Every particle effect in the game: move animations, ball seals, evolution sparkles and more."),
                S("name:CategoryPicker", "Kinds", "Show only one kind of effect, like move animations or Poké Ball bursts."),
                S("name:ParticleSearch", "Search", "Find an effect by its name or file number."),
                S("name:OpenButton", "Open", "Open the picked effect in the Particle editor, or double-click it in the list."));

            Add("ParticleEditorView", "Particle editor",
                S(null, "Particle editor", "Edit one particle effect: how its particles are made, move and look, with a live preview."),
                S("name:EmitterPicker", "Emitters", "An effect is made of emitters, each sending out particles. Pick one to edit, and tick Only this emitter to hide the rest."),
                S("name:FieldsPanel", "Settings", "The emitter's settings, grouped by what they control. Type in the filter box to find one, and click a colour to change it."),
                S("name:ParticlePreview", "Preview", "The effect playing as you edit. Replay starts it again."),
                S("name:TextureList", "Textures", "The pictures the particles are drawn with. Export saves one as a PNG and Replace puts a PNG in its place."));

            Add("BallCapsuleEditorView", "Ball capsules",
                S("name:CapsulePicker", "Capsules", "The seals on a trainer's Poké Ball, played as the Pokémon is sent out. The line beside it names the trainers who use it."),
                S("name:SealList", "Seals", "Every seal, with a search box above. Drag one onto the capsule, or pick it and press Place or double-click it."),
                S("name:BoardCanvas", "The capsule", "Drag placed seals to move them, and right-click one to remove it. Clear in the top bar takes them all off."),
                S("name:SealButtons", "Seal looks", "Edit sticker opens the seal's picture in All graphics. Edit particles opens its effect in the Particle editor."),
                S("name:SendOutPanel", "Send-out preview", "Pick a Pokémon and press the send-out button to watch the capsule play in battle. You can also pick the side and text speed."));

            Add("TilesetBuilderView", "Picture to background",
                S("toolbar", "Picture to Background", "Turn a PNG into a background the game can draw: its colours, its picture pieces and their layout. Open a PNG to start."),
                S("name:PictureArea", "Before and after", "Your picture as it is, and as the game would draw it. Any difference is what the screen can't hold."),
                S("name:OptionsPanel", "How it is drawn", "Pick sixteen banks of sixteen colours, or one list of 256. Keep the first colour clear so see-through parts stay see-through."),
                S("name:OptionsPanel", "What it comes to", "How many colours and tiles it needs, and why it won't fit when it doesn't."),
                S("name:SavePanel", "Saving", "Save the three files writes them side by side, ready for All graphics."));

            Add("GraphicPainterView", "Painter",
                S(null, "Painter", "Paint a graphic pixel by pixel. The line at the top says what it is and how big."),
                S("name:CanvasHost", "The picture", "Click or drag to paint with the picked colour. Hold Ctrl and use the wheel to zoom."),
                S("name:ColourList", "Colours", "Click a colour to paint with it. Change this colour alters the colour itself, and every pixel using it changes too."),
                S("name:ViewBar", "View", "Zoom in and out, turn on the pixel grid, and pick a frame when the file holds several pictures."),
                S("name:PainterButtons", "Saving", "Save writes it back into the project; save the ROM to keep it. Ctrl+S saves and Ctrl+Z undoes."));

            Add("PokedexGraphicsEditorView", "Pokédex graphics",
                S(null, "Pokédex graphics", "Both screens of a Pokédex page, drawn from this ROM, with every piece editable."),
                S("toolbar", "Page", "Pick the page to show, and its state where it has more than one."),
                S("name:LayerList", "Layers", "Everything drawn on the page, front first. Pick one here, or click it on a screen."),
                S("name:Screens", "The screens", "The top screen and the touch screen as the game draws them."),
                S("pick:LayerList>name:PartPanel", "Its files", "The files the picked layer is made from. Paint opens one in the painter, and Export and Import swap it for a PNG."));

            Add("SummaryScreenEditorView", "Summary screen",
                S(null, "Summary screen", "Both screens of a summary page, drawn from this ROM with a sample Pokémon."),
                S("toolbar", "Page", "Pick the page to show, and its state where it has more than one."),
                S("name:LayerList", "Layers", "Everything drawn on the page, front first. Pick one here, or click it on a screen."),
                S("pick:LayerList>name:PartPanel", "Its files", "The files the picked layer is made from. Paint opens one in the painter, and Export and Import swap it for a PNG."),
                S("name:BoxPanel", "Text boxes and bars", "Where the game puts each text box and the HP and EXP bars, in tiles. Pick one to outline it on the screen."));

            Add("HpBarLayoutEditorView", "HP bars",
                S(null, "HP bars", "Where each part of a battle HP bar is drawn: the name, level, HP numbers and gauge, status and the rest."),
                S("toolbar", "Which bar", "Pick the bar to lay out. Import PNG turns a picture of the bar as it should look in battle into its art."),
                S("name:Board", "The bar", "Every part is a box on the bar. Drag a box to move that part."),
                S("name:PartList", "Parts", "The same parts by name, with the column and row each one starts at."),
                S("name:BattlePreview", "In battle", "The bar on the battle screen as it will look, and where the whole bar sits on the screen."));

            Add("MoveBackgroundEditorView", "Move backgrounds",
                S("name:BackgroundList", "Backgrounds", "The backgrounds some moves put up during a battle. Pick one to see it."),
                S("name:FileNumbers", "Its files", "The drawing, colours and the three screens it uses, by file number."),
                S("name:PreviewPanel", "Preview", "Pick which of its screens to show."),
                S("toolbar", "Adding backgrounds", "In an hg-engine project, Add and Remove last change how many backgrounds there are."));
        }
    }
}
