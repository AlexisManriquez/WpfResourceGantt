using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfResourceGantt.ProjectManagement.Models.Templates
{
    public class TemplateGate
    {
        [Key]
        public int Id { get; set; }

        public string Name { get; set; } // e.g., "PDR", "CDR"

        public int SortOrder { get; set; } // To keep them in order

        // --- TEMPLATE LOGIC FIELDS ---
        public int DurationDays { get; set; } // Default duration for this phase/gate

        // Indicates which previous gates (by SortOrder) this gate depends on.
        // e.g., if SortOrder is 1, and it depends on 0, Predecessors = "0". 
        // When applied, TemplateService will map this to the actual WorkBreakdownItem IDs.
        public string? Predecessors { get; set; }

        [ForeignKey("ProjectTemplate")]
        public int ProjectTemplateId { get; set; }
        public virtual ProjectTemplate ProjectTemplate { get; set; }

        // A Gate contains Progress Blocks (checklists — only used when gate has NO child tasks)
        public virtual ICollection<TemplateProgressBlock> Blocks { get; set; } = new List<TemplateProgressBlock>();

        // A Gate can contain child Tasks (schedule-driving leaf nodes).
        // When Tasks exist, the Gate becomes a Summary Node and tasks become individually-schedulable.
        // GAO Rule: "If another task depends on this item finishing, it must be a Task, not a checklist item."
        public virtual ICollection<TemplateTask> Tasks { get; set; } = new List<TemplateTask>();
    }
}
