using GMS.Web.Models.Entities;
using System;
using System.Collections.Generic;

namespace GMS.Web.Models.ViewModels
{
    public class AdminDashboardViewModel
    {
        // KPI Summary Counters
        public int TotalCount { get; set; }
        public int OpenCount { get; set; }
        public int InProgressCount { get; set; }
        public int ResolvedCount { get; set; }
        public int OverdueCount { get; set; }
        public int HighPriorityCount { get; set; }

        // The list of complaints
        public List<Grievance> Grievances { get; set; } = new List<Grievance>();

        // Lists for filter dropdowns
        public List<Department> Departments { get; set; } = new List<Department>();
        public List<Category> Categories { get; set; } = new List<Category>();

        // Selected filter state (to persist fields in view form)
        public GrievanceStatus? SelectedStatus { get; set; }
        public int? SelectedDepartmentId { get; set; }
        public int? SelectedCategoryId { get; set; }
        public GrievancePriority? SelectedPriority { get; set; }
        public string? SearchQuery { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        // Additional admin fields
        public int TotalUsersCount { get; set; }
        public int TotalDepartmentsCount { get; set; }
        
        // Chart JSON representation
        public string ComplaintsByDepartmentJson { get; set; } = "[]";
        public string ComplaintsByCategoryJson { get; set; } = "[]";
        public string MonthlyComplaintTrendJson { get; set; } = "[]";
        public string OpenVsClosedJson { get; set; } = "[]";
        public string AverageResolutionTimeJson { get; set; } = "[]";

        // Recent Audit logs
        public List<AuditLog> RecentActivity { get; set; } = new List<AuditLog>();
    }
}
