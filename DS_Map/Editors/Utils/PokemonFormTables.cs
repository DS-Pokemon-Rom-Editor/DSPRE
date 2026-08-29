using System;

namespace DSPRE {
    /// <summary>
    /// Where each alternate form's sprites and palettes sit inside the alternate-forms NARC, per game.
    /// The 13 species listed here keep their default form in this NARC too, not in the main one.
    /// </summary>
    public static class PokemonFormTables {
        public static PokemonSpriteModel.FormSpriteData[] DP {
            get {
                return new PokemonSpriteModel.FormSpriteData[] {
                // Deoxys: character = 0 + (face/2) + form*2, palette = 134 + shiny (shared palette for all forms)
                new PokemonSpriteModel.FormSpriteData("Deoxys - Normal",   0,  1, 134, 135),
                new PokemonSpriteModel.FormSpriteData("Deoxys - Attack",   2,  3, 134, 135),
                new PokemonSpriteModel.FormSpriteData("Deoxys - Defense",  4,  5, 134, 135),
                new PokemonSpriteModel.FormSpriteData("Deoxys - Speed",    6,  7, 134, 135),
                
                // Unown: character = 8 + (face/2) + form*2, palette = 136 + shiny (shared palette)
                new PokemonSpriteModel.FormSpriteData("Unown - A",  8,  9, 136, 137),
                new PokemonSpriteModel.FormSpriteData("Unown - B", 10, 11, 136, 137),
                new PokemonSpriteModel.FormSpriteData("Unown - C", 12, 13, 136, 137),
                new PokemonSpriteModel.FormSpriteData("Unown - D", 14, 15, 136, 137),
                new PokemonSpriteModel.FormSpriteData("Unown - E", 16, 17, 136, 137),
                new PokemonSpriteModel.FormSpriteData("Unown - F", 18, 19, 136, 137),
                new PokemonSpriteModel.FormSpriteData("Unown - G", 20, 21, 136, 137),
                new PokemonSpriteModel.FormSpriteData("Unown - H", 22, 23, 136, 137),
                new PokemonSpriteModel.FormSpriteData("Unown - I", 24, 25, 136, 137),
                new PokemonSpriteModel.FormSpriteData("Unown - J", 26, 27, 136, 137),
                new PokemonSpriteModel.FormSpriteData("Unown - K", 28, 29, 136, 137),
                new PokemonSpriteModel.FormSpriteData("Unown - L", 30, 31, 136, 137),
                new PokemonSpriteModel.FormSpriteData("Unown - M", 32, 33, 136, 137),
                new PokemonSpriteModel.FormSpriteData("Unown - N", 34, 35, 136, 137),
                new PokemonSpriteModel.FormSpriteData("Unown - O", 36, 37, 136, 137),
                new PokemonSpriteModel.FormSpriteData("Unown - P", 38, 39, 136, 137),
                new PokemonSpriteModel.FormSpriteData("Unown - Q", 40, 41, 136, 137),
                new PokemonSpriteModel.FormSpriteData("Unown - R", 42, 43, 136, 137),
                new PokemonSpriteModel.FormSpriteData("Unown - S", 44, 45, 136, 137),
                new PokemonSpriteModel.FormSpriteData("Unown - T", 46, 47, 136, 137),
                new PokemonSpriteModel.FormSpriteData("Unown - U", 48, 49, 136, 137),
                new PokemonSpriteModel.FormSpriteData("Unown - V", 50, 51, 136, 137),
                new PokemonSpriteModel.FormSpriteData("Unown - W", 52, 53, 136, 137),
                new PokemonSpriteModel.FormSpriteData("Unown - X", 54, 55, 136, 137),
                new PokemonSpriteModel.FormSpriteData("Unown - Y", 56, 57, 136, 137),
                new PokemonSpriteModel.FormSpriteData("Unown - Z", 58, 59, 136, 137),
                new PokemonSpriteModel.FormSpriteData("Unown - !", 60, 61, 136, 137),
                new PokemonSpriteModel.FormSpriteData("Unown - ?", 62, 63, 136, 137),
                
                // Castform: character = 64 + (face*2) + form, palette = 138 + (shiny*4) + form
                // face*2 means: back=0,2 front=4,6 - but this doesn't fit standard pattern
                // Actually: character = 64 + (face * 2) + form where face is 0-3
                // So Normal form back is 64, front is 68; Sunny back is 65, front is 69, etc.
                new PokemonSpriteModel.FormSpriteData("Castform - Normal", 64, 68, 138, 142),
                new PokemonSpriteModel.FormSpriteData("Castform - Sunny",  65, 69, 139, 143),
                new PokemonSpriteModel.FormSpriteData("Castform - Rainy",  66, 70, 140, 144),
                new PokemonSpriteModel.FormSpriteData("Castform - Snowy",  67, 71, 141, 145),
                
                // Burmy: character = 72 + (face/2) + form*2, palette = 146 + shiny + form*2
                new PokemonSpriteModel.FormSpriteData("Burmy - Plant", 72, 73, 146, 147),
                new PokemonSpriteModel.FormSpriteData("Burmy - Sandy", 74, 75, 148, 149),
                new PokemonSpriteModel.FormSpriteData("Burmy - Trash", 76, 77, 150, 151),
                
                // Wormadam: character = 78 + (face/2) + form*2, palette = 152 + shiny + form*2
                new PokemonSpriteModel.FormSpriteData("Wormadam - Plant", 78, 79, 152, 153),
                new PokemonSpriteModel.FormSpriteData("Wormadam - Sandy", 80, 81, 154, 155),
                new PokemonSpriteModel.FormSpriteData("Wormadam - Trash", 82, 83, 156, 157),
                
                // Shellos: character = 84 + face + form (has gender sprites), palette = 158 + shiny + form*2
                new PokemonSpriteModel.FormSpriteData("Shellos - West", 84, 86, 158, 159),  // 84=FBack, 85=MBack, 86=FFront, 87=MFront
                new PokemonSpriteModel.FormSpriteData("Shellos - East", 85, 87, 160, 161),  // Actually face + form, so East adds 1
                
                // Gastrodon: character = 88 + face + form, palette = 162 + shiny + form*2
                new PokemonSpriteModel.FormSpriteData("Gastrodon - West", 88, 90, 162, 163),
                new PokemonSpriteModel.FormSpriteData("Gastrodon - East", 89, 91, 164, 165),
                
                // Cherrim: character = 92 + face + form, palette = 166 + (shiny*2) + form
                new PokemonSpriteModel.FormSpriteData("Cherrim - Overcast",  92, 94, 166, 168),
                new PokemonSpriteModel.FormSpriteData("Cherrim - Sunshine",  93, 95, 167, 169),
                
                // Arceus: character = 96 + (face/2) + form*2, palette = 170 + shiny + form*2
                new PokemonSpriteModel.FormSpriteData("Arceus - Normal",   96,  97, 170, 171),
                new PokemonSpriteModel.FormSpriteData("Arceus - Fighting", 98,  99, 172, 173),
                new PokemonSpriteModel.FormSpriteData("Arceus - Flying",  100, 101, 174, 175),
                new PokemonSpriteModel.FormSpriteData("Arceus - Poison",  102, 103, 176, 177),
                new PokemonSpriteModel.FormSpriteData("Arceus - Ground",  104, 105, 178, 179),
                new PokemonSpriteModel.FormSpriteData("Arceus - Rock",    106, 107, 180, 181),
                new PokemonSpriteModel.FormSpriteData("Arceus - Bug",     108, 109, 182, 183),
                new PokemonSpriteModel.FormSpriteData("Arceus - Ghost",   110, 111, 184, 185),
                new PokemonSpriteModel.FormSpriteData("Arceus - Steel",   112, 113, 186, 187),
                new PokemonSpriteModel.FormSpriteData("Arceus - ???",     114, 115, 188, 189),
                new PokemonSpriteModel.FormSpriteData("Arceus - Fire",    116, 117, 190, 191),
                new PokemonSpriteModel.FormSpriteData("Arceus - Water",   118, 119, 192, 193),
                new PokemonSpriteModel.FormSpriteData("Arceus - Grass",   120, 121, 194, 195),
                new PokemonSpriteModel.FormSpriteData("Arceus - Electric",122, 123, 196, 197),
                new PokemonSpriteModel.FormSpriteData("Arceus - Psychic", 124, 125, 198, 199),
                new PokemonSpriteModel.FormSpriteData("Arceus - Ice",     126, 127, 200, 201),
                new PokemonSpriteModel.FormSpriteData("Arceus - Dragon",  128, 129, 202, 203),
                new PokemonSpriteModel.FormSpriteData("Arceus - Dark",    130, 131, 204, 205),
                
                // Egg: character = 132 + form, palette = 206 + form (no back/front distinction)
                new PokemonSpriteModel.FormSpriteData("Egg - Normal",         132, 132, 206, 206),
                new PokemonSpriteModel.FormSpriteData("Egg - Manaphy", 133, 133, 207, 207),
                new PokemonSpriteModel.FormSpriteData("Bad Egg - Normal", 132, 132, 206, 206),
                };
            }
        }

        public static PokemonSpriteModel.FormSpriteData[] Platinum {
            get {
                return new PokemonSpriteModel.FormSpriteData[] {
                // Deoxys: character = 0 + (face/2) + form*2, palette = 154 + shiny
                new PokemonSpriteModel.FormSpriteData("Deoxys - Normal",   0,  1, 154, 155),
                new PokemonSpriteModel.FormSpriteData("Deoxys - Attack",   2,  3, 154, 155),
                new PokemonSpriteModel.FormSpriteData("Deoxys - Defense",  4,  5, 154, 155),
                new PokemonSpriteModel.FormSpriteData("Deoxys - Speed",    6,  7, 154, 155),
                
                // Unown: character = 8 + (face/2) + form*2, palette = 156 + shiny
                new PokemonSpriteModel.FormSpriteData("Unown - A",  8,  9, 156, 157),
                new PokemonSpriteModel.FormSpriteData("Unown - B", 10, 11, 156, 157),
                new PokemonSpriteModel.FormSpriteData("Unown - C", 12, 13, 156, 157),
                new PokemonSpriteModel.FormSpriteData("Unown - D", 14, 15, 156, 157),
                new PokemonSpriteModel.FormSpriteData("Unown - E", 16, 17, 156, 157),
                new PokemonSpriteModel.FormSpriteData("Unown - F", 18, 19, 156, 157),
                new PokemonSpriteModel.FormSpriteData("Unown - G", 20, 21, 156, 157),
                new PokemonSpriteModel.FormSpriteData("Unown - H", 22, 23, 156, 157),
                new PokemonSpriteModel.FormSpriteData("Unown - I", 24, 25, 156, 157),
                new PokemonSpriteModel.FormSpriteData("Unown - J", 26, 27, 156, 157),
                new PokemonSpriteModel.FormSpriteData("Unown - K", 28, 29, 156, 157),
                new PokemonSpriteModel.FormSpriteData("Unown - L", 30, 31, 156, 157),
                new PokemonSpriteModel.FormSpriteData("Unown - M", 32, 33, 156, 157),
                new PokemonSpriteModel.FormSpriteData("Unown - N", 34, 35, 156, 157),
                new PokemonSpriteModel.FormSpriteData("Unown - O", 36, 37, 156, 157),
                new PokemonSpriteModel.FormSpriteData("Unown - P", 38, 39, 156, 157),
                new PokemonSpriteModel.FormSpriteData("Unown - Q", 40, 41, 156, 157),
                new PokemonSpriteModel.FormSpriteData("Unown - R", 42, 43, 156, 157),
                new PokemonSpriteModel.FormSpriteData("Unown - S", 44, 45, 156, 157),
                new PokemonSpriteModel.FormSpriteData("Unown - T", 46, 47, 156, 157),
                new PokemonSpriteModel.FormSpriteData("Unown - U", 48, 49, 156, 157),
                new PokemonSpriteModel.FormSpriteData("Unown - V", 50, 51, 156, 157),
                new PokemonSpriteModel.FormSpriteData("Unown - W", 52, 53, 156, 157),
                new PokemonSpriteModel.FormSpriteData("Unown - X", 54, 55, 156, 157),
                new PokemonSpriteModel.FormSpriteData("Unown - Y", 56, 57, 156, 157),
                new PokemonSpriteModel.FormSpriteData("Unown - Z", 58, 59, 156, 157),
                new PokemonSpriteModel.FormSpriteData("Unown - !", 60, 61, 156, 157),
                new PokemonSpriteModel.FormSpriteData("Unown - ?", 62, 63, 156, 157),
                
                // Castform: character = 64 + (face*2) + form, palette = 158 + (shiny*4) + form
                new PokemonSpriteModel.FormSpriteData("Castform - Normal", 64, 68, 158, 162),
                new PokemonSpriteModel.FormSpriteData("Castform - Sunny",  65, 69, 159, 163),
                new PokemonSpriteModel.FormSpriteData("Castform - Rainy",  66, 70, 160, 164),
                new PokemonSpriteModel.FormSpriteData("Castform - Snowy",  67, 71, 161, 165),
                
                // Burmy: character = 72 + (face/2) + form*2, palette = 166 + shiny + form*2
                new PokemonSpriteModel.FormSpriteData("Burmy - Plant", 72, 73, 166, 167),
                new PokemonSpriteModel.FormSpriteData("Burmy - Sandy", 74, 75, 168, 169),
                new PokemonSpriteModel.FormSpriteData("Burmy - Trash", 76, 77, 170, 171),
                
                // Wormadam: character = 78 + (face/2) + form*2, palette = 172 + shiny + form*2
                new PokemonSpriteModel.FormSpriteData("Wormadam - Plant", 78, 79, 172, 173),
                new PokemonSpriteModel.FormSpriteData("Wormadam - Sandy", 80, 81, 174, 175),
                new PokemonSpriteModel.FormSpriteData("Wormadam - Trash", 82, 83, 176, 177),
                
                // Shellos: character = 84 + face + form, palette = 178 + shiny + form*2
                new PokemonSpriteModel.FormSpriteData("Shellos - West", 84, 86, 178, 179),
                new PokemonSpriteModel.FormSpriteData("Shellos - East", 85, 87, 180, 181),
                
                // Gastrodon: character = 88 + face + form, palette = 182 + shiny + form*2
                new PokemonSpriteModel.FormSpriteData("Gastrodon - West", 88, 90, 182, 183),
                new PokemonSpriteModel.FormSpriteData("Gastrodon - East", 89, 91, 184, 185),
                
                // Cherrim: character = 92 + face + form, palette = 186 + (shiny*2) + form
                new PokemonSpriteModel.FormSpriteData("Cherrim - Overcast", 92, 94, 186, 188),
                new PokemonSpriteModel.FormSpriteData("Cherrim - Sunshine", 93, 95, 187, 189),
                
                // Arceus: character = 96 + (face/2) + form*2, palette = 190 + shiny + form*2
                new PokemonSpriteModel.FormSpriteData("Arceus - Normal",   96,  97, 190, 191),
                new PokemonSpriteModel.FormSpriteData("Arceus - Fighting", 98,  99, 192, 193),
                new PokemonSpriteModel.FormSpriteData("Arceus - Flying",  100, 101, 194, 195),
                new PokemonSpriteModel.FormSpriteData("Arceus - Poison",  102, 103, 196, 197),
                new PokemonSpriteModel.FormSpriteData("Arceus - Ground",  104, 105, 198, 199),
                new PokemonSpriteModel.FormSpriteData("Arceus - Rock",    106, 107, 200, 201),
                new PokemonSpriteModel.FormSpriteData("Arceus - Bug",     108, 109, 202, 203),
                new PokemonSpriteModel.FormSpriteData("Arceus - Ghost",   110, 111, 204, 205),
                new PokemonSpriteModel.FormSpriteData("Arceus - Steel",   112, 113, 206, 207),
                new PokemonSpriteModel.FormSpriteData("Arceus - ???",     114, 115, 208, 209),
                new PokemonSpriteModel.FormSpriteData("Arceus - Fire",    116, 117, 210, 211),
                new PokemonSpriteModel.FormSpriteData("Arceus - Water",   118, 119, 212, 213),
                new PokemonSpriteModel.FormSpriteData("Arceus - Grass",   120, 121, 214, 215),
                new PokemonSpriteModel.FormSpriteData("Arceus - Electric",122, 123, 216, 217),
                new PokemonSpriteModel.FormSpriteData("Arceus - Psychic", 124, 125, 218, 219),
                new PokemonSpriteModel.FormSpriteData("Arceus - Ice",     126, 127, 220, 221),
                new PokemonSpriteModel.FormSpriteData("Arceus - Dragon",  128, 129, 222, 223),
                new PokemonSpriteModel.FormSpriteData("Arceus - Dark",    130, 131, 224, 225),
                
                // Egg: character = 132 + form, palette = 226 + form
                new PokemonSpriteModel.FormSpriteData("Egg - Normal",         132, 132, 226, 226),
                new PokemonSpriteModel.FormSpriteData("Egg - Manaphy", 133, 133, 227, 227),
                
                // Shaymin: character = 134 + (face/2) + form*2, palette = 228 + shiny + form*2
                new PokemonSpriteModel.FormSpriteData("Shaymin - Land", 134, 135, 228, 229),
                new PokemonSpriteModel.FormSpriteData("Shaymin - Sky",  136, 137, 230, 231),
                
                // Rotom: character = 138 + (face/2) + form*2, palette = 232 + shiny + form*2
                new PokemonSpriteModel.FormSpriteData("Rotom - Normal", 138, 139, 232, 233),
                new PokemonSpriteModel.FormSpriteData("Rotom - Heat",   140, 141, 234, 235),
                new PokemonSpriteModel.FormSpriteData("Rotom - Wash",   142, 143, 236, 237),
                new PokemonSpriteModel.FormSpriteData("Rotom - Frost",  144, 145, 238, 239),
                new PokemonSpriteModel.FormSpriteData("Rotom - Fan",    146, 147, 240, 241),
                new PokemonSpriteModel.FormSpriteData("Rotom - Mow",    148, 149, 242, 243),
                
                // Giratina: character = 150 + (face/2) + form*2, palette = 244 + shiny + form*2
                new PokemonSpriteModel.FormSpriteData("Giratina - Altered", 150, 151, 244, 245),
                new PokemonSpriteModel.FormSpriteData("Giratina - Origin",  152, 153, 246, 247),
                new PokemonSpriteModel.FormSpriteData("Bad Egg - Normal", 132, 132, 226, 226),
                };
            }
        }

        public static PokemonSpriteModel.FormSpriteData[] HGSS {
            get {
                return new PokemonSpriteModel.FormSpriteData[] {
                // Deoxys: character = 0 + (face/2) + form*2, palette = 0x9E (158) + shiny
                new PokemonSpriteModel.FormSpriteData("Deoxys - Normal",   0,  1, 158, 159),
                new PokemonSpriteModel.FormSpriteData("Deoxys - Attack",   2,  3, 158, 159),
                new PokemonSpriteModel.FormSpriteData("Deoxys - Defense",  4,  5, 158, 159),
                new PokemonSpriteModel.FormSpriteData("Deoxys - Speed",    6,  7, 158, 159),
                
                // Unown: character = 0x8 (8) + (face/2) + form*2, palette = 0xA0 (160) + shiny
                new PokemonSpriteModel.FormSpriteData("Unown - A",  8,  9, 160, 161),
                new PokemonSpriteModel.FormSpriteData("Unown - B", 10, 11, 160, 161),
                new PokemonSpriteModel.FormSpriteData("Unown - C", 12, 13, 160, 161),
                new PokemonSpriteModel.FormSpriteData("Unown - D", 14, 15, 160, 161),
                new PokemonSpriteModel.FormSpriteData("Unown - E", 16, 17, 160, 161),
                new PokemonSpriteModel.FormSpriteData("Unown - F", 18, 19, 160, 161),
                new PokemonSpriteModel.FormSpriteData("Unown - G", 20, 21, 160, 161),
                new PokemonSpriteModel.FormSpriteData("Unown - H", 22, 23, 160, 161),
                new PokemonSpriteModel.FormSpriteData("Unown - I", 24, 25, 160, 161),
                new PokemonSpriteModel.FormSpriteData("Unown - J", 26, 27, 160, 161),
                new PokemonSpriteModel.FormSpriteData("Unown - K", 28, 29, 160, 161),
                new PokemonSpriteModel.FormSpriteData("Unown - L", 30, 31, 160, 161),
                new PokemonSpriteModel.FormSpriteData("Unown - M", 32, 33, 160, 161),
                new PokemonSpriteModel.FormSpriteData("Unown - N", 34, 35, 160, 161),
                new PokemonSpriteModel.FormSpriteData("Unown - O", 36, 37, 160, 161),
                new PokemonSpriteModel.FormSpriteData("Unown - P", 38, 39, 160, 161),
                new PokemonSpriteModel.FormSpriteData("Unown - Q", 40, 41, 160, 161),
                new PokemonSpriteModel.FormSpriteData("Unown - R", 42, 43, 160, 161),
                new PokemonSpriteModel.FormSpriteData("Unown - S", 44, 45, 160, 161),
                new PokemonSpriteModel.FormSpriteData("Unown - T", 46, 47, 160, 161),
                new PokemonSpriteModel.FormSpriteData("Unown - U", 48, 49, 160, 161),
                new PokemonSpriteModel.FormSpriteData("Unown - V", 50, 51, 160, 161),
                new PokemonSpriteModel.FormSpriteData("Unown - W", 52, 53, 160, 161),
                new PokemonSpriteModel.FormSpriteData("Unown - X", 54, 55, 160, 161),
                new PokemonSpriteModel.FormSpriteData("Unown - Y", 56, 57, 160, 161),
                new PokemonSpriteModel.FormSpriteData("Unown - Z", 58, 59, 160, 161),
                new PokemonSpriteModel.FormSpriteData("Unown - !", 60, 61, 160, 161),
                new PokemonSpriteModel.FormSpriteData("Unown - ?", 62, 63, 160, 161),
                
                // Castform: character = 0x40 (64) + (face*2) + form, palette = 0xA2 (162) + (shiny*4) + form
                new PokemonSpriteModel.FormSpriteData("Castform - Normal", 64, 68, 162, 166),
                new PokemonSpriteModel.FormSpriteData("Castform - Sunny",  65, 69, 163, 167),
                new PokemonSpriteModel.FormSpriteData("Castform - Rainy",  66, 70, 164, 168),
                new PokemonSpriteModel.FormSpriteData("Castform - Snowy",  67, 71, 165, 169),
                
                // Burmy: character = 0x48 (72) + (face/2) + form*2, palette = 0xAA (170) + shiny + form*2
                new PokemonSpriteModel.FormSpriteData("Burmy - Plant", 72, 73, 170, 171),
                new PokemonSpriteModel.FormSpriteData("Burmy - Sandy", 74, 75, 172, 173),
                new PokemonSpriteModel.FormSpriteData("Burmy - Trash", 76, 77, 174, 175),
                
                // Wormadam: character = 0x4E (78) + (face/2) + form*2, palette = 0xB0 (176) + shiny + form*2
                new PokemonSpriteModel.FormSpriteData("Wormadam - Plant", 78, 79, 176, 177),
                new PokemonSpriteModel.FormSpriteData("Wormadam - Sandy", 80, 81, 178, 179),
                new PokemonSpriteModel.FormSpriteData("Wormadam - Trash", 82, 83, 180, 181),
                
                // Shellos: character = 0x54 (84) + face + form, palette = 0xB6 (182) + shiny + form*2
                new PokemonSpriteModel.FormSpriteData("Shellos - West", 84, 86, 182, 183),
                new PokemonSpriteModel.FormSpriteData("Shellos - East", 85, 87, 184, 185),
                
                // Gastrodon: character = 0x58 (88) + face + form, palette = 0xBA (186) + shiny + form*2
                new PokemonSpriteModel.FormSpriteData("Gastrodon - West", 88, 90, 186, 187),
                new PokemonSpriteModel.FormSpriteData("Gastrodon - East", 89, 91, 188, 189),
                
                // Cherrim: character = 0x5C (92) + face + form, palette = 0xBE (190) + (shiny*2) + form
                new PokemonSpriteModel.FormSpriteData("Cherrim - Overcast", 92, 94, 190, 192),
                new PokemonSpriteModel.FormSpriteData("Cherrim - Sunshine", 93, 95, 191, 193),
                
                // Arceus: character = 0x60 (96) + (face/2) + form*2, palette = 0xC2 (194) + shiny + form*2
                new PokemonSpriteModel.FormSpriteData("Arceus - Normal",   96,  97, 194, 195),
                new PokemonSpriteModel.FormSpriteData("Arceus - Fighting", 98,  99, 196, 197),
                new PokemonSpriteModel.FormSpriteData("Arceus - Flying",  100, 101, 198, 199),
                new PokemonSpriteModel.FormSpriteData("Arceus - Poison",  102, 103, 200, 201),
                new PokemonSpriteModel.FormSpriteData("Arceus - Ground",  104, 105, 202, 203),
                new PokemonSpriteModel.FormSpriteData("Arceus - Rock",    106, 107, 204, 205),
                new PokemonSpriteModel.FormSpriteData("Arceus - Bug",     108, 109, 206, 207),
                new PokemonSpriteModel.FormSpriteData("Arceus - Ghost",   110, 111, 208, 209),
                new PokemonSpriteModel.FormSpriteData("Arceus - Steel",   112, 113, 210, 211),
                new PokemonSpriteModel.FormSpriteData("Arceus - ???",     114, 115, 212, 213),
                new PokemonSpriteModel.FormSpriteData("Arceus - Fire",    116, 117, 214, 215),
                new PokemonSpriteModel.FormSpriteData("Arceus - Water",   118, 119, 216, 217),
                new PokemonSpriteModel.FormSpriteData("Arceus - Grass",   120, 121, 218, 219),
                new PokemonSpriteModel.FormSpriteData("Arceus - Electric",122, 123, 220, 221),
                new PokemonSpriteModel.FormSpriteData("Arceus - Psychic", 124, 125, 222, 223),
                new PokemonSpriteModel.FormSpriteData("Arceus - Ice",     126, 127, 224, 225),
                new PokemonSpriteModel.FormSpriteData("Arceus - Dragon",  128, 129, 226, 227),
                new PokemonSpriteModel.FormSpriteData("Arceus - Dark",    130, 131, 228, 229),
                
                // Egg: character = 0x84 (132) + form, palette = 0xE6 (230) + form
                new PokemonSpriteModel.FormSpriteData("Egg - Normal",         132, 132, 230, 230),
                new PokemonSpriteModel.FormSpriteData("Egg - Manaphy", 133, 133, 231, 231),
                
                // Shaymin: character = 0x86 (134) + (face/2) + form*2, palette = 0xE8 (232) + shiny + form*2
                new PokemonSpriteModel.FormSpriteData("Shaymin - Land", 134, 135, 232, 233),
                new PokemonSpriteModel.FormSpriteData("Shaymin - Sky",  136, 137, 234, 235),
                
                // Rotom: character = 0x8A (138) + (face/2) + form*2, palette = 0xEC (236) + shiny + form*2
                new PokemonSpriteModel.FormSpriteData("Rotom - Normal", 138, 139, 236, 237),
                new PokemonSpriteModel.FormSpriteData("Rotom - Heat",   140, 141, 238, 239),
                new PokemonSpriteModel.FormSpriteData("Rotom - Wash",   142, 143, 240, 241),
                new PokemonSpriteModel.FormSpriteData("Rotom - Frost",  144, 145, 242, 243),
                new PokemonSpriteModel.FormSpriteData("Rotom - Fan",    146, 147, 244, 245),
                new PokemonSpriteModel.FormSpriteData("Rotom - Mow",    148, 149, 246, 247),
                
                // Giratina: character = 0x96 (150) + (face/2) + form*2, palette = 0xF8 (248) + shiny + form*2
                new PokemonSpriteModel.FormSpriteData("Giratina - Altered", 150, 151, 248, 249),
                new PokemonSpriteModel.FormSpriteData("Giratina - Origin",  152, 153, 250, 251),
                
                // Pichu (Spiky-ear): character = 0x9A (154) + (face/2) + form*2, palette = 0xFC (252) + shiny + form*2
                // Note: form 0 is normal Pichu (uses main NARC), form 1 is Spiky-ear
                new PokemonSpriteModel.FormSpriteData("Pichu - Normal",    154, 155, 252, 253),
                new PokemonSpriteModel.FormSpriteData("Pichu - Spiky-ear", 156, 157, 254, 255),
                new PokemonSpriteModel.FormSpriteData("Bad Egg - Normal", 132, 132, 230, 230),
                };
            }
        }

    }
}
