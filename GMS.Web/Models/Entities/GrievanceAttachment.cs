using System;
using System.ComponentModel.DataAnnotations;

namespace GMS.Web.Models.Entities
{
    public class GrievanceAttachment
    {
        public int Id { get; set; }
        
        [Required]
        public int GrievanceId { get; set; }
        public Grievance? Grievance { get; set; }
        
        [Required]
        [StringLength(255)]
        public string FileName { get; set; } = string.Empty;
        
        [Required]
        [StringLength(500)]
        public string FilePath { get; set; } = string.Empty;
        
        [Required]
        public long FileSize { get; set; }
        
        [Required]
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    }
}
