using GMS.Web.Data;
using GMS.Web.Models.Entities;
using GMS.Web.Models.ViewModels;
using GMS.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace GMS.Web.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class AdminController : Controller
    {
        private readonly IGrievanceService _grievanceService;
        private readonly IRoutingService _routingService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public AdminController(
            IGrievanceService grievanceService,
            IRoutingService routingService,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context)
        {
            _grievanceService = grievanceService;
            _routingService = routingService;
            _userManager = userManager;
            _context = context;
        }

        // Admin dashboard grid (FR-17)
        [HttpGet]
        public async Task<IActionResult> Index(
            GrievanceStatus? status = null,
            int? departmentId = null,
            int? categoryId = null,
            GrievancePriority? priority = null,
            string? search = null,
            DateTime? startDate = null,
            DateTime? endDate = null)
        {
            // Gather statistics
            var totalCount = await _context.Grievances.CountAsync();
            var openCount = await _context.Grievances.CountAsync(g => g.Status == GrievanceStatus.Open);
            var progressCount = await _context.Grievances.CountAsync(g => g.Status == GrievanceStatus.InProgress);
            var resolvedCount = await _context.Grievances.CountAsync(g => g.Status == GrievanceStatus.Resolved);
            var overdueCount = await _context.Grievances.CountAsync(g => g.IsOverdue && g.Status != GrievanceStatus.Resolved);
            var highPriorityCount = await _context.Grievances.CountAsync(g => g.Priority == GrievancePriority.High && g.Status != GrievanceStatus.Resolved);

            // Fetch filtered grievances
            var grievances = await _grievanceService.GetAllGrievancesAsync(status, departmentId, categoryId, priority, search, startDate, endDate);

            var departments = await _context.Departments.ToListAsync();
            var categories = await _context.Categories.ToListAsync();

            var viewModel = new AdminDashboardViewModel
            {
                TotalCount = totalCount,
                OpenCount = openCount,
                InProgressCount = progressCount,
                ResolvedCount = resolvedCount,
                OverdueCount = overdueCount,
                HighPriorityCount = highPriorityCount,
                Grievances = grievances,
                Departments = departments,
                Categories = categories,
                SelectedStatus = status,
                SelectedDepartmentId = departmentId,
                SelectedCategoryId = categoryId,
                SelectedPriority = priority,
                SearchQuery = search,
                StartDate = startDate,
                EndDate = endDate
            };

            return View(viewModel);
        }

        // Admin detail panel (FR-10)
        [HttpGet]
        public async Task<IActionResult> Detail(int id)
        {
            var grievance = await _grievanceService.GetGrievanceByIdAsync(id);
            if (grievance == null) return NotFound();

            var departments = await _context.Departments.ToListAsync();

            var viewModel = new GrievanceDetailViewModel
            {
                Grievance = grievance,
                AllDepartments = departments,
                NewStatus = grievance.Status,
                NewDepartmentId = grievance.DepartmentId
            };

            return View(viewModel);
        }

        // Action: Change Status (FR-10, FR-11)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(GrievanceDetailViewModel model)
        {
            var adminId = _userManager.GetUserId(User) ?? "Admin";

            try
            {
                await _grievanceService.UpdateStatusAsync(model.Grievance.Id, model.NewStatus, adminId, model.StatusNotes);
                TempData["SuccessMessage"] = "Grievance status updated successfully.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error updating status: {ex.Message}";
            }

            return RedirectToAction(nameof(Detail), new { id = model.Grievance.Id });
        }

        // Action: Manual Routing (FR-07)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReRoute(GrievanceDetailViewModel model)
        {
            var adminId = _userManager.GetUserId(User) ?? "Admin";

            try
            {
                var routingNotes = string.IsNullOrWhiteSpace(model.ReRouteNotes) ? "Manual re-routing by administrator." : model.ReRouteNotes;
                await _routingService.ReRouteAsync(model.Grievance.Id, model.NewDepartmentId, adminId, routingNotes);
                TempData["SuccessMessage"] = "Grievance successfully re-routed.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error re-routing: {ex.Message}";
            }

            return RedirectToAction(nameof(Detail), new { id = model.Grievance.Id });
        }
    }
}
