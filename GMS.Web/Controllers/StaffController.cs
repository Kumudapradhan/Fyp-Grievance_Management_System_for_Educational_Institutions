using GMS.Web.Data;
using GMS.Web.Models.Entities;
using GMS.Web.Models.ViewModels;
using GMS.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace GMS.Web.Controllers
{
    [Authorize(Roles = "Staff")]
    public class StaffController : Controller
    {
        private readonly IGrievanceService _grievanceService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notificationService;

        public StaffController(
            IGrievanceService grievanceService,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context,
            INotificationService notificationService)
        {
            _grievanceService = grievanceService;
            _userManager = userManager;
            _context = context;
            _notificationService = notificationService;
        }

        // Staff department queue dashboard
        [HttpGet]
        public async Task<IActionResult> Index(
            GrievanceStatus? status = null, 
            GrievancePriority? priority = null, 
            string? search = null,
            bool myAssigned = false)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            // Populate layout unread notification badge
            ViewBag.StaffUnreadCount = await _context.Notifications
                .CountAsync(n => n.UserId == user.Id && !n.IsRead);

            // Find department where this user is the staff officer, or fall back to their registered department field
            var department = await _context.Departments
                .FirstOrDefaultAsync(d => d.StaffUserId == user.Id || d.Name == user.Department);

            if (department == null)
            {
                ViewBag.ErrorMessage = "No department has been formally assigned to your account. Please contact the administrator.";
                return View("NoDepartment");
            }

            // Gather statistics specific to this department
            var assignedCount = await _context.Grievances.CountAsync(g => g.DepartmentId == department.Id && g.AssignedStaffUserId == user.Id);
            var pendingCount = await _context.Grievances.CountAsync(g => g.DepartmentId == department.Id && g.Status == GrievanceStatus.Open && g.AssignedStaffUserId == null);
            var progressCount = await _context.Grievances.CountAsync(g => g.DepartmentId == department.Id && g.Status == GrievanceStatus.InProgress);
            
            var today = DateTime.Today;
            var resolvedTodayCount = await _context.Grievances.CountAsync(g => g.DepartmentId == department.Id && g.Status == GrievanceStatus.Resolved && g.ClosedAt >= today);
            var overdueCount = await _context.Grievances.CountAsync(g => g.DepartmentId == department.Id && g.IsOverdue && g.Status != GrievanceStatus.Resolved);

            // Fetch filtered grievances within staff member's department
            var departmentGrievances = await _grievanceService.GetAllGrievancesAsync(
                status: status,
                departmentId: department.Id,
                categoryId: null,
                priority: priority,
                search: search
            );

            if (myAssigned)
            {
                departmentGrievances = departmentGrievances.Where(g => g.AssignedStaffUserId == user.Id).ToList();
            }

            // Fetch recent activities in this department
            var recentActivity = await _context.GrievanceStatusHistories
                .Include(h => h.Grievance)
                .Include(h => h.ChangedByUser)
                .Where(h => h.Grievance.DepartmentId == department.Id)
                .OrderByDescending(h => h.ChangedAt)
                .Take(5)
                .ToListAsync();

            // Chart 1: Assigned vs Completed for this officer
            var myOpen = await _context.Grievances.CountAsync(g => g.AssignedStaffUserId == user.Id && g.Status != GrievanceStatus.Resolved);
            var myResolved = await _context.Grievances.CountAsync(g => g.AssignedStaffUserId == user.Id && g.Status == GrievanceStatus.Resolved);
            var assignedVsCompleted = new[]
            {
                new { label = "Active Assigned", count = myOpen },
                new { label = "Resolved", count = myResolved }
            };

            // Chart 2: Monthly workload (tickets resolved by this officer per month)
            var resolvedByMonth = await _context.Grievances
                .Where(g => g.AssignedStaffUserId == user.Id && g.Status == GrievanceStatus.Resolved && g.ClosedAt != null)
                .GroupBy(g => new { Year = g.ClosedAt!.Value.Year, Month = g.ClosedAt!.Value.Month })
                .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                .Select(g => new { label = $"{g.Key.Year}-{g.Key.Month:D2}", count = g.Count() })
                .ToListAsync();

            var viewModel = new StaffDashboardViewModel
            {
                AssignedCount = assignedCount,
                PendingCount = pendingCount,
                InProgressCount = progressCount,
                ResolvedTodayCount = resolvedTodayCount,
                OverdueCount = overdueCount,
                DepartmentGrievances = departmentGrievances,
                RecentActivity = recentActivity,
                AssignedVsCompletedJson = System.Text.Json.JsonSerializer.Serialize(assignedVsCompleted),
                MonthlyWorkloadJson = System.Text.Json.JsonSerializer.Serialize(resolvedByMonth),
                DepartmentName = department.Name,
                DepartmentDescription = department.Description,
                SelectedStatus = status,
                SelectedPriority = priority,
                SearchQuery = search,
                MyAssignedOnly = myAssigned
            };

            return View(viewModel);
        }

        // Staff ticket details view (allows status update, no re-routing)
        [HttpGet]
        public async Task<IActionResult> Detail(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var grievance = await _grievanceService.GetGrievanceByIdAsync(id);
            if (grievance == null) return NotFound();

            var department = await _context.Departments
                .FirstOrDefaultAsync(d => d.StaffUserId == user.Id || d.Name == user.Department);

            // Access Control: Staff can only view tickets assigned to them
            if (department == null || grievance.DepartmentId != department.Id || (grievance.AssignedStaffUserId != null && grievance.AssignedStaffUserId != user.Id))
            {
                return Forbid();
            }

            ViewBag.DepartmentName = department.Name;
            ViewBag.StaffUnreadCount = await _context.Notifications
                .CountAsync(n => n.UserId == user.Id && !n.IsRead);

            var viewModel = new GrievanceDetailViewModel
            {
                Grievance = grievance,
                NewStatus = grievance.Status
            };

            return View(viewModel);
        }

        // Action: Update Status (Staff resolution action)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(GrievanceDetailViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var grievance = await _context.Grievances.FindAsync(model.Grievance.Id);
            if (grievance == null) return NotFound();

            var department = await _context.Departments
                .FirstOrDefaultAsync(d => d.StaffUserId == user.Id || d.Name == user.Department);

            // Access Control: Staff can only update tickets assigned to them
            if (department == null || grievance.DepartmentId != department.Id || (grievance.AssignedStaffUserId != null && grievance.AssignedStaffUserId != user.Id))
            {
                return Forbid();
            }

            try
            {
                await _grievanceService.UpdateStatusAsync(model.Grievance.Id, model.NewStatus, user.Id, model.StatusNotes);
                TempData["SuccessMessage"] = "Grievance status updated successfully.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error updating status: {ex.Message}";
            }

            return RedirectToAction(nameof(Detail), new { id = model.Grievance.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkResolved(int grievanceId, string resolutionNotes)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            if (string.IsNullOrWhiteSpace(resolutionNotes))
            {
                TempData["ErrorMessage"] = "Resolution notes are required to resolve the grievance.";
                return RedirectToAction(nameof(Detail), new { id = grievanceId });
            }

            var grievance = await _context.Grievances
                .Include(g => g.Student)
                .Include(g => g.Department)
                .Include(g => g.Category)
                .FirstOrDefaultAsync(g => g.Id == grievanceId);

            if (grievance == null) return NotFound();

            var department = await _context.Departments
                .FirstOrDefaultAsync(d => d.StaffUserId == user.Id || d.Name == user.Department);

            // Access Control: Staff can only resolve tickets assigned to them
            if (department == null || grievance.DepartmentId != department.Id || (grievance.AssignedStaffUserId != null && grievance.AssignedStaffUserId != user.Id))
            {
                return Forbid();
            }

            var oldStatus = grievance.Status;
            grievance.Status = GrievanceStatus.Resolved;
            grievance.ResolutionNotes = resolutionNotes;
            grievance.LastUpdatedAt = DateTime.UtcNow;
            grievance.ClosedAt = DateTime.UtcNow;

            // Log status history
            var history = new GrievanceStatusHistory
            {
                GrievanceId = grievanceId,
                OldStatus = oldStatus,
                NewStatus = GrievanceStatus.Resolved,
                ChangedByUserId = user.Id,
                ChangedAt = DateTime.UtcNow,
                Notes = resolutionNotes
            };
            _context.GrievanceStatusHistories.Add(history);

            // Add Audit log
            var audit = new AuditLog
            {
                UserId = user.Id,
                Action = "MarkResolved",
                EntityType = "Grievance",
                EntityId = grievanceId.ToString(),
                Timestamp = DateTime.UtcNow,
                Details = $"Grievance resolved with notes. Status updated from {oldStatus} to Resolved."
            };
            _context.AuditLogs.Add(audit);

            // Notify student in-app (if not anonymous)
            if (!grievance.IsAnonymous && grievance.StudentId != null)
            {
                var messageText = $"Your grievance {grievance.TicketNumber} has been resolved by the {grievance.Department?.Name} department. Resolution: {resolutionNotes}";
                var notification = new Notification
                {
                    UserId = grievance.StudentId,
                    GrievanceId = grievance.Id,
                    Message = messageText,
                    IsRead = false,
                    SentAt = DateTime.UtcNow,
                    NotificationType = NotificationType.StatusChange
                };
                _context.Notifications.Add(notification);
            }

            await _context.SaveChangesAsync();

            // Notify student via email (if not anonymous)
            if (!grievance.IsAnonymous)
            {
                var emailAddress = grievance.Student?.Email;
                if (!string.IsNullOrEmpty(emailAddress))
                {
                    var body = $@"Hello,

Your grievance {grievance.TicketNumber} has been resolved by the {grievance.Department?.Name} department.

Resolution: {resolutionNotes}

Regards,
GMS Admin Portal";

                    await _notificationService.SendStudentEmailAsync(emailAddress, grievance.TicketNumber, "Ticket Resolved", body);
                }
            }

            TempData["SuccessMessage"] = "Grievance resolved successfully.";
            return RedirectToAction(nameof(Detail), new { id = grievanceId });
        }

        [HttpGet]
        public async Task<IActionResult> Notifications()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var department = await _context.Departments
                .FirstOrDefaultAsync(d => d.StaffUserId == user.Id || d.Name == user.Department);
            if (department != null)
            {
                ViewBag.DepartmentName = department.Name;
            }

            var notifications = await _context.Notifications
                .Include(n => n.Grievance)
                .Where(n => n.UserId == user.Id)
                .OrderByDescending(n => n.SentAt)
                .ToListAsync();

            var unreadCount = notifications.Count(n => !n.IsRead);
            ViewBag.StaffUnreadCount = unreadCount;

            // Mark all as read
            var unreadNotifications = notifications.Where(n => !n.IsRead).ToList();
            if (unreadNotifications.Any())
            {
                foreach (var note in unreadNotifications)
                {
                    note.IsRead = true;
                }
                await _context.SaveChangesAsync();
            }

            var viewModel = new StaffNotificationsViewModel
            {
                Notifications = notifications,
                UnreadCount = unreadCount
            };

            return View(viewModel);
        }
    }
}
