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

            var bitmaps = GetBitmapsFromVXX("COCKPIT.VGA");
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

        static List<Bitmap> GetBitmapsFromVXX(string fileName)
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
                for (int i = 0; i < tableOffsets.Count; i++)
                {
                    tables.Add(new VxxTable(binaryReader.ReadUInt32()));
                    tables[i].Offsets.Add(binaryReader.ReadUInt32() & 0x00FFFFFF);
                    if (tables[i].Offsets[0] > fileLength)
                    {
                        Console.WriteLine("Table offset at " + binaryReader.BaseStream.Position + " exceeds file length. Skipping.");
                        binaryReader.BaseStream.Seek(tableOffsets[i + 1], SeekOrigin.Begin);
                        continue;
                    }
                    else // read more pointers to image data from lv2 table
                    {
                        tables[i].Offsets[0] += tableOffsets[i]; // Set to file-absolute offset
                        while (binaryReader.BaseStream.Position < tables[i].Offsets[0])
                        {
                            tables[i].Offsets.Add(binaryReader.ReadUInt32() & 0x00FFFFFF);
                            tables[i].Offsets[^1] += tableOffsets[i]; // offset to image
                        }
                    }

                    for (int j = 0; j < tables[i].Offsets.Count; j++)
                    {
                        var bitmap = new Bitmap(320, 200);
                        bitmap.Tag = "vga" + i.ToString() + "_" + j.ToString();
                    }
                }
            }

            return bitmaps;
        }
    }
}
