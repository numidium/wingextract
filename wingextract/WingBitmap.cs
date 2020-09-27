using System.Collections.Generic;

namespace wingextract
{
    public class WingBitmap
    {
        public struct Color
        {
            public byte R { get; set; }
            public byte G { get; set; }
            public byte B { get; set; }
        }

        public struct Pixel
        {
            public Color Color { get; set; }
            public int X { get; set; }
            public int Y { get; set; }
        }

        public int Width { get; private set; }
        public int Height { get; private set; }
        public string FileName { get; private set; }
        public List<Pixel> Pixels { get; private set; }

        public WingBitmap(int width, int height, string fileName)
        {
            Width = width;
            Height = height;
            FileName = fileName;
            Pixels = new List<Pixel>();
        }

        public void SetPixel(int x, int y, Color color)
        {
            Pixels.Add(new Pixel
            {
                Color = color,
                X = x,
                Y = y
            });
        }
    }
}
