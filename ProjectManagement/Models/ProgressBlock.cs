using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace WpfResourceGantt.ProjectManagement.Models
{
    public class ProgressItem
    {
        public int Sequence { get; set; }
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("isCompleted")]
        public bool IsCompleted { get; set; }
    }
    public class ProgressBlock
    {
        public int Sequence { get; set; }
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        // Replaced simple counters with a list of actual items
        [JsonPropertyName("items")]
        public List<ProgressItem> Items { get; set; } = new List<ProgressItem>();

        [JsonPropertyName("isCompleted")]
        public bool IsCompleted { get; set; }
    }
}
