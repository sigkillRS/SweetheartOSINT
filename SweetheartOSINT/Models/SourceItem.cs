using System;
using System.Text.Json.Serialization;

namespace SweetheartOSINT.Models
{
    public class SourceItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Type { get; set; } = "note";
        public string Content { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [JsonIgnore]
        public bool IsLink => string.Equals(Type, "link", StringComparison.OrdinalIgnoreCase);
    }
}
