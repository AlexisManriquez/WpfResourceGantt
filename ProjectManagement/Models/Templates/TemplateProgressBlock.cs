using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfResourceGantt.ProjectManagement.Models.Templates
{
    public class TemplateProgressBlock
    {
        [Key]
        public int Id { get; set; }

        public string Name { get; set; } // e.g., "Documentation", "Testing"

        public int SortOrder { get; set; }

        [ForeignKey("TemplateGate")]
        public int TemplateGateId { get; set; }
        public virtual TemplateGate TemplateGate { get; set; }

        // A Block contains Items
        public virtual ICollection<TemplateProgressItem> Items { get; set; } = new List<TemplateProgressItem>();
    }
}
