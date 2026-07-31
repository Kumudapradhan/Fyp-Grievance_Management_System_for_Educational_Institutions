using GMS.Web.Models.Entities;
using System.Collections.Generic;

namespace GMS.Web.Models.ViewModels
{
    public class StudentDashboardViewModel
    {
        public List<Grievance> OwnGrievances { get; set; } = new List<Grievance>();
        public List<Notification> Notifications { get; set; } = new List<Notification>();
        public string StudentName { get; set; } = string.Empty;
        public string StudentIdString { get; set; } = string.Empty;

        // KPI summary
        public int TotalGrievancesCount { get; set; }
        public int OpenCount { get; set; }
        public int InProgressCount { get; set; }
        public int ResolvedCount { get; set; }
        public int HighPriorityCount { get; set; }

        // Chart JSON representation
        public string PersonalHistoryJson { get; set; } = "[]";
        public string StatusDistributionJson { get; set; } = "[]";
    }
}
