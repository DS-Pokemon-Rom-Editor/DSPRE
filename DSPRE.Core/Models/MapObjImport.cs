using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DSPRE.Models
{
    /// <summary>Builds a map model from an OBJ exported by DSPRE or PDSMS.</summary>
    public static class MapObjImport
    {
        public enum Measure
        {
            Dspre,

            MapStudio,
        }

        public static Measure Guess(string path)
        {
            try
            {
                foreach (string line in File.ReadLines(path).Take(5))
                    if (line.StartsWith("# Written by DSPRE", StringComparison.Ordinal)) return Measure.Dspre;
            }
            catch { }
            return Measure.MapStudio;
        }

        public static byte[] Build(string path, Measure measure, Func<string, (int w, int h)> sizeOf,
                                   out string whynot, out List<string> notes, float drawnAtScale = 64f)
        {
            notes = new List<string>();
            ObjMesh obj = ObjMesh.Read(path, out whynot);
            if (obj == null) return null;
            notes.AddRange(obj.Notes);

            (float x, float y, float z) Place(ObjMesh.Vec3 p) => measure == Measure.Dspre
                ? (p.X, p.Y, p.Z)
                : (p.X * MapTileset.TileWidth, p.Z * MapTileset.TileWidth, -p.Y * MapTileset.TileWidth);

            TileBake.Result r = new TileBake.Result();
            foreach (ObjMesh.Vec3 p in obj.Positions) r.Corners.Add(Place(p));

            float reach = r.Corners.Count == 0 ? 0 : r.Corners.Max(c => Math.Max(Math.Abs(c.x), Math.Abs(c.z)));
            if (reach > MapTileset.HalfMap * 1.5f)
                notes.Add($"Model extends {reach / MapTileset.TileWidth:0} tiles from the centre; check the scale.");

            HashSet<string> unsized = new HashSet<string>();
            foreach (ObjMesh.Face face in obj.Faces)
            {
                ObjMesh.Material m = face.Material >= 0 && face.Material < obj.Materials.Count ? obj.Materials[face.Material] : null;
                string picture = m == null ? "" : m.PictureName ?? m.Name;
                (int w, int h) = sizeOf?.Invoke(picture) ?? (0, 0);
                if (w <= 0 || h <= 0) { unsized.Add(picture); w = h = 1; }

                r.Faces.Add(face.Corners.Select(c => c.Position).ToArray());
                r.OnPicture.Add(face.Corners.Select(c =>
                {
                    ObjMesh.Vec2 uv = c.TexCoord >= 0 && c.TexCoord < obj.TexCoords.Count ? obj.TexCoords[c.TexCoord] : new ObjMesh.Vec2();
                    return (uv.U * w, uv.V * h);
                }).ToArray());
                r.Picture.Add(picture);
                r.Palette.Add(m?.PaletteName ?? "");
                r.Look.Add(m?.Look ?? MaterialLook.Plain.WithPictureSize(w, h));
                r.Light.Add(face.Corners.Select((c, i) =>
                {
                    MapTileset.Corner corner = new MapTileset.Corner();
                    if (face.Colours != null && i < face.Colours.Length) corner.Colour = face.Colours[i];
                    if (face.ColoursLast != null && i < face.ColoursLast.Length) corner.ColourLast = face.ColoursLast[i];
                    if (c.Normal >= 0 && c.Normal < obj.Normals.Count)
                    {
                        ObjMesh.Vec3 n = obj.Normals[c.Normal];
                        (float nx, float ny, float nz) = measure == Measure.Dspre ? (n.X, n.Y, n.Z) : (n.X, n.Z, -n.Y);
                        corner.Faces = true; corner.NX = nx; corner.NY = ny; corner.NZ = nz;
                    }
                    return corner;
                }).ToArray());
            }

            unsized.Remove("");
            if (unsized.Count > 0)
                notes.Add($"Missing textures: {string.Join(", ", unsized.Take(6))}");

            return TileBake.ToModel(r, out whynot, drawnAtScale);
        }
    }
}
