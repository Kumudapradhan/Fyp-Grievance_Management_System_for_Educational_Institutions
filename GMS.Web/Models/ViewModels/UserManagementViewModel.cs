using System;
using System.Collections.Generic;

namespace GMS.Web.Models.ViewModels
{
    public class UserManagementViewModel
    {
        public List<UserListItemViewModel> Users { get; set; } = new();
        public string? SelectedRole { get; set; }
        public string? SearchQuery { get; set; }
    }

    public class UserListItemViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? StudentId { get; set; }
        public string? Department { get; set; }
        public string Role { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public int GrievanceCount { get; set; }
    }
}
