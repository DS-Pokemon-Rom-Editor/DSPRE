using System;
using System.Collections.Generic;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia.Data
{
    public sealed class BattleAnimFunc
    {
        public int Id;
        public string Name = "";
        public string Summary = "";

        /// <summary>Short card labels for the routine's values, from the third value on.</summary>
        public string[] Labels = Array.Empty<string>();

        public string Source = "";

        public string[] Words = Array.Empty<string>();
    }

    public static class BattleAnimFuncs
    {
        private const string Flag = "who it acts on (a target flag)";
        private const string OwnWay = "which of the routine's ways of doing it";

        private static readonly string[] EmitMove =
        {
            "which emitter to move",
            "how far across past the target it ends up",
            "how far down past the target it ends up",
            "how many frames to wait before starting",
            "how many frames the move takes",
            "how high the arc goes",
            "0 from the attacker toward the defender, 1 the other way",
            "packed: the low half caps the move at that many frames, the high half skips that many frames at the start",
            "how much the path curves",
        };

        // The straight line reads the arc's word but never uses it.
        private static readonly string[] StraightMove = WithBlank(EmitMove, 5);

        private static string[] WithBlank(string[] words, int at)
        {
            string[] copy = (string[])words.Clone();
            copy[at] = "";
            return copy;
        }

        private static readonly BattleAnimFunc[] All =
        {
            new BattleAnimFunc { Id = 0, Name = "Unused", Summary = "Unused.", Source = "BattleAnimScriptFunc_Nop" },
            new BattleAnimFunc { Id = 1, Name = "Unused", Summary = "Unused.", Source = "BattleAnimScriptFunc_AnimExample" },
            new BattleAnimFunc { Id = 2, Name = "Unused", Summary = "Unused.", Source = "BattleAnimScriptFunc_SoundExample" },
            new BattleAnimFunc { Id = 3, Name = "Unused", Summary = "Unused.", Source = "BattleAnimScriptFunc_GenericExample" },

            new BattleAnimFunc { Id = 4, Name = "Turn", Summary = "Turns a Pokémon on the spot.", Labels = new[] { "Start angle", "End angle", "Frames", "Mode", "Centre across", "Centre down" },
                Source = "BattleAnimScriptFunc_RotateMon",
                Words = new[] { "angle to start at", "angle to end at", "how many frames the turn takes",
                                "0 turns the defender, 1 the attacker around the point given below, 2 the defender the other way",
                                "how far across it turns around", "how far down it turns around" } },

            new BattleAnimFunc { Id = 5, Name = "Strength squash", Summary = "Squashes the attacker down.", Labels = new[] { "Squash %", "", "Frames", "" },
                Source = "BattleAnimScriptFunc_Strength",
                Words = new[] { "how far down to squash, as a percentage", "", "how many frames the squash takes", "" } },

            new BattleAnimFunc { Id = 6, Name = "Bulk Up", Summary = "Only Bulk Up uses this.", Labels = new[] { "" }, Source = "BattleAnimScriptFunc_BulkUp", Words = new[] { "" } },
            new BattleAnimFunc { Id = 7, Name = "Double Team", Summary = "Only Double Team uses this.", Labels = new[] { "" }, Source = "BattleAnimScriptFunc_DoubleTeam", Words = new[] { "" } },
            new BattleAnimFunc { Id = 8, Name = "Quick Attack", Summary = "Only Quick Attack uses this.", Source = "BattleAnimScriptFunc_QuickAttack" },
            new BattleAnimFunc { Id = 9, Name = "Drill Peck", Summary = "Only Drill Peck uses this.", Source = "BattleAnimScriptFunc_DrillPeck" },

            new BattleAnimFunc { Id = 10, Name = "Spin", Summary = "Spins a Pokémon.", Labels = new[] { "Turns", "Frames per turn", "Battler" },
                Source = "BattleAnimScriptFunc_Submission",
                Words = new[] { "how many times it goes round", "how many frames each turn takes", "which battler it spins" } },

            new BattleAnimFunc { Id = 11, Name = "Confusion", Summary = "Only Confusion uses this.", Source = "BattleAnimScriptFunc_Confusion" },
            new BattleAnimFunc { Id = 12, Name = "Acid Armor", Summary = "Only Acid Armor uses this.", Source = "BattleAnimScriptFunc_AcidArmor" },
            new BattleAnimFunc { Id = 13, Name = "Growth", Summary = "Only Growth uses this.", Source = "BattleAnimScriptFunc_Growth" },
            new BattleAnimFunc { Id = 14, Name = "Meditate", Summary = "Only Meditate uses this.", Source = "BattleAnimScriptFunc_Meditate" },
            new BattleAnimFunc { Id = 15, Name = "Teleport", Summary = "Only Teleport uses this.", Source = "BattleAnimScriptFunc_Teleport" },
            new BattleAnimFunc { Id = 16, Name = "Flash", Summary = "Whitens the background and darkens the attacker, then brings both back.",
                Source = "BattleAnimScriptFunc_Flash" },
            new BattleAnimFunc { Id = 17, Name = "Night Shade attacker", Summary = "Only Night Shade uses this.", Source = "BattleAnimScriptFunc_NightShadeAttacker" },
            new BattleAnimFunc { Id = 18, Name = "Night Shade target", Summary = "Only Night Shade uses this.", Source = "BattleAnimScriptFunc_NightShadeDefender" },
            new BattleAnimFunc { Id = 19, Name = "Splash", Summary = "Only Splash uses this.", Source = "BattleAnimScriptFunc_Splash" },
            new BattleAnimFunc { Id = 20, Name = "Spite", Summary = "Only Spite uses this.", Source = "BattleAnimScriptFunc_Spite" },
            new BattleAnimFunc { Id = 22, Name = "Minimize", Summary = "Only Minimize uses this.", Labels = new[] { "" }, Source = "BattleAnimScriptFunc_Minimize", Words = new[] { "" } },
            new BattleAnimFunc { Id = 23, Name = "Faint Attack", Summary = "Only Faint Attack uses this.", Source = "BattleAnimScriptFunc_FaintAttack" },
            new BattleAnimFunc { Id = 24, Name = "Earthquake", Summary = "Only Earthquake uses this.", Labels = new[] { "" }, Source = "BattleAnimScriptFunc_Earthquake", Words = new[] { "" } },

            new BattleAnimFunc { Id = 25, Name = "Hops", Summary = "Makes a Pokémon hop.", Labels = new[] { "Style" }, Source = "BattleAnimScriptFunc_PlayfulHops", Words = new[] { OwnWay } },
            new BattleAnimFunc { Id = 26, Name = "Nightmare", Summary = "Only Nightmare uses this.", Labels = new[] { "Style" }, Source = "BattleAnimScriptFunc_Nightmare", Words = new[] { OwnWay } },

            new BattleAnimFunc { Id = 27, Name = "Flail shake", Summary = "Shakes a Pokémon.", Labels = new[] { "Style", "Across", "Down", "Frames per shake", "Shakes", "Target" },
                Source = "BattleAnimScriptFunc_Flail",
                Words = new[] { "0 for one way of shaking, anything else for the other",
                                "how far it moves across", "how far it moves down",
                                "how many frames each shake takes", "how many shakes", Flag } },

            new BattleAnimFunc { Id = 28, Name = "Magnitude", Summary = "Only Magnitude uses this.", Labels = new[] { "" }, Source = "BattleAnimScriptFunc_Magnitude", Words = new[] { "" } },
            new BattleAnimFunc { Id = 29, Name = "Return", Summary = "Only Return uses this.", Source = "BattleAnimScriptFunc_Return" },
            new BattleAnimFunc { Id = 30, Name = "Vital Throw", Summary = "Only Vital Throw uses this.", Source = "BattleAnimScriptFunc_VitalThrow" },
            new BattleAnimFunc { Id = 31, Name = "Swagger", Summary = "Only Swagger uses this.", Source = "BattleAnimScriptFunc_Swagger" },
            new BattleAnimFunc { Id = 32, Name = "Memento", Summary = "Only Memento uses this.", Source = "BattleAnimScriptFunc_Memento" },

            new BattleAnimFunc { Id = 33, Name = "Background fade", Summary = "Fades the background toward a colour and back.", Labels = new[] { "Palette", "Frames per step", "Start strength", "End strength", "Colour" },
                Source = "BattleAnimScriptFunc_FadeBg",
                Words = new[] { "which palette set: 0 the backdrop, 1 the first effect layer, 2 the second",
                                "how many frames each step of the fade takes",
                                "how strong it starts, out of 16", "how strong it ends, out of 16",
                                "the colour to fade toward" } },

            new BattleAnimFunc { Id = 34, Name = "Colour flash", Summary = "Flashes a Pokémon a colour.", Labels = new[] { "Target", "Frames per step", "Flashes", "Colour", "Strength", "Hold" },
                Source = "BattleAnimScriptFunc_FadeBattlerSprite",
                Words = new[] { Flag, "how many frames each step of the fade takes", "how many times it flashes",
                                "the colour to flash", "how strong the flash gets, out of 16",
                                "how many frames it holds at full strength" } },

            new BattleAnimFunc { Id = 35, Name = "Copy size", Summary = "Grows and shrinks a Pokémon copy.", Labels = new[] { "Side", "Opacity", "Start size", "End size", "Size divisor", "Times", "Frames per step", "Copy" },
                Source = "BattleAnimScriptFunc_ScalePokemonSprite",
                Words = new[] { "0 for the attacker's copy, anything else for the defender's",
                                "how see-through it is, out of 16", "the size it starts at", "the size it ends at",
                                "what to divide those two sizes by", "how many times it grows and shrinks",
                                "how many frames each step takes", "which of the four dropped copies" } },

            new BattleAnimFunc { Id = 36, Name = "Shake", Summary = "Shakes a Pokémon, a copy or the background.", Labels = new[] { "Across", "Down", "Frames per shake", "Shakes", "Target" },
                Source = "BattleAnimScriptFunc_Shake",
                Words = new[] { "how far it moves across, in pixels", "how far it moves down, in pixels",
                                "how many frames each shake takes", "how many shakes", Flag } },

            new BattleAnimFunc { Id = 37, Name = "Extrasensory", Summary = "Only Extrasensory uses this.", Source = "BattleAnimScriptFunc_Extrasensory" },

            new BattleAnimFunc { Id = 38, Name = "Copy fade", Summary = "Fades Pokémon copies in or out.", Labels = new[] { "Copies", "Start opacity", "End opacity", "Behind start", "Behind end", "Frames" },
                Source = "BattleAnimScriptFunc_AlphaFadePokemonSprite",
                Words = new[] { "which of the four dropped copies, one bit each",
                                "how solid the copy starts", "how solid it ends",
                                "how solid what is behind it starts", "how solid that ends",
                                "how many frames the fade takes" } },

            new BattleAnimFunc { Id = 40, Name = "Hide", Summary = "Hides or shows a Pokémon.", Labels = new[] { "Target", "Hide" },
                Source = "BattleAnimScriptFunc_HideBattler",
                Words = new[] { Flag, "0 to show it, anything else to hide it" } },

            new BattleAnimFunc { Id = 41, Name = "Fake Out curtain", Summary = "Only Fake Out uses this.", Source = "BattleAnimScriptFunc_FakeOutCurtain" },

            new BattleAnimFunc { Id = 42, Name = "Squash and stretch", Summary = "Squashes and stretches a Pokémon.", Labels = new[] { "Target", "Start width", "End width", "Start height", "End height", "Size divisor", "Times and hold", "Frames per step" },
                Source = "BattleAnimScriptFunc_ScaleBattlerSprite",
                Words = new[] { Flag, "the width it starts at", "the width it ends at",
                                "the height it starts at", "the height it ends at",
                                "what to divide those sizes by",
                                "packed: the low half is how many times, the high half is how many frames it holds",
                                "how many frames each step takes" } },

            new BattleAnimFunc { Id = 43, Name = "Fake Out", Summary = "Only Fake Out uses this.", Source = "BattleAnimScriptFunc_FakeOut" },

            new BattleAnimFunc { Id = 44, Name = "Scrolling background", Summary = "Scrolls a background behind the battle.", Labels = new[] { "Background", "Start across", "Start down", "Speed across", "Speed down", "Flip for foe", "Opacity", "Slowdown frames" },
                Source = "BattleAnimScriptFunc_ScrollCustomBg",
                Words = new[] { "which background to use", "how far across it starts", "how far down it starts",
                                "how fast it moves across", "how fast it moves down",
                                "whether to turn it around when the enemy is attacking",
                                "how solid it is", "how many frames between slowdowns" } },

            new BattleAnimFunc { Id = 45, Name = "Muddy Water", Summary = "Scrolls a background behind the battle.", Labels = new[] { "Background", "Start across", "Start down", "Speed across", "Speed down", "Flip for foe", "", "Slowdown frames" },
                Source = "BattleAnimScriptFunc_MuddyWater",
                Words = new[] { "which background to use", "how far across it starts", "how far down it starts",
                                "how fast it moves across", "how fast it moves down",
                                "whether to turn it around when the enemy is attacking",
                                "", "how many frames between slowdowns" } },

            new BattleAnimFunc { Id = 47, Name = "Megahorn attacker", Summary = "Only Megahorn uses this.", Source = "BattleAnimScriptFunc_MegahornAttacker" },
            new BattleAnimFunc { Id = 48, Name = "Megahorn target", Summary = "Only Megahorn uses this.", Source = "BattleAnimScriptFunc_MegahornDefender" },

            new BattleAnimFunc { Id = 49, Name = "Surf wave", Summary = "Draws Surf's wave.", Labels = new[] { "Style" }, Source = "BattleAnimScriptFunc_Surf", Words = new[] { OwnWay } },

            new BattleAnimFunc { Id = 50, Name = "Blink", Summary = "Blinks a Pokémon in and out.", Labels = new[] { "Blinks", "Frames per blink" }, Source = "BattleAnimScriptFunc_BlinkAttacker",
                Words = new[] { "how many times it blinks (the routine doubles this)", "how many frames each blink takes" } },

            new BattleAnimFunc { Id = 51, Name = "Slide", Summary = "Slides a Pokémon sideways and back.", Labels = new[] { "Frames", "Distance", "Target" }, Source = "BattleAnimScriptFunc_MoveBattlerX",
                Words = new[] { "how many frames the slide takes", "how far it goes across", Flag } },

            new BattleAnimFunc { Id = 52, Name = "Slide 2", Summary = "Slides a Pokémon sideways and back.", Labels = new[] { "Frames", "Distance", "Target" }, Source = "BattleAnimScriptFunc_MoveBattlerX2",
                Words = new[] { "how many frames the slide takes", "how far it goes across", Flag } },

            new BattleAnimFunc { Id = 53, Name = "Shake and stretch", Summary = "Shakes and stretches the attacker, then holds.", Labels = new[] { "First stretch", "Second stretch", "First frames", "Second frames", "Hold", "" }, Source = "BattleAnimScriptFunc_ShakeAndScaleAttacker",
                Words = new[] { "the first stretch", "the second stretch", "how many frames the first takes",
                                "how many frames the second takes", "how many frames to hold before coming back", "" } },

            new BattleAnimFunc { Id = 55, Name = "Camouflage", Summary = "Only Camouflage uses this.", Source = "BattleAnimScriptFunc_Camouflage" },

            new BattleAnimFunc { Id = 56, Name = "Superpower glow", Summary = "Puts a glow around the attacker.", Labels = new[] { "", "" }, Source = "BattleAnimScriptFunc_Superpower",
                Words = new[] { "", "" } },

            new BattleAnimFunc { Id = 57, Name = "Slide and back", Summary = "Slides a Pokémon and brings it back.", Labels = new[] { "Frames", "Across", "Down", "Target" }, Source = "BattleAnimScriptFunc_MoveBattler",
                Words = new[] { "how many frames the slide takes", "how far it goes across", "how far it goes down", Flag } },

            new BattleAnimFunc { Id = 58, Name = "Mimic", Summary = "Only Mimic uses this.", Source = "BattleAnimScriptFunc_Mimic" },
            new BattleAnimFunc { Id = 59, Name = "Shadow Punch", Summary = "Only Shadow Punch uses this.", Labels = new[] { "" }, Source = "BattleAnimScriptFunc_ShadowPunch", Words = new[] { "" } },

            new BattleAnimFunc { Id = 60, Name = "Circle", Summary = "Swings a Pokémon in a circle.", Labels = new[] { "Target", "Turns", "Frames per turn" }, Source = "BattleAnimScriptFunc_RevolveBattler",
                Words = new[] { Flag, "how many times it goes round", "how many frames each turn takes" } },

            new BattleAnimFunc { Id = 61, Name = "Slide off screen", Summary = "Slides a Pokémon off the screen.", Labels = new[] { "Target", "Frames" }, Source = "BattleAnimScriptFunc_MoveBattlerOffScreen",
                Words = new[] { Flag, "how many frames it takes" } },

            new BattleAnimFunc { Id = 62, Name = "Back in place", Summary = "Puts a Pokémon back in its place.", Labels = new[] { "Target" }, Source = "BattleAnimScriptFunc_MoveBattlerToDefaultPos",
                Words = new[] { Flag } },

            new BattleAnimFunc { Id = 63, Name = "Copy colour fade", Summary = "Fades Pokémon copies toward a colour.", Labels = new[] { "Copies", "Frames per step", "Step size", "Start strength", "End strength", "Colour" },
                Source = "BattleAnimScriptFunc_FadePokemonSprite",
                Words = new[] { "which of the four dropped copies, one bit each", "how many frames each step takes",
                                "how much each step changes it", "how strong it starts", "how strong it ends",
                                "the colour to fade toward" } },

            new BattleAnimFunc { Id = 65, Name = "Emitter line", Summary = "Moves an emitter in a straight line.", Labels = new[] { "Emitter", "Across", "Down", "Start wait", "Frames", "", "Direction", "Frame window", "Curve" },
                Source = "BattleAnimScriptFunc_MoveEmitterA2BLinear", Words = StraightMove },

            new BattleAnimFunc { Id = 66, Name = "Emitter arc", Summary = "Moves an emitter along an arc.", Labels = new[] { "Emitter", "Across", "Down", "Start wait", "Frames", "Arc height", "Direction", "Frame window", "Curve" },
                Source = "BattleAnimScriptFunc_MoveEmitterA2BParabolic", Words = EmitMove },

            new BattleAnimFunc { Id = 67, Name = "Wipe", Summary = "Wipes a Pokémon in or out.", Labels = new[] { "Target", "", "", "Step", "Frames per step", "Sketch style" },
                Source = "BattleAnimScriptFunc_BattlerPartialDraw",
                Words = new[] { Flag, "", "", "how far the edge moves each step, its sign choosing in or out",
                                "how many frames between steps", "1 to draw it the way Sketch does" } },

            new BattleAnimFunc { Id = 68, Name = "Background shake", Summary = "Shakes the background.", Labels = new[] { "Across", "Down", "Frames per shake", "Shakes", "Repeats", "Background frame" }, Source = "BattleAnimScriptFunc_ShakeBg",
                Words = new[] { "how far it moves across", "how far it moves down", "how many frames each shake takes",
                                "how many shakes", "how many extra times to run the whole thing",
                                "0 for one background frame, anything else for the other" } },

            new BattleAnimFunc { Id = 69, Name = "Pixelate", Summary = "Breaks a Pokémon copy into blocks and back.", Labels = new[] { "Copy", "Step", "Block width", "Block height" }, Source = "BattleAnimScriptFunc_PixelatePokemonSprite",
                Words = new[] { "which of the four dropped copies",
                                "how much to change the block size each step, negative to go back to none",
                                "block size across", "block size down" } },

            new BattleAnimFunc { Id = 70, Name = "Role Play", Summary = "Only Role Play uses this.", Labels = new[] { "" }, Source = "BattleAnimScriptFunc_RolePlay", Words = new[] { "" } },
            new BattleAnimFunc { Id = 71, Name = "Snatch", Summary = "Only Snatch uses this.", Labels = new[] { "Target" }, Source = "BattleAnimScriptFunc_Snatch", Words = new[] { Flag } },

            new BattleAnimFunc { Id = 72, Name = "Emitter circle", Summary = "Swings an emitter around a Pokémon.", Labels = new[] { "Emitter", "Start angle across", "End angle across", "Start angle down", "End angle down", "Width", "Height", "Frames", "Centre", "Particle set" },
                Source = "BattleAnimScriptFunc_RevolveEmitter",
                Words = new[] { "which emitter to move", "its starting angle across, in degrees",
                                "its ending angle across, in degrees", "its starting angle down, in degrees",
                                "its ending angle down, in degrees", "how wide the circle is",
                                "how tall the circle is", "how many frames the swing takes",
                                "0 to swing around the attacker, anything else around the defender",
                                "which set of particles to swing" } },

            new BattleAnimFunc { Id = 73, Name = "Emitter drop", Summary = "Moves an emitter down onto a Pokémon or up away from it.", Labels = new[] { "Emitter", "Battler", "Direction", "Frames", "Start wait", "Frame window" },
                Source = "BattleAnimScriptFunc_MoveEmitterViewportTop",
                Words = new[] { "which emitter to move", "0 uses the attacker's position, anything else the defender's",
                                "0 comes down onto the Pokémon from above the screen, anything else rises away from it",
                                "how many frames the move takes", "how many frames to wait before starting",
                                "packed: the low half caps the move at that many frames, the high half skips that many frames at the start" } },

            new BattleAnimFunc { Id = 74, Name = "Grayscale", Summary = "Drains the colour from the scene, or puts it back.", Labels = new[] { "Drain" },
                Source = "BattleAnimScriptFunc_SetBgGrayscale",
                Words = new[] { "0 to put the colours back, anything else to drain them" } },

            new BattleAnimFunc { Id = 75, Name = "Copy layering", Summary = "Sets where a Pokémon copy is drawn.", Labels = new[] { "Copy", "Frames", "Background layer", "Sprite order", "Battler", "Style", "Window" },
                Source = "BattleAnimScriptFunc_SetPokemonSpritePriority",
                Words = new[] { "which of the four dropped copies", "how many frames it lasts",
                                "which background layer to sit against", "where it sits among the sprites",
                                "which battler it is", OwnWay,
                                "the window type, used only by Dark Void" } },

            new BattleAnimFunc { Id = 76, Name = "Ripple", Summary = "Ripples the screen.", Labels = new[] { "Frames" }, Source = "BattleAnimScriptFunc_ScrollSwitchedBg",
                Words = new[] { "how many frames the ripple lasts" } },

            new BattleAnimFunc { Id = 77, Name = "Slide off or on", Summary = "Slides a Pokémon off the screen or back on.", Labels = new[] { "Off or on", "Target", "Frames", "", "" }, Source = "BattleAnimScriptFunc_MoveBattlerOnOrOffScreen",
                Words = new[] { "0 to send it off, anything else to bring it back", Flag, "how many frames it takes", "", "" } },

            new BattleAnimFunc { Id = 78, Name = "Draw copies", Summary = "Keeps the Pokémon drawn while particles load.", Labels = new[] { "Frames" },
                Source = "BattleAnimScriptFunc_RenderPokemonSprites",
                Words = new[] { "how many frames to keep them drawn, or 0 for the usual loading wait" } },

            new BattleAnimFunc { Id = 79, Name = "Sketch", Summary = "Only Sketch uses this.", Labels = new[] { "" }, Source = "BattleAnimScriptFunc_Sketch", Words = new[] { "" } },

            new BattleAnimFunc { Id = 82, Name = "Heal overlay", Summary = "Scrolls an overlay up behind a Pokémon.", Labels = new[] { "Background", "Behind" },
                Source = "BattleAnimScriptFunc_StatChangeHeal",
                Words = new[] { "which background graphic to scroll", "0 behind the attacker, anything else behind the defender" } },

            new BattleAnimFunc { Id = 83, Name = "Metal overlay", Summary = "Scrolls an overlay down behind a Pokémon.", Labels = new[] { "Background", "Behind" },
                Source = "BattleAnimScriptFunc_StatChangeMetal",
                Words = new[] { "which background graphic to scroll", "0 behind the attacker, anything else behind the defender" } },
        };

        private static readonly Dictionary<int, BattleAnimFunc> ById = Build();

        private static Dictionary<int, BattleAnimFunc> Build()
        {
            Dictionary<int, BattleAnimFunc> d = new Dictionary<int, BattleAnimFunc>(All.Length);
            foreach (BattleAnimFunc r in All) d[r.Id] = r;
            return d;
        }

        public const int TableSize = 84;

        public const int WorkSlots = 8 + 2;

        public static IReadOnlyCollection<BattleAnimFunc> Known => ById.Values;

        public static BattleAnimFunc Get(int id) => ById.TryGetValue(id, out BattleAnimFunc r) ? r : null;

        public static string WordMeaning(int id, int word)
        {
            BattleAnimFunc r = Get(id);
            if (r == null || word < 0 || word >= r.Words.Length) return null;
            return string.IsNullOrEmpty(r.Words[word]) ? null : r.Words[word];
        }

        public static string Label(int id, int word)
        {
            BattleAnimFunc r = Get(id);
            return r == null || word < 0 || word >= r.Labels.Length || r.Labels[word].Length == 0 ? null : r.Labels[word];
        }

        public static int WordsRead(int id) => Get(id)?.Words.Length ?? 0;
    }
}
