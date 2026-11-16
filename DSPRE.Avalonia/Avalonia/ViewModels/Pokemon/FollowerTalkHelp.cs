using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DSPRE.Editors;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;
using Slot = DSPRE.ROMFiles.FollowerTalk.Slot;

namespace DSPRE.Avalonia.ViewModels.Pokemon
{
    /// <summary>What each Follower talk field does in game, for its info button.</summary>
    public static class FollowerTalkHelp
    {
        public const string Chance = "How often this slot gets its turn. When the roll fails, the game moves on to the next slot.";
        public const string Reaction = "What happens when this slot is picked. Slots can share a reaction, so an edit to it shows up in all of them.";
        public const string Movement = "How the follower moves before its bubble and line. It's put back where it stood afterwards.";
        public const string Bubble = "The emote over the follower's head, shown after it moves.";
        public const string Sound = "The follower's cry, its weak cry or a sound effect. It only plays on movement frames with Sound ticked, so a step with no movement stays silent.";
        public const string Wait = "A pause after the line, at 30 frames a second.";
        public const string Line = "The text shown. Name codes become the nickname, species, player, place or held item. 0 shows no text.";
        public const string Question = "Asks Yes or No after the last line. Each answer can lead to another reaction.";
        public const string YesNo = "The reaction that plays after this answer. 0 ends the talk.";
        public const string Friendship = "Added to friendship when the reaction ends.";
        public const string Mood = "Added to mood when the reaction ends.";
        public const string Gift = "Given when the reaction ends. A Shiny Leaf the follower already has isn't given again, and no accessory is given while the fashion case is full.";
        public const string Facing = "The way the follower turns on this frame. Player keeps its current way, which is toward the player until a frame turns it.";
        public const string Length = "How long this frame lasts, at 30 a second.";
        public const string X = "Moves the follower sideways, in pixels (16 to a tile). Moves add up from frame to frame.";
        public const string Hop = "Lifts the follower, in pixels. Lifts add up from frame to frame. Diglett and Dugtrio stay on the ground.";
        public const string Z = "Moves the follower up or down the screen, in pixels (16 to a tile). Moves add up from frame to frame.";
        public const string FrameSound = "Plays the step's sound on this frame.";
        public const string Group = "Slots that ask for this group match this species.";
    }
}
