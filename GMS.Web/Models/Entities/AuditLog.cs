using System;
using System.ComponentModel.DataAnnotations;

namespace GMS.Web.Models.Entities
{
    public class AuditLog
    {
        public int Id { get; set; }
        
        public string? UserId { get; set; }
        public ApplicationUser? User { get; set; }
        
        [Required]
        [StringLength(100)]
        public string Action { get; set; } = string.Empty;
        
        [Required]
        [StringLength(100)]
        public string EntityType { get; set; } = string.Empty;
        
        [Required]
        [StringLength(100)]
        public string EntityId { get; set; } = string.Empty;
        
        [Required]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        
        [StringLength(2000)]
        public string Details { get; set; } = string.Empty;
    }
}
