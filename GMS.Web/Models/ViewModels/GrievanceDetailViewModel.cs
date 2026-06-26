using GMS.Web.Models.Entities;
using System.Collections.Generic;

namespace GMS.Web.Models.ViewModels
{
    public class GrievanceDetailViewModel
    {
        public Grievance Grievance { get; set; } = null!;
        public List<Department> AllDepartments { get; set; } = new List<Department>();
        
        // Form model for updating ticket status
        public GrievanceStatus NewStatus { get; set; }
        public string? StatusNotes { get; set; }

        // Form model for re-routing the department
        public int NewDepartmentId { get; set; }
        public string? ReRouteNotes { get; set; }
    }
}
