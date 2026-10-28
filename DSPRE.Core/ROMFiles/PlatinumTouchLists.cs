using System.Collections.Generic;
using System.Linq;
using Page = DSPRE.ROMFiles.TouchListButtons.Page;
using Spots = DSPRE.ROMFiles.TouchListButtons.Spots;
using static DSPRE.ROMFiles.ButtonNames;

namespace DSPRE.ROMFiles
{
    /// <summary>Platinum's bag, badge case, summary, PC and Pokétch touch lists, as <see cref="TouchListButtons"/>.</summary>
    public static class PlatinumTouchLists
    {
        // Each pocket button is 5 by 5 tiles placed at a u8 tile x and y.
        private static Spots Tiles(uint at) => new Spots((int)at, 2, 0, 1, 8);

        public static TouchListButtons Bag()
        {
            RomInfo.PtBagTouchSites s = RomLocator.PtBagTouchSites;
            if (s == null) return null;
            string[] rings = { "Inner edge", "Outer edge" };
            return TouchListButtons.Read(new[]
            {
                new Page("8 pockets", new[] { "Items", "Medicine", "Poké Balls", "TMs and HMs", "Berries", "Mail", "Battle items", "Key items" },
                         s.Overlay, (int)s.Pockets8, Spots: new[] { Tiles(s.Spots8) }),
                new Page("7 pockets", Numbered("Pocket", 7), s.Overlay, (int)s.Pockets7, Spots: new[] { Tiles(s.Spots7) }),
                new Page("4 pockets", Numbered("Pocket", 4), s.Overlay, (int)s.Pockets4, Spots: new[] { Tiles(s.Spots4) }),
                new Page("1 pocket", Numbered("Pocket", 1), s.Overlay, (int)s.Pocket1, Spots: new[] { Tiles(s.Spot1) }),
                new Page("Dial button", new[] { "Button" }, s.Overlay, (int)s.Dial, Locked: FixedArt(0)),
                new Page("Dial when touched", rings, s.Overlay, (int)s.DialTouched, Pairs: true),
                new Page("Dial when held", rings, s.Overlay, (int)s.DialHeld, Pairs: true),
            });
        }

        public static TouchListButtons TrainerCase()
        {
            RomInfo.PtTrainerCaseSites s = RomLocator.PtTrainerCaseSites;
            if (s == null) return null;
            string[] badges = { "Lid button", "Coal Badge", "Forest Badge", "Cobble Badge", "Fen Badge", "Relic Badge", "Mine Badge", "Icicle Badge", "Beacon Badge" };
            // Badge and sparkle sprites: s32 x and y each, for badges 1 to 8.
            Spots[] sprites = { new Spots((int)s.BadgeSpots, 8, 0, 4, 1, 1), new Spots((int)s.SparkleSpots, 8, 0, 4, 1, 1) };
            return TouchListButtons.Read(new[]
            {
                new Page("Badge case", badges, s.Overlay, (int)s.Badges, Spots: sprites, Locked: FixedArt(0)),
                new Page("Badge case without badges", new[] { badges[0] }, s.Overlay, (int)s.Closed, Locked: FixedArt(0)),
            });
        }

        public static TouchListButtons Summary()
        {
            RomInfo.PtSummaryTouchSites s = RomLocator.PtSummaryTouchSites;
            if (s == null) return null;
            // Each button entry is page, palette, tile x and tile y.
            Spots Buttons(uint at) => new Spots((int)at, 4, 2, 1, 8);
            return TouchListButtons.Read(new[]
            {
                new Page("Pages", new[] { "Info", "Memo", "Stats", "Battle moves", "Condition", "Contest moves", "Ribbons", "Back" },
                         TouchListButtons.InArm9, (int)s.Normal, Spots: new[] { Buttons(s.NormalSpots) }),
                new Page("Pages without contests", new[] { "Info", "Memo", "Stats", "Battle moves", "Back" }, TouchListButtons.InArm9, (int)s.NoContest,
                         Spots: new[] { Buttons(s.NoContestSpots) }),
                new Page("Moves only", Numbered("Button", 2), TouchListButtons.InArm9, (int)s.MovesOnly, Spots: new[] { Buttons(s.MovesOnlySpots) }),
            });
        }

        public static TouchListButtons Pc()
        {
            RomInfo.PtPcTouchSites s = RomLocator.PtPcTouchSites;
            if (s == null) return null;
            return TouchListButtons.Read(new[]
            {
                new Page("Corner buttons", new[] { "Box list", "Markings" }, s.Overlay, (int)s.Corners),
                new Page("Compare", new[] { "Box list", "Markings", "Compare" }, s.Overlay, (int)s.Compare),
                new Page("Markings", Numbered("Mark", 6), s.Overlay, (int)s.Markings),
            });
        }

        // Each Pokétch app's name and buttons, in PoketchTouchSites.Apps order.
        private static readonly (string Name, string[] Buttons)[] Apps =
        {
            ("Digital watch", new[] { "Screen" }),
            ("Stopwatch", new[] { "Button" }),
            ("Calculator", Enumerable.Range(0, 10).Select(i => i.ToString()).Concat(new[] { "Point", "Minus", "Plus", "Times", "Divide", "Equals", "Clear" }).ToArray()),
            ("Memo pad", Numbered("Button", 2)),
            ("Analog watch", new[] { "Screen" }),
            ("Counter", new[] { "Button" }),
            ("Pedometer", new[] { "Button" }),
            ("Backlight", Numbered("Button", 2)),
            ("Roulette", Numbered("Button", 3)),
            ("Move tester", Numbered("Button", 6)),
            ("Matchup checker", Numbered("Button", 3)),
            ("Alarm clock", Numbered("Button", 6)),
            ("Kitchen timer", Numbered("Button", 11)),
            ("Calendar", Numbered("Day", 37)),
            ("Trainer counter", Numbered("Button", 3)),
            ("Pokémon history", Numbered("Pokémon", 12)),
        };

        public static TouchListButtons Poketch()
        {
            RomInfo.PoketchTouchSites s = RomLocator.PoketchTouchSites;
            if (s == null || s.Apps.Length != Apps.Length) return null;
            IEnumerable<Page> apps = s.Apps.Select((a, i) => new Page(Apps[i].Name, Apps[i].Buttons, a.Overlay, (int)a.Touch, Ended: false));
            return TouchListButtons.Read(new[]
            {
                new Page("Frame", new[] { "Up", "Down", "Screen" }, s.FrameOverlay, (int)s.Frame, Ended: false, Locked: FixedArt(0, 1)),
            }.Concat(apps).ToArray());
        }
    }
}
