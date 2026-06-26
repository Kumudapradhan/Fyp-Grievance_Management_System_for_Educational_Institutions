using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace GMS.Web.Models.Entities
{
    public enum GrievanceStatus
    {
        Open = 0,
        InProgress = 1,
        Resolved = 2
    }

    public enum GrievancePriority
    {
        Normal = 0,
        High = 1
    }

    public class Grievance
    {
        public int Id { get; set; }
        
        [Required]
        [StringLength(50)]
        public string TicketNumber { get; set; } = string.Empty;
        
        [Required]
        [StringLength(150)]
        public string Title { get; set; } = string.Empty;
        
        [Required]
        [MinLength(50)]
        public string Description { get; set; } = string.Empty;
        
        [Required]
        public int CategoryId { get; set; }
        public Category? Category { get; set; }
        
        [Required]
        public int DepartmentId { get; set; }
        public Department? Department { get; set; }
        
        public string? StudentId { get; set; }
        public ApplicationUser? Student { get; set; }
        
        public bool IsAnonymous { get; set; }
        
        [Required]
        public GrievanceStatus Status { get; set; } = GrievanceStatus.Open;
        
        [Required]
        public GrievancePriority Priority { get; set; } = GrievancePriority.Normal;
        
        [Required]
        public DateTime IncidentDate { get; set; }
        
        [Required]
        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
        
        [Required]
        public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
        
        [StringLength(2000)]
        public string? ResolutionNotes { get; set; }
        
        public bool IsOverdue { get; set; }
        public bool IsRepetitive { get; set; }

        public string? AssignedStaffUserId { get; set; }
        public ApplicationUser? AssignedStaffUser { get; set; }

        public ICollection<GrievanceAttachment> Attachments { get; set; } = new List<GrievanceAttachment>();
        public ICollection<GrievanceStatusHistory> StatusHistory { get; set; } = new List<GrievanceStatusHistory>();
    }
}
