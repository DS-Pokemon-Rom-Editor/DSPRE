using System;
using System.Collections.Generic;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// The parts of a particle emitter this preview reads but does not act on, and what each would change.
    ///
    /// A field pulled out of the ROM and then dropped on the floor is the quiet way for a preview to be
    /// wrong, so each one is written down here with what would look different if it were simulated.
    /// Research/Moves/Animation/MoveAnimationParticleFields.md is generated from this, and a test keeps
    /// the two in step.
    /// </summary>
    public static class SpaFieldNotes
    {
        public sealed class Note
        {
            public string Field;
            /// <summary>What would look different if this were acted on.</summary>
            public string WouldChange;
            /// <summary>Where the games' own use of it was read.</summary>
            public string Source;
        }

        /// <summary>Read off the emitter record, deliberately not simulated.</summary>
        public static readonly IReadOnlyList<Note> NotSimulated = new[]
        {
            new Note
            {
                Field = "SelfDestruct",
                WouldChange = "When a move is judged to have finished, by at most a frame or two. The emitter "
                            + "throws itself away once it has stopped emitting instead of sitting there empty, "
                            + "and nothing is drawn after its particles are gone either way.",
                Source = "battle_particle.c",
            },
        };

        /// <summary>
        /// Read by the drawing code rather than by the movement code. These are not gaps: where a particle
        /// goes and how it is drawn are separate jobs, and this list exists so that a field turning up in
        /// neither place is noticed.
        /// </summary>
        public static readonly IReadOnlyList<string> DrawnNotMoved = new[]
        {
            "DrawType", "PosX", "PosY", "PosZ", "RepeatS", "RepeatT", "Aspect", "FlipS", "FlipT",
            "PolyRotAxis", "PolyRefPlane", "DpolFaceEmitter", "ChildDrawType", "ChildPolyRotAxis",
            "ChildPolyRefPlane", "DbbScale", "OffsetX", "OffsetY",
        };

        /// <summary>The document, so it cannot drift from this table.</summary>
        public static string BuildDocument(IReadOnlyList<string> allFields)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("[Research](../../ResearchNotes.md) / [Move Research](../MoveResearch.md) / Move Animation Particle Fields\n\n");
            sb.Append("# Particle emitter fields\n\n");
            sb.Append("Generated from `SpaFieldNotes.cs`. Do not edit by hand; `SpaFieldDocTests` rewrites it.\n\n");
            sb.Append("An emitter record in a `.spa` archive holds ").Append(allFields.Count)
              .Append(" fields. Every one of them is read. This says which ones the preview acts on, which ")
              .Append("ones only the drawing code looks at, and which ones it deliberately ignores.\n\n");

            sb.Append("## Read but not acted on\n\n");
            sb.Append("| field | what acting on it would change | read from |\n|---|---|---|\n");
            foreach (var n in NotSimulated)
                sb.Append("| `").Append(n.Field).Append("` | ").Append(n.WouldChange)
                  .Append(" | ").Append(n.Source).Append(" |\n");

            sb.Append("\n## Used when drawing, not when moving\n\n");
            sb.Append("Where a particle goes and how it is drawn are separate jobs. These decide how it looks ")
              .Append("on screen and nothing about its path, so the movement code never reads them.\n\n");
            foreach (var f in DrawnNotMoved) sb.Append("- `").Append(f).Append("`\n");

            var handled = new HashSet<string>(DrawnNotMoved, StringComparer.Ordinal);
            foreach (var n in NotSimulated) handled.Add(n.Field);

            sb.Append("\n## Acted on\n\n");
            sb.Append("The remaining fields drive the preview: how many particles there are, where they start, ")
              .Append("how fast and in what direction they leave, how long they and the emitter live, and how ")
              .Append("their size, colour, transparency, texture and spin change over that life.\n\n");
            foreach (var f in allFields)
                if (!handled.Contains(f)) sb.Append("- `").Append(f).Append("`\n");

            return sb.ToString();
        }
    }
}
