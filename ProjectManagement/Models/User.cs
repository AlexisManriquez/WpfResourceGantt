using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace WpfResourceGantt.ProjectManagement.Models
{
    public class User
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("section")]
        public string? Section { get; set; }

        [JsonPropertyName("role")]
        public Role Role { get; set; }

        [JsonPropertyName("hourlyRate")]
        public decimal? HourlyRate { get; set; }

        [JsonPropertyName("weeklyCapacity")]
        public double WeeklyCapacity { get; set; } = 40.0;

        // This will only be populated for Section Chiefs
        [JsonPropertyName("managedProjectManagerIds")]
        public List<string>? ManagedProjectManagerIds { get; set; } = new List<string>();

        [JsonIgnore]
        public string GroupHeader
        {
            get
            {
                switch (Role)
                {
                    case Role.Developer:
                        // Format "B" into "Section B"
                        string sec = string.IsNullOrWhiteSpace(Section) ? "General" : Section;
                        // Check if it already has the word "Section" to avoid "Section Section B"
                        if (!sec.StartsWith("Section", StringComparison.OrdinalIgnoreCase))
                        {
                            return $"Section {sec}";
                        }
                        return sec;

                    case Role.FlightChief:
                        return "Flight Chief";

                    case Role.SectionChief:
                        return "Section Chief";

                    case Role.ProjectManager:
                        return "Project Managers";

                    default:
                        return Role.ToString();
                }
            }
        }

        // --- NEW: Property for Custom Sorting ---
        [JsonIgnore]
        public int GroupOrder
        {
            get
            {
                switch (Role)
                {
                    case Role.FlightChief: return 0;  // Top
                    case Role.SectionChief: return 1;
                    case Role.ProjectManager: return 2;
                    case Role.Developer: return 3;    // Bottom
                    default: return 4;
                }
            }
        }
    }
}
