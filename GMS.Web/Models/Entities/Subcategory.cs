using System;
using System.ComponentModel.DataAnnotations;

namespace GMS.Web.Models.Entities
{
    public class Subcategory
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        public int CategoryId { get; set; }
        public Category? Category { get; set; }
    }
}
