using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace wingextract
{
    public class GraphicsIO
    {
        private struct VxxTable
        {
            public uint BlockLength { get; private set; }
            public List<uint> Offsets { get; private set; }

            public VxxTable(uint blockLength)
            {
                BlockLength = blockLength;
                Offsets = new List<uint>();
            }
        }

        /// <summary>
        /// Loads a color palette from a binary file containing an array of RGB colors.
        /// </summary>
        /// <param name="fileName">The filesystem path to the palette file.</param>
        /// <returns>An array of full-alpha colors.</returns>
        public static WingBitmap.Color[] LoadPalette(string fileName)
        {
            const int paletteColorCount = 256;
            var palette = new WingBitmap.Color[paletteColorCount];
            var stream = File.OpenRead(fileName);
            if (stream.Length != paletteColorCount * 3)
                return null;
            using (var binaryReader = new BinaryReader(stream))
            {
                int i = 0;
                while (binaryReader.PeekChar() != -1 && i < paletteColorCount)
                    palette[i++] = new WingBitmap.Color 
                    {
                        R = binaryReader.ReadByte(),
                        G = binaryReader.ReadByte(),
                        B = binaryReader.ReadByte() 
                    };
            }

            return palette;
        }

        /// <summary>
        /// Converts Wing Commander VGA images to a usable bitmap format.
        /// VGA files contain a table of image collections. Each collection contains a sequence of images.
        /// Each image contains a sequence of run-length encoded pixels plotted about a specified origin.
        /// </summary>
        /// <param name="fileName">The filesystem path to the VGA file.</param>
        /// <param name="palette">A color palette to use when creating the bitmaps. Must have a length of 256.</param>
        /// <returns>A list of bitmaps from a Wing Commander VGA file.</returns>
        public static List<WingBitmap> GetBitmapsFromVXX(string fileName, WingBitmap.Color[] palette)
        {
            if (!File.Exists(fileName))
                return null;
            if (palette.Length != 256)
                return null;
            var bitmaps = new List<WingBitmap>();
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
                        Console.WriteLine("Table offset at " + binaryReader.BaseStream.Position + " (collection " + collectionIndex + ")" + " exceeds file length. Skipping.");
                        if (collectionIndex < tableOffsets.Count - 1)
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
                        var bitmap = new WingBitmap(320, 200, "vga" + collectionIndex.ToString() + "_" + imageIndex.ToString() + ".png");

                        var x2 = binaryReader.ReadInt16();
                        var x1 = binaryReader.ReadInt16();
                        var y1 = binaryReader.ReadInt16();
                        var y2 = binaryReader.ReadInt16();
                        // origin = (x1, y1)
                        var width = (short)(x1 + x2 + 1);
                        var height = (short)(y1 + y2 + 1);
                        short key; // Contains the 15-bit loop run length followed by a flag in the LSB that determines whether or not to proceed to the 2nd level loop
                        while ((key = binaryReader.ReadInt16()) != 0)
                        {
                            var dx = binaryReader.ReadInt16();
                            var dy = binaryReader.ReadInt16();
                            // Some keys and Y coordinates are very large for some reason. Extra data?
                            // Example: spark image collection (4) in COCKPIT.VGA
                            var pixelY = y1 + dy;
                            byte colorIndex;
                            if ((key & 1) == 0) // Perform 1st level loop
                            {
                                for (int runIndex = 0; runIndex < key >> 1; runIndex++)
                                {
                                    var pixelX = x1 + dx;
                                    colorIndex = binaryReader.ReadByte();
                                    if (ValidatePixel(pixelX, pixelY, width, height, bitmap.Width, bitmap.Height))
                                        bitmap.SetPixel(pixelX, pixelY, palette[colorIndex]);
                                    else
                                        WriteInvalidPixelError(collectionIndex, imageIndex, pixelX, pixelY);
                                    dx++;
                                }
                            }
                            else // When 2nd key exists, paint a sequence of single pixels or RLE lines until iterated to 1st key value
                            {
                                short runIndex = 0;
                                while (runIndex < key >> 1) // Loop for key length
                                {
                                    var key2 = binaryReader.ReadByte();
                                    if ((key2 & 1) == 0) // Not an RLE string
                                    {
                                        for (int i = 0; i < key2 >> 1; i++)
                                        {
                                            var pixelX = x1 + dx;
                                            colorIndex = binaryReader.ReadByte();
                                            if (ValidatePixel(pixelX, pixelY, width, height, bitmap.Width, bitmap.Height))
                                                bitmap.SetPixel(pixelX, pixelY, palette[colorIndex]);
                                            else
                                                WriteInvalidPixelError(collectionIndex, imageIndex, pixelX, pixelY);
                                            runIndex++;
                                            dx++;
                                        }
                                    }
                                    else
                                    {
                                        colorIndex = binaryReader.ReadByte();
                                        for (int i = 0; i < key2 >> 1; i++)
                                        {
                                            var pixelX = x1 + dx;
                                            if (ValidatePixel(pixelX, pixelY, width, height, bitmap.Width, bitmap.Height))
                                                bitmap.SetPixel(x1 + dx, pixelY, palette[colorIndex]);
                                            else
                                                WriteInvalidPixelError(collectionIndex, imageIndex, x1 + dx, pixelY);
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

        private static bool ValidatePixel(int x, int y, int width, int height, int bitmapWidth, int bitmapHeight)
            => !(x >= width || y >= height || x < 0 || y < 0 || x >= bitmapWidth || y >= bitmapHeight);
        

        private static void WriteInvalidPixelError(int collectionIndex, int imageIndex, int x, int y)
            => Console.WriteLine(collectionIndex.ToString() + "_" + imageIndex.ToString() + " - " + "Invalid pixel coordinates: " + x + ", " + y);
    }
}
