using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfResourceGantt.ProjectManagement.Models.Templates
{
    public class TemplateProgressItem
    {
        [Key]
        public int Id { get; set; }

        public string Description { get; set; } // The checklist text

        public int SortOrder { get; set; }

        [ForeignKey("TemplateProgressBlock")]
        public int TemplateProgressBlockId { get; set; }
        public virtual TemplateProgressBlock TemplateProgressBlock { get; set; }
    }
}
