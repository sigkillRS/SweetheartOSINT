using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using SweetheartOSINT.Services;

namespace SweetheartOSINT.Models
{
    public class ProjectData
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = "Новый проект";
        public int Percent { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        public string PreviewFile { get; set; } = "preview.png";
        public List<SourceItem> Sources { get; set; } = new List<SourceItem>();
        public List<string> ImageFiles { get; set; } = new List<string>();
        public List<Sticker> Stickers { get; set; } = new List<Sticker>();
        public List<StickerLink> StickerLinks { get; set; } = new List<StickerLink>();
        public CanvasViewport CanvasViewport { get; set; } = new CanvasViewport();

        [JsonIgnore]
        public string PreviewPath => StorageService.GetPreviewPath(this);

        [JsonIgnore]
        public int SourcesCount => Sources == null ? 0 : Sources.Count;

        [JsonIgnore]
        public int ImagesCount => ImageFiles == null ? 0 : ImageFiles.Count;

        [JsonIgnore]
        public IEnumerable<string> ImagePaths => ImageFiles == null
            ? Enumerable.Empty<string>()
            : ImageFiles.Select(file => StorageService.GetImagePath(this, file));
    }
}
