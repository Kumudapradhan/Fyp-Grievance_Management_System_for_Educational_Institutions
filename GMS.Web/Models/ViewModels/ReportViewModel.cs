using System.Collections.Generic;

namespace GMS.Web.Models.ViewModels
{
    public class ReportViewModel
    {
        public int TotalSubmitted { get; set; }
        public int TotalResolved { get; set; }
        public int TotalOverdue { get; set; }
        public int TotalHighPriority { get; set; }
        public List<ChartDataPoint> GrievancesByDepartment { get; set; } = new();
        public List<ChartDataPoint> GrievancesByStatus { get; set; } = new();
        public List<ChartDataPoint> GrievancesByCategory { get; set; } = new();
        public List<ChartDataPoint> MonthlyVolume { get; set; } = new();
        public List<ChartDataPoint> AvgResolutionByDept { get; set; } = new();
    }

    public class ChartDataPoint
    {
        public string Label { get; set; } = string.Empty;
        public double Value { get; set; }
    }
}
