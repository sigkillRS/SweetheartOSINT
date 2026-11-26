using System;

namespace SweetheartOSINT.Models
{
    public class StickerLink
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string FromId { get; set; }
        public string ToId { get; set; }
    }
}
