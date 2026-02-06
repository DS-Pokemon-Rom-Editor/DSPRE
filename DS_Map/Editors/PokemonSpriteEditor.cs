using DSPRE.Editors.Utils;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using static DSPRE.RomInfo;

namespace DSPRE.Editors {
    public partial class PokemonSpriteEditor : Form {
        #region Constants and Static Data
        private static readonly string formName = "Sprite Editor";
        
        private static readonly string[] spriteTypeNames = { 
            "Female backsprite", "Male backsprite", "Female frontsprite", "Male frontsprite", "Shiny" 
        };

        /// <summary>
        /// Represents sprite template data for a Pokemon form.
        /// Based on game's BuildPokemonSpriteTemplate functions.
        /// </summary>
        private struct FormSpriteData {
            public string Name;
            public int BackSpriteIndex;   // character index for back sprite (face/2 = 0)
            public int FrontSpriteIndex;  // character index for front sprite (face/2 = 1)
            public int NormalPaletteIndex;
            public int ShinyPaletteIndex;
            public bool HasGenderDifference; // If true, uses 4 sprites; otherwise back=front for both genders
            
            public FormSpriteData(string name, int backIdx, int frontIdx, int normalPal, int shinyPal, bool genderDiff = false) {
                Name = name;
                BackSpriteIndex = backIdx;
                FrontSpriteIndex = frontIdx;
                NormalPaletteIndex = normalPal;
                ShinyPaletteIndex = shinyPal;
                HasGenderDifference = genderDiff;
            }
        }

        private readonly int[] validPalettesHGSS = new int[]
        {
            158, 159, 160, 161, 162, 163, 164, 165, 166, 167,
            168, 169, 170, 171, 172, 173, 174, 175, 176, 177,
            178, 179, 180, 181, 182, 183, 184, 185, 186, 187,
            188, 189, 190, 191, 192, 193, 194, 195, 196, 197,
            198, 199, 200, 201, 202, 203, 204, 205, 206, 207,
            208, 209, 210, 211, 212, 213, 214, 215, 216, 217,
            218, 219, 220, 221, 222, 223, 224, 225, 226, 227,
            228, 229, 230, 231, 232, 233, 234, 235, 236, 237,
            238, 239, 240, 241, 242, 243, 244, 245, 246, 247,
            248, 249, 250, 251, 252, 253, 254, 255, 258, 260
        };

        private readonly int[] validPalettesPt = new int[]
        {
            154, 155, 156, 157, 158, 159, 160, 161, 162, 163,
            164, 165, 166, 167, 168, 169, 170, 171, 172, 173,
            174, 175, 176, 177, 178, 179, 180, 181, 182, 183,
            184, 185, 186, 187, 188, 189, 190, 191, 192, 193,
            194, 195, 196, 197, 198, 199, 200, 201, 202, 203,
            204, 205, 206, 207, 208, 209, 210, 211, 212, 213,
            214, 215, 216, 217, 218, 219, 220, 221, 222, 223,
            224, 225, 226, 227, 228, 229, 230, 231, 232, 233,
            234, 235, 236, 237, 238, 239, 240, 241, 242, 243,
            244, 245, 246, 247, 250, 252
        };

        private readonly int[] validPalettesDP = new int[]
        {
            134, 135, 136, 137, 138, 139, 140, 141, 142, 145,
            146, 147, 148, 149, 150, 151, 152, 153, 154, 155,
            156, 157, 158, 159, 160, 161, 162, 163, 164, 165,
            166, 167, 168, 169, 170, 171, 172, 173, 174, 175,
            176, 177, 178, 179, 180, 181, 182, 183, 184, 185,
            186, 187, 188, 189, 190, 191, 192, 193, 194, 195,
            196, 197, 198, 199, 200, 201, 202, 203, 204, 205,
            206, 207, 210, 212
        };

        /// <summary>
        /// Form sprite data for Diamond/Pearl's OTHERPOKE NARC.
        /// Based on BuildPokemonSpriteTemplateDP from game code.
        /// Format: character = base + (face/2) where face: 0=FBack, 1=MBack, 2=FFront, 3=MFront
        /// So back sprites use base+0, front sprites use base+1
        /// </summary>
        private FormSpriteData[] GetFormDataDP() {
            return new FormSpriteData[] {
                // Deoxys: character = 0 + (face/2) + form*2, palette = 134 + shiny (shared palette for all forms)
                new FormSpriteData("Deoxys - Normal",   0,  1, 134, 135),
                new FormSpriteData("Deoxys - Attack",   2,  3, 134, 135),
                new FormSpriteData("Deoxys - Defense",  4,  5, 134, 135),
                new FormSpriteData("Deoxys - Speed",    6,  7, 134, 135),
                
                // Unown: character = 8 + (face/2) + form*2, palette = 136 + shiny (shared palette)
                new FormSpriteData("Unown - A",  8,  9, 136, 137),
                new FormSpriteData("Unown - B", 10, 11, 136, 137),
                new FormSpriteData("Unown - C", 12, 13, 136, 137),
                new FormSpriteData("Unown - D", 14, 15, 136, 137),
                new FormSpriteData("Unown - E", 16, 17, 136, 137),
                new FormSpriteData("Unown - F", 18, 19, 136, 137),
                new FormSpriteData("Unown - G", 20, 21, 136, 137),
                new FormSpriteData("Unown - H", 22, 23, 136, 137),
                new FormSpriteData("Unown - I", 24, 25, 136, 137),
                new FormSpriteData("Unown - J", 26, 27, 136, 137),
                new FormSpriteData("Unown - K", 28, 29, 136, 137),
                new FormSpriteData("Unown - L", 30, 31, 136, 137),
                new FormSpriteData("Unown - M", 32, 33, 136, 137),
                new FormSpriteData("Unown - N", 34, 35, 136, 137),
                new FormSpriteData("Unown - O", 36, 37, 136, 137),
                new FormSpriteData("Unown - P", 38, 39, 136, 137),
                new FormSpriteData("Unown - Q", 40, 41, 136, 137),
                new FormSpriteData("Unown - R", 42, 43, 136, 137),
                new FormSpriteData("Unown - S", 44, 45, 136, 137),
                new FormSpriteData("Unown - T", 46, 47, 136, 137),
                new FormSpriteData("Unown - U", 48, 49, 136, 137),
                new FormSpriteData("Unown - V", 50, 51, 136, 137),
                new FormSpriteData("Unown - W", 52, 53, 136, 137),
                new FormSpriteData("Unown - X", 54, 55, 136, 137),
                new FormSpriteData("Unown - Y", 56, 57, 136, 137),
                new FormSpriteData("Unown - Z", 58, 59, 136, 137),
                new FormSpriteData("Unown - !", 60, 61, 136, 137),
                new FormSpriteData("Unown - ?", 62, 63, 136, 137),
                
                // Castform: character = 64 + (face*2) + form, palette = 138 + (shiny*4) + form
                // face*2 means: back=0,2 front=4,6 - but this doesn't fit standard pattern
                // Actually: character = 64 + (face * 2) + form where face is 0-3
                // So Normal form back is 64, front is 68; Sunny back is 65, front is 69, etc.
                new FormSpriteData("Castform - Normal", 64, 68, 138, 142),
                new FormSpriteData("Castform - Sunny",  65, 69, 139, 143),
                new FormSpriteData("Castform - Rainy",  66, 70, 140, 144),
                new FormSpriteData("Castform - Snowy",  67, 71, 141, 145),
                
                // Burmy: character = 72 + (face/2) + form*2, palette = 146 + shiny + form*2
                new FormSpriteData("Burmy - Plant", 72, 73, 146, 147),
                new FormSpriteData("Burmy - Sandy", 74, 75, 148, 149),
                new FormSpriteData("Burmy - Trash", 76, 77, 150, 151),
                
                // Wormadam: character = 78 + (face/2) + form*2, palette = 152 + shiny + form*2
                new FormSpriteData("Wormadam - Plant", 78, 79, 152, 153),
                new FormSpriteData("Wormadam - Sandy", 80, 81, 154, 155),
                new FormSpriteData("Wormadam - Trash", 82, 83, 156, 157),
                
                // Shellos: character = 84 + face + form (has gender sprites), palette = 158 + shiny + form*2
                new FormSpriteData("Shellos - West", 84, 86, 158, 159, true),  // 84=FBack, 85=MBack, 86=FFront, 87=MFront
                new FormSpriteData("Shellos - East", 85, 87, 160, 161, true),  // Actually face + form, so East adds 1
                
                // Gastrodon: character = 88 + face + form, palette = 162 + shiny + form*2
                new FormSpriteData("Gastrodon - West", 88, 90, 162, 163, true),
                new FormSpriteData("Gastrodon - East", 89, 91, 164, 165, true),
                
                // Cherrim: character = 92 + face + form, palette = 166 + (shiny*2) + form
                new FormSpriteData("Cherrim - Overcast",  92, 94, 166, 168, true),
                new FormSpriteData("Cherrim - Sunshine",  93, 95, 167, 169, true),
                
                // Arceus: character = 96 + (face/2) + form*2, palette = 170 + shiny + form*2
                new FormSpriteData("Arceus - Normal",   96,  97, 170, 171),
                new FormSpriteData("Arceus - Fighting", 98,  99, 172, 173),
                new FormSpriteData("Arceus - Flying",  100, 101, 174, 175),
                new FormSpriteData("Arceus - Poison",  102, 103, 176, 177),
                new FormSpriteData("Arceus - Ground",  104, 105, 178, 179),
                new FormSpriteData("Arceus - Rock",    106, 107, 180, 181),
                new FormSpriteData("Arceus - Bug",     108, 109, 182, 183),
                new FormSpriteData("Arceus - Ghost",   110, 111, 184, 185),
                new FormSpriteData("Arceus - Steel",   112, 113, 186, 187),
                new FormSpriteData("Arceus - ???",     114, 115, 188, 189),
                new FormSpriteData("Arceus - Fire",    116, 117, 190, 191),
                new FormSpriteData("Arceus - Water",   118, 119, 192, 193),
                new FormSpriteData("Arceus - Grass",   120, 121, 194, 195),
                new FormSpriteData("Arceus - Electric",122, 123, 196, 197),
                new FormSpriteData("Arceus - Psychic", 124, 125, 198, 199),
                new FormSpriteData("Arceus - Ice",     126, 127, 200, 201),
                new FormSpriteData("Arceus - Dragon",  128, 129, 202, 203),
                new FormSpriteData("Arceus - Dark",    130, 131, 204, 205),
                
                // Egg: character = 132 + form, palette = 206 + form (no back/front distinction)
                new FormSpriteData("Egg",         132, 132, 206, 206),
                new FormSpriteData("Manaphy Egg", 133, 133, 207, 207),
            };
        }

        /// <summary>
        /// Form sprite data for Platinum's PL_OTHERPOKE NARC.
        /// Based on BuildPokemonSpriteTemplate from game code.
        /// </summary>
        private FormSpriteData[] GetFormDataPt() {
            return new FormSpriteData[] {
                // Deoxys: character = 0 + (face/2) + form*2, palette = 154 + shiny
                new FormSpriteData("Deoxys - Normal",   0,  1, 154, 155),
                new FormSpriteData("Deoxys - Attack",   2,  3, 154, 155),
                new FormSpriteData("Deoxys - Defense",  4,  5, 154, 155),
                new FormSpriteData("Deoxys - Speed",    6,  7, 154, 155),
                
                // Unown: character = 8 + (face/2) + form*2, palette = 156 + shiny
                new FormSpriteData("Unown - A",  8,  9, 156, 157),
                new FormSpriteData("Unown - B", 10, 11, 156, 157),
                new FormSpriteData("Unown - C", 12, 13, 156, 157),
                new FormSpriteData("Unown - D", 14, 15, 156, 157),
                new FormSpriteData("Unown - E", 16, 17, 156, 157),
                new FormSpriteData("Unown - F", 18, 19, 156, 157),
                new FormSpriteData("Unown - G", 20, 21, 156, 157),
                new FormSpriteData("Unown - H", 22, 23, 156, 157),
                new FormSpriteData("Unown - I", 24, 25, 156, 157),
                new FormSpriteData("Unown - J", 26, 27, 156, 157),
                new FormSpriteData("Unown - K", 28, 29, 156, 157),
                new FormSpriteData("Unown - L", 30, 31, 156, 157),
                new FormSpriteData("Unown - M", 32, 33, 156, 157),
                new FormSpriteData("Unown - N", 34, 35, 156, 157),
                new FormSpriteData("Unown - O", 36, 37, 156, 157),
                new FormSpriteData("Unown - P", 38, 39, 156, 157),
                new FormSpriteData("Unown - Q", 40, 41, 156, 157),
                new FormSpriteData("Unown - R", 42, 43, 156, 157),
                new FormSpriteData("Unown - S", 44, 45, 156, 157),
                new FormSpriteData("Unown - T", 46, 47, 156, 157),
                new FormSpriteData("Unown - U", 48, 49, 156, 157),
                new FormSpriteData("Unown - V", 50, 51, 156, 157),
                new FormSpriteData("Unown - W", 52, 53, 156, 157),
                new FormSpriteData("Unown - X", 54, 55, 156, 157),
                new FormSpriteData("Unown - Y", 56, 57, 156, 157),
                new FormSpriteData("Unown - Z", 58, 59, 156, 157),
                new FormSpriteData("Unown - !", 60, 61, 156, 157),
                new FormSpriteData("Unown - ?", 62, 63, 156, 157),
                
                // Castform: character = 64 + (face*2) + form, palette = 158 + (shiny*4) + form
                new FormSpriteData("Castform - Normal", 64, 68, 158, 162),
                new FormSpriteData("Castform - Sunny",  65, 69, 159, 163),
                new FormSpriteData("Castform - Rainy",  66, 70, 160, 164),
                new FormSpriteData("Castform - Snowy",  67, 71, 161, 165),
                
                // Burmy: character = 72 + (face/2) + form*2, palette = 166 + shiny + form*2
                new FormSpriteData("Burmy - Plant", 72, 73, 166, 167),
                new FormSpriteData("Burmy - Sandy", 74, 75, 168, 169),
                new FormSpriteData("Burmy - Trash", 76, 77, 170, 171),
                
                // Wormadam: character = 78 + (face/2) + form*2, palette = 172 + shiny + form*2
                new FormSpriteData("Wormadam - Plant", 78, 79, 172, 173),
                new FormSpriteData("Wormadam - Sandy", 80, 81, 174, 175),
                new FormSpriteData("Wormadam - Trash", 82, 83, 176, 177),
                
                // Shellos: character = 84 + face + form, palette = 178 + shiny + form*2
                new FormSpriteData("Shellos - West", 84, 86, 178, 179, true),
                new FormSpriteData("Shellos - East", 85, 87, 180, 181, true),
                
                // Gastrodon: character = 88 + face + form, palette = 182 + shiny + form*2
                new FormSpriteData("Gastrodon - West", 88, 90, 182, 183, true),
                new FormSpriteData("Gastrodon - East", 89, 91, 184, 185, true),
                
                // Cherrim: character = 92 + face + form, palette = 186 + (shiny*2) + form
                new FormSpriteData("Cherrim - Overcast", 92, 94, 186, 188, true),
                new FormSpriteData("Cherrim - Sunshine", 93, 95, 187, 189, true),
                
                // Arceus: character = 96 + (face/2) + form*2, palette = 190 + shiny + form*2
                new FormSpriteData("Arceus - Normal",   96,  97, 190, 191),
                new FormSpriteData("Arceus - Fighting", 98,  99, 192, 193),
                new FormSpriteData("Arceus - Flying",  100, 101, 194, 195),
                new FormSpriteData("Arceus - Poison",  102, 103, 196, 197),
                new FormSpriteData("Arceus - Ground",  104, 105, 198, 199),
                new FormSpriteData("Arceus - Rock",    106, 107, 200, 201),
                new FormSpriteData("Arceus - Bug",     108, 109, 202, 203),
                new FormSpriteData("Arceus - Ghost",   110, 111, 204, 205),
                new FormSpriteData("Arceus - Steel",   112, 113, 206, 207),
                new FormSpriteData("Arceus - ???",     114, 115, 208, 209),
                new FormSpriteData("Arceus - Fire",    116, 117, 210, 211),
                new FormSpriteData("Arceus - Water",   118, 119, 212, 213),
                new FormSpriteData("Arceus - Grass",   120, 121, 214, 215),
                new FormSpriteData("Arceus - Electric",122, 123, 216, 217),
                new FormSpriteData("Arceus - Psychic", 124, 125, 218, 219),
                new FormSpriteData("Arceus - Ice",     126, 127, 220, 221),
                new FormSpriteData("Arceus - Dragon",  128, 129, 222, 223),
                new FormSpriteData("Arceus - Dark",    130, 131, 224, 225),
                
                // Egg: character = 132 + form, palette = 226 + form
                new FormSpriteData("Egg",         132, 132, 226, 226),
                new FormSpriteData("Manaphy Egg", 133, 133, 227, 227),
                
                // Shaymin: character = 134 + (face/2) + form*2, palette = 228 + shiny + form*2
                new FormSpriteData("Shaymin - Land", 134, 135, 228, 229),
                new FormSpriteData("Shaymin - Sky",  136, 137, 230, 231),
                
                // Rotom: character = 138 + (face/2) + form*2, palette = 232 + shiny + form*2
                new FormSpriteData("Rotom - Normal", 138, 139, 232, 233),
                new FormSpriteData("Rotom - Heat",   140, 141, 234, 235),
                new FormSpriteData("Rotom - Wash",   142, 143, 236, 237),
                new FormSpriteData("Rotom - Frost",  144, 145, 238, 239),
                new FormSpriteData("Rotom - Fan",    146, 147, 240, 241),
                new FormSpriteData("Rotom - Mow",    148, 149, 242, 243),
                
                // Giratina: character = 150 + (face/2) + form*2, palette = 244 + shiny + form*2
                new FormSpriteData("Giratina - Altered", 150, 151, 244, 245),
                new FormSpriteData("Giratina - Origin",  152, 153, 246, 247),
            };
        }

        // Current form data based on game family
        private FormSpriteData[] currentFormData;

        /// <summary>
        /// Form sprite data for HeartGold/SoulSilver's OTHERPOKE NARC.
        /// Based on GetMonSpriteCharAndPlttNarcIdsEx from game code.
        /// Hex values from code: 0x48=72, 0x4E=78, 0x54=84, 0x58=88, 0x5C=92, 0x60=96, 0x40=64
        /// Palettes: 0x9E=158, 0xA0=160, 0xA2=162, 0xAA=170, 0xB0=176, 0xB6=182, 0xBA=186, 0xBE=190, 0xC2=194
        /// 0x84=132, 0x86=134, 0x8A=138, 0x96=150, 0x9A=154, 0xE6=230, 0xE8=232, 0xEC=236, 0xF8=248, 0xFC=252
        /// </summary>
        private FormSpriteData[] GetFormDataHGSS() {
            return new FormSpriteData[] {
                // Deoxys: character = 0 + (face/2) + form*2, palette = 0x9E (158) + shiny
                new FormSpriteData("Deoxys - Normal",   0,  1, 158, 159),
                new FormSpriteData("Deoxys - Attack",   2,  3, 158, 159),
                new FormSpriteData("Deoxys - Defense",  4,  5, 158, 159),
                new FormSpriteData("Deoxys - Speed",    6,  7, 158, 159),
                
                // Unown: character = 0x8 (8) + (face/2) + form*2, palette = 0xA0 (160) + shiny
                new FormSpriteData("Unown - A",  8,  9, 160, 161),
                new FormSpriteData("Unown - B", 10, 11, 160, 161),
                new FormSpriteData("Unown - C", 12, 13, 160, 161),
                new FormSpriteData("Unown - D", 14, 15, 160, 161),
                new FormSpriteData("Unown - E", 16, 17, 160, 161),
                new FormSpriteData("Unown - F", 18, 19, 160, 161),
                new FormSpriteData("Unown - G", 20, 21, 160, 161),
                new FormSpriteData("Unown - H", 22, 23, 160, 161),
                new FormSpriteData("Unown - I", 24, 25, 160, 161),
                new FormSpriteData("Unown - J", 26, 27, 160, 161),
                new FormSpriteData("Unown - K", 28, 29, 160, 161),
                new FormSpriteData("Unown - L", 30, 31, 160, 161),
                new FormSpriteData("Unown - M", 32, 33, 160, 161),
                new FormSpriteData("Unown - N", 34, 35, 160, 161),
                new FormSpriteData("Unown - O", 36, 37, 160, 161),
                new FormSpriteData("Unown - P", 38, 39, 160, 161),
                new FormSpriteData("Unown - Q", 40, 41, 160, 161),
                new FormSpriteData("Unown - R", 42, 43, 160, 161),
                new FormSpriteData("Unown - S", 44, 45, 160, 161),
                new FormSpriteData("Unown - T", 46, 47, 160, 161),
                new FormSpriteData("Unown - U", 48, 49, 160, 161),
                new FormSpriteData("Unown - V", 50, 51, 160, 161),
                new FormSpriteData("Unown - W", 52, 53, 160, 161),
                new FormSpriteData("Unown - X", 54, 55, 160, 161),
                new FormSpriteData("Unown - Y", 56, 57, 160, 161),
                new FormSpriteData("Unown - Z", 58, 59, 160, 161),
                new FormSpriteData("Unown - !", 60, 61, 160, 161),
                new FormSpriteData("Unown - ?", 62, 63, 160, 161),
                
                // Castform: character = 0x40 (64) + (face*2) + form, palette = 0xA2 (162) + (shiny*4) + form
                new FormSpriteData("Castform - Normal", 64, 68, 162, 166),
                new FormSpriteData("Castform - Sunny",  65, 69, 163, 167),
                new FormSpriteData("Castform - Rainy",  66, 70, 164, 168),
                new FormSpriteData("Castform - Snowy",  67, 71, 165, 169),
                
                // Burmy: character = 0x48 (72) + (face/2) + form*2, palette = 0xAA (170) + shiny + form*2
                new FormSpriteData("Burmy - Plant", 72, 73, 170, 171),
                new FormSpriteData("Burmy - Sandy", 74, 75, 172, 173),
                new FormSpriteData("Burmy - Trash", 76, 77, 174, 175),
                
                // Wormadam: character = 0x4E (78) + (face/2) + form*2, palette = 0xB0 (176) + shiny + form*2
                new FormSpriteData("Wormadam - Plant", 78, 79, 176, 177),
                new FormSpriteData("Wormadam - Sandy", 80, 81, 178, 179),
                new FormSpriteData("Wormadam - Trash", 82, 83, 180, 181),
                
                // Shellos: character = 0x54 (84) + face + form, palette = 0xB6 (182) + shiny + form*2
                new FormSpriteData("Shellos - West", 84, 86, 182, 183, true),
                new FormSpriteData("Shellos - East", 85, 87, 184, 185, true),
                
                // Gastrodon: character = 0x58 (88) + face + form, palette = 0xBA (186) + shiny + form*2
                new FormSpriteData("Gastrodon - West", 88, 90, 186, 187, true),
                new FormSpriteData("Gastrodon - East", 89, 91, 188, 189, true),
                
                // Cherrim: character = 0x5C (92) + face + form, palette = 0xBE (190) + (shiny*2) + form
                new FormSpriteData("Cherrim - Overcast", 92, 94, 190, 192, true),
                new FormSpriteData("Cherrim - Sunshine", 93, 95, 191, 193, true),
                
                // Arceus: character = 0x60 (96) + (face/2) + form*2, palette = 0xC2 (194) + shiny + form*2
                new FormSpriteData("Arceus - Normal",   96,  97, 194, 195),
                new FormSpriteData("Arceus - Fighting", 98,  99, 196, 197),
                new FormSpriteData("Arceus - Flying",  100, 101, 198, 199),
                new FormSpriteData("Arceus - Poison",  102, 103, 200, 201),
                new FormSpriteData("Arceus - Ground",  104, 105, 202, 203),
                new FormSpriteData("Arceus - Rock",    106, 107, 204, 205),
                new FormSpriteData("Arceus - Bug",     108, 109, 206, 207),
                new FormSpriteData("Arceus - Ghost",   110, 111, 208, 209),
                new FormSpriteData("Arceus - Steel",   112, 113, 210, 211),
                new FormSpriteData("Arceus - ???",     114, 115, 212, 213),
                new FormSpriteData("Arceus - Fire",    116, 117, 214, 215),
                new FormSpriteData("Arceus - Water",   118, 119, 216, 217),
                new FormSpriteData("Arceus - Grass",   120, 121, 218, 219),
                new FormSpriteData("Arceus - Electric",122, 123, 220, 221),
                new FormSpriteData("Arceus - Psychic", 124, 125, 222, 223),
                new FormSpriteData("Arceus - Ice",     126, 127, 224, 225),
                new FormSpriteData("Arceus - Dragon",  128, 129, 226, 227),
                new FormSpriteData("Arceus - Dark",    130, 131, 228, 229),
                
                // Egg: character = 0x84 (132) + form, palette = 0xE6 (230) + form
                new FormSpriteData("Egg",         132, 132, 230, 230),
                new FormSpriteData("Manaphy Egg", 133, 133, 231, 231),
                
                // Shaymin: character = 0x86 (134) + (face/2) + form*2, palette = 0xE8 (232) + shiny + form*2
                new FormSpriteData("Shaymin - Land", 134, 135, 232, 233),
                new FormSpriteData("Shaymin - Sky",  136, 137, 234, 235),
                
                // Rotom: character = 0x8A (138) + (face/2) + form*2, palette = 0xEC (236) + shiny + form*2
                new FormSpriteData("Rotom - Normal", 138, 139, 236, 237),
                new FormSpriteData("Rotom - Heat",   140, 141, 238, 239),
                new FormSpriteData("Rotom - Wash",   142, 143, 240, 241),
                new FormSpriteData("Rotom - Frost",  144, 145, 242, 243),
                new FormSpriteData("Rotom - Fan",    146, 147, 244, 245),
                new FormSpriteData("Rotom - Mow",    148, 149, 246, 247),
                
                // Giratina: character = 0x96 (150) + (face/2) + form*2, palette = 0xF8 (248) + shiny + form*2
                new FormSpriteData("Giratina - Altered", 150, 151, 248, 249),
                new FormSpriteData("Giratina - Origin",  152, 153, 250, 251),
                
                // Pichu (Spiky-ear): character = 0x9A (154) + (face/2) + form*2, palette = 0xFC (252) + shiny + form*2
                // Note: form 0 is normal Pichu (uses main NARC), form 1 is Spiky-ear
                new FormSpriteData("Pichu - Normal",    154, 155, 252, 253),
                new FormSpriteData("Pichu - Spiky-ear", 156, 157, 254, 255),
            };
        }
        #endregion

        #region Instance Fields
        private readonly string[] pokenames;
        private readonly PokemonEditor parentEditor;
        
        private NarcReader narcReader;
        private PictureBox[,] displayPictureBoxes;
        private bool[] usedEntries;
        private SpriteSet currentSprites;
        private int currentLoadedId;
        private bool isLoadingOtherForms = false;
        
        public bool dirty = false;
        #endregion

        #region Constructor
        public PokemonSpriteEditor(Control parent, PokemonEditor pokeEditor) {
            this.parentEditor = pokeEditor;
            this.pokenames = RomInfo.GetPokemonNames();
            
            InitializeComponent();
            
            this.Text = formName;
            this.FormBorderStyle = FormBorderStyle.None;
            this.Size = parent.Size;
            this.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top | AnchorStyles.Bottom;
            
            SetupPictureBoxes();
            InitializePaletteComboBoxes();
            
            Helpers.DisableHandlers();
            LoadSprites();
            Helpers.EnableHandlers();
            
            SaveBox.SelectedIndex = 0;
        }
        
        private void InitializePaletteComboBoxes() {
            int[] validPalettes = GetValidPalettesForGameFamily();
            foreach (var item in validPalettes) {
                BasePalette.Items.Add(item);
                ShinyPalette.Items.Add(item);
            }
        }
        
        private int[] GetValidPalettesForGameFamily() {
            switch (RomInfo.gameFamily) {
                case RomInfo.GameFamilies.DP:
                    return validPalettesDP;
                case RomInfo.GameFamilies.Plat:
                    return validPalettesPt;
                default:
                    return validPalettesHGSS;
            }
        }
        #endregion

        #region Dirty State Management
        public bool CheckDiscardChanges() {
            if (!dirty) {
                return true;
            }

            DialogResult result = MessageBox.Show(
                "Sprite Editor\nThere are unsaved changes to the current Sprite data.\nDiscard and proceed?", 
                "Sprite Editor - Unsaved changes", 
                MessageBoxButtons.YesNo, 
                MessageBoxIcon.Question);
                
            if (result == DialogResult.Yes) {
                return true;
            }

            IndexBox.SelectedIndex = currentLoadedId;
            return false;
        }

        private void SetDirty(bool status) {
            if (status) {
                dirty = true;
                this.Text = formName + "*";
            } else {
                dirty = false;
                this.Text = formName;
            }
            parentEditor.UpdateTabPageNames();
        }
        #endregion

        #region Event Handlers
        private void IndexBox_SelectedIndexChanged(object sender, EventArgs e) {
            this.Update();
            if (Helpers.HandlersDisabled) {
                return;
            }
            
            if (!isLoadingOtherForms) {
                parentEditor.TrySyncIndices((ComboBox)sender);
            }
            
            Helpers.DisableHandlers();
            if (CheckDiscardChanges()) {
                ChangeLoadedFile(((ComboBox)sender).SelectedIndex);
            }
            Helpers.EnableHandlers();
        }

        #region File Loading
        public void ChangeLoadedFile(int toLoad) {
            currentLoadedId = toLoad;
            
            Helpers.DisableHandlers();
            IndexBox.SelectedIndex = toLoad;
            Helpers.EnableHandlers();
            
            currentSprites = new SpriteSet();
            
            if (!isLoadingOtherForms) {
                LoadMainSprites(toLoad);
            } else {
                LoadOtherFormSprites(toLoad);
            }
            
            LoadImages();
            OpenPngs.Enabled = true;
            SetDirty(false);
        }
        
        private void LoadMainSprites(int selectedIndex) {
            int baseOffset = selectedIndex * 6;
            
            for (int i = 0; i < 4; i++) {
                if (narcReader.fe[baseOffset + i].Size == 6448) {
                    narcReader.OpenEntry(baseOffset + i);
                    currentSprites.Sprites[i] = MakeImage(narcReader.fs);
                    narcReader.Close();
                }
            }
            
            if (narcReader.fe[baseOffset + 4].Size == 72) {
                narcReader.OpenEntry(baseOffset + 4);
                currentSprites.Normal = SetPal(narcReader.fs);
                narcReader.Close();
            }
            
            if (narcReader.fe[baseOffset + 5].Size == 72) {
                narcReader.OpenEntry(baseOffset + 5);
                currentSprites.Shiny = SetPal(narcReader.fs);
                narcReader.Close();
            }
        }
        
        private void LoadOtherFormSprites(int selectedIndex) {
            if (currentFormData == null || selectedIndex >= currentFormData.Length) {
                MessageBox.Show($"Invalid form index: {selectedIndex}", "Error");
                return;
            }
            
            FormSpriteData formData = currentFormData[selectedIndex];
            
            // Load back sprite
            if (narcReader.fe[formData.BackSpriteIndex].Size == 6448) {
                narcReader.OpenEntry(formData.BackSpriteIndex);
                Bitmap backSprite = MakeImage(narcReader.fs);
                narcReader.Close();
                
                // For forms without gender difference, use same sprite for both genders
                currentSprites.Sprites[0] = backSprite; // Female back
                currentSprites.Sprites[1] = backSprite; // Male back (same as female for most forms)
            }
            
            // Load front sprite
            if (narcReader.fe[formData.FrontSpriteIndex].Size == 6448) {
                narcReader.OpenEntry(formData.FrontSpriteIndex);
                Bitmap frontSprite = MakeImage(narcReader.fs);
                narcReader.Close();
                
                currentSprites.Sprites[2] = frontSprite; // Female front
                currentSprites.Sprites[3] = frontSprite; // Male front (same as female for most forms)
            }
            
            // Load normal palette
            if (narcReader.fe[formData.NormalPaletteIndex].Size == 72) {
                narcReader.OpenEntry(formData.NormalPaletteIndex);
                currentSprites.Normal = SetPal(narcReader.fs);
                narcReader.Close();
            }
            
            // Load shiny palette
            if (narcReader.fe[formData.ShinyPaletteIndex].Size == 72) {
                narcReader.OpenEntry(formData.ShinyPaletteIndex);
                currentSprites.Shiny = SetPal(narcReader.fs);
                narcReader.Close();
            }
        }
        #endregion

        private void BasePalette_SelectedIndexChanged(object sender, EventArgs e) {
            if (Helpers.HandlersDisabled) {
                return;
            }
            
            if (narcReader.fe[(int)BasePalette.SelectedItem].Size == 72) {
                narcReader.OpenEntry((int)BasePalette.SelectedItem);
                currentSprites.Normal = SetPal(narcReader.fs);
                narcReader.Close();
            }
            LoadImages();
            SetDirty(true);
        }

        private void ShinyPalette_SelectedIndexChanged(object sender, EventArgs e) {
            if (Helpers.HandlersDisabled) {
                return;
            }
            
            if (narcReader.fe[(int)ShinyPalette.SelectedItem].Size == 72) {
                narcReader.OpenEntry((int)ShinyPalette.SelectedItem);
                currentSprites.Shiny = SetPal(narcReader.fs);
                narcReader.Close();
            }
            LoadImages();
            SetDirty(true);
        }

        #region UI Setup
        private void SetupPictureBoxes() {
            displayPictureBoxes = new PictureBox[2, 4];

            femaleBackNormalPic.Name = "0";
            displayPictureBoxes[0, 0] = femaleBackNormalPic;

            maleBackNormalPic.Name = "1";
            displayPictureBoxes[1, 0] = maleBackNormalPic;

            femaleFrontNormalPic.Name = "2";
            displayPictureBoxes[0, 1] = femaleFrontNormalPic;

            maleFrontNormalPic.Name = "3";
            displayPictureBoxes[1, 1] = maleFrontNormalPic;

            femaleBackShinyPic.Name = "4";
            displayPictureBoxes[0, 2] = femaleBackShinyPic;

            maleBackShinyPic.Name = "5";
            displayPictureBoxes[1, 2] = maleBackShinyPic;

            femaleFrontShinyPic.Name = "6";
            displayPictureBoxes[0, 3] = femaleFrontShinyPic;

            maleFrontShinyPic.Name = "7";
            displayPictureBoxes[1, 3] = maleFrontShinyPic;
        }
        #endregion

        #region Image Display
        private void LoadImages() {
            // Clear all displays first
            for (int i = 0; i < displayPictureBoxes.GetLength(0); i++) {
                for (int j = 0; j < displayPictureBoxes.GetLength(1); j++) {
                    displayPictureBoxes[i, j].Image = null;
                }
            }
            
            if (currentSprites.Normal == null) {
                return;
            }
            
            if (currentSprites.Shiny == null) {
                currentSprites.Shiny = currentSprites.Normal;
            }
            
            for (int i = 0; i < 4; i++) {
                if (currentSprites.Sprites[i] != null) {
                    // Display shiny version
                    currentSprites.Sprites[i].Palette = currentSprites.Shiny;
                    displayPictureBoxes[(i % 2), ((i / 2) + 2)].Image = new Bitmap(currentSprites.Sprites[i], 320, 160);
                    
                    // Display normal version
                    currentSprites.Sprites[i].Palette = currentSprites.Normal;
                    displayPictureBoxes[(i % 2), (i / 2)].Image = new Bitmap(currentSprites.Sprites[i], 320, 160);
                }
            }
        }
        #endregion

        #region Image Validation
        private Bitmap CheckSize(Bitmap image, string filename, string name, int spritenumber = 2) {
            IndexedBitmapHandler handler = new IndexedBitmapHandler();
            
            if (image.PixelFormat != PixelFormat.Format8bppIndexed) {
                DialogResult result = MessageBox.Show(
                    $"{filename} is not 8bpp Indexed! Attempt conversion?", 
                    "Incompatible image format", 
                    MessageBoxButtons.YesNo);
                    
                if (result != DialogResult.Yes) {
                    return null;
                }
                
                image = handler.Convert(image, PixelFormat.Format8bppIndexed);
                if (image == null || image.PixelFormat != PixelFormat.Format8bppIndexed || image.Palette == null) {
                    MessageBox.Show("Conversion failed.", "Failed");
                    return null;
                }
            }
            
            if (!IsValidSpriteSize(image)) {
                image = TryResizeSprite(image, handler, filename);
                if (image == null) {
                    return null;
                }
            }
            
            // Adjust sprite dimensions to standard size
            if (image.Width == 64) {
                image = handler.Resize(image, 48, 8, 0, 0);
            }
            if (image.Height == 64) {
                image = handler.Resize(image, 0, 0, 0, 16);
            }
            if (image.Width == 80) {
                image = handler.Resize(image, 40, 0, 0, 0);
            }
            
            if (image.Palette.Entries.Length > 16) {
                MessageBox.Show($"{filename} has too many colors. Must have 16 or less.", "Too many colors");
                return null;
            }
            
            return image;
        }
        
        private bool IsValidSpriteSize(Bitmap image) {
            bool validHeight = (image.Height == 64 || image.Height == 80);
            bool validWidth = (image.Width == 64 || image.Width == 80 || image.Width == 160);
            return validHeight && validWidth;
        }
        
        private Bitmap TryResizeSprite(Bitmap image, IndexedBitmapHandler handler, string filename) {
            int imagescale = 0;
            
            if ((image.Width / 64 == image.Height / 64) && (image.Width % 64 == 0) && (image.Height % 64 == 0)) {
                imagescale = image.Width / 64;
            }
            if ((image.Width / 80 == image.Height / 80) && (image.Width % 80 == 0) && (image.Height % 80 == 0)) {
                imagescale = image.Width / 80;
            }
            if ((image.Width / 160 == image.Height / 80) && (image.Width % 160 == 0) && (image.Height % 80 == 0)) {
                imagescale = image.Width / 160;
            }
            
            if (imagescale > 1) {
                DialogResult result = MessageBox.Show(
                    $"{filename} is too large. Attempt to shrink?", 
                    "Image too large", 
                    MessageBoxButtons.YesNo);
                    
                if (result != DialogResult.Yes) {
                    return null;
                }
                return handler.Resize(image, 0, 0, imagescale, imagescale);
            }
            
            MessageBox.Show($"{filename} is wrong size. Must be 64x64, 80x80 or 160x80.", "Wrong size");
            return null;
        }
        #endregion

        private void OpenPngs_Click(object sender, EventArgs e) {
            if (!OpenPngs.Enabled) {
                return;
            }
            
            OpenPngs.Enabled = false;
            PictureBox source = sender as PictureBox;
            int index = Convert.ToInt32(source.Name);
            
            using (OpenFileDialog openFileDialog = new OpenFileDialog()) {
                openFileDialog.Title = "Choose an image";
                openFileDialog.CheckPathExists = true;
                openFileDialog.Filter = "Supported formats: *.bmp, *.gif, *.png | *.bmp; *.gif; *.png";
                
                if (openFileDialog.ShowDialog() != DialogResult.OK) {
                    OpenPngs.Enabled = true;
                    return;
                }
                
                Bitmap image = new Bitmap(openFileDialog.FileName);
                IndexedBitmapHandler handler = new IndexedBitmapHandler();
                
                if (index > 3) {
                    // Loading shiny palette
                    image = CheckSize(image, openFileDialog.FileName, "Shiny");
                    if (image == null) {
                        OpenPngs.Enabled = true;
                        return;
                    }
                    
                    ColorPalette temp = handler.AlternatePalette(currentSprites.Sprites[index % 4], image);
                    currentSprites.Shiny = temp ?? image.Palette;
                } else {
                    // Loading normal sprite
                    image = CheckSize(image, openFileDialog.FileName, spriteTypeNames[index], index);
                    if (image == null) {
                        OpenPngs.Enabled = true;
                        return;
                    }
                    
                    bool match = handler.PaletteEquals(currentSprites.Normal, image);
                    if (!match) {
                        DialogResult result = MessageBox.Show(
                            "Image's palette does not match the current palette. Use PaletteMatch?", 
                            "Palette mismatch", 
                            MessageBoxButtons.YesNo);
                            
                        if (result == DialogResult.Yes) {
                            image = handler.PaletteMatch(currentSprites.Normal, image, usedEntries);
                            usedEntries = handler.IsUsed(image, usedEntries);
                        } else {
                            usedEntries = handler.IsUsed(image);
                        }
                        currentSprites.Normal = image.Palette;
                    }
                    currentSprites.Sprites[index] = image;
                }
            }
            
            OpenPngs.Enabled = true;
            LoadImages();
            SetDirty(true);
        }

        private void SaveChanges_Click(object sender, EventArgs e) {
            if (!OpenPngs.Enabled) {
                return;
            }
            int selectedIndex = IndexBox.SelectedIndex;

            // If editing main sprites, files are organized as groups of 6 entries per pokemon
            if (!isLoadingOtherForms) {
                int baseOffset = selectedIndex * 6;

                for (int i = 0; i < 4; i++) {
                    if (currentSprites.Sprites[i] == null) continue;
                    if (narcReader.fe[baseOffset + i].Size == 6448) {
                        narcReader.OpenEntry(baseOffset + i);
                        SaveBin(narcReader.fs, currentSprites.Sprites[i]);
                        narcReader.Close();
                    }
                }

                if (narcReader.fe[baseOffset + 4].Size == 72 && currentSprites.Normal != null) {
                    narcReader.OpenEntry(baseOffset + 4);
                    SavePal(narcReader.fs, currentSprites.Normal);
                    narcReader.Close();
                }

                if (narcReader.fe[baseOffset + 5].Size == 72 && currentSprites.Shiny != null) {
                    narcReader.OpenEntry(baseOffset + 5);
                    SavePal(narcReader.fs, currentSprites.Shiny);
                    narcReader.Close();
                }
            }
            else {
                // Other-forms NARC uses arbitrary indices defined in currentFormData
                if (currentFormData == null || selectedIndex < 0 || selectedIndex >= currentFormData.Length) {
                    MessageBox.Show("Invalid form data selected. Save aborted.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                FormSpriteData form = currentFormData[selectedIndex];

                // Back sprite index -> write appropriate image. Many forms reuse same file for both genders.
                if (form.BackSpriteIndex >= 0 && form.BackSpriteIndex < narcReader.fe.Length && currentSprites.Sprites[0] != null) {
                    if (narcReader.fe[form.BackSpriteIndex].Size == 6448) {
                        narcReader.OpenEntry(form.BackSpriteIndex);
                        // Some forms store a single image used for both back slots; write using Sprites[0] which was loaded
                        SaveBin(narcReader.fs, currentSprites.Sprites[0]);
                        narcReader.Close();
                    }
                }

                // Front sprite index
                if (form.FrontSpriteIndex >= 0 && form.FrontSpriteIndex < narcReader.fe.Length && currentSprites.Sprites[2] != null) {
                    if (narcReader.fe[form.FrontSpriteIndex].Size == 6448) {
                        narcReader.OpenEntry(form.FrontSpriteIndex);
                        SaveBin(narcReader.fs, currentSprites.Sprites[2]);
                        narcReader.Close();
                    }
                }

                // Normal palette
                if (form.NormalPaletteIndex >= 0 && form.NormalPaletteIndex < narcReader.fe.Length && currentSprites.Normal != null) {
                    if (narcReader.fe[form.NormalPaletteIndex].Size == 72) {
                        narcReader.OpenEntry(form.NormalPaletteIndex);
                        SavePal(narcReader.fs, currentSprites.Normal);
                        narcReader.Close();
                    }
                }

                // Shiny palette
                if (form.ShinyPaletteIndex >= 0 && form.ShinyPaletteIndex < narcReader.fe.Length && currentSprites.Shiny != null) {
                    if (narcReader.fe[form.ShinyPaletteIndex].Size == 72) {
                        narcReader.OpenEntry(form.ShinyPaletteIndex);
                        SavePal(narcReader.fs, currentSprites.Shiny);
                        narcReader.Close();
                    }
                }
            }

            SetDirty(false);
        }

        // Credit to loadingNOW and SCV for the original PokeDsPic and PokeDsPicPlatinum, 
        // without which this would never have happened. In addition to G4SpriteEditor
        
        private void btnSaveAs_Click(object sender, EventArgs e) {
            using (SaveFileDialog saveFileDialog = new SaveFileDialog()) {
                saveFileDialog.Title = "Save Image Set";
                saveFileDialog.CheckPathExists = true;
                saveFileDialog.Filter = "*.png|*.png";
                
                if (saveFileDialog.ShowDialog() != DialogResult.OK) {
                    return;
                }
                
                string baseFileName = saveFileDialog.FileName.Replace(".png", "");
                bool shinySaved = false;
                
                // Save front sprites (priority for shiny)
                if (currentSprites.Sprites[2] != null) {
                    if (currentSprites.Shiny != null) {
                        currentSprites.Sprites[2].Palette = currentSprites.Shiny;
                        SavePNG(currentSprites.Sprites[2], baseFileName + "Shiny.png");
                        shinySaved = true;
                    }
                    currentSprites.Sprites[2].Palette = currentSprites.Normal;
                    SavePNG(currentSprites.Sprites[2], baseFileName + "FFront.png");
                }
                
                if (currentSprites.Sprites[3] != null) {
                    if (currentSprites.Shiny != null && !shinySaved) {
                        currentSprites.Sprites[3].Palette = currentSprites.Shiny;
                        SavePNG(currentSprites.Sprites[3], baseFileName + "Shiny.png");
                        shinySaved = true;
                    }
                    currentSprites.Sprites[3].Palette = currentSprites.Normal;
                    SavePNG(currentSprites.Sprites[3], baseFileName + "MFront.png");
                }
                
                // Save back sprites
                if (currentSprites.Sprites[0] != null) {
                    if (currentSprites.Shiny != null && !shinySaved) {
                        currentSprites.Sprites[0].Palette = currentSprites.Shiny;
                        SavePNG(currentSprites.Sprites[0], baseFileName + "Shiny.png");
                        shinySaved = true;
                    }
                    currentSprites.Sprites[0].Palette = currentSprites.Normal;
                    SavePNG(currentSprites.Sprites[0], baseFileName + "FBack.png");
                }
                
                if (currentSprites.Sprites[1] != null) {
                    if (currentSprites.Shiny != null && !shinySaved) {
                        currentSprites.Sprites[1].Palette = currentSprites.Shiny;
                        SavePNG(currentSprites.Sprites[1], baseFileName + "Shiny.png");
                    }
                    currentSprites.Sprites[1].Palette = currentSprites.Normal;
                    SavePNG(currentSprites.Sprites[1], baseFileName + "MBack.png");
                }
            }
        }

        private void SaveSingle_Click(object sender, EventArgs e) {
            int index = SaveBox.SelectedIndex;
            
            if (currentSprites.Sprites[index % 4] == null) {
                MessageBox.Show("Image is empty.");
                return;
            }
            
            using (SaveFileDialog saveFileDialog = new SaveFileDialog()) {
                saveFileDialog.Title = "Save As PNG";
                saveFileDialog.OverwritePrompt = true;
                saveFileDialog.CheckPathExists = true;
                saveFileDialog.Filter = "*.png|*.png";
                
                if (saveFileDialog.ShowDialog() != DialogResult.OK) {
                    return;
                }
                
                Bitmap image = currentSprites.Sprites[index % 4];
                image.Palette = index > 3 ? currentSprites.Shiny : currentSprites.Normal;
                SavePNG(image, saveFileDialog.FileName);
            }
        }

        private void btnOpenOther_Click(object sender, EventArgs e) {
            if (!CheckDiscardChanges()) {
                return;
            }
            
            Helpers.DisableHandlers();
            
            // Clear dirty state since we're switching modes (user already confirmed discard)
            SetDirty(false);
            
            isLoadingOtherForms = !isLoadingOtherForms; // Toggle between main and forms view
            
            // Update button text based on mode
            OpenOther.Text = isLoadingOtherForms ? "Main Sprites" : "Open Forms";
            
            // Hide palette controls - they're no longer needed since we handle them automatically
            BasePalette.Visible = false;
            ShinyPalette.Visible = false;
            BasePalette.Enabled = false;
            ShinyPalette.Enabled = false;
            
            LoadSprites();
            
            Helpers.EnableHandlers();
        }

        private void btnLoadSheet_Click(object sender, EventArgs e) {
            if (!OpenPngs.Enabled) {
                return;
            }
            
            OpenPngs.Enabled = false;
            
            using (OpenFileDialog openFileDialog = new OpenFileDialog()) {
                openFileDialog.Title = "Select a sprite sheet";
                openFileDialog.CheckPathExists = true;
                openFileDialog.Filter = "Supported formats: *.bmp, *.gif, *.png | *.bmp; *.gif; *.png";
                
                if (openFileDialog.ShowDialog() != DialogResult.OK) {
                    OpenPngs.Enabled = true;
                    return;
                }
                
                Bitmap image = new Bitmap(openFileDialog.FileName);
                
                if (image.Width != 256 || image.Height != 64) {
                    MessageBox.Show("The sprite sheet should be 256x64.");
                    OpenPngs.Enabled = true;
                    return;
                }
                
                IndexedBitmapHandler handler = new IndexedBitmapHandler();
                image = handler.Convert(image, PixelFormat.Format8bppIndexed);
                image.Palette = StandardizeColors(image);
                
                Bitmap[] tiles = handler.Split(image, 64, 64);
                SpriteSet sprites = new SpriteSet();
                
                bool[] used = handler.IsUsed(tiles[0]);
                used = handler.IsUsed(tiles[2], used);
                
                // Process front sprite
                Bitmap temp = handler.ShrinkPalette(tiles[0], used);
                sprites.Normal = temp.Palette;
                temp = handler.Resize(temp, 8, 8, 8, 8);
                temp = handler.Concat(temp, temp);
                sprites.Sprites[2] = temp;
                sprites.Sprites[3] = temp;
                
                // Process back sprite
                temp = handler.ShrinkPalette(tiles[2], used);
                temp = handler.Resize(temp, 8, 8, 8, 8);
                if (RomInfo.gameFamily == RomInfo.GameFamilies.DP) {
                    temp = handler.Resize(temp, 0, 0, 0, 80);
                } else {
                    temp = handler.Concat(temp, temp);
                }
                sprites.Sprites[0] = temp;
                sprites.Sprites[1] = temp;
                
                // Process shiny palette
                temp = handler.ShrinkPalette(tiles[1], used);
                temp = handler.Resize(temp, 8, 8, 8, 8);
                temp = handler.Concat(temp, temp);
                sprites.Shiny = handler.AlternatePalette(sprites.Sprites[2], temp);
                
                currentSprites = sprites;
            }
            
            OpenPngs.Enabled = true;
            LoadImages();
            SetDirty(true);
        }

        private void MakeShiny_Click(object sender, EventArgs e) {
            if (!OpenPngs.Enabled) {
                return;
            }
            
            OpenPngs.Enabled = false;
            
            using (OpenFileDialog openFileDialog = new OpenFileDialog()) {
                openFileDialog.Title = "Choose the base image";
                openFileDialog.CheckPathExists = true;
                openFileDialog.Filter = "Supported formats: *.bmp, *.gif, *.png | *.bmp; *.gif; *.png";
                
                if (openFileDialog.ShowDialog() != DialogResult.OK) {
                    OpenPngs.Enabled = true;
                    return;
                }
                
                string baseFilename = openFileDialog.FileName;
                
                openFileDialog.Title = "Choose the shiny image";
                if (openFileDialog.ShowDialog() != DialogResult.OK) {
                    OpenPngs.Enabled = true;
                    return;
                }
                
                Bitmap baseImage = new Bitmap(baseFilename);
                Bitmap shinyImage = new Bitmap(openFileDialog.FileName);
                IndexedBitmapHandler handler = new IndexedBitmapHandler();
                
                ColorPalette temp = handler.AlternatePalette(baseImage, shinyImage);
                if (temp != null) {
                    currentSprites.Shiny = temp;
                } else {
                    MessageBox.Show("Failed!", "Failed");
                }
            }
            
            OpenPngs.Enabled = true;
            LoadImages();
            SetDirty(true);
        }
        #endregion

        #region Utility Methods
        private ColorPalette StandardizeColors(Bitmap image) {
            ColorPalette pal = image.Palette;
            bool hasOffColors = false;
            
            for (int i = 0; i < pal.Entries.Length; i++) {
                if ((pal.Entries[i].R % 8 != 0) || (pal.Entries[i].G % 8 != 0) || (pal.Entries[i].B % 8 != 0)) {
                    hasOffColors = true;
                    break;
                }
            }
            
            if (hasOffColors) {
                for (int i = 0; i < pal.Entries.Length; i++) {
                    byte r = (byte)(pal.Entries[i].R - (pal.Entries[i].R % 8));
                    byte g = (byte)(pal.Entries[i].G - (pal.Entries[i].G % 8));
                    byte b = (byte)(pal.Entries[i].B - (pal.Entries[i].B % 8));
                    pal.Entries[i] = Color.FromArgb(r, g, b);
                }
            }
            
            return pal;
        }

        private void SavePNG(Bitmap image, string filename) {
            IndexedBitmapHandler handler = new IndexedBitmapHandler();
            byte[] array = handler.GetArray(image);
            Bitmap temp = handler.MakeImage(image.Width, image.Height, array, image.PixelFormat);
            ColorPalette cleaned = handler.CleanPalette(image);
            temp.Palette = cleaned;
            temp.Save(filename, ImageFormat.Png);
        }
        #endregion

        #region Binary Operations
        private Bitmap MakeImage(FileStream fs) {
            fs.Seek(48L, SeekOrigin.Current);
            BinaryReader binaryReader = new BinaryReader(fs);
            
            ushort[] array = new ushort[3200];
            for (int i = 0; i < 3200; i++) {
                array[i] = binaryReader.ReadUInt16();
            }
            
            uint num = array[0];
            if (RomInfo.gameFamily != RomInfo.GameFamilies.DP) {
                for (int j = 0; j < 3200; j++) {
                    unchecked {
                        array[j] = (ushort)(array[j] ^ (ushort)(num & 0xFFFF));
                        num *= 1103515245;
                        num += 24691;
                    }
                }
            } else {
                num = array[3199];
                for (int j = 3199; j >= 0; j--) {
                    unchecked {
                        array[j] = (ushort)(array[j] ^ (ushort)(num & 0xFFFF));
                        num *= 1103515245;
                        num += 24691;
                    }
                }
            }
            
            Bitmap resultBitmap = new Bitmap(160, 80, PixelFormat.Format8bppIndexed);
            Rectangle rect = new Rectangle(0, 0, 160, 80);
            
            byte[] pixelArray = new byte[12800];
            for (int k = 0; k < 3200; k++) {
                pixelArray[k * 4] = (byte)(array[k] & 0xF);
                pixelArray[k * 4 + 1] = (byte)((array[k] >> 4) & 0xF);
                pixelArray[k * 4 + 2] = (byte)((array[k] >> 8) & 0xF);
                pixelArray[k * 4 + 3] = (byte)((array[k] >> 12) & 0xF);
            }
            
            BitmapData bitmapData = resultBitmap.LockBits(rect, ImageLockMode.WriteOnly, resultBitmap.PixelFormat);
            IntPtr scan = bitmapData.Scan0;
            Marshal.Copy(pixelArray, 0, scan, 12800);
            resultBitmap.UnlockBits(bitmapData);
            
            Bitmap tempBitmap = new Bitmap(1, 1, PixelFormat.Format4bppIndexed);
            ColorPalette palette = tempBitmap.Palette;
            for (int l = 0; l < 16; l++) {
                palette.Entries[l] = Color.FromArgb(l << 4, l << 4, l << 4);
            }
            resultBitmap.Palette = palette;
            
            if (resultBitmap == null) {
                MessageBox.Show("MakeImage Failed");
                return null;
            }
            
            return resultBitmap;
        }

        private ColorPalette SetPal(FileStream fs) {
            fs.Seek(40L, SeekOrigin.Current);
            
            ushort[] array = new ushort[16];
            BinaryReader binaryReader = new BinaryReader(fs);
            for (int i = 0; i < 16; i++) {
                array[i] = binaryReader.ReadUInt16();
            }
            
            Bitmap bitmap = new Bitmap(1, 1, PixelFormat.Format4bppIndexed);
            ColorPalette palette = bitmap.Palette;
            
            for (int j = 0; j < 16; j++) {
                palette.Entries[j] = Color.FromArgb(
                    (array[j] & 0x1F) << 3, 
                    ((array[j] >> 5) & 0x1F) << 3, 
                    ((array[j] >> 10) & 0x1F) << 3);
            }
            
            return palette;
        }

        private void LoadSprites() {
            if (!isLoadingOtherForms) {
                narcReader = new NarcReader(RomInfo.gameDirs[DirNames.pokemonBattleSprites].packedDir);
                usedEntries = new bool[narcReader.fe.Length];
                
                for (int i = 0; i < narcReader.fe.Length; i++) {
                    usedEntries[i] = (narcReader.fe[i].Size > 0);
                }
                
                IndexBox.Items.Clear();
                for (int i = 0; i < pokenames.Length; i++) {
                    IndexBox.Items.Add($"{i:D3} {pokenames[i]}");
                }
                
                // Load first entry (index 1 to skip "None/Egg" at 0)
                ChangeLoadedFile(1);
            } else {
                // Load form data based on game family
                switch (RomInfo.gameFamily) {
                    case RomInfo.GameFamilies.DP:
                        currentFormData = GetFormDataDP();
                        break;
                    case RomInfo.GameFamilies.Plat:
                        currentFormData = GetFormDataPt();
                        break;
                    case RomInfo.GameFamilies.HGSS:
                        currentFormData = GetFormDataHGSS();
                        break;
                    default:
                        currentFormData = GetFormDataPt(); // Fallback to Platinum
                        break;
                }
                
                narcReader = new NarcReader(RomInfo.gameDirs[DirNames.otherPokemonBattleSprites].packedDir);
                usedEntries = new bool[narcReader.fe.Length];
                
                for (int i = 0; i < narcReader.fe.Length; i++) {
                    usedEntries[i] = (narcReader.fe[i].Size > 0);
                }
                
                IndexBox.Items.Clear();
                for (int i = 0; i < currentFormData.Length; i++) {
                    IndexBox.Items.Add($"{i:D3} {currentFormData[i].Name}");
                }
                
                // Load first form entry
                ChangeLoadedFile(0);
            }
        }

        private void SaveBin(FileStream fs, Bitmap source) {
            BinaryWriter binaryWriter = new BinaryWriter(fs);
            Rectangle rect = new Rectangle(0, 0, 160, 80);
            
            BitmapData bitmapData = source.LockBits(rect, ImageLockMode.ReadOnly, source.PixelFormat);
            IntPtr scan = bitmapData.Scan0;
            byte[] array = new byte[12800];
            Marshal.Copy(scan, array, 0, 12800);
            source.UnlockBits(bitmapData);
            
            ushort[] array2 = new ushort[3200];
            for (int i = 0; i < 3200; i++) {
                array2[i] = (ushort)((array[i * 4] & 0xF) | 
                                     ((array[i * 4 + 1] & 0xF) << 4) | 
                                     ((array[i * 4 + 2] & 0xF) << 8) | 
                                     ((array[i * 4 + 3] & 0xF) << 12));
            }
            
            uint num = 0u;
            if (RomInfo.gameFamily != RomInfo.GameFamilies.DP) {
                for (int j = 0; j < 3200; j++) {
                    unchecked {
                        array2[j] = (ushort)(array2[j] ^ (ushort)(num & 0xFFFF));
                        num *= 1103515245;
                        num += 24691;
                    }
                }
            } else {
                num = 31315u;
                for (int k = 3199; k >= 0; k--) {
                    num += array2[k];
                }
                for (int k = 3199; k >= 0; k--) {
                    unchecked {
                        array2[k] = (ushort)(array2[k] ^ (ushort)(num & 0xFFFF));
                        num *= 1103515245;
                        num += 24691;
                    }
                }
            }
            
            byte[] header = new byte[48] {
                82, 71, 67, 78, 255, 254, 0, 1, 48, 25, 0, 0, 16, 0, 1, 0,
                82, 65, 72, 67, 32, 25, 0, 0, 10, 0, 20, 0, 3, 0, 0, 0,
                0, 0, 0, 0, 1, 0, 0, 0, 0, 25, 0, 0, 24, 0, 0, 0
            };
            
            for (int k = 0; k < 48; k++) {
                binaryWriter.Write(header[k]);
            }
            
            for (int l = 0; l < 3200; l++) {
                binaryWriter.Write(array2[l]);
            }
        }

        private void SavePal(FileStream fs, ColorPalette palette) {
            byte[] buffer = new byte[40] {
                82, 76, 67, 78, 255, 254, 0, 1, 72, 0, 0, 0, 16, 0, 1, 0,
                84, 84, 76, 80, 56, 0, 0, 0, 4, 0, 10, 0, 0, 0, 0, 0,
                32, 0, 0, 0, 16, 0, 0, 0
            };
            
            BinaryWriter binaryWriter = new BinaryWriter(fs);
            binaryWriter.Write(buffer, 0, 40);
            
            ushort[] array = new ushort[16];
            for (int i = 0; i < 16; i++) {
                array[i] = (ushort)(((palette.Entries[i].R >> 3) & 0x1F) | 
                                    (((palette.Entries[i].G >> 3) & 0x1F) << 5) | 
                                    (((palette.Entries[i].B >> 3) & 0x1F) << 10));
            }
            
            for (int j = 0; j < 16; j++) {
                binaryWriter.Write(array[j]);
            }
        }
        #endregion
    }
}