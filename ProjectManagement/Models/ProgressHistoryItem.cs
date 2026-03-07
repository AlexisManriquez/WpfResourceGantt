using System;
using System.Text.Json.Serialization;

namespace WpfResourceGantt.ProjectManagement.Models
{
    public class ProgressHistoryItem
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonPropertyName("date")]
        public DateTime Date { get; set; }

        [JsonPropertyName("expectedProgress")]
        public double ExpectedProgress { get; set; } // 0.0 to 1.0

        [JsonPropertyName("actualProgress")]
        public double ActualProgress { get; set; } // 0.0 to 1.0

        // --- NEW PROPERTY ---
        [JsonPropertyName("actualWork")]
        public double? ActualWork { get; set; } // Cumulative Hours Spent (ACWP Helper)
    }
}
