using GMS.Web.Models.Entities;
using System.Collections.Generic;

namespace GMS.Web.Models.ViewModels
{
    public class StaffDashboardViewModel
    {
        public int AssignedCount { get; set; }
        public int PendingCount { get; set; }
        public int InProgressCount { get; set; }
        public int ResolvedTodayCount { get; set; }
        public int OverdueCount { get; set; }

        public List<Grievance> DepartmentGrievances { get; set; } = new List<Grievance>();
        public List<GrievanceStatusHistory> RecentActivity { get; set; } = new List<GrievanceStatusHistory>();

        public string AssignedVsCompletedJson { get; set; } = "[]";
        public string MonthlyWorkloadJson { get; set; } = "[]";

        public string DepartmentName { get; set; } = string.Empty;
        public string DepartmentDescription { get; set; } = string.Empty;

        public GrievanceStatus? SelectedStatus { get; set; }
        public GrievancePriority? SelectedPriority { get; set; }
        public string? SearchQuery { get; set; }
        public bool MyAssignedOnly { get; set; }
    }
}
