# DSPRE 3.0 User Changelog

*Changes since [2.3](https://github.com/DS-Pokemon-Rom-Editor/DSPRE/blob/main/Changelogs/CHANGELOG_2.3_User.md).*

---

## 3.0

DSPRE 3.0 is a rebuilt interface. The editing itself works the way you already know it, but the shell
around it is new, it runs on Linux as well as Windows, and a lot of things that used to need a
separate window or a manual step are now part of the main view.

This file is a work in progress while 3.0 is being finished.

### The application itself

- DSPRE now runs on Linux as well as Windows, from the same codebase.
- New interface throughout, with light and dark themes that follow whichever you pick.
- A welcome screen with your recent projects, and a guided tour that walks through the main parts of
  the program the first time you open it.
- Press Ctrl+P anywhere to jump straight to any editor by typing its name, instead of hunting through
  the menus.
- Opening a header loads everything attached to it at once, so its scripts, events, encounters, text
  and the rest are already there when you switch tabs.
- Long-running work shows what it is doing instead of freezing the window, so you can tell the
  difference between "busy" and "stuck".
- Editors remember unsaved changes and say so before you lose them.
- Dropdown labels that used to be hardcoded can be renamed or added to, per project or globally.

### Maps and events

- Maps render in 3D, with the whole matrix stitched together so you can see a header's maps in place
  rather than one at a time.
- Buildings can be moved with a drag handle in the 3D view.
- Overworld sprites and event markers are drawn in the scene, so events sit where they actually are.
- The event editor has a 2D button for a flat top-down view with no perspective, the way the old
  editor looked, and it still shows every map in the header at once. The choice is remembered.
- Map permissions can be painted either straight onto the 3D map or on a flat grid, whichever suits.
- Camera controls and speed are configurable.

### Editors that are new in 3.0

- Move animation editing: a battle script editor with a storyboard view, plus a battle message editor.
- Trainer sprite editor, trainer card editor, trainer class editor, and a button to add a new trainer
  class.
- Title screen editor and dungeon cut-in editor.
- Game icon and banner editor.
- Fly destination editor and Vs. Seeker rematch editor.
- Encounter editors for Trophy Garden, Safari Zone, Bug Contest, Great Marsh, Honey Trees and
  Headbutt trees.
- Bulk editors for learnsets, TM/HM machines and trainer AI flags, plus a tool to reorder Pokémon.
- Ground item script editor.
- Sprite import and export wizards, and a palette colour editor with favourites.
- Research helper, address helper, header search, and a validation pass that finds broken references
  and tells you which headers use a given file.
- Character map manager, custom script command manager, and a script command reference.
- hg-engine support: link a checkout, edit its data alongside the ROM's, and compile from inside
  DSPRE.
- ROM patch toolbox for ARM9 expansion, matrix and header expansion, BDHCam and the rest.
- Audio editor: everything the ROM can play, in one window, with a tab each for Pokémon cries, music,
  fanfares and sound effects. The lists are built from the sound archive's own names, so what you see
  is what the game calls things. Search runs across all four tabs at once. Anything you pick is drawn
  as a waveform you can click to play from, with play, stop and loop, and can be saved out as a WAV.
  A fifth tab lists the sounds the music and the sound effects are actually made of, and any of those
  can be replaced with a WAV of your own, as can a cry. The music, fanfares and sound effects
  themselves cannot: they are written-out notes played on those sounds rather than sounds of their
  own.
- A Pokémon's cry can now be played, exported and replaced from the Pokémon editor as well.

### Fixes

- Fixed the DPPt Wild Pokémon Editor naming every encounter file after the wrong place. The list was
  built from the encounter file's own number instead of the location stored on the map header, which
  are unrelated, so the entry labelled Route 201 was really some other area. Editing and saving from
  that entry wrote to whatever area actually owned it.
- Fixed event markers and overworld sprites vanishing from the 3D view after switching to another
  tab and back. They only came back if you unticked and reticked the checkbox for them.
- Fixed the Sprites tab claiming a Pokémon's own default form shares its Personal Data with the base
  Pokémon. It only means anything for a form that has no entry of its own, so Deoxys and friends were
  showing a note that did not apply to them.
- Fixed building a ROM from a project that was extracted by a different DSPRE version. config.yaml
  now carries the padding fields every bundled ds-rom version expects, so the same project builds in
  both this and the 2.x releases.
- The Trainer Flag Bulk and Vs. Seeker editors no longer freeze the window while they open. They read
  a lot of files, so they now say what they are doing while they do it.
- Fixed every Game Boy Sounds track playing as silence in DSPRE's own previews. HeartGold plays that
  music on the console's own square-wave and noise generators rather than from recorded samples, and
  nothing here knew how to make that sound. 218 of the game's tunes and effects were affected.
- Fixed a Pokémon's cry being played back as silence. The note it is written with has no length, and
  a note with no length is never stopped, so it sounds until its sample runs out.
- Fixed two moves reading as garbage in the move animation editor. Iron Tail and Volt Tackle both use
  a command whose number DSPRE had wrong by one, so both stopped decoding a third of the way in and
  showed nonsense from there on. Every one of the 501 move scripts in both Platinum and HeartGold now
  reads right through to its end.
- The animation preview was checked against both real games for the first time, by running Platinum and
  HeartGold in an emulator and measuring the screen frame by frame. Leer darkens the screen in both, Tackle darkens
  it in neither, and Tackle moves the attacker about as far in both. A whole HeartGold turn was
  captured, four animations across 864 frames, and nothing in it flashes in either the game or the
  preview. The preview runs animations shorter than the game's own on-screen sequence, which is
  expected: the game's includes the message and the damage that follow, and a move's animation script
  covers neither.
- Fixed four moves whose animation preview never ended: Harden, Iron Tail, Metal Claw and Iron
  Defense left their overlay running for ever, so the preview never reported the move as finished.
- The move animation editor has three ways of reading a script, and remembers which you last used.
  Guided groups the commands by what part of the move they belong to. Script puts one command a line in
  columns with loops and subroutine bodies indented. Raw shows every word exactly where it sits in the
  ROM, with its number and its hex. Clicking any line explains it underneath, including where in the
  games that explanation came from.
- Scripts now show the shorthands they were actually written in. One line of the games' own source was
  being shown as seventeen commands, and it appears in 425 of the 501 moves, so two of every five
  things on screen were boilerplate nobody wrote. Move scripts are now about 40% shorter to read
  without a single byte changing.
- Routines and targets are named instead of numbered. A command that shook "264" now says it shakes
  the defender, and every routine shows its name. You can rename any routine to whatever you call it,
  per project or everywhere, and all three views follow.
- Move animations now play the Pokémon's cry where the move asks for it, stop a sound where the move
  stops one, show and hide the dropped copies of a Pokémon, and follow a background whose speed changes
  part way through. All four were being skipped.
- Two moves that flash the screen white while darkening the attacker now do so.
- Every command either game uses is now either carried out or says on screen that it is not, instead
  of a few of them being passed over without a word.
- The move animation editor now says when a move does something the preview cannot draw, instead of
  quietly leaving it out. Two moves fade a layer that is not drawn here and one swings a second set of
  particles; all three now say so under the preview.
- Two status overlays that were doing nothing at all, the one for getting health back and the one for
  turning metallic, now play.
- Move animation commands that are handed fewer values than they use now behave the way the games do,
  which is to treat the missing ones as zero and carry on rather than skip the command.
- The move animation editor's preview column scrolls when the window is short. Its controls and the
  storyboard used to be cut off at the bottom with nothing to show they were there.
- Fixed screen shakes hitting the wrong Pokémon. The command that shakes a sprite names its targets as
  a set, and 28 of its uses name a partner that only exists in a double battle, where the real game
  shakes nobody. The preview was shaking the defender instead.
- Fixed Weather Ball's animation preview taking the wrong branch. The command that picks an animation
  by the weather always jumps, and DSPRE was carrying on past it instead.
- Fixed the crash reporter taking the whole application down with it. When a background task failed
  with nobody waiting on it, the reporter tried to put a dialog up from a thread that cannot own a
  window, and that second failure closed DSPRE. Those are now written to a crash report instead.
- Fixed the move animation editor's first tab being an empty box on every archive except the move
  animations. It now says which archive the reading views are for and which tabs to use instead.
- The move animation editor opens wider and the command list keeps the space it needs, so command
  values are no longer cut off at the edge of the pane. A drag handle between the list and the preview
  lets you give either side more room.
- Fixed the guided view showing "Where it ends" in the middle of a move. A move with two animations
  carries a spare ending early in the file, and the headings were coming out in the order they were
  first seen.
- The guided view no longer files setting up a command's values under timing, and branches between
  versions of a move now have a heading of their own instead of sitting among the waits.
- The raw view's explanation panel now names the routine a call runs, what each of its values means
  and the file and line it was read from, the same as the other two views. It was showing only a
  general note about the command.
- A target flag now reads as who it hits in the explanation panel rather than as a number.
- Fixed the summary beside a battle effect running off the right-hand edge behind a scrollbar, and
  sitting halfway down the panel instead of at the top.
- Fixed the way one move moves a particle emitter up or down. It was swinging back and forth for ever
  instead of travelling once between the Pokémon and the top of the screen, and two of the six values
  it takes were written down wrongly.
- Fixed settings that the games give names to showing as bare numbers in the two readable views. That
  was 7,650 values in each game, covering where an effect is anchored, which way it points, what it
  follows and which camera it uses.
- The two readable views leave out settings that are switched off instead of printing them as "None".
  That was the longest line in either game, and it cut the lines too wide to read at the editor's
  normal size from one in twenty to about one in seventy. The raw view still shows every word.
- Fixed Pokemon being flung off the side of the screen during a move and left there. Three of the
  movement effects added the sprite's whole position every frame to an offset that nothing clears, so a
  charge that should carry it 40 pixels and bring it back walked it 480 pixels off a 256 pixel screen
  and left it there for the rest of the move. Megahorn was the worst of them.
- The HP numbers in the animation preview sit inside the gauge box instead of running over its bottom
  edge, and the HP bar is now the two rows the game draws rather than four, in the game's own green,
  with the white edges and the black empty channel it has. All measured off a real battle.
- The move animation preview can be checked against the real game move by move. Seventy-seven moves,
  chosen so that between them they use every command, every setting and every effect routine the two
  games have, are staged into a real battle and recorded frame by frame, with the preview recorded
  beside them.
- The move animation preview starts on a real battle background and real grass, instead of on black
  with placeholder platforms. A battle always has both, and on black every move that swaps the
  background looked brighter than it does in the game. Confusion, Earthquake and Magnitude all read
  wrong that way. Both are still yours to change from the two dropdowns.
- Noted, not yet built: Secret Power never plays its own animation in the game. The game looks at the
  ground being fought on and plays another move's animation instead, Mud-Slap on gravel, Needle Arm on
  grass, Rock Throw in a cave, and so on. The preview plays Secret Power's own, which is a short burst of
  orange sparks, so the two do not match and cannot until the preview can be told which ground to assume.
- Fixed sixteen moves whose effects were drawn in the wrong place in the animation preview. One of the six
  ways a move can anchor an effect means "leave it where the move's own data puts it", and DSPRE was moving
  those onto the defending Pokemon instead. Blizzard showed a plain field where the game shows an icy wash,
  and Sweet Scent's petals were in the wrong place. Both look right now.
- The move animation preview is now checked against the real game for every way a move can place its
  particles. There are six places a move can tie an effect to and ten shapes it can throw particles in,
  counted from all 501 scripts and 485 particle archives in each game, and each one now has a recording of
  the real battle beside the preview. Placement comes out broadly right; what differs is how the particles
  look and, in two moves so far, a background change the preview does not draw.
- Found, not yet fixed: the animation preview's Pokemon-rotation effect ignores everything the move tells
  it. Peck asks for the Pokemon being pecked to tip ten degrees for two frames; the preview rocks the
  attacking Pokemon eighteen degrees for sixteen frames instead. The move animation report lists this and
  four other places where the preview does not draw what the move says.
- The HP bar in the animation preview uses the game's own amber and red, and draws the black row under
  the bar that the frame graphic leaves brown. All three bar colours and every row of both gauges were
  read off real battles.
- The move animation preview says when a particle's picture could not be read, instead of quietly drawing
  a plain dot in its place. A plain dot looks like a real effect, so a move could look finished when part
  of it was missing. No particle in either game fails to load today; this is for ones you add or edit.
- The move animation preview can be checked for a particle it cannot draw. Every particle texture in the
  game is decoded and counted, 1,437 in Platinum and 1,438 in HeartGold across 971 archives, and a check
  fails if one of them stops decoding. There is also a new note listing how big every particle in the game
  is meant to be, which is what a particle drawn at the wrong size has to be checked against.
- The move animation preview can be checked for a background it cannot draw. Every background any move
  asks for is now read out of both games and drawn, 501 scripts each, 49 in Platinum and 50 in HeartGold,
  and a check fails if one of them stops working.
- Script commands are named the way the script editor names them. DSPRE was reading the older database,
  where command 0 is "Nop" while the editor, the formatter and the language server all call it "Noop";
  1,961 of 2,412 commands across the three games were showing a name that does not exist in the editor.
  Scripts written before the rename still open.

### Additions

- A new Graphics window under the Graphics menu lists every 2D graphic in the game in one place, over six
  thousand of them, sorted into tabs by what they are for, and you can search it. Entries are listed under
  the name the game gives them, so a Pokemon's battle sprites read as "VENUSAUR, front, male" rather than
  as a file number. Picking one shows it. Anything that cannot be shown says why in
  plain words instead of being blank. Every entry can be saved: as a PNG that keeps its numbered colours,
  so it can be put straight back, or as the file exactly as it sits in the ROM. Where a picture can be put
  back in, it can, with a size check first. Where it cannot, the reason is on the button.
- A new Models and textures window, also under the Graphics menu, lists every model, texture set and
  animation in the game, over a thousand of them, sorted into tabs, and can show any model in three
  dimensions with its textures on. Buildings and map scenery take their pictures from a set shared by a
  whole map, so the model itself does not say which one belongs to it; the window offers the choice, and
  changing it repaints the model. A model can be saved as a Collada or glTF file that other 3D programs
  open, with whichever pictures are showing, and anything at all can be saved exactly as it sits in the
  ROM. Things that are not models, like animations and texture sets, say what they are and why there is
  nothing to show, rather than leaving an empty box.
- Any drawing the Graphics window can show can now be painted. The brush lays down a colour number, which
  is what the pixels really hold, and the colours themselves can be changed: change one and every pixel
  using it changes with it, which the window says out loud rather than leaving you to discover. Undo is
  there. An edit that could not be written back is refused before it is made, with the reason, so nothing
  is half done.
- Pokemon battle sprites can now be viewed and painted like any other graphic. These are stored with their
  pixels scrambled, so anything reading them straight got coloured static; they are unscrambled on the way
  in and scrambled again on the way out. The one thing that cannot be changed is the top-left four pixels
  of a sprite, because those two bytes are what the scrambling is keyed from. They are empty in every
  sprite in the game.
- Party icons show in the right colours. Every icon shares one set of colours and each Pokemon picks its
  own bank out of it, which was not being read, so every icon was drawn in the first Pokemon's colours.
  Venusaur came out yellow.
- Party icons show at their real size. Most of these files do not record their own shape, and laying their
  pixels out in one long row reported a 32 by 64 icon as 256 by 8.
- Battle backgrounds, move effect drawings, fonts and location banners can be edited now. These are kept
  squeezed down in the ROM and used to be refused for want of a way to squeeze them back; every archive
  the Graphics window lists can now be written to.
- The painter has a pixel grid, with a stronger line every eight pixels because that is how the games
  store them, and zooms from one to thirty two times over with Ctrl and the wheel.
- A file that holds more than one picture can be painted one picture at a time. A party icon is two
  frames of the walking animation stacked up and a battle sprite is two side by side; both now offer the
  frames separately, with the whole strip still there to pick.
- Item icons are listed under the item that uses them, read from the game's own table, and take their
  colours from the entry that table names rather than the nearest one.
- Models can be given a movement and played. Buildings keep their movements in a separate archive and do
  not record which one is theirs, so it is offered as a choice. A movement that leaves a model where it
  was says so rather than looking broken.
- Music, fanfares and sound effects can be saved as MIDI files that other music programs open. The notes
  and their timing are exact. The instruments are not: the games play these on samples kept in the ROM,
  which no MIDI can carry, so the file names an instrument number and the program you open it in picks
  its own sound for it.
- The Audio Editor draws the notes of whatever is picked, time running across and pitch running up, one
  colour per track, with the playhead following the music. Click it anywhere to play from there.
- The Graphics window lists a whole thing per row instead of one file per row. A Pokemon's battle sprites
  are six files and a trainer is five, so those now sit under one row with the pieces offered above the
  picture: back and front, the colours, the shiny colours. Searching a Pokemon's name finds it once
  instead of seven times.
- Pokemon battle sprites are drawn in the right colours. All four sprites of a Pokemon share one set and
  the only other set is the shiny one, but the colours were being found by looking for the nearest set in
  the archive, which picked the PREVIOUS Pokemon's shiny colours for both back sprites. Neighbouring
  Pokemon are often close in colour, so it looked plausible; Venusaur's back was drawn in Ivysaur's shiny
  green. Every back sprite in every game was affected.
- A model opens on the movement the game actually gives it. The games carry a table saying which movement
  each building uses, so a windmill turns and a door opens without hunting through the whole archive.
  Everything else is still there to try on it, and the window says which of the two you are looking at.
- Alternate form sprites sit with the Pokemon they belong to. That archive is not laid out in runs: a
  form's two drawings sit near the front and its two sets of colours a hundred files further on, so they
  were listed as unrelated files. They are grouped and named now, so Wormadam's three forms read as
  Wormadam - Plant, Sandy and Trash rather than as numbers.
- A Shiny box shows any Pokemon in its shiny colours. It is the same drawing either way; only which of
  its two sets of colours is used changes, which is all shiny means in these games.
- Every archive that has a shape now lists things rather than files. A battle backdrop is one row with
  its drawing, its three sets of colours for day, evening and night, and the arrangement every backdrop
  shares. A patch of battle ground is one row with both sides and its three sets of colours. A move
  effect is one row that reaches into four archives for its drawing, colours, layout and timing. A
  Pokemon's party icon carries its alternate forms' icons with it.
- Battle backdrops and battle ground look like themselves. A backdrop's tiles are a heap of pieces until
  the arrangement puts them in order, and both were being painted with whichever palette happened to sit
  nearest in the archive, so the grass came out black and white.
- The tabs are split finer: Pokemon sprites, Pokemon icons, Trainers, Battle scenery, Battle screen,
  Move effects, Items, Fonts, Text boxes and Places, rather than five headings that each held everything.
- Sets of pictures are listed under the name of the first picture in them, so the overworld archive turns
  from four hundred numbered sets into babyboy1, beachboy, campboy and the rest. Searching an NPC by name
  finds it.
- Models are listed under the names their own files carry. A building is gym_wall01 or gym01_stage
  rather than the six hundredth "Buildings, outside", and searching those names finds them.
- The Pokemon Sprite Editor, the Trainer Sprite Editor and the Item Editor can hand what they are showing
  straight to the Graphics window, which opens on that exact drawing rather than on a list of six
  thousand. Items do not sit in their icon archive in item order, so that one goes through the game's own
  table.
- Building animations work in Diamond, Pearl and Platinum. Those games write a shorter record in the
  animation list than HeartGold does, and the reader required the longer one, so it rejected every entry
  and no building in those three games animated anywhere in DSPRE.

- The move animation preview can show the second animation of a move that has two. The games alternate
  them by the battle's turn count, and 22 moves in each game have a second one that could not be seen
  or edited here before. The choice only appears on moves that have one.
- The party icon preview can show either of the icon's two animation frames instead of only the
  first.
- The update prompt shows the release notes for the new version, formatted, instead of only the
  version numbers.
- The Evolutions tab tells you when an alternate form has no evolutions of its own and that they
  belong on the base Pokémon.

### Graphics

- Added a Graphics window under Graphics, All graphics, listing every 2D graphic in the ROM across
  the archives DSPRE knows how to read, with a search box and a picture of whatever is selected.
- Graphics are sorted into tabs rather than one long list: Pokémon sprites, Pokémon icons, Trainers,
  Battle scenery, Battle chrome, Move effects, Items, Text and fonts, Windows, and Places.
- Things that belong to one graphic are now one row with parts under it instead of separate entries.
  A Pokémon's row holds its four poses, its normal and shiny colours and its animation, an item's row
  holds its drawing and its colours, and a battle backdrop's row holds its drawing, its layout and
  its colours for each time of day.
- Rows are named with the game's own names where the game has one. Items, Pokémon, Trainer classes,
  moves and models read as names rather than file numbers.
- Alternate forms are attached to the Pokémon they belong to rather than sitting in a separate list
  of unnamed files, for both battle sprites and party icons. Forms that share one drawing are shown
  once rather than repeated.
- Added a Shiny tick box for anything that has a second set of colours. Entries with only one set
  explain why the box is off instead of leaving it dead.
- Added painting. Any indexed graphic can be drawn on pixel by pixel, and the colours themselves can
  be changed, so a graphic is not stuck with the palette it shipped with.
- Fixed Pokémon battle sprites showing as coloured noise. The games store these scrambled and DSPRE
  was not unscrambling them.
- Fixed Pokémon battle sprites being drawn in the wrong colours. The four poses of one Pokémon share
  one set of colours and a separate shiny set, and DSPRE was picking the nearest palette in the file,
  which handed most Pokémon the previous Pokémon's shiny colours. This affected 988 sprites per game.
- Fixed party icons being drawn in the wrong colours, which come from a per-species table in the ROM
  rather than from the icon file.
- Text box frames are now grouped and named the way the games name them, so each of the twenty text
  box styles is one row with its own drawing and colours rather than fifty loose files. The colours
  sit after the drawings rather than beside them, so pairing them by position would have put the
  wrong colours on every style.
- Text box frames now draw in their own colours instead of solid black, because each style's row now
  knows which set of colours belongs to it.
- Fixed the Graphics window keeping the first game's idea of where the colours are in an archive
  after a second game was opened. In HeartGold the text box colours start one file later than in
  Platinum, so opening HeartGold second showed every text box style in the wrong colours.

- The location splash screens are now named after the places they belong to, and the two screens
  that two places share are one row naming both rather than two rows claiming the same files.

### Weather, fonts and banners

- The fonts are now named from the games' own list: the system font, the dialogue font, the button
  font, the touch screen font and the width tables that go with them, instead of eight numbers.
- The location banner archive is grouped into its drawings with the colours and arrangements that
  belong to each, three of them in Platinum and one in HeartGold.
- The map screen overlay is deliberately left ungrouped. That name points at two different archives:
  in Diamond, Pearl and Platinum it is the weather, and in HeartGold and SoulSilver it is something
  else entirely, so one set of names cannot be right for both and guessing would put the wrong name
  on things.

### Models and textures

- Added a Models window under Graphics, All models and textures, listing every 3D model in the ROM
  with its textures applied rather than bare, and the model's own name from inside the file.
- Models are grouped with the textures and animations that belong to them, and a model that has an
  animation of its own plays it by default.
- Fixed animated models sliding across the screen instead of moving their parts, caused by
  re-centring the model on every frame of the animation.
- Fixed buildings never animating in Diamond, Pearl and Platinum. Those games store the animation
  record in 20 bytes where HeartGold and SoulSilver use 24, and DSPRE only read the longer one.
- Movements are listed by name instead of by number. Every one of them carries a name written by
  whoever built it, and DSPRE was already reading those and throwing them away: door_op, door_cl,
  gym01_lift. All 89 in HeartGold and all 32 in Platinum have one.
- Building animations in Diamond, Pearl and Platinum now report whether they wait to be set off. That
  was only read for HeartGold and SoulSilver because nobody had traced what the other games do with
  the field, and both games turn out to test the same bit. A second flag, whether something has to
  put the animation on the map in the first place, was not read for either game and now is. The
  time-of-day animation really is HeartGold and SoulSilver only, so that one stays off elsewhere.
- Added putting a file in. A finished NSBMD, NSBTX or animation file can now be put in place of any
  entry, which is what the tools that build these models produce. The kind is checked first, so a set
  of pictures cannot be dropped into a model's slot.
- An OBJ or a glTF still cannot be turned into a model. The window says so plainly rather than
  offering a button that fails: a model in these games is a list of drawing commands for the DS's own
  hardware, and DSPRE cannot write that list.

### Battle graphics

- The battle screen is now grouped and named instead of being a run of numbered files. Every entry of
  the battle furniture archive is named from the games' own index list, so the HP bar, the box with
  the Pokémon's name in it, the six balls, the type badges, the message frame and the platforms can
  be found by name.
- Split into tabs: Battle HP bars, Battle icons, Battle platforms and Battle screen, with rows in
  file order.
- A row shows the thing assembled rather than its loose tiles, using the layout the game uses, so an
  HP bar looks like the box you see in a battle.
- Fixed the assembled picture being built from the wrong drawing. DSPRE looked for the nearest
  drawing to a layout, and the nearest one is the previous thing's, so every gauge was put together
  out of the wrong pieces.
- The thrown ball graphics are named from the ROM's own item names, so renaming a ball in the Item
  Editor renames it here too. Where two of them share one drawing, both names are shown.
- A whole picture can now be painted as it appears and put back into the pieces it is drawn from,
  both for an assembled sprite and for a whole background. An HP bar is edited as the box you see and
  a battle backdrop as the backdrop, rather than as a heap of loose tiles in no particular order.
- Putting a background back says how many squares changed and how many of them are drawn from a piece
  used elsewhere, because those places changed too. Backgrounds reuse their pieces to save room, so
  that is the format working as intended rather than something going wrong, and it is better said
  than discovered later.
- Checked that editing a squeezed-down graphic writes it back squeezed and holding the same picture.
  Nothing in these games keeps an archive inside another archive, so there is no second layer to
  worry about: 5,211 entries in HeartGold and 4,248 in Platinum were looked at across every archive
  DSPRE opens, flat and 3D, and none of them holds one.
- Named the four spare entries that look like HP bars and name boxes but that nothing in the games
  draws, so they are no longer mistaken for the real ones.
- Added the battle furniture archive to Diamond and Pearl, which DSPRE never read at all. That is 279
  more graphics reachable in those games.

### Event Editor

- An apricorn tree now shows an Apricorn bed field with an explanation, instead of the trainer's
  Sight range field being hidden on it. They are the same byte, the engine's param0, and on a tree it
  picks which apricorn bed the tree reads. That is why editing it changes the apricorn's colour: the
  colour is not stored in the map at all, the game asks the save what is growing in that bed. The
  sprite placed in the map is only what the tree looks like before the game replaces it.

### Battle scenes

- Added a Battle scenes window under Graphics, listing every set of battle scenery in the game
  together with the places that fight on it, since a backdrop is half a graphic and half a number in
  a map header. The ground and the time of day can be changed to see the scene as it is in play.
- Battle scenery no place uses is still listed, so scenery is not invisible just because it is spare.
- Places are named with the name the game shows the player where DSPRE can read it, and with the
  internal code where it cannot, rather than showing a name that belongs to somewhere else.

### Moving between editors

- The Graphics window and the ordinary editors now hand off to each other in both directions. The
  Sprite Editor, Trainer Sprite Editor, Item Editor, Trainer Card Editor, Dungeon Cutin Editor and
  Battle scenes can send a graphic to the Graphics window to be painted, and the Graphics window has
  a button back to whichever editor owns the numbers behind the graphic showing.

### Audio

- Added an Audio Editor under Audio, listing the music, sound effects and Pokémon cries in the ROM,
  with playback and a waveform.
- Added a note track showing the notes of a sequence as they play.
- Added exporting a sequence to a MIDI file.
- Fixed MIDI export stopping after eight seconds, which was the preview limit leaking into the
  export. This affected 311 sequences in HeartGold and 196 in Platinum.
- Fixed tracks that use the DS's square and noise channels playing silently, which had left 218 of
  the Game Boy sound effects inaudible.
- Added a Sounds tab listing the sounds the music and the sound effects are actually made of, which
  nothing in DSPRE could reach before: 608 of them in HeartGold and 380 in Platinum, including the
  instruments the tunes play and the noises behind the sound effects. Each one can be heard, saved
  as a WAV and replaced with one.
- Replacing a sound warns first that anything else playing it will play the new one, since the games
  share them.
- Fixed replacing a sound rebuilding every other sound in the same set, which squeezed them all a
  second time and lost a little of each. Sounds nobody changed are now written back untouched. This
  affected cries too in principle, and would have quietly degraded all 158 instruments in the shared
  set every time one of them was changed.
- Fixed the Fanfares tab being empty in Diamond, Pearl and Platinum. The tabs were split on the
  names the sound archive gives its sequences, and those games name none of their fanfares the way
  HeartGold does. They are now split on which player the game hands each sequence to, which is what
  the game itself goes by, and Platinum's 20 fanfares are listed.
- The note track now says why it is empty when a cry or a sound is picked, instead of asking you to
  pick something when you already have.
