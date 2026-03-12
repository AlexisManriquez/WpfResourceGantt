using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace WpfResourceGantt.ProjectManagement.Models
{
    public class ProgressItem
    {
        [Timestamp]
        public byte[] RowVersion { get; set; }

        [NotMapped]
        [JsonIgnore]
        public bool IsDirty { get; set; }

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
        [Timestamp]
        public byte[] RowVersion { get; set; }

        [NotMapped]
        [JsonIgnore]
        public bool IsDirty { get; set; }

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
