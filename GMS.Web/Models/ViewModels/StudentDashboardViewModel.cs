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
    }
}
