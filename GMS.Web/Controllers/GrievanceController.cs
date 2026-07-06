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
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace GMS.Web.Controllers
{
    [Authorize]
    public class GrievanceController : Controller
    {
        private readonly IGrievanceService _grievanceService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (int count, DateTime resetTime)> _rateLimits = new();

        public GrievanceController(
            IGrievanceService grievanceService,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context)
        {
            _grievanceService = grievanceService;
            _userManager = userManager;
            _context = context;
        }

        // Student Dashboard (FR-09)
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId)) return Challenge();

            var user = await _userManager.GetUserAsync(User);
            var grievances = await _grievanceService.GetGrievancesByStudentAsync(userId);
            
            // Get unread notifications
            var notifications = await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .OrderByDescending(n => n.SentAt)
                .ToListAsync();

            var viewModel = new StudentDashboardViewModel
            {
                OwnGrievances = grievances,
                Notifications = notifications,
                StudentName = user?.FullName ?? "Student",
                StudentIdString = user?.StudentId ?? ""
            };

            return View(viewModel);
        }

        // Submit Grievance (FR-03, FR-04, FR-05)
        [HttpGet]
        public async Task<IActionResult> Submit()
        {
            var categories = await _context.Categories.ToListAsync();
            ViewBag.Categories = new SelectList(categories, "Id", "Name");
            return View(new GrievanceSubmitViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(GrievanceSubmitViewModel model)
        {
            if (ModelState.IsValid)
            {
                var userId = _userManager.GetUserId(User);
                var user = await _userManager.GetUserAsync(User);

                var grievance = new Grievance
                {
                    Title = model.Title,
                    Description = model.Description,
                    CategoryId = model.CategoryId,
                    IncidentDate = model.IncidentDate,
                    IsAnonymous = model.IsAnonymous,
                    StudentId = model.IsAnonymous ? null : userId
                };

                try
                {
                    // Use student's email as session email for receipts (especially if anonymous)
                    var sessionEmail = user?.Email;

                    var result = await _grievanceService.SubmitGrievanceAsync(grievance, model.EvidenceFiles, sessionEmail);
                    return RedirectToAction("Confirm", new { ticketNumber = result.TicketNumber });
                }
                catch (ArgumentException ex)
                {
                    ModelState.AddModelError(string.Empty, ex.Message);
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError(string.Empty, $"An unexpected error occurred during submission: {ex.Message}");
                }
            }

            var categories = await _context.Categories.ToListAsync();
            ViewBag.Categories = new SelectList(categories, "Id", "Name");
            return View(model);
        }

        [HttpGet]
        public IActionResult Confirm(string ticketNumber)
        {
            ViewBag.TicketNumber = ticketNumber;
            return View();
        }

        // Student Detail View (FR-09, FR-10)
        [HttpGet]
        public async Task<IActionResult> Detail(int id)
        {
            var userId = _userManager.GetUserId(User);
            var grievance = await _grievanceService.GetGrievanceByIdAsync(id);

            if (grievance == null)
            {
                return NotFound();
            }

            // Verify Ownership: Student can only view their own grievance unless they are Admin/Staff
            if (!User.IsInRole("Administrator") && !User.IsInRole("Staff"))
            {
                if (grievance.StudentId != userId || grievance.IsAnonymous)
                {
                    // Anonymous grievances can't be bound to user dashboard, must track via direct code lookup
                    return Forbid();
                }
            }

            return View(grievance);
        }

        // Direct search tracking for anonymous or direct ticket tracking (FR-08)
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Track(string? ticketNumber)
        {
            if (string.IsNullOrWhiteSpace(ticketNumber))
            {
                return View();
            }

            // Implement IP-based rate limiting to prevent brute force sequential enumerations
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var now = DateTime.UtcNow;

            if (_rateLimits.TryGetValue(ipAddress, out var limit))
            {
                if (now < limit.resetTime)
                {
                    if (limit.count >= 5) // max 5 searches per minute
                    {
                        ViewBag.Error = "Too many tracking attempts. Please wait a minute and try again.";
                        return View();
                    }
                    _rateLimits[ipAddress] = (limit.count + 1, limit.resetTime);
                }
                else
                {
                    _rateLimits[ipAddress] = (1, now.AddMinutes(1));
                }
            }
            else
            {
                _rateLimits[ipAddress] = (1, now.AddMinutes(1));
            }

            var grievance = await _context.Grievances
                .Include(g => g.Category)
                .Include(g => g.Department)
                .Include(g => g.StatusHistory)
                .FirstOrDefaultAsync(g => g.TicketNumber == ticketNumber.Trim());

            if (grievance == null)
            {
                ViewBag.Error = "Ticket reference number not found.";
                return View();
            }

            // Scrub student details if ticket is anonymous to safeguard privacy
            if (grievance.IsAnonymous)
            {
                grievance.StudentId = null;
                grievance.Student = null;
            }

            return View("TrackResult", grievance);
        }

        // Download attachment (NFR-01 validation)
        [HttpGet]
        public async Task<IActionResult> DownloadAttachment(int id)
        {
            var attachment = await _context.GrievanceAttachments
                .Include(a => a.Grievance)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (attachment == null) return NotFound();

            var userId = _userManager.GetUserId(User);

            // Authorization check
            if (!User.IsInRole("Administrator") && !User.IsInRole("Staff"))
            {
                if (attachment.Grievance?.StudentId != userId || attachment.Grievance.IsAnonymous)
                {
                    return Forbid();
                }
            }

            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", attachment.FilePath.TrimStart('/'));
            if (!System.IO.File.Exists(filePath))
            {
                return NotFound("Physical file not found on server.");
            }

            var contentType = "application/octet-stream";
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            if (ext == ".pdf") contentType = "application/pdf";
            else if (ext == ".png") contentType = "image/png";
            else if (ext == ".jpg" || ext == ".jpeg") contentType = "image/jpeg";
            else if (ext == ".docx") contentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

            return PhysicalFile(filePath, contentType, attachment.FileName);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkNotificationRead(int id)
        {
            var userId = _userManager.GetUserId(User);
            var notification = await _context.Notifications.FindAsync(id);
            
            if (notification != null && notification.UserId == userId)
            {
                notification.IsRead = true;
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllNotificationsRead()
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId)) return Challenge();

            var notifications = await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ToListAsync();

            foreach (var note in notifications)
            {
                note.IsRead = true;
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "All notifications marked as read.";
            return RedirectToAction(nameof(Index));
        }

        // AJAX endpoint for dynamically displaying department name
        [HttpGet]
        [AllowAnonymous]
        public async Task<JsonResult> GetDepartmentForCategory(int categoryId)
        {
            var category = await _context.Categories
                .Include(c => c.DefaultDepartment)
                .FirstOrDefaultAsync(c => c.Id == categoryId);

            if (category != null && category.DefaultDepartment != null)
            {
                return Json(new { success = true, departmentName = category.DefaultDepartment.Name });
            }
            return Json(new { success = false, departmentName = "General Administration" });
        }
    }
}
