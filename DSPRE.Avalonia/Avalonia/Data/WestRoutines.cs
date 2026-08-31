using System;
using System.Collections.Generic;

namespace DSPRE.Avalonia.Data
{
    /// <summary>One thing a move-effect script can call, and what each word handed to it means.</summary>
    public sealed class WestRoutine
    {
        public int Id;
        public string Name = "";
        public string Summary = "";

        /// <summary>The C function and the file and line it is in, so anybody can check this.</summary>
        public string Source = "";

        /// <summary>What each word means, in order. An empty entry is a word the routine never reads.</summary>
        public string[] Words = Array.Empty<string>();
    }

    /// <summary>
    /// Every routine a move-effect script can call, read out of the games' own C rather than guessed.
    ///
    /// A script calls one with FUNC_CALL id, count, words. The id is the routine's index in
    /// WeSysSP_FuncTable (west_sp.c:218 indexes it directly, no offset), and the words land in
    /// waza_eff_gp_wk. WEST_FUNC_CALL copies count words in and zeros the remaining slots of the ten
    /// (we_sys.h:92), so a routine handed too few words still runs and reads zeros for the rest.
    ///
    /// Where a word picks out Pokemon it is a target flag; see <see cref="WestTargetFlags"/>, whose names
    /// are relative to the move rather than to the sides of the field.
    ///
    /// Many of these are one move's own effect and take nothing at all. Where the summary says only that,
    /// it is because the routine is written for a single move and its behaviour lives in the task it hands
    /// off to, not in anything the script tells it.
    /// </summary>
    public static class WestRoutines
    {
        private const string Flag = "who it acts on (a target flag)";
        private const string OwnWay = "which of the routine's ways of doing it";

        // EMIT_STRAIGHT and EMIT_PARABOLIC share one reader, EmitMove_Init (wsp_tool.c:3703), called only
        // from wsp_tool.c:3831 and :3992. The other two emitter routines look similar and are not.
        private static readonly string[] EmitMove =
        {
            "which emitter to move",
            "how far past the target it ends up, across",
            "how far past the target it ends up, down",
            "how many frames to wait before starting",
            "how many frames the move takes",
            "how high the arc goes",
            "0 from the attacker toward the defender, 1 the other way",
            "packed: the low half is when to stop looping, the high half a spare loop count",
            "how much the path curves",
        };

        private static readonly WestRoutine[] All =
        {
            new WestRoutine { Id = 0, Name = "TEST_1", Summary = "A sample routine the games left in. Does nothing.", Source = "WestSp_Sample, wsp_sample.c:64" },
            new WestRoutine { Id = 1, Name = "TEST_2", Summary = "A sample routine the games left in. Does nothing.", Source = "WestSp_SampleEffectTCB, wsp_sample.c:126" },
            new WestRoutine { Id = 2, Name = "TEST_3", Summary = "A sample routine the games left in. Does nothing.", Source = "WestSp_SampleSoundTCB, wsp_sample.c:193" },
            new WestRoutine { Id = 3, Name = "TEST_4", Summary = "A sample routine the games left in. Does nothing.", Source = "WestSp_SampleTCB, wsp_sample.c:258" },

            new WestRoutine { Id = 4, Name = "POKEROTA_00", Summary = "Turns the attacker on the spot.",
                Source = "WestSp_EffectTCBPokeRota00, wsp_tool.c:697",
                Words = new[] { "angle to start at", "angle to end at", "how many frames the turn takes",
                                "1 to turn around the point given below, anything else around the middle of the sprite",
                                "the point to turn around, across", "the point to turn around, down" } },

            new WestRoutine { Id = 5, Name = "WE_070", Summary = "Squashes the attacker down (Strength).",
                Source = "WestSp_WE_070, wsp_goto.c:424",
                Words = new[] { "how far down to squash, as a percentage", "", "how many frames the squash takes", "" } },

            new WestRoutine { Id = 6, Name = "WE_339", Summary = "One move's own effect.", Source = "WestSp_WE_339, wsp_goto.c:592", Words = new[] { "" } },
            new WestRoutine { Id = 7, Name = "WE_104", Summary = "One move's own effect.", Source = "WestSp_WE_104, wsp_goto.c:781", Words = new[] { "" } },
            new WestRoutine { Id = 8, Name = "WE_098", Summary = "One move's own effect.", Source = "WestSp_WE_098, wsp_tomoya.c:131" },
            new WestRoutine { Id = 9, Name = "WE_065", Summary = "One move's own effect.", Source = "WestSp_WE_065, wsp_tomoya.c:344" },

            new WestRoutine { Id = 10, Name = "WE_066", Summary = "Turns the attacker while moving it.",
                Source = "WestSp_WE_066, wsp_tool.c:824",
                Words = new[] { "where the turn starts", "where it ends", OwnWay } },

            new WestRoutine { Id = 11, Name = "WE_093", Summary = "One move's own effect.", Source = "WestSp_WE_093, wsp_tomoya.c:960" },
            new WestRoutine { Id = 12, Name = "WE_151", Summary = "One move's own effect.", Source = "WestSp_WE_151, wsp_tomoya.c:1226" },
            new WestRoutine { Id = 13, Name = "WE_074", Summary = "One move's own effect.", Source = "WestSp_WE_074, wsp_goto.c:944" },
            new WestRoutine { Id = 14, Name = "WE_096", Summary = "One move's own effect.", Source = "WestSp_WE_096, wsp_goto.c:1045" },
            new WestRoutine { Id = 15, Name = "WE_100", Summary = "One move's own effect.", Source = "WestSp_WE_100, wsp_goto.c:1196" },
            new WestRoutine { Id = 16, Name = "WE_148", Summary = "Whitens the background and darkens the attacker together, holds, then brings both back.",
                Source = "WestSp_WE_148, wsp_goto.c:1352" },
            new WestRoutine { Id = 17, Name = "WE_101AT", Summary = "One move's own effect, on the attacker.", Source = "WestSp_WE_101AT, wsp_tomoya.c:1525" },
            new WestRoutine { Id = 18, Name = "WE_101DF", Summary = "One move's own effect, on the defender.", Source = "WestSp_WE_101DF, wsp_tomoya.c:1577" },
            new WestRoutine { Id = 19, Name = "WE_150", Summary = "One move's own effect.", Source = "WestSp_WE_150, wsp_goto.c:1510" },
            new WestRoutine { Id = 20, Name = "WE_180", Summary = "One move's own effect.", Source = "WestSp_WE_180, wsp_tomoya.c:1874" },
            new WestRoutine { Id = 22, Name = "WE_107", Summary = "One move's own effect.", Source = "WestSp_WE_107, wsp_goto.c:1821", Words = new[] { "" } },
            new WestRoutine { Id = 23, Name = "WE_185", Summary = "One move's own effect.", Source = "WestSp_WE_185, wsp_tomoya.c:2820" },
            new WestRoutine { Id = 24, Name = "WE_089", Summary = "One move's own effect.", Source = "WestSp_WE_089, wsp_goto.c:1999", Words = new[] { "" } },

            new WestRoutine { Id = 25, Name = "WE_204", Summary = "One move's own effect.", Source = "WestSp_WE_204, wsp_tomoya.c:3776", Words = new[] { OwnWay } },
            new WestRoutine { Id = 26, Name = "WE_171", Summary = "One move's own effect.", Source = "WestSp_WE_171, wsp_goto.c:2123", Words = new[] { OwnWay } },

            new WestRoutine { Id = 27, Name = "WE_175 / SHAKE", Summary = "Shakes a Pokemon, in one of two ways.",
                Source = "WestSp_WE_175, wsp_goto.c:2313",
                Words = new[] { "0 for one way of shaking, anything else for the other",
                                "how far it moves across", "how far it moves down",
                                "how many frames each shake takes", "how many shakes", Flag } },

            new WestRoutine { Id = 28, Name = "WE_222", Summary = "One move's own effect.", Source = "WestSp_WE_222, wsp_goto.c:2412", Words = new[] { "" } },
            new WestRoutine { Id = 29, Name = "WE_216", Summary = "One move's own effect.", Source = "WestSp_WE_216, wsp_tomoya.c:4386" },
            new WestRoutine { Id = 30, Name = "WE_233", Summary = "One move's own effect.", Source = "WestSp_WE_233, wsp_tomoya.c:4562" },
            new WestRoutine { Id = 31, Name = "WE_207_MAIN", Summary = "One move's own effect.", Source = "WestSp_WE_207_MAIN, wsp_tomoya.c:3944" },
            new WestRoutine { Id = 32, Name = "WE_262", Summary = "One move's own effect.", Source = "WestSp_WE_262, wsp_tomoya.c:5471" },

            new WestRoutine { Id = 33, Name = "HAIKEI_PAL_FADE", Summary = "Fades the background's colours toward one colour and back.",
                Source = "WestSp_WE_HaikeiPalFade, wsp_tool.c:1114",
                Words = new[] { "which palette set: 0 the backdrop, 1 the first effect layer, 2 the second",
                                "how many frames each step of the fade takes",
                                "how strong it starts, out of 16", "how strong it ends, out of 16",
                                "the colour to fade toward" } },

            new WestRoutine { Id = 34, Name = "SSP_POKE_PAL_FADE", Summary = "Flashes a Pokemon a colour, over and over.",
                Source = "WestSp_WE_SSPPokePalFade, wsp_tool.c:1243",
                Words = new[] { Flag, "how many frames each step of the fade takes", "how many times it flashes",
                                "the colour to flash", "how strong the flash gets, out of 16",
                                "how many frames it holds at full strength" } },

            new WestRoutine { Id = 35, Name = "CAP_POKE_SCALE_UPDOWN", Summary = "Grows and shrinks a dropped copy of a Pokemon.",
                Source = "WestSp_WE_CAPPokeScaleUpDown, wsp_tool.c:1523",
                Words = new[] { "0 for the attacker's copy, anything else for the defender's",
                                "how see-through it is, out of 16", "the size it starts at", "the size it ends at",
                                "what to divide those two sizes by", "how many times it grows and shrinks",
                                "how many frames each step takes", "which of the four dropped copies" } },

            new WestRoutine { Id = 36, Name = "WT_SHAKE", Summary = "Shakes a Pokemon, a dropped copy, or the background.",
                Source = "WestSp_WE_T01, wsp_tool.c:115",
                Words = new[] { "how far it moves across, in pixels", "how far it moves down, in pixels",
                                "how many frames each shake takes", "how many shakes", Flag } },

            new WestRoutine { Id = 37, Name = "WE_326", Summary = "One move's own effect.", Source = "WestSp_WE_326DF, wsp_tomoya.c:6319" },

            new WestRoutine { Id = 38, Name = "CAP_ALPHA_FADE", Summary = "Fades dropped copies in or out.",
                Source = "WestSp_WE_CAP_NormalAlphaFade, wsp_tool.c:1895",
                Words = new[] { "which of the four dropped copies, one bit each",
                                "how solid the copy starts", "how solid it ends",
                                "how solid what is behind it starts", "how solid that ends",
                                "how many frames the fade takes" } },

            new WestRoutine { Id = 40, Name = "SSP_POKE_VANISH", Summary = "Hides or shows a Pokemon.",
                Source = "WestSp_WE_SSP_PokeVanish, wsp_tool.c:1955",
                Words = new[] { Flag, "0 to show it, anything else to hide it" } },

            new WestRoutine { Id = 41, Name = "WE_252_BACK", Summary = "One move's own effect, on the background.", Source = "WestSp_WE_252Back, wsp_tomoya.c:6514" },

            new WestRoutine { Id = 42, Name = "SSP_POKE_SCALE_UPDOWN", Summary = "Squashes and stretches a Pokemon, over and over.",
                Source = "WestSp_WE_SSPPokeScaleUpDown, wsp_tool.c:1716",
                Words = new[] { Flag, "the width it starts at", "the width it ends at",
                                "the height it starts at", "the height it ends at",
                                "what to divide those sizes by",
                                "packed: the low half is how many times, the high half is how many frames it holds",
                                "how many frames each step takes" } },

            new WestRoutine { Id = 43, Name = "WE_252_POKE", Summary = "One move's own effect, on a Pokemon.", Source = "WestSp_WE_252SSPPoke, wsp_tomoya.c:6775" },

            new WestRoutine { Id = 44, Name = "WE_T02", Summary = "Slides a background across the screen behind the battle.",
                Source = "WestSp_WE_T02, wsp_tool.c:299",
                Words = new[] { "which background to use", "where it starts, across", "where it starts, down",
                                "how fast it moves across", "how fast it moves down",
                                "whether to turn it around when the enemy is attacking",
                                "how solid it is", "how many frames it lasts" } },

            new WestRoutine { Id = 45, Name = "WE_T22", Summary = "Slides a background across the screen behind the battle.",
                Source = "WestSp_WE_T22, wsp_tool.c:528",
                Words = new[] { "which background to use", "where it starts, across", "where it starts, down",
                                "how fast it moves across", "how fast it moves down",
                                "whether to turn it around when the enemy is attacking",
                                "how solid it is", "how many frames it lasts" } },

            new WestRoutine { Id = 47, Name = "WE_224AT", Summary = "One move's own effect, on the attacker.", Source = "WestSp_WE_224AT, wsp_tomoya.c:7027" },
            new WestRoutine { Id = 48, Name = "WE_224DF", Summary = "One move's own effect, on the defender.", Source = "WestSp_WE_224DF, wsp_tomoya.c:7168" },

            new WestRoutine { Id = 49, Name = "WE_057", Summary = "The Surf wave.", Source = "WestSp_WE_057, wsp_goto.c:2789", Words = new[] { OwnWay } },

            new WestRoutine { Id = 50, Name = "WE_T03", Summary = "Blinks a Pokemon in and out.", Source = "WestSp_WE_T03, wsp_tool.c:2018",
                Words = new[] { "how many times it blinks (the routine doubles this)", "how many frames each blink takes" } },

            new WestRoutine { Id = 51, Name = "WE_T04", Summary = "Slides a Pokemon sideways and back.", Source = "WestSp_WE_T04, wsp_tool.c:2078",
                Words = new[] { "how many frames the slide takes", "how far it goes across", Flag } },

            new WestRoutine { Id = 52, Name = "WE_T05", Summary = "Slides a Pokemon sideways and back.", Source = "WestSp_WE_T05, wsp_tool.c:2181",
                Words = new[] { "how many frames the slide takes", "how far it goes across", Flag } },

            new WestRoutine { Id = 53, Name = "WE_T06", Summary = "Slides a Pokemon and holds it there.", Source = "WestSp_WE_T06, wsp_tool.c:2372",
                Words = new[] { "where the slide starts", "", "where it ends", "",
                                "how many frames to hold before coming back", Flag } },

            new WestRoutine { Id = 55, Name = "WE_293", Summary = "One move's own effect.", Source = "WestSp_WE_293, wsp_goto2.c:997" },

            new WestRoutine { Id = 56, Name = "WE_T08", Summary = "Puts a glow around the attacker (Superpower).", Source = "WestSp_WE_T08, wsp_tool.c:2623",
                Words = new[] { OwnWay, "" } },

            new WestRoutine { Id = 57, Name = "WE_T10", Summary = "Slides a Pokemon and brings it back.", Source = "WestSp_WE_T10, wsp_tool.c:2695",
                Words = new[] { "how many frames the slide takes", "how far it goes across", "how far it goes down", Flag } },

            new WestRoutine { Id = 58, Name = "WE_102", Summary = "One move's own effect.", Source = "WestSp_WE_102, wsp_100.c:85" },
            new WestRoutine { Id = 59, Name = "WE_325", Summary = "One move's own effect.", Source = "WestSp_WE_325, wsp_300.c:120", Words = new[] { "" } },

            new WestRoutine { Id = 60, Name = "WE_KAITEN", Summary = "Swings a Pokemon around in a circle.", Source = "WestSp_WE_Kaiten, wsp_tool.c:2802",
                Words = new[] { Flag, "where the swing starts", "where it ends" } },

            new WestRoutine { Id = 61, Name = "WE_DISP_OUT", Summary = "Slides a Pokemon off the screen.", Source = "WestSp_WE_DispOut, wsp_tool.c:2861",
                Words = new[] { Flag, "how many frames it takes" } },

            new WestRoutine { Id = 62, Name = "WE_DISP_DEF", Summary = "Puts a Pokemon straight back where it belongs.", Source = "WestSp_WE_DispDef, wsp_tool.c:2997",
                Words = new[] { Flag } },

            new WestRoutine { Id = 63, Name = "WE_OAM_PAL_FADE", Summary = "Fades the colours of dropped copies toward one colour.",
                Source = "WestSp_WE_OAM_PalFade, wsp_tool.c:3067",
                Words = new[] { "which of the four dropped copies, one bit each", "how many frames each step takes",
                                "how the fade is applied", "how strong it starts", "how strong it ends",
                                "the colour to fade toward" } },

            new WestRoutine { Id = 65, Name = "EMIT_STRAIGHT", Summary = "Moves a particle emitter in a straight line.",
                Source = "WSP_Emitter_Straight, wsp_tool.c:3820", Words = EmitMove },

            new WestRoutine { Id = 66, Name = "EMIT_PARABOLIC", Summary = "Moves a particle emitter along an arc.",
                Source = "WSP_Emitter_Parabolic, wsp_tool.c:3981", Words = EmitMove },

            new WestRoutine { Id = 67, Name = "RECT_VIEW", Summary = "Wipes a Pokemon in or out behind a moving edge.",
                Source = "WSP_RectView, wsp_tool.c:3359",
                Words = new[] { Flag, "", "where the edge starts", "where the edge ends",
                                "how many frames the wipe takes", "0 to wipe one way, anything else the other" } },

            new WestRoutine { Id = 68, Name = "BG_SHAKE", Summary = "Shakes the background.", Source = "WestSp_WE_BgShake, wsp_tool.c:3536",
                Words = new[] { "how far it moves across", "how far it moves down", "how many frames each shake takes",
                                "how many shakes", "how many extra times to run the whole thing",
                                "0 for one background frame, anything else for the other" } },

            new WestRoutine { Id = 69, Name = "MOSAIC", Summary = "Breaks a dropped copy into blocks and back.", Source = "WSP_Mosaic, wsp_tool.c:3620",
                Words = new[] { "which of the four dropped copies",
                                "how much to change the block size each step, negative to go back to none",
                                "block size across", "block size down" } },

            new WestRoutine { Id = 70, Name = "WSP_272", Summary = "One move's own effect.", Source = "WSP_272, wsp_300.c:404", Words = new[] { "" } },
            new WestRoutine { Id = 71, Name = "WSP_289", Summary = "One move's own effect.", Source = "WSP_289, wsp_300.c:594", Words = new[] { Flag } },

            new WestRoutine { Id = 72, Name = "EMIT_ROTATION", Summary = "Swings a particle emitter around a Pokemon.",
                Source = "WSP_Emitter_Rotation, wsp_tool.c:4090",
                Words = new[] { "which emitter to move", "the angle it starts at, across, in degrees",
                                "the angle it ends at, across, in degrees", "the angle it starts at, down, in degrees",
                                "the angle it ends at, down, in degrees", "how wide the circle is",
                                "how tall the circle is", "how many frames the swing takes",
                                "0 to swing around the attacker, anything else around the defender",
                                "which set of particles to swing" } },

            new WestRoutine { Id = 73, Name = "EMIT_SIMPLE_UD", Summary = "Moves a particle emitter up or down.",
                Source = "WSP_Emitter_SimpleUD, wsp_tool.c:3877",
                Words = new[] { "which emitter to move", "0 uses the attacker's position, anything else the defender's",
                                "0 comes down onto the Pokemon from above the screen, anything else rises away from it",
                                "how many frames the move takes", "how many frames to wait before starting",
                                "packed: the low half is when to stop looping, the high half a spare loop count" } },

            new WestRoutine { Id = 74, Name = "PALCOL_CHANGE", Summary = "Drains the colour out of the scene, or puts it back.",
                Source = "WSP_PalColChange, wsp_tool.c:4518",
                Words = new[] { "0 to put the colours back, anything else to drain them" } },

            new WestRoutine { Id = 75, Name = "POKE_OAM_VIEW", Summary = "Changes how a dropped copy is drawn and where it sits in the stack.",
                Source = "WSP_PokeOAM_View, wsp_tool.c:4701",
                Words = new[] { "which of the four dropped copies", "how many frames it lasts",
                                "which background layer to sit against", "where it sits among the sprites",
                                "which copy is being dropped", OwnWay,
                                "0 for the attacker's side, anything else for the defender's" } },

            new WestRoutine { Id = 76, Name = "LASTER", Summary = "Ripples the screen line by line.", Source = "WestSp_WE_Laster, wsp_tool.c:4975",
                Words = new[] { "how many frames the ripple lasts" } },

            new WestRoutine { Id = 77, Name = "DISP_MOVE", Summary = "Slides a Pokemon off the screen or back on.", Source = "WestSp_WE_DispMove, wsp_tool.c:2921",
                // The scripts hand this five words and it reads three; the last two are never looked at.
                Words = new[] { "0 to send it off, anything else to bring it back", Flag, "how many frames it takes", "", "" } },

            new WestRoutine { Id = 78, Name = "ALL_DROP", Summary = "Keeps all four Pokemon drawn as sprites while the particle data loads.",
                Source = "WSP_AllPokeDrop, wsp_tool.c:4873",
                Words = new[] { "how many frames to keep them drawn, or 0 for the usual loading wait" } },

            new WestRoutine { Id = 79, Name = "WSP_166", Summary = "One move's own effect.", Source = "WSP_166, wsp_300.c:758", Words = new[] { "" } },

            // Both read their two words through StatusEffect_Param_SetUp (wsp_steff.c).
            new WestRoutine { Id = 82, Name = "ST_EFF_RECOVER", Summary = "Scrolls an overlay upward behind a Pokemon, for getting its health back.",
                Source = "StatusEffect_Recover, wsp_steff.c:355",
                Words = new[] { "which background graphic to scroll", "0 behind the attacker, anything else behind the defender" } },

            new WestRoutine { Id = 83, Name = "ST_EFF_METAL", Summary = "Scrolls an overlay downward behind a Pokemon, for turning metallic.",
                Source = "StatusEffect_Metal, wsp_steff.c:377",
                Words = new[] { "which background graphic to scroll", "0 behind the attacker, anything else behind the defender" } },
        };

        private static readonly Dictionary<int, WestRoutine> ById = Build();

        private static Dictionary<int, WestRoutine> Build()
        {
            var d = new Dictionary<int, WestRoutine>(All.Length);
            foreach (var r in All) d[r.Id] = r;
            return d;
        }

        /// <summary>How many entries the games' own routine table has (NELEMS(WeSysSP_FuncTable)).</summary>
        public const int TableSize = 84;

        /// <summary>How many work slots a routine can read, whatever the script passed (WE_GENE_WK_MAX).</summary>
        public const int WorkSlots = 8 + 2;

        public static IReadOnlyCollection<WestRoutine> Known => ById.Values;

        public static WestRoutine Get(int id) => ById.TryGetValue(id, out var r) ? r : null;

        /// <summary>What one word handed to a routine means, or null when the routine never reads it.</summary>
        public static string WordMeaning(int id, int word)
        {
            var r = Get(id);
            if (r == null || word < 0 || word >= r.Words.Length) return null;
            return string.IsNullOrEmpty(r.Words[word]) ? null : r.Words[word];
        }

        /// <summary>How many words the routine looks at. Anything past this the game ignores.</summary>
        public static int WordsRead(int id) => Get(id)?.Words.Length ?? 0;
    }
}
