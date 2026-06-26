using System;
using System.ComponentModel.DataAnnotations;

namespace GMS.Web.Models.Entities
{
    public enum NotificationType
    {
        Submission = 0,
        StatusChange = 1,
        Overdue = 2,
        RepetitiveFlag = 3,
        Assignment = 4
    }

    public class Notification
    {
        public int Id { get; set; }
        
        [Required]
        public string UserId { get; set; } = string.Empty;
        public ApplicationUser? User { get; set; }
        
        [Required]
        public int GrievanceId { get; set; }
        public Grievance? Grievance { get; set; }
        
        [Required]
        [StringLength(1000)]
        public string Message { get; set; } = string.Empty;
        
        public bool IsRead { get; set; } = false;
        
        [Required]
        public DateTime SentAt { get; set; } = DateTime.UtcNow;
        
        [Required]
        public NotificationType NotificationType { get; set; }
    }
}
