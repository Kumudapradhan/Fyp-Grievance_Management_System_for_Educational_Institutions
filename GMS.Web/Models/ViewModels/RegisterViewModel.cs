using System.ComponentModel.DataAnnotations;

namespace GMS.Web.Models.ViewModels
{
    public class RegisterViewModel
    {
        [Required]
        [Display(Name = "Full Name")]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} characters long.", MinimumLength = 3)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Register As")]
        public string Role { get; set; } = "Student"; // "Student" or "Staff"

        // Student-specific fields
        [Display(Name = "Student ID")]
        public string? StudentId { get; set; }

        [Display(Name = "Academic Programme / Course")]
        public string? Programme { get; set; }

        // Staff-specific fields
        [Display(Name = "Assigned Department")]
        public string? Department { get; set; }
    }
}
