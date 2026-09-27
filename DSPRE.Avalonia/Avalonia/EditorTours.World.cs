namespace DSPRE.Avalonia
{
    public static partial class EditorTours
    {
        static partial void RegisterWorld()
        {
            Add("MapEditorView", "Map Editor",
                S("type:NsbmdGlControl", "The map", "Drag to pan, right-drag to orbit, wheel to zoom. Turn on 🖌 Paint to paint squares straight onto the map, or ✋ Move buildings to drag them."),
                S("name:ViewModeBox", "What you edit", "One map, the whole matrix, or every map of this header at once."),
                S("toolbar", "Textures and overlays", "The second row picks this area's map and building textures and what is drawn over the map: collisions, square types or nothing."),
                S("tab:Permissions", "Permissions", "Where the player can walk, and what each square is: grass, water, ledges, doors. Pick a value, then paint on the grids or the map."),
                S("tab:Buildings", "Buildings", "Every building placed on this map. Pick one to move, turn or swap it; drag its handles in the 3D view."),
                S("tab:Area", "Area", "The light of this map's area and, in some games, its terrain animation and indoor setting. Every header in the same area shares them."),
                S("tab:Files / I/O", "Files", "Export this map's model, and bring in or save out its terrain, sound plates and movement permissions."),
                S("name:EditModelButton", "Changing the ground", "Build or reshape this map from tiles, or import a map made in Pokémon DS Map Studio."),
                S("name:SaveButton", "Saving", "Save keeps your edits; in its own window Ctrl+S does too. Closing with unsaved edits asks first."));

            Add("EventEditorView", "Event Editor",
                S("name:GlHost", "The map", "Every event on this header's maps. Click one to select it, or turn on ✋ Move and drag it. Drag to pan, right-drag to orbit, wheel to zoom."),
                S("name:ShowBar", "What's shown", "Hide kinds of events, switch to a flat 2D view, or centre the camera on the selected event."),
                S("tab:Overworlds", "People and objects", "Everyone and everything standing on the map. Pick one to set its sprite, script, movement and, for trainers, who they are."),
                S("tab:Warps", "Warps", "Doors and exits. Each one sends the player to a warp in another header; ↪ Go to destination opens it."),
                S("tab:Triggers", "Triggers", "Squares that run a script when stepped on, if a variable holds the right value."),
                S("tab:Spawnables", "Spawnables", "Signs, hidden items and other things the player reads or finds by facing a square."),
                S("name:EventTabs", "Jump to other editors", "Fields that show ↗ when you point at them lead to another editor: a person's trainer, a warp's destination. Right-click and pick Open, or Ctrl+click."),
                S("name:SaveButton", "Saving", "Save keeps your edits; in its own window Ctrl+S does too. Closing with unsaved edits asks first."));

            Add("HeaderEditorView", "Header Editor",
                S("list", "Headers", "Every place in the game, grouped by location. Search above the list, and right-click a header to open its map, events, scripts, text, wild Pokémon or matrix."),
                S("name:LocationGroup", "Name and type", "The name shown on screen when you arrive, the kind of place it is and, in some games, its area icon."),
                S("name:SoundGroup", "Music and weather", "Day and night music, the weather and the camera angle. Type a number or pick from the list."),
                S("name:LinkedFilesGroup", "Linked files", "The matrix, events, scripts, text and wild Pokémon this place uses. 🔗 Open takes you to each one."),
                S("name:MapSettingsGroup", "Map settings", "Whether the player can ride the bike, run, use an Escape Rope or fly here, plus phone and radio in some games."),
                S("name:HgssGroup", "Region settings", "Where the place sits on the town map, which Pokémon may follow you, Mom's call and whether it is Johto or Kanto."),
                S("name:PreviewRail", "Previews", "See the area icon, the weather and the camera angle you picked."),
                S("toolbar", "Tools", "Copy and paste a whole header, move headers in and out as files, or add and remove headers when the ROM allows it."),
                S("toolbar", "Saving", "Save keeps this header (Ctrl+S) and Reset goes back to the saved one. Ctrl+Z undoes, Ctrl+Y redoes."));

            Add("HeaderFieldsView", "Header tab",
                S("name:LocationGroup", "Name and type", "The name shown on screen when you arrive, the kind of place it is and, in some games, its area icon."),
                S("name:SoundGroup", "Music and weather", "Day and night music, the weather and the camera angle. Type a number or pick from the list."),
                S("name:LinkedFilesGroup", "Linked files", "The matrix, events, scripts, text and wild Pokémon this place uses. 🔗 Open takes you to each one."),
                S("name:MapSettingsGroup", "Map settings", "Whether the player can ride the bike, run, use an Escape Rope or fly here, plus phone and radio in some games."),
                S("name:HgssGroup", "Region settings", "Where the place sits on the town map, which Pokémon may follow you, Mom's call and whether it is Johto or Kanto."),
                S("name:PreviewRail", "Previews", "See the area icon, the weather and the camera angle you picked."),
                S(null, "Saving", "💾 Save header above the tabs keeps these fields, and ↺ Reset goes back to the saved header. Each other tab saves its own data."));

            Add("MatrixEditorView", "Matrix Editor",
                S("toolbar", "The matrix", "A matrix is the grid that joins maps into one seamless world. Pick one here."),
                S("name:LegendBar", "Colours", "Cells are coloured by the kind of place they belong to: town, route, cave and so on."),
                S("tab:Maps", "Maps", "Which map sits in each cell. With Paint off, click a cell to see its value and double-click it to open that map."),
                S("tab:Headers", "Headers", "Which header each cell belongs to, and so its name, music and events. With Paint off, click a cell to open its header."),
                S("tab:Heights", "Heights", "How high each map sits, so maps at different levels line up."),
                S("tab:Sections", "Sections", "Add a headers or heights section to a matrix that has none, which shows its tab."),
                S("name:PaintToggle", "Painting", "Turn on ✏ Paint, set the paint value on the tab, then click or drag across cells to fill them."),
                S("name:SpawnButton", "Starting point", "Makes the selected cell the place a new game begins."),
                S("toolbar", "Saving", "Save keeps the matrix; in its own window Ctrl+S does too. Import and Export move a matrix in and out as a file."));

            Add("AreaDataEditorView", "Area Data Editor",
                S("toolbar", "Area data", "An area sets the look shared by every header that uses it. Pick one here."),
                S("name:AreaFields", "Textures", "The texture sets the map and its buildings are drawn with. Every header using this area changes with them."),
                S("name:AreaFields", "Light and more", "The light type, and in some games the terrain animation, such as moving water, and whether the area is indoors."),
                S("toolbar", "Saving", "Save keeps your edits, and Undo and Redo step back and forward. In its own window Ctrl+S, Ctrl+Z and Ctrl+Y work too."));

            Add("CameraEditorView", "Camera Editor",
                S("list", "Camera angles", "Each row is a camera a header can use. Headers pick one by its number."),
                S("list", "Changing a camera", "Double-click a cell to change its distance, rotation, field of view or clipping. Some games also have offsets."),
                S("list", "One camera", "Export and Import at the end of a row move that single camera in and out as a file."),
                S("toolbar", "Saving", "Save keeps the table (Ctrl+S) and Discard throws your edits away. The table buttons move every camera at once."));

            Add("BuildingEditorView", "Building Editor",
                S("list", "Buildings", "Every building model in the game. Pick one to see it."),
                S("name:GlHost", "Preview", "The picked building in 3D. Drag to pan, right-drag to orbit, wheel to zoom."),
                S("toolbar", "Textures and sets", "Pick the texture set to view it with. Interior switches to indoor buildings in games that have them."),
                S("toolbar", "Saving", "Import replaces the picked building's model and Export saves it out. Save keeps imports (Ctrl+S), Discard undoes them."));

            Add("FlyEditorView", "Fly / Warp Editor",
                S("tab:Game-Over Warps", "After a loss", "Where the player wakes up after losing a battle: the header and the square to stand on."),
                S("tab:Fly Warps", "Fly", "Where the player lands when flying to each place."),
                S("tab:Unlock Settings", "Unlocking", "When each place opens up for Fly, and which ones are fly points or wake-up spots. The columns depend on the game."),
                S("toolbar", "Saving", "Save keeps every table (Ctrl+S) and Discard throws your edits away."));

            Add("SpawnEditorView", "Spawn Point Editor",
                S("name:SpawnHeaderRow", "Where you start", "The header a new game begins in, and which way the player faces."),
                S("name:CoordsGrid", "Exact square", "The matrix cell and the square inside that map where the player appears."),
                S("name:MoneyRow", "Starting money", "How much money the player has at the start."),
                S("toolbar", "Saving", "Save keeps it (Ctrl+S), Discard reloads the saved spawn. Show all headers brings back the full list when it was narrowed to one map."));

            Add("HeaderSearchView", "Advanced Header Search",
                S("name:QueryPanel", "What to search", "Pick a field such as music, weather or script file, then how to compare it."),
                S("name:ValueBox", "Value", "Type the value to look for, then Search. Tick Auto to search as you type."),
                S("name:ResultList", "Results", "Every header that matches your search."),
                S("name:ResultList", "Open one", "Double-click a result to open it in the Header Editor."));

            Add("DistortionWorldView", "Distortion World",
                S("name:FloorList", "Floors", "Each floor of the Distortion World. Pick one to edit it."),
                S("tab:Map>name:GlHost", "The map", "The floor in 3D. Drag to pan, right-drag to orbit, wheel to zoom."),
                S("tab:Map>name:MapViewBar", "View", "Change the camera, show one floor or the whole world, or colour squares by gravity. ▶ Animate plays it; ✏ Edit map model edits the map picked beside it."),
                S("tab:Map>name:SurfaceBox", "Surfaces", "Pick the ground or a wall, floor or ceiling surface to paint."),
                S("tab:Collision", "Collision", "Pick Walkable or Blocked and paint the grid for the chosen surface."),
                S("tab:Tiles", "Tiles", "Pick a square type and paint it onto the chosen surface."),
                S("tab:Gravity", "Gravity", "The surfaces the player walks on: which way each one faces, where it is and how big."),
                S("tab:Gravity changes", "Gravity changes", "Spots where the player flips onto another surface: where, how they move and where they land."),
                S("tab:Camera", "Camera", "Zones that turn the camera as the player walks through them."),
                S("tab:Fading props", "Fading props", "Objects that appear or vanish, grouped so a trigger can show them."),
                S("tab:Prop triggers", "Prop triggers", "Zones that show or hide a group of fading props when the player walks in facing a set way."),
                S("toolbar", "Saving", "Save keeps every change (Ctrl+S) and Discard throws them away."));

            Add("MapModelEditorView", "Map model editor",
                S("tab:Tiles>name:SetupBar", "Getting started", "Start with From map to use this map's own tiles, or Import a Pokémon DS Map Studio tileset, map or model. Apply builds the map from your tiles."),
                S("tab:Tiles>name:TilePalette", "Tiles", "Pick a tile to paint with, or search by name or texture."),
                S("tab:Tiles>name:Painter", "Painting", "Paint the map square by square. With the pen, right-click picks a tile and middle-click fills."),
                S("tab:Tiles>name:ToolStrip", "Tools", "Paint, erase, fill, lines, shapes and selections. Heights paints how high each square is; a selection can become a ramp or stairs."),
                S("tab:Tiles>name:LayerPanel", "Layers", "Tiles stack in layers. Pick one to paint on, hide it, or shift, copy and clear it."),
                S("tab:Smart", "Smart drawings", "Drawings that join their edges for you as you paint. + adds one and − removes it."),
                S("tab:Tile", "One tile", "Change the picked tile's size, material, offset and default collision."),
                S("tab:Shape>name:GlHost", "Shape", "Click a face of the model to pick it; Shift+click corners to build a new face."),
                S("tab:Shape>name:ShapePanel", "Editing faces", "Paint a face with a texture, delete or add faces, move corners, and set the walkable plates."),
                S("tab:Shape>name:ShapeBar", "Camera and files", "Turn the camera, undo, or bring the shape in and out as a model file."),
                S("name:DoneButton", "Done", "Done returns to the map. Save the map there to keep what you changed."));
        }
    }
}
