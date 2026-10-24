namespace DSPRE.Avalonia
{
    public static partial class EditorTours
    {
        static partial void RegisterWorld()
        {
            Add("MapEditorView", "Map editor",
                S("type:NsbmdGlControl", "Map", "Drag to pan, right-drag to orbit, wheel to zoom. Paint and Move buildings edit it."),
                S("name:ViewModeBox", "View", "One map, the whole matrix, or every map of this header."),
                S("toolbar", "Textures and overlays", "The area's textures, and the collision or square type overlay."),
                S("tab:Permissions", "Permissions", "Where the player can walk and what each square is."),
                S("tab:Buildings", "Buildings", "Every building on this map. Drag its handles in 3D."),
                S("tab:Area", "Area", "The area's light, terrain animation and indoor setting, shared by its headers."),
                S("tab:Files", "Files", "Export the model, or import and export terrain, sound plates and permissions."),
                S("name:EditModelButton", "Ground", "Build the map from tiles, or import a map or model."),
                S(null, "Shortcuts", "Arrow keys nudge a building. Ctrl+S saves, Ctrl+Z undoes, Ctrl+Y redoes."));

            Add("EventEditorView", "Event editor",
                S("name:GlHost", "Map", "Click an event to pick it. Move drags events. Drag pans, wheel zooms."),
                S("name:ShowBar", "View", "Hide kinds of events, switch to 2D, or centre on the picked event."),
                S("tab:Overworlds", "People and objects", "Everyone on the map, with their sprite, script and movement."),
                S("tab:Warps", "Warps", "Doors and exits. Go to destination opens the other side."),
                S("tab:Triggers", "Triggers", "Squares that run a script when a variable matches."),
                S("tab:Spawnables", "Spawnables", "Signs, hidden items and things read by facing a square."),
                S("name:EventTabs", "Linked editors", "Ctrl+click a trainer or warp target to edit it in its editor."),
                S(null, "Shortcuts", "Arrows nudge, Delete removes, Ctrl+D copies an event. Ctrl+S saves, Ctrl+Z undoes, Ctrl+Y redoes."));

            Add("HeaderEditorView", "Header editor",
                S("list", "Headers", "Every place, by location. Right-click one to open its other parts."),
                S("name:LocationGroup", "Name and type", "The arrival name, the kind of place and its area icon."),
                S("name:SoundGroup", "Music, weather and camera", "Music, weather, camera and battle background."),
                S("name:LinkedFilesGroup", "Linked files", "The files this place uses. Open goes to each."),
                S("name:MapSettingsGroup", "Map settings", "Bike, run, Escape Rope, Fly, and the HeartGold and SoulSilver extras."),
                S("toolbar", "Tools", "Copy, paste, import, export, add or remove headers."),
                S(null, "Shortcuts", "Ctrl+S saves, Ctrl+Z undoes, Ctrl+Y redoes."));

            Add("HeaderFieldsView", "Header tab",
                S("name:LocationGroup", "Name and type", "The arrival name, the kind of place and its area icon."),
                S("name:SoundGroup", "Music, weather and camera", "Music, weather, camera and battle background."),
                S("name:LinkedFilesGroup", "Linked files", "The files this place uses. Open shows each in its tab."),
                S("name:MapSettingsGroup", "Map settings", "Bike, run, Escape Rope, Fly, and the HeartGold and SoulSilver extras."),
                S(null, "Saving", "These fields save with the toolbar. Other tabs save separately."));

            Add("MatrixEditorView", "Matrix editor",
                S("toolbar", "Matrix", "The grid that joins maps into one world."),
                S("name:LegendBar", "Colours", "Cells by kind of place, and heights from low to high."),
                S("tab:Maps", "Maps", "The map in each cell. Double-click to edit it in the Map editor."),
                S("tab:Headers", "Headers", "The header for each cell. Click to edit it in the Header editor."),
                S("tab:Heights", "Heights", "How high each map sits."),
                S("name:PaintToggle", "Painting", "Turn on Paint, set the value, then click or drag cells."),
                S("name:SpawnButton", "Starting point", "Makes the picked cell where a new game begins."),
                S("toolbar", "Files", "Import or export a matrix."),
                S(null, "Shortcuts", "Ctrl+S saves, Ctrl+Z undoes, Ctrl+Y redoes."));

            Add("AreaDataEditorView", "Area data editor",
                S("toolbar", "Area data", "The look shared by every header in an area."),
                S("name:AreaFields", "Textures", "The map and building texture sets."),
                S("name:AreaFields", "Light and more", "Light type, terrain animation and indoor setting."),
                S(null, "Shortcuts", "Ctrl+S saves, Ctrl+Z undoes, Ctrl+Y redoes."));

            Add("CameraEditorView", "Camera editor",
                S("list", "Camera angles", "Each camera a header can pick by number."),
                S("list", "Changing a camera", "Double-click a cell to change it."),
                S("list", "One camera", "The row's Export and Import move one camera."),
                S("toolbar", "Whole table", "Export or import every camera."),
                S(null, "Shortcuts", "Ctrl+S saves, Ctrl+Z undoes, Ctrl+Y redoes."));

            Add("BuildingEditorView", "Building editor",
                S("list", "Buildings", "Every building model."),
                S("name:GlHost", "Preview", "Drag to pan, right-drag to orbit, wheel to zoom."),
                S("toolbar", "Textures and sets", "The texture set, and indoor buildings where the game has them."),
                S("toolbar", "Models", "Import or export the picked model."),
                S(null, "Shortcuts", "Ctrl+S saves, Ctrl+Z undoes, Ctrl+Y redoes."));

            Add("FlyEditorView", "Fly / warp editor",
                S("tab:Whiteout warps", "After a loss", "Where the player wakes after losing."),
                S("tab:Fly warps", "Fly", "Where the player lands for each place."),
                S("tab:Unlock settings", "Unlocking", "When each place opens for Fly, and its flags."),
                S(null, "Shortcuts", "Ctrl+S saves, Ctrl+Z undoes, Ctrl+Y redoes."));

            Add("SpawnEditorView", "Spawn point editor",
                S("name:SpawnHeaderRow", "Start", "The header a new game begins in, and the facing."),
                S("name:CoordsGrid", "Exact square", "The matrix cell and square the player appears on."),
                S("name:MoneyRow", "Starting money", "The player's money at the start."),
                S("toolbar", "All headers", "Shows the full header list again."),
                S(null, "Shortcuts", "Ctrl+S saves, Ctrl+Z undoes, Ctrl+Y redoes."));

            Add("HeaderSearchView", "Advanced header search",
                S("name:QueryPanel", "Field", "The field to search and how to compare it."),
                S("name:ValueBox", "Value", "The value to look for. Auto searches as you type."),
                S("name:ResultList", "Results", "Every matching header."),
                S("name:ResultList", "Editing", "Double-click a result to edit it in the Header editor."));

            Add("DistortionWorldView", "Distortion World",
                S("name:FloorList", "Floors", "Each Distortion World floor."),
                S("tab:Map>name:GlHost", "Map", "Drag to pan, right-drag to orbit, wheel to zoom."),
                S("tab:Map>name:MapViewBar", "View", "Camera, one floor or all, and gravity colours."),
                S("tab:Map>name:SurfaceBox", "Surfaces", "The surface to paint."),
                S("tab:Collision", "Collision", "Paint walkable or blocked squares."),
                S("tab:Tiles", "Tiles", "Paint square types."),
                S("tab:Gravity", "Gravity", "Each walkable surface's facing, place and size."),
                S("tab:Gravity changes", "Gravity changes", "Where the player flips onto another surface."),
                S("tab:Camera", "Camera", "Zones that turn the camera."),
                S("tab:Fading props", "Fading props", "Objects that appear or vanish, in groups."),
                S("tab:Prop triggers", "Prop triggers", "Zones that show or hide a group of props."),
                S(null, "Shortcuts", "Ctrl+S saves, Ctrl+Z undoes, Ctrl+Y redoes."));

            Add("MapModelEditorView", "Map model editor",
                S("tab:Tiles>name:SetupBar", "Getting started", "Start from this map's tiles or an import. Apply builds the map."),
                S("tab:Tiles>name:TilePalette", "Tiles", "Pick a tile, or search by name or texture."),
                S("tab:Tiles>name:Painter", "Painting", "Right-click picks a tile, middle-click fills."),
                S("tab:Tiles>name:ToolStrip", "Tools", "Paint, erase, fill, shapes, heights, ramps and stairs."),
                S("tab:Tiles>name:LayerPanel", "Layers", "Pick, hide, shift, copy or clear a layer."),
                S("tab:Smart", "Smart drawings", "Drawings that join their edges as you paint."),
                S("tab:Tile", "Tile", "Size, material, offset and default collision."),
                S("tab:Shape>name:GlHost", "Shape", "Click a face to pick it. Shift+click corners for a new face."),
                S("tab:Shape>name:ShapePanel", "Faces", "Texture, add, delete and move faces, and walkable plates."),
                S("tab:Shape>name:ShapeBar", "Camera and files", "Turn the camera, undo, or import and export the shape."),
                S("name:DoneButton", "Done", "Back to the map. Save it there."));
        }
    }
}
