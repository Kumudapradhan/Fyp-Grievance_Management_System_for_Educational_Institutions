using System.ComponentModel.DataAnnotations;

namespace GMS.Web.Models.ViewModels
{
    public class EditProfileViewModel
    {
        [Required]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        [EmailAddress]
        [Display(Name = "Email Address")]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "Student ID")]
        public string? StudentId { get; set; }

        [Display(Name = "Academic Programme")]
        public string? Programme { get; set; }

        [Display(Name = "Assigned Department")]
        public string? Department { get; set; }
    }
}
