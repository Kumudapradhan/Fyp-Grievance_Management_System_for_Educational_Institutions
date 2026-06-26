using GMS.Web.Data;
using GMS.Web.Models.Entities;
using GMS.Web.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GMS.Web.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class ReportController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ReportController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Analytics report view (FR-18)
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var model = new ReportViewModel();

            // 1. Bar Chart: Grievances by Department (current month)
            var startOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
            var deptGrievances = await _context.Grievances
                .Include(g => g.Department)
                .Where(g => g.SubmittedAt >= startOfMonth)
                .GroupBy(g => g.Department.Name)
                .Select(g => new { DeptName = g.Key, Count = g.Count() })
                .ToListAsync();

            model.DepartmentLabels = deptGrievances.Select(d => d.DeptName).ToList();
            model.DepartmentCounts = deptGrievances.Select(d => d.Count).ToList();

            // 2. Line Chart: Volume over 6 months
            var sixMonthsAgo = DateTime.UtcNow.AddMonths(-5);
            var startOfSixMonthsAgo = new DateTime(sixMonthsAgo.Year, sixMonthsAgo.Month, 1);
            
            var monthlyGrievances = await _context.Grievances
                .Where(g => g.SubmittedAt >= startOfSixMonthsAgo)
                .ToListAsync();

            var monthlyGroups = monthlyGrievances
                .GroupBy(g => new { Year = g.SubmittedAt.Year, Month = g.SubmittedAt.Month })
                .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                .Select(g => new
                {
                    Label = $"{System.Globalization.CultureInfo.InvariantCulture.DateTimeFormat.GetAbbreviatedMonthName(g.Key.Month)} {g.Key.Year}",
                    Count = g.Count()
                }).ToList();

            model.MonthlyLabels = monthlyGroups.Select(m => m.Label).ToList();
            model.MonthlyCounts = monthlyGroups.Select(m => m.Count).ToList();

            // 3. Pie Chart: Status split
            var statusGroups = await _context.Grievances
                .GroupBy(g => g.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            model.StatusLabels = statusGroups.Select(s => s.Status.ToString()).ToList();
            model.StatusCounts = statusGroups.Select(s => s.Count).ToList();

            // 4. Table: Average resolution time per department
            var resolvedGrievances = await _context.Grievances
                .Include(g => g.Department)
                .Where(g => g.Status == GrievanceStatus.Resolved)
                .ToListAsync();

            var resolutionTimes = resolvedGrievances
                .GroupBy(g => g.Department?.Name ?? "Unassigned")
                .Select(g =>
                {
                    var totalDays = g.Sum(x => (x.LastUpdatedAt - x.SubmittedAt).TotalDays);
                    var avgDays = g.Any() ? (totalDays / g.Count()) : 0;
                    return new DeptResolutionTime
                    {
                        DepartmentName = g.Key,
                        AverageResolutionTimeDays = Math.Round(avgDays, 2),
                        ResolvedCount = g.Count()
                    };
                })
                .OrderBy(r => r.DepartmentName)
                .ToList();

            model.ResolutionTimes = resolutionTimes;

            return View(model);
        }
    }
}
