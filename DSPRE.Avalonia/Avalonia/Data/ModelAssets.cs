using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE;
using DSPRE.Avalonia;
using DSPRE.ROMFiles;
using LibNDSFormats.NSBMD;
using LibNDSFormats.NSBTX;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// The 3D side of the game: the models, the texture bundles that dress them, and the animations that
    /// move them.
    /// </summary>
    public static class ModelAssets
    {
        public enum Group { Overworld, Buildings, Maps, Other }

        public sealed class Archive
        {
            public DirNames Dir;
            public string Title { get; init; }
            public Group In { get; init; }
            public string What { get; init; }
            public string DeepEditor { get; init; }

            /// <summary>
            /// The archive holding the picture sets for these models, when they are kept apart from the
            /// models themselves.
            /// </summary>
            public DirNames? TextureArchive { get; init; }

            /// <summary>
            /// The archive holding the movement for these models, when it is kept apart from them.
            /// </summary>
            public DirNames? AnimationArchive { get; init; }

            /// <summary>True for the models inside buildings. </summary>
            public bool Indoor { get; init; }
        }

        /// <summary>The eight archives holding 3D data, from the census in
        /// Research/Graphics/GraphicsCensus.md.</summary>
        public static readonly Archive[] All =
        {
            new Archive { Dir = DirNames.OWSprites, Title = "Overworld people and objects", In = Group.Overworld,
                What = "Everything that stands about on the map: people, signs, items and the rest.",
                DeepEditor = "Overworld Editor" },

            new Archive { Dir = DirNames.exteriorBuildingModels, Title = "Buildings, outside", TextureArchive = DirNames.buildingTextures, AnimationArchive = DirNames.buildingAnimations, In = Group.Buildings,
                What = "The buildings as seen from the outside on the world map.",
                DeepEditor = "Building Editor" },
            new Archive { Dir = DirNames.interiorBuildingModels, Title = "Buildings, inside", TextureArchive = DirNames.buildingTextures, AnimationArchive = DirNames.buildingAnimations, Indoor = true, In = Group.Buildings,
                What = "The insides of buildings you can walk into.", DeepEditor = "Building Editor" },
            new Archive { Dir = DirNames.buildingTextures, Title = "Building textures", In = Group.Buildings,
                What = "The pictures painted onto the buildings.", DeepEditor = "NSBTX Texture Editor" },
            new Archive { Dir = DirNames.buildingAnimations, Title = "Building animations", In = Group.Buildings,
                What = "Doors opening, windmills turning, and the rest of what buildings do." },

            new Archive { Dir = DirNames.mapTextures, Title = "Map textures", In = Group.Maps,
                What = "The pictures painted onto the ground and scenery of each map.",
                DeepEditor = "NSBTX Texture Editor" },
            new Archive { Dir = DirNames.groundAnimations, Title = "Ground animations", In = Group.Maps,
                What = "Water and other ground that moves." },

            new Archive { Dir = DirNames.titleScreenGraphics, Title = "Title screen", In = Group.Other,
                What = "The logo and background of the game's own title screen.",
                DeepEditor = "Title Screen Editor" },
        };

        public enum Kind { Unknown, Model, TextureBundle, JointAnimation, TextureAnimation, TextureSwap,
                           VisibilityAnimation, MaterialAnimation, NotThreeD, Empty }

        public static Kind Identify(byte[] b)
        {
            if (b == null || b.Length < 4) return Kind.Empty;
            switch (System.Text.Encoding.ASCII.GetString(b, 0, 4))
            {
                case "BMD0": return Kind.Model;
                case "BTX0": return Kind.TextureBundle;
                case "BCA0": return Kind.JointAnimation;
                case "BTA0": return Kind.TextureAnimation;
                case "BTP0": return Kind.TextureSwap;
                case "BVA0": return Kind.VisibilityAnimation;
                case "BMA0": return Kind.MaterialAnimation;
                default: return Kind.NotThreeD;
            }
        }

        /// <summary>Plain words for what one entry is.</summary>
        public static string Describe(Kind k) => k switch
        {
            Kind.Model => "A model: the shape of a thing, in three dimensions.",
            Kind.TextureBundle => "A set of pictures that get painted onto models.",
            Kind.JointAnimation => "Movement for a model's joints, like a door swinging.",
            Kind.TextureAnimation => "A picture that slides or scrolls across a model, like flowing water.",
            Kind.TextureSwap => "A list of which picture to show when, like a flickering sign.",
            Kind.VisibilityAnimation => "A list of which parts of a model to show or hide when.",
            Kind.MaterialAnimation => "Changes to how a model's surface is lit or coloured over time.",
            Kind.Empty => "This entry is empty.",
            _ => "This entry is not 3D data.",
        };

        /// <summary>A short name for a kind, for use inside a sentence. Describe says what it is; this
        /// says what to call it.</summary>
        public static string ShortName(Kind k) => k switch
        {
            Kind.Model => "a model",
            Kind.TextureBundle => "a set of pictures",
            Kind.JointAnimation => "joint movement",
            Kind.TextureAnimation => "a sliding picture",
            Kind.TextureSwap => "a list of pictures to swap between",
            Kind.VisibilityAnimation => "a list of parts to show and hide",
            Kind.MaterialAnimation => "changes to a surface over time",
            Kind.Empty => "nothing",
            _ => "something that is not 3D data",
        };

        /// <summary>What can be done with one entry, and why not when not.</summary>
        public sealed class Options
        {
            public Kind Kind;
            public bool CanShow;       // the renderer can draw it
            public bool CanSaveModel;  // as a file another 3D program opens
            public string SaveNote;    // what that file can and cannot carry, or why there is none
            public string ShowNote;    // why it cannot be shown, when it cannot
        }

        public static Options WhatCanBeDone(Archive a, int index)
        {
            var narc = new ScriptNarc(a.Dir);
            if (!narc.Available)
                return new Options { Kind = Kind.Empty, ShowNote = "This game does not have this archive.",
                                     SaveNote = "This game does not have this archive." };

            var b = narc.Get(index);
            var kind = Identify(b);
            var o = new Options { Kind = kind };

            switch (kind)
            {
                case Kind.Model:
                    o.CanShow = true;
                    o.CanSaveModel = true;
                    o.SaveNote = "Saves as a file other 3D programs open, with the shape and the pictures "
                               + "painted on it. Movement is not included: the animations are separate "
                               + "entries and are not carried over.";
                    break;

                case Kind.TextureBundle:
                    o.CanShow = false;
                    o.ShowNote = "This is a set of pictures for painting onto models, not a model itself, "
                               + "so there is no shape to show. Open it in the NSBTX Texture Editor to see "
                               + "the pictures.";
                    o.CanSaveModel = false;
                    o.SaveNote = "There is no model here to save as a 3D file. The whole file can still be "
                               + "saved exactly as it is.";
                    break;

                case Kind.JointAnimation:
                case Kind.TextureAnimation:
                case Kind.TextureSwap:
                case Kind.VisibilityAnimation:
                case Kind.MaterialAnimation:
                    o.CanShow = false;
                    o.ShowNote = "This is movement for a model, not a model. It needs the model it belongs "
                               + "to before there is anything to see.";
                    o.CanSaveModel = false;
                    o.SaveNote = "Movement on its own is not something a 3D file can hold. The whole file "
                               + "can still be saved exactly as it is.";
                    break;

                case Kind.Empty:
                    o.ShowNote = o.SaveNote = "This entry is empty.";
                    break;

                default:
                    o.ShowNote = o.SaveNote = "This entry is not 3D data, so there is nothing to show or "
                                            + "convert. The whole file can still be saved as it is.";
                    break;
            }
            return o;
        }

        /// <summary>Reads a model so it can be drawn. Null when the entry is not one, or will not read.</summary>
        public static NSBMD LoadModel(Archive a, int index)
        {
            var narc = new ScriptNarc(a.Dir);
            if (!narc.Available) return null;
            var b = narc.Get(index);
            if (Identify(b) != Kind.Model) return null;
            try { using var ms = new MemoryStream(b); return NSBMDLoader.LoadNSBMD(ms); }
            catch (Exception ex) { AppLogger.Error("ModelAssets.LoadModel failed: " + ex.Message); return null; }
        }

        /// <summary>The pictures a model carries inside itself, when it carries any.</summary>
        public static byte[] EmbeddedTextures(Archive a, int index)
        {
            var narc = new ScriptNarc(a.Dir);
            if (!narc.Available) return null;
            var model = narc.Get(index);
            if (Identify(model) != Kind.Model) return null;
            try
            {
                var tex = NSBUtils.GetTexturesFromTexturedNSBMD(model);
                return tex != null && tex.Length > 4 ? tex : null;
            }
            catch { return null; }
        }

        /// <summary>The picture set sitting right after a model in the same archive. Overworld people keep
        /// their model and their pictures next to each other this way.</summary>
        public static byte[] NeighbouringTextures(Archive a, int index)
        {
            var narc = new ScriptNarc(a.Dir);
            if (!narc.Available) return null;
            for (int i = index + 1; i < index + 4; i++)
            {
                var b = narc.Get(i);
                if (b == null) break;
                if (Identify(b) == Kind.TextureBundle) return b;
            }
            return null;
        }

        /// <summary>How many picture sets this kind of model can be dressed in.</summary>
        public static int TextureSetCount(Archive a)
        {
            if (a.TextureArchive == null) return 0;
            var narc = new ScriptNarc(a.TextureArchive.Value);
            return narc.Available ? narc.Count : 0;
        }

        /// <summary>One of the picture sets from the archive this kind of model takes them from.</summary>
        public static byte[] TextureSet(Archive a, int setIndex)
        {
            if (a.TextureArchive == null) return null;
            var narc = new ScriptNarc(a.TextureArchive.Value);
            if (!narc.Available) return null;
            var b = narc.Get(setIndex);
            return Identify(b) == Kind.TextureBundle ? b : null;
        }

        public sealed class TextureCoverage
        {
            public int RequiredTextures { get; init; }
            public int MatchedTextures { get; init; }
            public int RequiredPalettes { get; init; }
            public int MatchedPalettes { get; init; }
            public bool HasMatches => MatchedTextures > 0;
            public bool Complete => RequiredTextures > 0
                && MatchedTextures == RequiredTextures && MatchedPalettes == RequiredPalettes;
        }

        /// <summary>Compares the names a model requests with the names a texture bundle actually contains.</summary>
        public static TextureCoverage Coverage(NSBMD model, byte[] bundle)
        {
            if (model?.models == null || bundle == null) return new TextureCoverage();
            try
            {
                using var input = new MemoryStream(bundle);
                NSBTXLoader.LoadNsbtx(input, out var textures, out var palettes);
                var haveTextures = new HashSet<string>(textures.Select(t => t.texname), StringComparer.Ordinal);
                var havePalettes = new HashSet<string>(palettes.Select(p => p.palname), StringComparer.Ordinal);
                var wantTextures = new HashSet<string>(model.models.SelectMany(m => m.Textures)
                    .Select(t => t.texname).Where(n => !string.IsNullOrEmpty(n)), StringComparer.Ordinal);
                var wantPalettes = new HashSet<string>(model.models.SelectMany(m => m.Palettes)
                    .Select(p => p.palname).Where(n => !string.IsNullOrEmpty(n)), StringComparer.Ordinal);
                return new TextureCoverage
                {
                    RequiredTextures = wantTextures.Count,
                    MatchedTextures = wantTextures.Count(haveTextures.Contains),
                    RequiredPalettes = wantPalettes.Count,
                    MatchedPalettes = wantPalettes.Count(havePalettes.Contains),
                };
            }
            catch { return new TextureCoverage(); }
        }

        /// <summary>Reads the pictures and palettes in a texture-only entry.</summary>
        public static bool LoadTextureBundle(Archive a, int index,
            out List<NSBMDTexture> textures, out List<NSBMDPalette> palettes)
        {
            textures = new List<NSBMDTexture>();
            palettes = new List<NSBMDPalette>();
            var narc = new ScriptNarc(a.Dir);
            if (!narc.Available) return false;
            var bytes = narc.Get(index);
            if (Identify(bytes) != Kind.TextureBundle) return false;
            try
            {
                using var input = new MemoryStream(bytes);
                NSBTXLoader.LoadNsbtx(input, out textures, out palettes);
                return textures.Count > 0;
            }
            catch { return false; }
        }

        /// <summary>The pictures to draw a model with. A chosen set wins; otherwise whatever the model
        /// carries itself, or the set filed next to it.</summary>
        public static byte[] TexturesFor(Archive a, int index, int chosenSet = -1)
        {
            if (chosenSet >= 0) return TextureSet(a, chosenSet);
            return EmbeddedTextures(a, index) ?? NeighbouringTextures(a, index);
        }

        /// <summary>Paints a set of pictures onto a model that has been read but not yet dressed.</summary>
        public static bool Dress(NSBMD nsbmd, byte[] textures)
        {
            if (nsbmd == null || textures == null || textures.Length <= 4) return false;
            try
            {
                nsbmd.materials = NSBTXLoader.LoadNsbtx(new MemoryStream(textures), out nsbmd.Textures, out nsbmd.Palettes);
                nsbmd.MatchTextures();
                return true;
            }
            catch (Exception ex) { AppLogger.Error("ModelAssets.Dress failed: " + ex.Message); return false; }
        }

        /// <summary>The name a model file calls itself, without reading the model.</summary>
        public static string NameInFile(byte[] file)
        {
            if (file == null || file.Length < 32) return null;
            int at = Find(file, "MDL0");
            if (at < 0 || at + 10 > file.Length) return null;

            int num = file[at + 9];
            if (num <= 0 || num > 64) return null;
            int names = at + 24 + num * 8;
            if (names + 16 > file.Length) return null;

            var raw = System.Text.Encoding.ASCII.GetString(file, names, 16);
            int end = raw.IndexOf('\0');
            string name = (end >= 0 ? raw.Substring(0, end) : raw).Trim();
            foreach (char c in name) if (c < 32 || c > 126) return null;
            return name.Length == 0 ? null : name;
        }

        private static int Find(byte[] b, string tag)
        {
            for (int i = 0; i + 4 <= b.Length && i < 512; i++)
                if (b[i] == tag[0] && b[i + 1] == tag[1] && b[i + 2] == tag[2] && b[i + 3] == tag[3])
                    return i;
            return -1;
        }

        /// <summary>One file of a thing, and what that file is.</summary>
        public sealed class UnitPart
        {
            public int Index;
            public string Name;
            public Kind Kind;
        }

        /// <summary>One thing in a 3D archive. </summary>
        public sealed class Unit
        {
            public Archive Archive;
            public string Name;
            public List<UnitPart> Parts = new();
            public int First => Parts.Count > 0 ? Parts[0].Index : 0;
        }

        /// <summary>Plain words for a piece of a thing.</summary>
        private static string PartName(Kind k) => k switch
        {
            Kind.Model => "The shape",
            Kind.TextureBundle => "Its pictures",
            Kind.JointAnimation => "Movement",
            Kind.TextureAnimation => "Sliding pictures",
            Kind.TextureSwap => "Changing pictures",
            Kind.VisibilityAnimation => "What shows when",
            Kind.MaterialAnimation => "Colour over time",
            Kind.Empty => "Empty",
            _ => "Other data",
        };

        /// <summary>
        /// Breaks a 3D archive into the things it holds, and gives each the name its file carries.
        /// </summary>
        public static List<Unit> Units(Archive a, int fileCount)
        {
            var units = new List<Unit>();
            var narc = new ScriptNarc(a.Dir);
            if (!narc.Available || fileCount <= 0) return units;

            Unit open = null;
            for (int i = 0; i < fileCount; i++)
            {
                byte[] b;
                try { b = narc.Get(i); } catch { b = null; }
                var kind = Identify(b);

                if (kind == Kind.Model)
                {
                    open = new Unit { Archive = a, Name = NameInFile(b) ?? a.Title };
                    // fall through to add the part below
                    open.Parts.Add(new UnitPart { Index = i, Name = PartName(kind), Kind = kind });
                    units.Add(open);
                    continue;
                }

                // Only the kinds a model actually carries with it get folded in. An archive that is all
                // picture sets or all movement keeps one row each, because there is no model to attach to.
                bool belongsToAModel = open != null
                    && (kind == Kind.TextureBundle || kind == Kind.JointAnimation
                        || kind == Kind.TextureAnimation || kind == Kind.TextureSwap
                        || kind == Kind.VisibilityAnimation || kind == Kind.MaterialAnimation);

                if (belongsToAModel)
                {
                    open.Parts.Add(new UnitPart { Index = i, Name = PartName(kind), Kind = kind });
                    continue;
                }

                open = null;
                // A set of pictures is named after the first picture in it, which is who it actually is.
                string lonely = kind == Kind.TextureBundle ? FirstTextureName(b) : null;
                var lone = new Unit { Archive = a, Name = lonely ?? a.Title };
                lone.Parts.Add(new UnitPart { Index = i, Name = PartName(kind), Kind = kind });
                units.Add(lone);
            }
            return units;
        }

        /// <summary>
        /// What one entry calls itself, whatever kind it is, or null when it says nothing.
        /// </summary>
        public static string NameOf(Archive a, int index)
        {
            var narc = new ScriptNarc(a.Dir);
            if (!narc.Available) return null;
            var b = narc.Get(index);
            if (b == null) return null;

            var kind = Identify(b);
            if (kind == Kind.Model) return NameInFile(b);
            if (kind == Kind.TextureBundle) return FirstTextureName(b);
            return null;
        }

        /// <summary>The name of the first picture in a set, read with the real reader rather than by
        /// hunting for readable bytes, which found rubbish in half the files.</summary>
        private static string FirstTextureName(byte[] file)
        {
            try
            {
                using var ms = new MemoryStream(file);
                NSBTXLoader.LoadNsbtx(ms, out var textures, out _);
                var first = textures?.FirstOrDefault();
                string name = first?.texname?.Trim();
                if (string.IsNullOrEmpty(name)) return null;
                foreach (char c in name) if (c < 32 || c > 126) return null;

                // The pictures in a set are numbered off the set's name, as babyboy1.1, babyboy1.2 and so
                // on, so the number belongs to the picture rather than to the set.
                int dot = name.LastIndexOf('.');
                if (dot > 0 && dot < name.Length - 1 && name.Skip(dot + 1).All(char.IsDigit))
                    name = name.Substring(0, dot);
                return name;
            }
            catch { return null; }
        }

        /// <summary>
        /// How well a movement's own name says it belongs to a model: 2 when the two names are the same,
        /// 1 when one starts with the other, 0 when they have nothing to do with each other.
        ///
        /// The names really do line up. Across HeartGold's 340 building models and 273 movements, 85 of
        /// the movements carry a name at all; 8 of those are exactly a model's name and 46 share a start
        /// with one, so this finds real pairs rather than coincidences.
        /// </summary>
        /// <summary>
        /// A name cut down to the thing it names: lower case, without the game's own prefix, and with
        /// anything after the first underscore dropped. So pl_manene and manene_aruku both come out as
        /// manene, which is what lets a clip be matched to the model it was written for.
        /// </summary>
        public static string BaseName(string name)
        {
            string s = (name ?? "").Trim().ToLowerInvariant();
            foreach (string prefix in new[] { "pl_", "dp_", "hg_", "ss_", "pt_", "d_", "p_" })
                if (s.StartsWith(prefix, StringComparison.Ordinal)) { s = s.Substring(prefix.Length); break; }
            int us = s.IndexOf('_');
            return us > 0 ? s.Substring(0, us) : s;
        }

        /// <summary>How sure we are that an animation was written for a model.</summary>
        public enum Match
        {
            None,
            /// <summary>The cut-down names agree. Loose, because cutting throws detail away.</summary>
            SameFamily,
            /// <summary>One name starts with the other, sharing a real run of characters.</summary>
            SameStart,
            /// <summary>The names are the same.</summary>
            Exact,
        }

        /// <summary>
        /// How well an animation's clips fit a model. Two rules, because neither finds everything: the
        /// cut-down name catches door_op against p_door, and the shared start catches treeeff01 against
        /// treeeff. Counted over three games, using both finds pairs either alone would miss.
        /// </summary>
        public static Match MatchFor(IEnumerable<string> clipNames, string modelName)
        {
            if (string.IsNullOrWhiteSpace(modelName) || clipNames == null) return Match.None;
            var clips = clipNames.Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
            if (clips.Count == 0) return Match.None;

            var best = Match.None;
            foreach (string clip in clips)
            {
                int shared = NameMatch(modelName, clip);
                if (shared == 2) return Match.Exact;
                if (shared == 1) best = Match.SameStart;
                else if (best == Match.None)
                {
                    string want = BaseName(modelName);
                    // A base of one or two letters says nothing; most of a game's models share those.
                    if (want.Length >= 3 && BaseName(clip) == want) best = Match.SameFamily;
                }
            }
            return best;
        }

        /// <summary>Whether any of an animation's clips was written for this model.</summary>
        public static bool BelongsTo(IEnumerable<string> clipNames, string modelName)
            => MatchFor(clipNames, modelName) != Match.None;

        public static int NameMatch(string modelName, string movementName)
        {
            if (string.IsNullOrWhiteSpace(modelName) || string.IsNullOrWhiteSpace(movementName)) return 0;
            modelName = modelName.Trim();
            movementName = movementName.Trim();
            if (string.Equals(modelName, movementName, StringComparison.OrdinalIgnoreCase)) return 2;

            // A shared start has to be a real one. Two names that only agree on "en_" say nothing, since
            // most of a game's models begin that way.
            int shared = 0;
            while (shared < modelName.Length && shared < movementName.Length
                   && char.ToLowerInvariant(modelName[shared]) == char.ToLowerInvariant(movementName[shared]))
                shared++;
            if (shared < 4) return 0;
            return movementName.StartsWith(modelName, StringComparison.OrdinalIgnoreCase)
                || modelName.StartsWith(movementName, StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        }

        /// <summary>
        /// The movements the game itself gives this model, as entries of the movement archive.
        /// </summary>
        public static IReadOnlyList<int> OwnAnimations(Archive a, int index)
        {
            if (a.AnimationArchive == null) return Array.Empty<int>();
            try
            {
                if (!BuildingAnimationSet.Available) return Array.Empty<int>();
                var info = BuildingAnimationSet.InfoFor(index, a.Indoor);
                if (info == null || !info.Animates) return Array.Empty<int>();
                return info.UsedCodes.Where(c => c >= 0).Distinct().ToList();
            }
            catch (Exception ex)
            {
                AppLogger.Error("ModelAssets.OwnAnimations failed: " + ex.Message);
                return Array.Empty<int>();
            }
        }

        /// <summary>How many movements this kind of model can be given.</summary>
        public static int AnimationCount(Archive a)
        {
            if (a.AnimationArchive == null) return 0;
            var narc = new ScriptNarc(a.AnimationArchive.Value);
            return narc.Available ? narc.Count : 0;
        }

        /// <summary>One movement out of the archive these models take theirs from, or the one filed next
        /// to the model when it keeps its own.</summary>
        public static JointAnimation AnimationFor(Archive a, int index, int chosen)
        {
            byte[] raw = null;
            if (chosen >= 0 && a.AnimationArchive != null)
            {
                var other = new ScriptNarc(a.AnimationArchive.Value);
                if (other.Available) raw = other.Get(chosen);
            }
            else if (chosen < 0)
            {
                // Overworld people keep their model, pictures and movement together, so look just after.
                var narc = new ScriptNarc(a.Dir);
                if (!narc.Available) return null;
                for (int i = index + 1; i < index + 4; i++)
                {
                    var b = narc.Get(i);
                    if (b == null) break;
                    if (Identify(b) == Kind.JointAnimation) { raw = b; break; }
                }
            }

            if (raw == null || Identify(raw) != Kind.JointAnimation) return null;
            try
            {
                var anim = JointAnimation.Load(raw);
                return anim != null && anim.Moves ? anim : null;
            }
            catch (Exception ex) { AppLogger.Error("ModelAssets.AnimationFor failed: " + ex.Message); return null; }
        }

        /// <summary>Saves an entry exactly as it sits in the ROM. Always possible for a real entry.</summary>

        /// <summary>
        /// What a mesh from a 3D program cannot do, said once so every place that needs it says the same.
        /// </summary>
        public const string CanConvertAMesh =
            "An OBJ is turned into a model as it goes in: its corners, the way they face, where they " + 
            "land on their pictures, and the colours and pictures its materials name. A finished NSBMD " + 
            "goes in as it is.";

        /// <summary>Puts a file back into an archive, in place of one entry.</summary>
        /// <summary>
        /// Puts a mesh in as a model, turning it into one on the way. Returns why not, or null, and
        /// hands back a line saying what it came to.
        /// </summary>
        public static string ImportMesh(Archive a, int index, string path, out string note)
        {
            note = null;
            var narc = new ScriptNarc(a.Dir);
            if (!narc.Available) return "This game does not have this archive.";
            var there = narc.Get(index);
            if (there == null) return "There is no entry here to put a model in place of.";
            if (Identify(there) != Kind.Model)
                return $"This entry holds {ShortName(Identify(there))}, not a model, so a mesh does not "
                     + "belong here. Pick an entry that holds a model.";

            var mesh = ObjMesh.Read(path, out string whynot);
            if (mesh == null) return whynot;

            var textures = new List<DsTexture>();
            foreach (var m in mesh.Materials)
            {
                if (m.TexturePath == null) continue;
                byte[] png;
                try { png = File.ReadAllBytes(m.TexturePath); } catch { continue; }
                if (!AnyPng.TryReadRgba(png, out var rgba, out int w, out int h, out string pngWhy))
                    return $"{Path.GetFileName(m.TexturePath)} could not be read: {pngWhy}";
                var t = DsTexture.From(rgba, w, h, m.Name);
                if (t.Whynot != null) return t.Whynot;
                textures.Add(t);
            }

            var made = NsbmdWriter.Build(mesh, textures);
            if (made.Whynot != null) return made.Whynot;

            narc.Put(index, made.Bytes);
            note = made.Summary + (made.Notes.Count > 0 ? " " + string.Join(" ", made.Notes) : "");
            return null;
        }

        public static string ImportRaw(Archive a, int index, string path)
        {
            var narc = new ScriptNarc(a.Dir);
            if (!narc.Available) return "This game does not have this archive.";

            var there = narc.Get(index);
            if (there == null) return "There is no entry here to put a file in place of.";

            // A mesh is not 3D data the DS can read until it has been turned into some, so it takes
            // the long way in.
            if (string.Equals(Path.GetExtension(path), ".obj", StringComparison.OrdinalIgnoreCase))
                return ImportMesh(a, index, path, out _);

            byte[] file;
            try { file = File.ReadAllBytes(path); }
            catch (Exception ex) { return "That file could not be read: " + ex.Message; }
            if (file.Length < 4) return "That file is too short to be 3D data.";

            var was = Identify(there);
            var now = Identify(file);

            if (now == Kind.NotThreeD || now == Kind.Empty)
                return "That file is not 3D data. " + CanConvertAMesh;

            if (was != Kind.NotThreeD && was != Kind.Empty && now != was)
                return $"This entry holds {ShortName(was)} and that file holds {ShortName(now)}. Put a "
                     + "file of the same kind in, or pick the entry that kind belongs in.";

            narc.Put(index, file);
            return null;
        }

        /// <summary>Whether an entry can have a file put in over it, and why not when it cannot.</summary>
        public static string CannotImportBecause(Archive a, int index)
        {
            var narc = new ScriptNarc(a.Dir);
            if (!narc.Available) return "This game does not have this archive.";
            var there = narc.Get(index);
            if (there == null || there.Length == 0)
                return "This entry is empty, so there is nothing to say what belongs here.";
            var kind = Identify(there);
            if (kind == Kind.NotThreeD)
                return "This entry is not 3D data, so this window does not know what could go in it.";
            return null;
        }

        public static string SaveRaw(Archive a, int index, string path)
        {
            var narc = new ScriptNarc(a.Dir);
            if (!narc.Available) return "This game does not have this archive.";
            var b = narc.Get(index);
            if (b == null || b.Length == 0) return "This entry is empty.";
            File.WriteAllBytes(path, b);
            return null;
        }

        public static int Count(Archive a)
        {
            var narc = new ScriptNarc(a.Dir);
            return narc.Available ? narc.Count : 0;
        }
    }
}
