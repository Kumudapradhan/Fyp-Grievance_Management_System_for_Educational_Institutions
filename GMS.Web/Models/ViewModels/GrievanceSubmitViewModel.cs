using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using GMS.Web.Models.Entities;


namespace GMS.Web.Models.ViewModels
{
    public class GrievanceSubmitViewModel
    {
        [Required(ErrorMessage = "Title is required.")]
        [StringLength(150, ErrorMessage = "Title cannot exceed 150 characters.")]
        [Display(Name = "Complaint Title")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please describe your grievance.")]
        [MinLength(50, ErrorMessage = "Description must be at least 50 characters long to provide sufficient detail.")]
        [DataType(DataType.MultilineText)]
        [Display(Name = "Grievance Description")]
        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "Category selection is required.")]
        [Display(Name = "Grievance Category")]
        public int CategoryId { get; set; }

        [Required(ErrorMessage = "Incident Date is required.")]
        [DataType(DataType.Date)]
        [Display(Name = "Date of Incident")]
        public DateTime IncidentDate { get; set; } = DateTime.Today;

        [Display(Name = "Submit Anonymously?")]
        public bool IsAnonymous { get; set; }

        [Display(Name = "Priority")]
        public GrievancePriority Priority { get; set; } = GrievancePriority.Low;


        [Display(Name = "Evidence Attachments (Optional, PDF/Images/Word, Max 5MB each)")]
        public List<IFormFile> EvidenceFiles { get; set; } = new List<IFormFile>();
    }
}
