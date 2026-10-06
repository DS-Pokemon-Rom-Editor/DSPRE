using System;
using System.Collections.Generic;

namespace DSPRE.Avalonia.Data
{
    public sealed class BattleAnimFunc
    {
        public int Id;
        public string Name = "";
        public string Summary = "";

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
            "how far past the target it ends up, across",
            "how far past the target it ends up, down",
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
            new BattleAnimFunc { Id = 0, Name = "Nop", Summary = "A sample routine the games left in. Does nothing.", Source = "BattleAnimScriptFunc_Nop" },
            new BattleAnimFunc { Id = 1, Name = "AnimExample", Summary = "A sample routine the games left in. Does nothing.", Source = "BattleAnimScriptFunc_AnimExample" },
            new BattleAnimFunc { Id = 2, Name = "SoundExample", Summary = "A sample routine the games left in. Does nothing.", Source = "BattleAnimScriptFunc_SoundExample" },
            new BattleAnimFunc { Id = 3, Name = "GenericExample", Summary = "A sample routine the games left in. Does nothing.", Source = "BattleAnimScriptFunc_GenericExample" },

            new BattleAnimFunc { Id = 4, Name = "RotateMon", Summary = "Turns a Pokemon on the spot.",
                Source = "BattleAnimScriptFunc_RotateMon",
                Words = new[] { "angle to start at", "angle to end at", "how many frames the turn takes",
                                "0 turns the defender, 1 the attacker around the point given below, 2 the defender the other way",
                                "the point to turn around, across", "the point to turn around, down" } },

            new BattleAnimFunc { Id = 5, Name = "Strength", Summary = "Squashes the attacker down (Strength).",
                Source = "BattleAnimScriptFunc_Strength",
                Words = new[] { "how far down to squash, as a percentage", "", "how many frames the squash takes", "" } },

            new BattleAnimFunc { Id = 6, Name = "BulkUp", Summary = "One move's own effect.", Source = "BattleAnimScriptFunc_BulkUp", Words = new[] { "" } },
            new BattleAnimFunc { Id = 7, Name = "DoubleTeam", Summary = "One move's own effect.", Source = "BattleAnimScriptFunc_DoubleTeam", Words = new[] { "" } },
            new BattleAnimFunc { Id = 8, Name = "QuickAttack", Summary = "One move's own effect.", Source = "BattleAnimScriptFunc_QuickAttack" },
            new BattleAnimFunc { Id = 9, Name = "DrillPeck", Summary = "One move's own effect.", Source = "BattleAnimScriptFunc_DrillPeck" },

            new BattleAnimFunc { Id = 10, Name = "Submission", Summary = "Spins a Pokemon round (Submission).",
                Source = "BattleAnimScriptFunc_Submission",
                Words = new[] { "how many times it goes round", "how many frames each turn takes", "which battler it spins" } },

            new BattleAnimFunc { Id = 11, Name = "Confusion", Summary = "One move's own effect.", Source = "BattleAnimScriptFunc_Confusion" },
            new BattleAnimFunc { Id = 12, Name = "AcidArmor", Summary = "One move's own effect.", Source = "BattleAnimScriptFunc_AcidArmor" },
            new BattleAnimFunc { Id = 13, Name = "Growth", Summary = "One move's own effect.", Source = "BattleAnimScriptFunc_Growth" },
            new BattleAnimFunc { Id = 14, Name = "Meditate", Summary = "One move's own effect.", Source = "BattleAnimScriptFunc_Meditate" },
            new BattleAnimFunc { Id = 15, Name = "Teleport", Summary = "One move's own effect.", Source = "BattleAnimScriptFunc_Teleport" },
            new BattleAnimFunc { Id = 16, Name = "Flash", Summary = "Whitens the background and darkens the attacker together, holds, then brings both back.",
                Source = "BattleAnimScriptFunc_Flash" },
            new BattleAnimFunc { Id = 17, Name = "NightShadeAttacker", Summary = "One move's own effect, on the attacker.", Source = "BattleAnimScriptFunc_NightShadeAttacker" },
            new BattleAnimFunc { Id = 18, Name = "NightShadeDefender", Summary = "One move's own effect, on the defender.", Source = "BattleAnimScriptFunc_NightShadeDefender" },
            new BattleAnimFunc { Id = 19, Name = "Splash", Summary = "One move's own effect.", Source = "BattleAnimScriptFunc_Splash" },
            new BattleAnimFunc { Id = 20, Name = "Spite", Summary = "One move's own effect.", Source = "BattleAnimScriptFunc_Spite" },
            new BattleAnimFunc { Id = 22, Name = "Minimize", Summary = "One move's own effect.", Source = "BattleAnimScriptFunc_Minimize", Words = new[] { "" } },
            new BattleAnimFunc { Id = 23, Name = "FaintAttack", Summary = "One move's own effect.", Source = "BattleAnimScriptFunc_FaintAttack" },
            new BattleAnimFunc { Id = 24, Name = "Earthquake", Summary = "One move's own effect.", Source = "BattleAnimScriptFunc_Earthquake", Words = new[] { "" } },

            new BattleAnimFunc { Id = 25, Name = "PlayfulHops", Summary = "One move's own effect.", Source = "BattleAnimScriptFunc_PlayfulHops", Words = new[] { OwnWay } },
            new BattleAnimFunc { Id = 26, Name = "Nightmare", Summary = "One move's own effect.", Source = "BattleAnimScriptFunc_Nightmare", Words = new[] { OwnWay } },

            new BattleAnimFunc { Id = 27, Name = "Flail", Summary = "Shakes a Pokemon, in one of two ways.",
                Source = "BattleAnimScriptFunc_Flail",
                Words = new[] { "0 for one way of shaking, anything else for the other",
                                "how far it moves across", "how far it moves down",
                                "how many frames each shake takes", "how many shakes", Flag } },

            new BattleAnimFunc { Id = 28, Name = "Magnitude", Summary = "One move's own effect.", Source = "BattleAnimScriptFunc_Magnitude", Words = new[] { "" } },
            new BattleAnimFunc { Id = 29, Name = "Return", Summary = "One move's own effect.", Source = "BattleAnimScriptFunc_Return" },
            new BattleAnimFunc { Id = 30, Name = "VitalThrow", Summary = "One move's own effect.", Source = "BattleAnimScriptFunc_VitalThrow" },
            new BattleAnimFunc { Id = 31, Name = "Swagger", Summary = "One move's own effect.", Source = "BattleAnimScriptFunc_Swagger" },
            new BattleAnimFunc { Id = 32, Name = "Memento", Summary = "One move's own effect.", Source = "BattleAnimScriptFunc_Memento" },

            new BattleAnimFunc { Id = 33, Name = "FadeBg", Summary = "Fades the background's colours toward one colour and back.",
                Source = "BattleAnimScriptFunc_FadeBg",
                Words = new[] { "which palette set: 0 the backdrop, 1 the first effect layer, 2 the second",
                                "how many frames each step of the fade takes",
                                "how strong it starts, out of 16", "how strong it ends, out of 16",
                                "the colour to fade toward" } },

            new BattleAnimFunc { Id = 34, Name = "FadeBattlerSprite", Summary = "Flashes a Pokemon a colour, over and over.",
                Source = "BattleAnimScriptFunc_FadeBattlerSprite",
                Words = new[] { Flag, "how many frames each step of the fade takes", "how many times it flashes",
                                "the colour to flash", "how strong the flash gets, out of 16",
                                "how many frames it holds at full strength" } },

            new BattleAnimFunc { Id = 35, Name = "ScalePokemonSprite", Summary = "Grows and shrinks a dropped copy of a Pokemon.",
                Source = "BattleAnimScriptFunc_ScalePokemonSprite",
                Words = new[] { "0 for the attacker's copy, anything else for the defender's",
                                "how see-through it is, out of 16", "the size it starts at", "the size it ends at",
                                "what to divide those two sizes by", "how many times it grows and shrinks",
                                "how many frames each step takes", "which of the four dropped copies" } },

            new BattleAnimFunc { Id = 36, Name = "Shake", Summary = "Shakes a Pokemon, a dropped copy, or the background.",
                Source = "BattleAnimScriptFunc_Shake",
                Words = new[] { "how far it moves across, in pixels", "how far it moves down, in pixels",
                                "how many frames each shake takes", "how many shakes", Flag } },

            new BattleAnimFunc { Id = 37, Name = "Extrasensory", Summary = "One move's own effect.", Source = "BattleAnimScriptFunc_Extrasensory" },

            new BattleAnimFunc { Id = 38, Name = "AlphaFadePokemonSprite", Summary = "Fades dropped copies in or out.",
                Source = "BattleAnimScriptFunc_AlphaFadePokemonSprite",
                Words = new[] { "which of the four dropped copies, one bit each",
                                "how solid the copy starts", "how solid it ends",
                                "how solid what is behind it starts", "how solid that ends",
                                "how many frames the fade takes" } },

            new BattleAnimFunc { Id = 40, Name = "HideBattler", Summary = "Hides or shows a Pokemon.",
                Source = "BattleAnimScriptFunc_HideBattler",
                Words = new[] { Flag, "0 to show it, anything else to hide it" } },

            new BattleAnimFunc { Id = 41, Name = "FakeOutCurtain", Summary = "One move's own effect, on the background.", Source = "BattleAnimScriptFunc_FakeOutCurtain" },

            new BattleAnimFunc { Id = 42, Name = "ScaleBattlerSprite", Summary = "Squashes and stretches a Pokemon, over and over.",
                Source = "BattleAnimScriptFunc_ScaleBattlerSprite",
                Words = new[] { Flag, "the width it starts at", "the width it ends at",
                                "the height it starts at", "the height it ends at",
                                "what to divide those sizes by",
                                "packed: the low half is how many times, the high half is how many frames it holds",
                                "how many frames each step takes" } },

            new BattleAnimFunc { Id = 43, Name = "FakeOut", Summary = "One move's own effect, on a Pokemon.", Source = "BattleAnimScriptFunc_FakeOut" },

            new BattleAnimFunc { Id = 44, Name = "ScrollCustomBg", Summary = "Slides a background across the screen behind the battle.",
                Source = "BattleAnimScriptFunc_ScrollCustomBg",
                Words = new[] { "which background to use", "where it starts, across", "where it starts, down",
                                "how fast it moves across", "how fast it moves down",
                                "whether to turn it around when the enemy is attacking",
                                "how solid it is", "how many frames between each slowing down" } },

            new BattleAnimFunc { Id = 45, Name = "MuddyWater", Summary = "Slides a background across the screen behind the battle.",
                Source = "BattleAnimScriptFunc_MuddyWater",
                Words = new[] { "which background to use", "where it starts, across", "where it starts, down",
                                "how fast it moves across", "how fast it moves down",
                                "whether to turn it around when the enemy is attacking",
                                "", "how many frames between each slowing down" } },

            new BattleAnimFunc { Id = 47, Name = "MegahornAttacker", Summary = "One move's own effect, on the attacker.", Source = "BattleAnimScriptFunc_MegahornAttacker" },
            new BattleAnimFunc { Id = 48, Name = "MegahornDefender", Summary = "One move's own effect, on the defender.", Source = "BattleAnimScriptFunc_MegahornDefender" },

            new BattleAnimFunc { Id = 49, Name = "Surf", Summary = "The Surf wave.", Source = "BattleAnimScriptFunc_Surf", Words = new[] { OwnWay } },

            new BattleAnimFunc { Id = 50, Name = "BlinkAttacker", Summary = "Blinks a Pokemon in and out.", Source = "BattleAnimScriptFunc_BlinkAttacker",
                Words = new[] { "how many times it blinks (the routine doubles this)", "how many frames each blink takes" } },

            new BattleAnimFunc { Id = 51, Name = "MoveBattlerX", Summary = "Slides a Pokemon sideways and back.", Source = "BattleAnimScriptFunc_MoveBattlerX",
                Words = new[] { "how many frames the slide takes", "how far it goes across", Flag } },

            new BattleAnimFunc { Id = 52, Name = "MoveBattlerX2", Summary = "Slides a Pokemon sideways and back.", Source = "BattleAnimScriptFunc_MoveBattlerX2",
                Words = new[] { "how many frames the slide takes", "how far it goes across", Flag } },

            new BattleAnimFunc { Id = 53, Name = "ShakeAndScaleAttacker", Summary = "Shakes and stretches the attacker, then holds it.", Source = "BattleAnimScriptFunc_ShakeAndScaleAttacker",
                Words = new[] { "the first stretch", "the second stretch", "how many frames the first takes",
                                "how many frames the second takes", "how many frames to hold before coming back", "" } },

            new BattleAnimFunc { Id = 55, Name = "Camouflage", Summary = "One move's own effect.", Source = "BattleAnimScriptFunc_Camouflage" },

            new BattleAnimFunc { Id = 56, Name = "Superpower", Summary = "Puts a glow around the attacker (Superpower).", Source = "BattleAnimScriptFunc_Superpower",
                Words = new[] { "", "" } },

            new BattleAnimFunc { Id = 57, Name = "MoveBattler", Summary = "Slides a Pokemon and brings it back.", Source = "BattleAnimScriptFunc_MoveBattler",
                Words = new[] { "how many frames the slide takes", "how far it goes across", "how far it goes down", Flag } },

            new BattleAnimFunc { Id = 58, Name = "Mimic", Summary = "One move's own effect.", Source = "BattleAnimScriptFunc_Mimic" },
            new BattleAnimFunc { Id = 59, Name = "ShadowPunch", Summary = "One move's own effect.", Source = "BattleAnimScriptFunc_ShadowPunch", Words = new[] { "" } },

            new BattleAnimFunc { Id = 60, Name = "RevolveBattler", Summary = "Swings a Pokemon around in a circle.", Source = "BattleAnimScriptFunc_RevolveBattler",
                Words = new[] { Flag, "how many times it goes round", "how many frames each turn takes" } },

            new BattleAnimFunc { Id = 61, Name = "MoveBattlerOffScreen", Summary = "Slides a Pokemon off the screen.", Source = "BattleAnimScriptFunc_MoveBattlerOffScreen",
                Words = new[] { Flag, "how many frames it takes" } },

            new BattleAnimFunc { Id = 62, Name = "MoveBattlerToDefaultPos", Summary = "Puts a Pokemon straight back where it belongs.", Source = "BattleAnimScriptFunc_MoveBattlerToDefaultPos",
                Words = new[] { Flag } },

            new BattleAnimFunc { Id = 63, Name = "FadePokemonSprite", Summary = "Fades the colours of dropped copies toward one colour.",
                Source = "BattleAnimScriptFunc_FadePokemonSprite",
                Words = new[] { "which of the four dropped copies, one bit each", "how many frames each step takes",
                                "how much each step changes it", "how strong it starts", "how strong it ends",
                                "the colour to fade toward" } },

            new BattleAnimFunc { Id = 65, Name = "MoveEmitterA2BLinear", Summary = "Moves a particle emitter in a straight line.",
                Source = "BattleAnimScriptFunc_MoveEmitterA2BLinear", Words = StraightMove },

            new BattleAnimFunc { Id = 66, Name = "MoveEmitterA2BParabolic", Summary = "Moves a particle emitter along an arc.",
                Source = "BattleAnimScriptFunc_MoveEmitterA2BParabolic", Words = EmitMove },

            new BattleAnimFunc { Id = 67, Name = "BattlerPartialDraw", Summary = "Wipes a Pokemon in or out behind a moving edge.",
                Source = "BattleAnimScriptFunc_BattlerPartialDraw",
                Words = new[] { Flag, "", "", "how far the edge moves each step, its sign choosing in or out",
                                "how many frames between steps", "1 to draw it the way Sketch does" } },

            new BattleAnimFunc { Id = 68, Name = "ShakeBg", Summary = "Shakes the background.", Source = "BattleAnimScriptFunc_ShakeBg",
                Words = new[] { "how far it moves across", "how far it moves down", "how many frames each shake takes",
                                "how many shakes", "how many extra times to run the whole thing",
                                "0 for one background frame, anything else for the other" } },

            new BattleAnimFunc { Id = 69, Name = "PixelatePokemonSprite", Summary = "Breaks a dropped copy into blocks and back.", Source = "BattleAnimScriptFunc_PixelatePokemonSprite",
                Words = new[] { "which of the four dropped copies",
                                "how much to change the block size each step, negative to go back to none",
                                "block size across", "block size down" } },

            new BattleAnimFunc { Id = 70, Name = "RolePlay", Summary = "One move's own effect.", Source = "BattleAnimScriptFunc_RolePlay", Words = new[] { "" } },
            new BattleAnimFunc { Id = 71, Name = "Snatch", Summary = "One move's own effect.", Source = "BattleAnimScriptFunc_Snatch", Words = new[] { Flag } },

            new BattleAnimFunc { Id = 72, Name = "RevolveEmitter", Summary = "Swings a particle emitter around a Pokemon.",
                Source = "BattleAnimScriptFunc_RevolveEmitter",
                Words = new[] { "which emitter to move", "the angle it starts at, across, in degrees",
                                "the angle it ends at, across, in degrees", "the angle it starts at, down, in degrees",
                                "the angle it ends at, down, in degrees", "how wide the circle is",
                                "how tall the circle is", "how many frames the swing takes",
                                "0 to swing around the attacker, anything else around the defender",
                                "which set of particles to swing" } },

            new BattleAnimFunc { Id = 73, Name = "MoveEmitterViewportTop", Summary = "Moves a particle emitter up or down.",
                Source = "BattleAnimScriptFunc_MoveEmitterViewportTop",
                Words = new[] { "which emitter to move", "0 uses the attacker's position, anything else the defender's",
                                "0 comes down onto the Pokemon from above the screen, anything else rises away from it",
                                "how many frames the move takes", "how many frames to wait before starting",
                                "packed: the low half caps the move at that many frames, the high half skips that many frames at the start" } },

            new BattleAnimFunc { Id = 74, Name = "SetBgGrayscale", Summary = "Drains the colour out of the scene, or puts it back.",
                Source = "BattleAnimScriptFunc_SetBgGrayscale",
                Words = new[] { "0 to put the colours back, anything else to drain them" } },

            new BattleAnimFunc { Id = 75, Name = "SetPokemonSpritePriority", Summary = "Changes how a dropped copy is drawn and where it sits in the stack.",
                Source = "BattleAnimScriptFunc_SetPokemonSpritePriority",
                Words = new[] { "which of the four dropped copies", "how many frames it lasts",
                                "which background layer to sit against", "where it sits among the sprites",
                                "which battler it is", OwnWay,
                                "the window type, used only by Dark Void" } },

            new BattleAnimFunc { Id = 76, Name = "ScrollSwitchedBg", Summary = "Ripples the screen line by line.", Source = "BattleAnimScriptFunc_ScrollSwitchedBg",
                Words = new[] { "how many frames the ripple lasts" } },

            new BattleAnimFunc { Id = 77, Name = "MoveBattlerOnOrOffScreen", Summary = "Slides a Pokemon off the screen or back on.", Source = "BattleAnimScriptFunc_MoveBattlerOnOrOffScreen",
                Words = new[] { "0 to send it off, anything else to bring it back", Flag, "how many frames it takes", "", "" } },

            new BattleAnimFunc { Id = 78, Name = "RenderPokemonSprites", Summary = "Keeps all four Pokemon drawn as sprites while the particle data loads.",
                Source = "BattleAnimScriptFunc_RenderPokemonSprites",
                Words = new[] { "how many frames to keep them drawn, or 0 for the usual loading wait" } },

            new BattleAnimFunc { Id = 79, Name = "Sketch", Summary = "One move's own effect.", Source = "BattleAnimScriptFunc_Sketch", Words = new[] { "" } },

            new BattleAnimFunc { Id = 82, Name = "StatChangeHeal", Summary = "Scrolls an overlay upward behind a Pokemon, for getting its health back.",
                Source = "BattleAnimScriptFunc_StatChangeHeal",
                Words = new[] { "which background graphic to scroll", "0 behind the attacker, anything else behind the defender" } },

            new BattleAnimFunc { Id = 83, Name = "StatChangeMetal", Summary = "Scrolls an overlay downward behind a Pokemon, for turning metallic.",
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

        public static int WordsRead(int id) => Get(id)?.Words.Length ?? 0;
    }
}
