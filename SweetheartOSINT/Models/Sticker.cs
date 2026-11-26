using System;
using System.Collections.Generic;

namespace SweetheartOSINT.Models
{
    public class Sticker
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; } = 230;
        public double Height { get; set; } = 190;
        public string Text { get; set; }
        public string ImageFile { get; set; }
        public double ImageRotation { get; set; }
        public List<StickerSource> Sources { get; set; } = new List<StickerSource>();
    }

    public class StickerSource
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Url { get; set; }
    }
}
