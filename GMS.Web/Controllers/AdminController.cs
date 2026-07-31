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
        private readonly INotificationService _notificationService;

        public AdminController(
            IGrievanceService grievanceService,
            IRoutingService routingService,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context,
            INotificationService notificationService)
        {
            _grievanceService = grievanceService;
            _routingService = routingService;
            _userManager = userManager;
            _context = context;
            _notificationService = notificationService;
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

            var totalUsers = await _userManager.Users.CountAsync();
            var totalDepts = departments.Count;

            // Chart data 1: Complaints by Department
            var deptData = await _context.Grievances
                .GroupBy(g => g.Department.Name)
                .Select(g => new { label = g.Key, count = g.Count() })
                .ToListAsync();

            // Chart data 2: Complaints by Category
            var catData = await _context.Grievances
                .GroupBy(g => g.Category.Name)
                .Select(g => new { label = g.Key, count = g.Count() })
                .ToListAsync();

            // Chart data 3: Monthly Trend
            var trendData = await _context.Grievances
                .GroupBy(g => new { Year = g.SubmittedAt.Year, Month = g.SubmittedAt.Month })
                .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                .Select(g => new { label = $"{g.Key.Year}-{g.Key.Month:D2}", count = g.Count() })
                .ToListAsync();

            // Chart data 4: Open vs Closed
            var openVsClosed = new[]
            {
                new { label = "Active Open", count = openCount + progressCount },
                new { label = "Resolved", count = resolvedCount }
            };

            // Chart data 5: Average Resolution Time (in days) per Department (safe for in-memory DB tests)
            var resolvedGrievances = await _context.Grievances
                .Where(g => g.Status == GrievanceStatus.Resolved && g.ClosedAt != null)
                .Select(g => new { DeptName = g.Department.Name, g.SubmittedAt, g.ClosedAt })
                .ToListAsync();

            var resTimeByDept = resolvedGrievances
                .GroupBy(g => g.DeptName)
                .Select(g => new {
                    label = g.Key,
                    count = Math.Round(g.Average(x => (x.ClosedAt!.Value - x.SubmittedAt).TotalDays), 1)
                })
                .ToList();

            // Recent activity audit logs
            var recentActivity = await _context.AuditLogs
                .Include(a => a.User)
                .OrderByDescending(a => a.Timestamp)
                .Take(5)
                .ToListAsync();

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
                EndDate = endDate,
                TotalUsersCount = totalUsers,
                TotalDepartmentsCount = totalDepts,
                ComplaintsByDepartmentJson = System.Text.Json.JsonSerializer.Serialize(deptData),
                ComplaintsByCategoryJson = System.Text.Json.JsonSerializer.Serialize(catData),
                MonthlyComplaintTrendJson = System.Text.Json.JsonSerializer.Serialize(trendData),
                OpenVsClosedJson = System.Text.Json.JsonSerializer.Serialize(openVsClosed),
                AverageResolutionTimeJson = System.Text.Json.JsonSerializer.Serialize(resTimeByDept),
                RecentActivity = recentActivity
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
            var staffUsers = await _userManager.GetUsersInRoleAsync("Staff");

            var viewModel = new GrievanceDetailViewModel
            {
                Grievance = grievance,
                AllDepartments = departments,
                AllStaffUsers = staffUsers.ToList(),
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignStaff(int grievanceId, string staffUserId, string? assignmentNote)
        {
            var adminId = _userManager.GetUserId(User) ?? "Admin";

            var grievance = await _context.Grievances
                .Include(g => g.Category)
                .Include(g => g.Department)
                .FirstOrDefaultAsync(g => g.Id == grievanceId);

            if (grievance == null) return NotFound();

            var staffUser = await _userManager.FindByIdAsync(staffUserId);
            if (staffUser == null || !await _userManager.IsInRoleAsync(staffUser, "Staff"))
            {
                TempData["ErrorMessage"] = "Selected user is not a valid staff member.";
                return RedirectToAction(nameof(Detail), new { id = grievanceId });
            }

            var staffDepartment = await _context.Departments
                .FirstOrDefaultAsync(d => d.StaffUserId == staffUserId || d.Name == staffUser.Department);

            if (staffDepartment == null || staffDepartment.Id != grievance.DepartmentId)
            {
                TempData["ErrorMessage"] = "Selected staff member is not assigned to this grievance's department.";
                return RedirectToAction(nameof(Detail), new { id = grievanceId });
            }

            grievance.AssignedStaffUserId = staffUserId;
            grievance.LastUpdatedAt = DateTime.UtcNow;

            var noteStr = string.IsNullOrWhiteSpace(assignmentNote) ? "" : $" Note: {assignmentNote}";
            var historyNotes = $"Assigned to staff member {staffUser.FullName} by administrator.{noteStr}";

            // Log status history
            var history = new GrievanceStatusHistory
            {
                GrievanceId = grievanceId,
                OldStatus = grievance.Status,
                NewStatus = grievance.Status,
                ChangedByUserId = adminId,
                ChangedAt = DateTime.UtcNow,
                Notes = historyNotes
            };
            _context.GrievanceStatusHistories.Add(history);

            // Log audit log
            var audit = new AuditLog
            {
                UserId = adminId,
                Action = "AssignStaff",
                EntityType = "Grievance",
                EntityId = grievanceId.ToString(),
                Timestamp = DateTime.UtcNow,
                Details = $"Grievance {grievance.TicketNumber} assigned to staff member {staffUser.FullName} ({staffUser.Email})."
            };
            _context.AuditLogs.Add(audit);

            await _context.SaveChangesAsync();

            // Anonymity-safe notification
            string message;
            if (grievance.IsAnonymous)
            {
                message = $"You have been assigned grievance {grievance.TicketNumber} (Anonymous Filing) — {grievance.Department?.Name} — Category: {grievance.Category?.Name}.{(string.IsNullOrWhiteSpace(assignmentNote) ? "" : $" Note from admin: {assignmentNote}")}";
            }
            else
            {
                message = $"You have been assigned grievance {grievance.TicketNumber} — {grievance.Department?.Name} — Category: {grievance.Category?.Name}.{(string.IsNullOrWhiteSpace(assignmentNote) ? "" : $" Note from admin: {assignmentNote}")}";
            }

            await _notificationService.SendNotificationAsync(staffUserId, grievanceId, message, NotificationType.Assignment);

            TempData["SuccessMessage"] = "Staff member assigned successfully.";
            return RedirectToAction(nameof(Detail), new { id = grievanceId });
        }

        [HttpGet]
        public async Task<IActionResult> Users(string? role = null, string? search = null)
        {
            var currentUserId = _userManager.GetUserId(User);
            var query = _userManager.Users.Where(u => u.Id != currentUserId);

            if (!string.IsNullOrEmpty(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(u => u.FullName.ToLower().Contains(s) || u.Email.ToLower().Contains(s) || (u.StudentId != null && u.StudentId.ToLower().Contains(s)));
            }

            var allUsers = await query.OrderByDescending(u => u.CreatedAt).ToListAsync();
            var listItems = new List<UserListItemViewModel>();

            foreach (var user in allUsers)
            {
                var roles = await _userManager.GetRolesAsync(user);
                var userRole = roles.FirstOrDefault() ?? "No Role";

                if (!string.IsNullOrEmpty(role) && !userRole.Equals(role, StringComparison.OrdinalIgnoreCase))
                {
                    continue; // filter by role
                }

                var grievanceCount = 0;
                if (userRole == "Student")
                {
                    grievanceCount = await _context.Grievances.CountAsync(g => g.StudentId == user.Id);
                }

                listItems.Add(new UserListItemViewModel
                {
                    Id = user.Id,
                    FullName = user.FullName,
                    Email = user.Email ?? string.Empty,
                    StudentId = user.StudentId,
                    Department = user.Department,
                    Role = userRole,
                    IsActive = user.IsActive,
                    CreatedAt = user.CreatedAt,
                    GrievanceCount = grievanceCount
                });
            }

            var viewModel = new UserManagementViewModel
            {
                Users = listItems,
                SelectedRole = role,
                SearchQuery = search
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleUserStatus(string userId)
        {
            var currentAdminId = _userManager.GetUserId(User) ?? "Admin";
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            user.IsActive = !user.IsActive;
            var result = await _userManager.UpdateAsync(user);

            if (result.Succeeded)
            {
                var audit = new AuditLog
                {
                    UserId = currentAdminId,
                    Action = "ToggleUserStatus",
                    EntityType = "ApplicationUser",
                    EntityId = userId,
                    Timestamp = DateTime.UtcNow,
                    Details = $"Toggled status of user {user.FullName} ({user.Email}) to {(user.IsActive ? "Active" : "Inactive")}."
                };
                _context.AuditLogs.Add(audit);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"User status updated successfully. Account is now {(user.IsActive ? "Active" : "Inactive")}.";
            }
            else
            {
                TempData["ErrorMessage"] = "Failed to update user status.";
            }

            return RedirectToAction(nameof(Users));
        }

        [HttpGet]
        public async Task<IActionResult> Departments()
        {
            var departments = await _context.Departments
                .Include(d => d.StaffUser)
                .ToListAsync();

            var staffUsers = await _userManager.GetUsersInRoleAsync("Staff");
            ViewBag.StaffUsers = staffUsers.ToList();

            return View(departments);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignDepartmentStaff(int departmentId, string? staffUserId)
        {
            var currentAdminId = _userManager.GetUserId(User) ?? "Admin";
            var department = await _context.Departments.FindAsync(departmentId);
            if (department == null) return NotFound();

            department.StaffUserId = string.IsNullOrEmpty(staffUserId) ? null : staffUserId;
            await _context.SaveChangesAsync();

            string detailsStr;
            if (string.IsNullOrEmpty(staffUserId))
            {
                detailsStr = $"Unassigned staff from department {department.Name}.";
            }
            else
            {
                var staffUser = await _userManager.FindByIdAsync(staffUserId);
                detailsStr = $"Assigned staff user {staffUser?.FullName ?? staffUserId} to department {department.Name}.";
            }

            var audit = new AuditLog
            {
                UserId = currentAdminId,
                Action = "AssignDepartmentStaff",
                EntityType = "Department",
                EntityId = departmentId.ToString(),
                Timestamp = DateTime.UtcNow,
                Details = detailsStr
            };
            _context.AuditLogs.Add(audit);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Department staff assignment updated successfully.";
            return RedirectToAction(nameof(Departments));
        }

        [HttpGet]
        public async Task<IActionResult> AuditLog(string? search = null, DateTime? from = null, DateTime? to = null, int page = 1)
        {
            var query = _context.AuditLogs
                .Include(a => a.User)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(a => 
                    a.Action.ToLower().Contains(s) || 
                    a.EntityType.ToLower().Contains(s) || 
                    a.Details.ToLower().Contains(s) || 
                    (a.User != null && a.User.FullName.ToLower().Contains(s))
                );
            }

            if (from.HasValue)
            {
                query = query.Where(a => a.Timestamp >= from.Value);
            }

            if (to.HasValue)
            {
                var toDate = to.Value.Date.AddDays(1).AddSeconds(-1);
                query = query.Where(a => a.Timestamp <= toDate);
            }

            var totalLogsCount = await query.CountAsync();
            var pageSize = 25;
            var logs = await query
                .OrderByDescending(a => a.Timestamp)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalLogsCount / pageSize);
            ViewBag.SearchQuery = search;
            ViewBag.FromDate = from?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = to?.ToString("yyyy-MM-dd");

            return View(logs);
        }
    }
}
