using System;
using System.ComponentModel.DataAnnotations;

namespace GMS.Web.Models.Entities
{
    public class Comment
    {
        public int Id { get; set; }

        [Required]
        public int GrievanceId { get; set; }
        public Grievance? Grievance { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;
        public ApplicationUser? User { get; set; }

        [Required]
        [StringLength(2000)]
        public string Content { get; set; } = string.Empty;

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
