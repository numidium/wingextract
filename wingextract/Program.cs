using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

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

            var palette = GraphicsIO.LoadPalette(paletteFileName);
            if (palette == null)
            {
                Console.WriteLine("Palette file is of incorrect length. Must contain 256 RGB colors.");
                return;
            }

            var bitmaps = GraphicsIO.GetBitmapsFromVXX("COCKPIT.VGA", palette);
            WriteBitmaps(GetGDIBitmaps(bitmaps));
        }

        public static Bitmap GetGDIBitmap(WingBitmap wingBitmap)
        {
            var bitmap = new Bitmap(wingBitmap.Width, wingBitmap.Height)
            {
                Tag = wingBitmap.FileName
            };

            foreach (var pixel in wingBitmap.Pixels)
                bitmap.SetPixel(pixel.X, pixel.Y, Color.FromArgb(pixel.Color.R, pixel.Color.G, pixel.Color.B));

            return bitmap;
        }

        public static List<Bitmap> GetGDIBitmaps(List<WingBitmap> wingBitmaps)
        {
            var bitmaps = new List<Bitmap>();
            foreach (var wingBitmap in wingBitmaps)
                bitmaps.Add(GetGDIBitmap(wingBitmap));

            return bitmaps;
        }

        public static void WriteBitmaps(List<Bitmap> bitmaps)
        {
            foreach (var bitmap in bitmaps)
            {
                string fileName = (string)bitmap.Tag;
                Console.WriteLine("Writing image to file: " + fileName);
                bitmap.Save("./Output/" + fileName);
            }
        }
    }
}
