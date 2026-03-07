// File: WpfResourceGantt.ProjectManagement.Models.AdminTask.cs
using System;
using System.ComponentModel.DataAnnotations;

namespace WpfResourceGantt.ProjectManagement.Models
{
    public class AdminTask
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        public string? Name { get; set; }
        public string? Description { get; set; }

        // Links directly to a user, not via ResourceAssignment
        public string? AssignedUserId { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        // We use the same Enum as the Gantt View for simplicity
        public TaskStatus Status { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public string AssignedUserName { get; set; }
    }
}
