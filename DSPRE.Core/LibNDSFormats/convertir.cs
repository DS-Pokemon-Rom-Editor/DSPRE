/*
 * Copyright (C) 2011  pleoNeX
 *
 *   This program is free software: you can redistribute it and/or modify
 *   it under the terms of the GNU General Public License as published by
 *   the Free Software Foundation, either version 3 of the License, or
 *   (at your option) any later version.
 *
 *   This program is distributed in the hope that it will be useful,
 *   but WITHOUT ANY WARRANTY; without even the implied warranty of
 *   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *   GNU General Public License for more details.
 *
 *   You should have received a copy of the GNU General Public License
 *   along with this program.  If not, see <http://www.gnu.org/licenses/>. 
 *
 * Programador: pleoNeX
 * Programa utilizado: Microsoft Visual C# 2010 Express
 * Fecha: 18/02/2011
 * 
 */
using MKDS_Course_Editor;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace Tinke
{
    public static class Convertir
    {
        #region Paleta
        /// <summary>
        /// A partir de un array de bytes devuelve un array de colores.
        /// </summary>
        /// <param name="bytes">Bytes para convertir</param>
        /// <returns>Colores de la paleta.</returns>
        public static Color[] BGR555ToColorArray(this byte[] bytes)
        {
            Color[] paleta = new Color[bytes.Length / 2];

            for (int i = 0; i < paleta.Length; i++)
            {
                paleta[i] = BGR555ToColor(bytes[i * 2], bytes[i * 2 + 1]);
            }
            return paleta;
        }
        public static byte[] ColorArrayToBGR555(this Color[] palette)
        {
            byte[] ret = new byte[palette.Length * 2];

            for (int i = 0; i < palette.Length; i++)
            {
                (byte b1, byte b2) = palette[i].ToBGR555();
                (ret[(i * 2) + 0], ret[(i * 2) + 1]) = (b1, b2);
            }

            return ret;
        }
        public static (byte b1, byte b2) ToBGR555(this Color c)
        {
            byte[] result = BitConverter.GetBytes((short)nclr_e.EncodeColor(c.ToArgb(), BGR555_));
            return (result[0], result[1]);
        }
        /// <summary>
        /// Convierte dos bytes en un color.
        /// </summary>
        /// <param name="byte1">Primer byte</param>
        /// <param name="byte2">Segundo byte</param>
        /// <returns>Color convertido</returns>
        public static Color BGR555ToColor(byte byte1, byte byte2)
        {
            /*int r, b; double g;

            r = (byte1 % 0x20) * 0x8;
            g = (byte1 / 0x20 + ((byte2 % 0x4) * 7.96875)) * 0x8;
            b = byte2 / 0x4 * 0x8;
            try
            {
                return System.Drawing.Color.FromArgb(r, (int)g, b);
            }
            catch (Exception)
            {
                // A colour with its top bit set pushes blue past 255 and draws black.
                return Color.Black;
            }*/
            return Color.FromArgb(DecodeColor(BitConverter.ToInt16(new byte[] { byte1, byte2 }, 0), BGR555_));

        }
        public static byte[][] BytesToTiles_NoChanged(byte[] bytes, int tilesX, int tilesY)
        {
            List<byte[]> tiles = new List<byte[]>();
            List<byte> temp = new List<byte>();

            for (int ht = 0; ht < tilesY; ht++)
            {
                for (int wt = 0; wt < tilesX; wt++)
                {
                    // Get the tile data
                    for (int h = 0; h < 8; h++)
                    {
                        for (int w = 0; w < 8; w++)
                        {
                            temp.Add(bytes[wt * 8 + ht * tilesX * 64 + (w + h * 8 * tilesX)]);
                        }
                    }
                    // Set the tile data
                    tiles.Add(temp.ToArray());
                    temp.Clear();
                }
            }

            return tiles.ToArray();
        }
        public static nclr_e.CColorFormat BGR555_ = new nclr_e.CColorFormat("BGR555", 10, 16, new int[] {
        3, 2, 1, 5, 5, 5
    });
        public static int[][] ShiftList = new int[][] { 
            new int[] { 0, 0 }, 
            new int[] { 1, 255 }, 
            new int[] { 3, 85 }, 
            new int[] { 7, 36 }, 
            new int[] { 15, 17 }, 
            new int[] { 31, 8 }, 
            new int[] { 63, 4 }, 
            new int[] { 127, 2 }, 
            new int[] { 255, 1 } 
        };

        public static int DecodeColor(int value, nclr_e.CColorFormat format)
        {
            int[] res = format.GetResolution();
            int rgb = Color.FromArgb(255, 0, 0, 0).ToArgb();
            int shift = 0;
            int length = res.Length / 2;

            for (int i = 0; i < length; i++)
            {
                int mode = res[length - i - 1];
                int nshift = res[length * 2 - i - 1];
                int mult = ShiftList[nshift][1];
                int and = ShiftList[nshift][0];
                int n = (value >> shift & and) * mult;

                switch (mode)
                {
                    case 0: // '\0'
                        rgb |= n << 24;
                        break;

                    case 1: // '\001'
                        rgb |= n << 16;
                        break;

                    case 2: // '\002'
                        rgb |= n << 8;
                        break;

                    case 3: // '\003'
                        rgb |= n;
                        break;

                    case 4: // '\004'
                        rgb = n << 16 | n << 8 | n;
                        break;
                }
                shift += nshift;
            }

            return rgb;
        }
        #endregion

        #region Tiles
        /// <summary>
        /// Convierte una array de Tiles en bytes
        /// </summary>
        /// <param name="tiles">Tiles para convertir</param>
        /// <returns>Array de bytes</returns>
        public static byte[] TilesToBytes(byte[][] tiles)
        {
            List<byte> resul = new List<byte>();

            for (int i = 0; i < tiles.Length; i++)
                for (int j = 0; j < 64; j++)
                    resul.Add(tiles[i][j]);

            return resul.ToArray();

        }
        /// <summary>
        /// Convierte una array de bytes en otra de tiles
        /// </summary>
        /// <param name="bytes">Bytes para convertir</param>
        /// <returns>Array de tiles</returns>
        public static byte[][] BytesToTiles(byte[] bytes)
        {
            List<byte[]> resul = new List<byte[]>();
            List<byte> temp = new List<byte>();

            for (int i = 0; i < bytes.Length / 64; i++)
            {
                for (int j = 0; j < 64; j++)
                    temp.Add(bytes[j + i * 64]);

                resul.Add(temp.ToArray());
                temp.Clear();
            }

            return resul.ToArray();

        }
        #endregion

        /// <summary>
        /// Convierte una array de bytes en formato 4-bit a otro en formato 8-bit
        /// </summary>
        /// <param name="bits4">Datos de entrada en formato 4-bit (valor máximo 15 (0xF))</param>
        /// <returns>Devuelve una array de bytes en 8-bit</returns>
        public static Byte[] Bit4ToBit8(byte[] bits4)
        {
            List<byte> bits8 = new List<byte>();

            for (int i = 0; i < bits4.Length; i += 2)
            {
                string nByte = String.Format("{0:X}", bits4[i]);
                nByte += String.Format("{0:X}", bits4[i + 1]);

                bits8.Add((byte)Convert.ToInt32(nByte, 16));
            }

            return bits8.ToArray();

        }
        /// <summary>
        /// Convierte una array de bytes en formato 8-bit a otro en formato 4-bit
        /// </summary>
        /// <param name="bits8">Datos de entrada en formato 8-bit</param>
        /// <returns>Devuelve una array de bytes en 4-bit</returns>
        public static Byte[] Bit8ToBit4(byte[] bits8)
        {
            List<byte> bits4 = new List<byte>();

            for (int i = 0; i < bits8.Length; i++)
            {
                string nByte = String.Format("{0:X}", bits8[i]);
                if (nByte.Length == 1)
                    nByte = '0' + nByte;
                bits4.Add((byte)Convert.ToInt32(nByte[0].ToString(), 16));
                bits4.Add((byte)Convert.ToInt32(nByte[1].ToString(), 16));
            }

            return bits4.ToArray();

        }
    }
}
