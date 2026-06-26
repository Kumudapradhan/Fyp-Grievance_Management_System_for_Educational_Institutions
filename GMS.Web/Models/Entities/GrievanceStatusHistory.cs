using System;
using System.ComponentModel.DataAnnotations;

namespace GMS.Web.Models.Entities
{
    public class GrievanceStatusHistory
    {
        public int Id { get; set; }
        
        [Required]
        public int GrievanceId { get; set; }
        public Grievance? Grievance { get; set; }
        
        [Required]
        public GrievanceStatus OldStatus { get; set; }
        
        [Required]
        public GrievanceStatus NewStatus { get; set; }
        
        [Required]
        public string ChangedByUserId { get; set; } = string.Empty;
        public ApplicationUser? ChangedByUser { get; set; }
        
        [Required]
        public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
        
        [StringLength(1000)]
        public string? Notes { get; set; }
    }
}
