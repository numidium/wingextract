using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;

namespace wingextract
{
    class Program
    {
        static void Main(string[] args)
        {
            const string paletteFileName = "./Data/WC1.PAL";
            if (!File.Exists(paletteFileName))
            {
                Console.WriteLine("Couldn't find palette file.");
                return;
            }

            var palette = LoadPalette(paletteFileName);
            if (palette == null)
            {
                Console.WriteLine("Palette file is of incorrect length.");
                return;
            }

            var bitmaps = GetBitmapsFromVXX("ARROW.VGA", palette);
            WriteBitmaps(bitmaps);
        }

        static Color[] LoadPalette(string fileName)
        {
            const int paletteColorCount = 256;
            var palette = new Color[paletteColorCount];
            var stream = File.OpenRead(fileName);
            if (stream.Length != paletteColorCount * 3)
                return null;
            var binaryReader = new BinaryReader(stream);
            int i = 0;
            while (binaryReader.PeekChar() != -1 && i < paletteColorCount)
                palette[i++] = Color.FromArgb(binaryReader.ReadByte(), binaryReader.ReadByte(), binaryReader.ReadByte());
            binaryReader.Close();

            return palette;
        }

        static List<Bitmap> GetBitmapsFromVXX(string fileName, Color[] palette)
        {
            if (!File.Exists(fileName))
                return null;
            var bitmaps = new List<Bitmap>();
            using (var binaryReader = new BinaryReader(File.OpenRead(fileName)))
            {
                var fileLength = binaryReader.ReadUInt32();
                var tableOffsets = new List<uint>();
                tableOffsets.Add(binaryReader.ReadUInt32() & 0x00FFFFFF); // length of lv1 table
                while (binaryReader.BaseStream.Position < tableOffsets[0])
                    tableOffsets.Add(binaryReader.ReadUInt32() & 0x00FFFFFF);
                var tables = new List<VxxTable>();

                // Iterate through image collections
                for (int collectionIndex = 0; collectionIndex < tableOffsets.Count; collectionIndex++)
                {
                    tables.Add(new VxxTable(binaryReader.ReadUInt32()));
                    tables.Last().Offsets.Add(binaryReader.ReadUInt32() & 0x00FFFFFF);
                    if (tables.Last().Offsets[0] > fileLength)
                    {
                        Console.WriteLine("Table offset at " + binaryReader.BaseStream.Position + " exceeds file length. Skipping.");
                        binaryReader.BaseStream.Seek(tableOffsets[collectionIndex + 1], SeekOrigin.Begin);
                        continue;
                    }
                    else // read more pointers to image data from lv2 table
                    {
                        tables.Last().Offsets[0] += tableOffsets[collectionIndex]; // Set to file-absolute offset
                        while (binaryReader.BaseStream.Position < tables.Last().Offsets[0])
                        {
                            tables.Last().Offsets.Add(binaryReader.ReadUInt32() & 0x00FFFFFF);
                            tables.Last().Offsets[^1] += tableOffsets[collectionIndex]; // offset to image
                        }
                    }

                    // Apply pixels to bitmaps
                    for (int imageIndex = 0; imageIndex < tables.Last().Offsets.Count; imageIndex++)
                    {
                        var bitmap = new Bitmap(320, 200)
                        {
                            Tag = "vga" + collectionIndex.ToString() + "_" + imageIndex.ToString() + ".png"
                        };

                        var x2 = binaryReader.ReadInt16();
                        var x1 = binaryReader.ReadInt16();
                        var y1 = binaryReader.ReadInt16();
                        var y2 = binaryReader.ReadInt16();
                        var width = (short)(x1 + x2 + 1);
                        var height = (short)(y1 + y2 + 1);
                        var origin = new Tuple<short, short>(x1, y1);
                        while (true)
                        {
                            var key = binaryReader.ReadInt16();
                            if (key == 0)
                                break;
                            var dx = binaryReader.ReadInt16();
                            var dy = binaryReader.ReadInt16();
                            var carry = key & 1;
                            byte colorIndex;
                            if (carry == 0) // Not an RLE string
                            {
                                for (int i = 0; i < key >> 1; i++)
                                {
                                    colorIndex = binaryReader.ReadByte();
                                    if (dx + origin.Item1 >= width || origin.Item2 >= height ||
                                        dx + origin.Item1 < 0 || dy + origin.Item2 < 0)
                                        Console.WriteLine("Skipping invalid pixel coordinates: " + origin.Item1 + ", " + origin.Item2);
                                    else
                                        bitmap.SetPixel(origin.Item1 + dx, origin.Item2 + dy, palette[colorIndex]);
                                    dx++;
                                }
                            }
                            else
                            {
                                short runIndex = 0;
                                while (runIndex < key >> 1) // Loop for run length
                                {
                                    var buffer = binaryReader.ReadByte();
                                    if ((buffer & 1) == 0)
                                    {
                                        for (int i = 0; i < buffer >> 1; i++)
                                        {
                                            colorIndex = binaryReader.ReadByte();
                                            if (dx + origin.Item1 >= width || dy + origin.Item2 >= height ||
                                                dx + origin.Item1 < 0 || dy + origin.Item2 < 0)
                                                Console.WriteLine("Skipping invalid pixel coordinates: " + origin.Item1 + ", " + origin.Item2);
                                            else
                                                bitmap.SetPixel(origin.Item1 + dx, origin.Item2 + dy, palette[colorIndex]);
                                            runIndex++;
                                            dx++;
                                        }
                                    }
                                    else
                                    {
                                        colorIndex = binaryReader.ReadByte();
                                        for (int i = 0; i < buffer >> 1; i++)
                                        {
                                            if (dx + origin.Item1 >= width || origin.Item2 >= height ||
                                                dx + origin.Item1 < 0 || dy + origin.Item2 < 0)
                                                Console.WriteLine("Skipping invalid pixel coordinates: " + origin.Item1 + ", " + origin.Item2);
                                            else
                                                bitmap.SetPixel(origin.Item1 + dx, origin.Item2 + dy, palette[colorIndex]);
                                            runIndex++;
                                            dx++;
                                        }
                                    }
                                }
                            }
                        }

                        bitmaps.Add(bitmap);
                    }
                }
            }

            return bitmaps;
        }

        static void WriteBitmaps(List<Bitmap> bitmaps)
        {
            foreach (var bitmap in bitmaps)
            {
                string fileName = (string)bitmap.Tag;
                Console.WriteLine("Writing image to file: " + fileName);
                bitmap.Save(fileName);
            }
        }
    }
}
