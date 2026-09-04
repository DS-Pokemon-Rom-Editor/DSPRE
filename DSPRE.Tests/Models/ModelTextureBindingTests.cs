using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using DSPRE.Avalonia.Gl;
using DSPRE.ROMFiles;
using LibNDSFormats.NSBMD;
using Xunit;

namespace DSPRE.Tests
{
    public class ModelTextureBindingTests
    {
        [Fact]
        public void EveryMaterialIsMatchedEvenWhenNoPolygonListExists()
        {
            var model = new NSBMDModel();
            model.Materials.Add(new NSBMDMaterial());
            model.Materials.Add(new NSBMDMaterial());
            model.Textures.Add(new NSBMDTexture { texname = "wall", texmatid = { 1 } });
            model.Palettes.Add(new NSBMDPalette { palname = "wall_pl", palmatid = { 1 } });

            var texture = new NSBMDTexture
            {
                texname = "wall", format = 2, width = 8, height = 8, texdata = new byte[16],
            };
            var palette = new NSBMDPalette
            {
                palname = "wall_pl", paldata = new[] { new RGBA { R = 7, G = 8, B = 9, A = 255 } },
            };
            var file = new NSBMD { models = new[] { model } };
            file.Textures.Add(texture);
            file.Palettes.Add(palette);

            file.MatchTextures();

            Assert.Same(texture.texdata, model.Materials[1].texdata);
            Assert.Same(palette.paldata, model.Materials[1].paldata);
            Assert.False(model.Materials[1].missingExternalTexture);
        }

        [Fact]
        public void ABundleMissingARequestedTextureKeepsTheModelsMaterialColour()
        {
            var colour = Color.FromArgb(255, 12, 34, 56);
            var model = new NSBMDModel();
            model.Materials.Add(new NSBMDMaterial { DiffuseColor = colour });
            model.Textures.Add(new NSBMDTexture { texname = "missing", texmatid = { 0 } });
            var file = new NSBMD { models = new[] { model } };

            file.MatchTextures();

            Assert.True(model.Materials[0].missingExternalTexture);
            Assert.Equal(colour, model.Materials[0].DiffuseColor);
        }

        [Fact]
        public void TransparentColourZeroDoesNotModifyTheSharedPalette()
        {
            var original = new RGBA { R = 10, G = 20, B = 30, A = 255 };
            var palette = new[] { original, new RGBA { R = 40, G = 50, B = 60, A = 255 } };
            var material = new NSBMDMaterial
            {
                format = 2, width = 2, height = 2, color0 = 1,
                texdata = new byte[] { 0 }, paldata = palette,
            };

            var decoded = NsbmdTextureDecoder.Decode(material);

            Assert.NotNull(decoded);
            Assert.Equal(0, decoded.Rgba[3]);
            Assert.Equal(original.R, palette[0].R);
            Assert.Equal(original.G, palette[0].G);
            Assert.Equal(original.B, palette[0].B);
            Assert.Equal(original.A, palette[0].A);
        }

        [Fact]
        public void BuildingModelListUsesItsCountAndSixteenBitIds()
        {
            var ids = BuildingModelTextureSets.ParseModelIds(new byte[]
            {
                3, 0, 4, 0, 0x34, 0x12, 9, 0,
            });

            Assert.Equal(new[] { 4, 0x1234, 9 }, ids);
        }

        [Fact]
        public void TruncatedBuildingModelListIsRejected()
        {
            Assert.Throws<InvalidDataException>(() =>
                BuildingModelTextureSets.ParseModelIds(new byte[] { 2, 0, 4, 0 }));
        }

        [Fact]
        public void GeometryUsesDisplayListVertexColours()
        {
            var data = new List<byte>();
            data.AddRange(new byte[] { 0x40, 0x20, 0x23, 0x23 });
            Word(data, 0);                 // triangles
            Word(data, 0x001f);            // red, in the DS RGB555 command format
            Vertex(data, 0, 0, 0);
            Vertex(data, 4096, 0, 0);
            data.AddRange(new byte[] { 0x23, 0x41, 0xff, 0xff });
            Vertex(data, 0, 4096, 0);

            var model = new NSBMDModel { modelScale = 1 };
            model.Materials.Add(new NSBMDMaterial { DiffuseColor = Color.White, Alpha = 31 });
            model.Polygons.Add(new NSBMDPolygon { MatId = 0, StackID = -1, PolyData = data.ToArray() });

            var shown = NsbmdGeometry.BuildModel(model);

            Assert.Single(shown.Parts);
            Assert.Equal(3, shown.Parts[0].VertexCount);
            for (int i = 0; i < shown.Parts[0].Vertices.Length; i += 8)
            {
                Assert.Equal(1f, shown.Parts[0].Vertices[i + 5]);
                Assert.Equal(0f, shown.Parts[0].Vertices[i + 6]);
                Assert.Equal(0f, shown.Parts[0].Vertices[i + 7]);
            }
        }

        private static void Vertex(List<byte> bytes, int x, int y, int z)
        {
            Word(bytes, (y << 16) | (x & 0xffff));
            Word(bytes, z & 0xffff);
        }

        private static void Word(List<byte> bytes, int value) => bytes.AddRange(BitConverter.GetBytes(value));
    }
}
