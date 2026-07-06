using System;
using System.ComponentModel.DataAnnotations;

namespace GMS.Web.Models.Entities
{
    public class SystemSetting
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Key { get; set; } = string.Empty;

        [Required]
        [StringLength(1000)]
        public string Value { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        [Required]
        public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
