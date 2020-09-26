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

            var palette = GraphicsIO.LoadPalette(paletteFileName);
            if (palette == null)
            {
                Console.WriteLine("Palette file is of incorrect length. Must contain 256 RGB colors.");
                return;
            }

            var bitmaps = GraphicsIO.GetBitmapsFromVXX("ARROW.VGA", palette);
            GraphicsIO.WriteBitmaps(bitmaps);
        }
    }
}
