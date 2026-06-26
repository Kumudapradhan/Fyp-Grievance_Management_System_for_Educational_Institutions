using System.Collections.Generic;

namespace GMS.Web.Models.ViewModels
{
    public class DeptResolutionTime
    {
        public string DepartmentName { get; set; } = string.Empty;
        public double AverageResolutionTimeDays { get; set; }
        public int ResolvedCount { get; set; }
    }

    public class ReportViewModel
    {
        // Bar Chart: Grievances by Department
        public List<string> DepartmentLabels { get; set; } = new List<string>();
        public List<int> DepartmentCounts { get; set; } = new List<int>();

        // Line Chart: Volume over 6 months
        public List<string> MonthlyLabels { get; set; } = new List<string>();
        public List<int> MonthlyCounts { get; set; } = new List<int>();

        // Pie Chart: Grievances by Status
        public List<string> StatusLabels { get; set; } = new List<string>();
        public List<int> StatusCounts { get; set; } = new List<int>();

        // Table: Average resolution time per department
        public List<DeptResolutionTime> ResolutionTimes { get; set; } = new List<DeptResolutionTime>();
    }
}
