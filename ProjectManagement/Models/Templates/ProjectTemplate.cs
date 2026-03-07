using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfResourceGantt.ProjectManagement.Models.Templates
{
    public class ProjectTemplate
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } // e.g., "Standard Software Rollout"

        public string Description { get; set; }

        // A template contains a list of Gates (Level 3 items)
        public virtual ICollection<TemplateGate> Gates { get; set; } = new List<TemplateGate>();
    }
}
