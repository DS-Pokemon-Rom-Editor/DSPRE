using DSPRE.Avalonia.Gl;
using DSPRE.Models;
using System;
using System.IO;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests.Models
{
    public class MeshPickTests
    {
        private readonly ITestOutputHelper _out;
        public MeshPickTests(ITestOutputHelper o) { _out = o; }

        [SkippableFact]
        public void AStraightLineDownFindsTheGroundUnderIt()
        {
            Skip.If(!Directory.Exists(TestRoms.Platinum), "Platinum test project not configured");

            var (name, bytes) = MapModels.Of(TestRoms.Platinum, false).First();
            var mesh = MapMesh.Read(bytes, out _);
            Assert.NotNull(mesh);

            int hits = 0;
            foreach (var face in mesh.Faces.Take(200))
            {
                var middle = Middle(mesh, face);
                var hit = MeshPick.Nearest(mesh, null, middle.x, middle.y + 10f, middle.z, 0f, -1f, 0f);
                if (hit == null) continue;

                Assert.True(hit.Value.Distance > 0f);
                Assert.True(Math.Abs(hit.Value.X - middle.x) < 8f);
                hits++;
            }

            Assert.True(hits > 0, $"{name}: no face was found under any line, so this proved nothing.");
            _out.WriteLine($"{name}: {hits} of 200 faces found from straight above.");
        }

        [Fact]
        public void TheNearerOfTwoStackedFacesIsTheOneThatIsPicked()
        {
            var mesh = Flat(0f, 1f);

            var above = MeshPick.Nearest(mesh, null, 0.5f, 5f, 0.5f, 0f, -1f, 0f);
            Assert.NotNull(above);
            Assert.Equal(1, mesh.Faces[above.Value.Face].Run);

            var below = MeshPick.Nearest(mesh, null, 0.5f, -5f, 0.5f, 0f, 1f, 0f);
            Assert.NotNull(below);
            Assert.Equal(0, mesh.Faces[below.Value.Face].Run);
        }

        [Fact]
        public void AFaceSeenExactlyEdgeOnIsNotPicked()
        {
            var mesh = Flat(0f, 1f);

            var along = MeshPick.Nearest(mesh, null, -5f, 0f, 0.5f, 1f, 0f, 0f);
            Assert.Null(along);
        }

        [Fact]
        public void AClickThatMissesEverythingFindsNothing()
        {
            var mesh = Flat(0f, 1f);
            Assert.Null(MeshPick.Nearest(mesh, null, 50f, 5f, 50f, 0f, -1f, 0f));
        }

        [Fact]
        public void TheSceneMatrixIsUsedRatherThanTheNumbersInTheFile()
        {
            var mesh = Flat(0f, 1f);

            var moved = Mat4.Translate(10f, 0f, 0f);
            Assert.Null(MeshPick.Nearest(mesh, moved, 0.5f, 5f, 0.5f, 0f, -1f, 0f));
            Assert.NotNull(MeshPick.Nearest(mesh, moved, 10.5f, 5f, 0.5f, 0f, -1f, 0f));
        }

        [Fact]
        public void OnlyFacesThatAreAllowedArePicked()
        {
            var mesh = Flat(0f, 1f);

            var hit = MeshPick.Nearest(mesh, null, 0.5f, 5f, 0.5f, 0f, -1f, 0f,
                                       f => mesh.Faces[f].Run == 0);
            Assert.NotNull(hit);
            Assert.Equal(0, mesh.Faces[hit.Value.Face].Run);
        }

        [Fact]
        public void ThePickSaysWhichCornerOfTheFaceItLandedNearest()
        {
            var mesh = Flat(0f);

            var hit = MeshPick.Nearest(mesh, null, 0.95f, 5f, 0.95f, 0f, -1f, 0f);
            Assert.NotNull(hit);

            var corner = mesh.Vertices[hit.Value.NearestCorner];
            Assert.Equal(1f, corner.X, 3);
            Assert.Equal(1f, corner.Z, 3);
        }

        private static MapMesh Flat(params float[] heights)
        {
            var mesh = (MapMesh)Activator.CreateInstance(typeof(MapMesh), nonPublic: true);
            for (int h = 0; h < heights.Length; h++)
            {
                int at = mesh.Vertices.Count;
                foreach (var (x, z) in new[] { (0f, 0f), (1f, 0f), (1f, 1f), (0f, 1f) })
                    mesh.Vertices.Add(new MapMesh.Vertex { X = x, Y = heights[h], Z = z });

                mesh.Faces.Add(new MapMesh.Face
                {
                    Shape = 0,
                    Run = h,
                    Material = -1,
                    Corners = new[] { at, at + 1, at + 2, at + 3 },
                    OnPicture = new (float, float)[4],
                });
            }
            return mesh;
        }

        private static (float x, float y, float z) Middle(MapMesh mesh, MapMesh.Face face)
        {
            float x = 0, y = 0, z = 0;
            foreach (int c in face.Corners)
            {
                x += mesh.Vertices[c].X; y += mesh.Vertices[c].Y; z += mesh.Vertices[c].Z;
            }
            int n = face.Corners.Length;
            return (x / n, y / n, z / n);
        }
    }
}
