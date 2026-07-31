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

            // Gather student statistics
            var totalGrievances = grievances.Count;
            var open = grievances.Count(g => g.Status == GrievanceStatus.Open);
            var inProgress = grievances.Count(g => g.Status == GrievanceStatus.InProgress);
            var resolved = grievances.Count(g => g.Status == GrievanceStatus.Resolved);
            var highPriority = grievances.Count(g => g.Priority == GrievancePriority.High && g.Status != GrievanceStatus.Resolved);

            // Personal complaint history: Group by month and count
            var historyData = grievances
                .GroupBy(g => new { Year = g.SubmittedAt.Year, Month = g.SubmittedAt.Month })
                .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                .Select(g => new { label = $"{g.Key.Year}-{g.Key.Month:D2}", count = g.Count() })
                .ToList();

            // Status distribution: Open, In Progress, Resolved
            var distributionData = new[]
            {
                new { label = "Open", count = open },
                new { label = "In Progress", count = inProgress },
                new { label = "Resolved", count = resolved }
            };

            var viewModel = new StudentDashboardViewModel
            {
                OwnGrievances = grievances,
                Notifications = notifications,
                StudentName = user?.FullName ?? "Student",
                StudentIdString = user?.StudentId ?? "",
                TotalGrievancesCount = totalGrievances,
                OpenCount = open,
                InProgressCount = inProgress,
                ResolvedCount = resolved,
                HighPriorityCount = highPriority,
                PersonalHistoryJson = System.Text.Json.JsonSerializer.Serialize(historyData),
                StatusDistributionJson = System.Text.Json.JsonSerializer.Serialize(distributionData)
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
                    Priority = model.Priority,
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
                .Include(g => g.AssignedStaffUser)
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

        // Notifications Page
        [HttpGet]
        public async Task<IActionResult> Notifications()
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId)) return Challenge();

            var notifications = await _context.Notifications
                .Include(n => n.Grievance)
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.SentAt)
                .ToListAsync();

            return View(notifications);
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

            return RedirectToAction(nameof(Notifications));
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
            return RedirectToAction(nameof(Notifications));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddComment(int grievanceId, string content)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId) || string.IsNullOrWhiteSpace(content))
            {
                return RedirectToAction(nameof(Detail), new { id = grievanceId });
            }

            var grievance = await _context.Grievances.FindAsync(grievanceId);
            if (grievance == null) return NotFound();

            // Authorization check
            if (!User.IsInRole("Administrator") && !User.IsInRole("Staff") && grievance.StudentId != userId)
            {
                return Forbid();
            }

            var comment = new Comment
            {
                GrievanceId = grievanceId,
                UserId = userId,
                Content = content.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            _context.Comments.Add(comment);
            
            // Save audit or notification if needed, but simple comment is fine
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Detail), new { id = grievanceId });
        }

        // My Grievances — dedicated standalone list page (student)
        [HttpGet]
        public async Task<IActionResult> MyGrievances(string? status = null, string? search = null)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId)) return Challenge();

            var grievances = await _grievanceService.GetGrievancesByStudentAsync(userId);

            // Apply status filter
            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<GrievanceStatus>(status, true, out var parsedStatus))
            {
                grievances = grievances.Where(g => g.Status == parsedStatus).ToList();
            }

            // Apply text search on title or ticket number
            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();
                grievances = grievances
                    .Where(g => g.Title.Contains(search, StringComparison.OrdinalIgnoreCase)
                             || g.TicketNumber.Contains(search, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            ViewBag.StatusFilter = status ?? "";
            ViewBag.SearchFilter = search ?? "";
            return View(grievances);
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
