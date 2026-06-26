using System.ComponentModel.DataAnnotations;

namespace GMS.Web.Models.Entities
{
    public class Category
    {
        public int Id { get; set; }
        
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;
        
        [Required]
        public int DefaultDepartmentId { get; set; }
        public Department? DefaultDepartment { get; set; }
    }
}
