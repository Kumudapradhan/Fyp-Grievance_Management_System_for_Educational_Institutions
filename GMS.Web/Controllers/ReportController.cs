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

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var model = new ReportViewModel
            {
                TotalSubmitted = await _context.Grievances.CountAsync(),
                TotalResolved = await _context.Grievances.CountAsync(g => g.Status == GrievanceStatus.Resolved),
                TotalOverdue = await _context.Grievances.CountAsync(g => g.IsOverdue && g.Status != GrievanceStatus.Resolved),
                TotalHighPriority = await _context.Grievances.CountAsync(g => g.Priority == GrievancePriority.High && g.Status != GrievanceStatus.Resolved)
            };

            // 1. Grievances by Department
            model.GrievancesByDepartment = await _context.Grievances
                .Include(g => g.Department)
                .GroupBy(g => g.Department.Name)
                .Select(g => new ChartDataPoint
                {
                    Label = g.Key ?? "Unassigned",
                    Value = g.Count()
                })
                .ToListAsync();

            // 2. Grievances by Status
            model.GrievancesByStatus = await _context.Grievances
                .GroupBy(g => g.Status)
                .Select(g => new ChartDataPoint
                {
                    Label = g.Key.ToString(),
                    Value = g.Count()
                })
                .ToListAsync();

            // 3. Grievances by Category
            model.GrievancesByCategory = await _context.Grievances
                .Include(g => g.Category)
                .GroupBy(g => g.Category.Name)
                .Select(g => new ChartDataPoint
                {
                    Label = g.Key ?? "Uncategorized",
                    Value = g.Count()
                })
                .ToListAsync();

            // 4. Monthly Volume (last 6 months)
            var sixMonthsAgo = DateTime.UtcNow.AddMonths(-5);
            var startOfSixMonthsAgo = new DateTime(sixMonthsAgo.Year, sixMonthsAgo.Month, 1);
            var monthlyGrievances = await _context.Grievances
                .Where(g => g.SubmittedAt >= startOfSixMonthsAgo)
                .ToListAsync();

            for (int i = 5; i >= 0; i--)
            {
                var date = DateTime.UtcNow.AddMonths(-i);
                var label = $"{System.Globalization.CultureInfo.InvariantCulture.DateTimeFormat.GetAbbreviatedMonthName(date.Month)} {date.Year}";
                var count = monthlyGrievances.Count(g => g.SubmittedAt.Year == date.Year && g.SubmittedAt.Month == date.Month);
                model.MonthlyVolume.Add(new ChartDataPoint
                {
                    Label = label,
                    Value = count
                });
            }

            // 5. Average Resolution Days by Department
            var resolvedGrievances = await _context.Grievances
                .Include(g => g.Department)
                .Where(g => g.Status == GrievanceStatus.Resolved)
                .ToListAsync();

            model.AvgResolutionByDept = resolvedGrievances
                .GroupBy(g => g.Department?.Name ?? "Unassigned")
                .Select(g => new ChartDataPoint
                {
                    Label = g.Key,
                    Value = g.Any() ? Math.Round(g.Average(x => (x.LastUpdatedAt - x.SubmittedAt).TotalDays), 2) : 0
                })
                .OrderBy(r => r.Label)
                .ToList();

            return View(model);
        }
    }
}
