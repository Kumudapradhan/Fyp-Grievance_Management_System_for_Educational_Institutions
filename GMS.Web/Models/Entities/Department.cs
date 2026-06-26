using System.ComponentModel.DataAnnotations;

namespace GMS.Web.Models.Entities
{
    public class Department
    {
        public int Id { get; set; }
        
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;
        
        [StringLength(500)]
        public string? Description { get; set; }
        
        public string? StaffUserId { get; set; }
        public ApplicationUser? StaffUser { get; set; }
    }
}
