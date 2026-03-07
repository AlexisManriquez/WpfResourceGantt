using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace WpfResourceGantt.ProjectManagement.Models
{
    public enum AssignmentRole
    {
        Primary,
        Secondary
    }

    public class ResourceAssignment
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("workItemId")]
        public string WorkItemId { get; set; }

        [JsonPropertyName("developerId")]
        public string DeveloperId { get; set; }

        [JsonPropertyName("role")]
        public AssignmentRole Role { get; set; }
    }
}
