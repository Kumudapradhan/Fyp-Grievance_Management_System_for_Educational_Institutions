using GMS.Web.Data;
using GMS.Web.Models.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GMS.Web.Services
{
    public interface IGrievanceService
    {
        Task<Grievance> SubmitGrievanceAsync(Grievance grievance, List<IFormFile> files, string? sessionEmail);
        Task UpdateStatusAsync(int grievanceId, GrievanceStatus newStatus, string changedByUserId, string? notes);
        Task<Grievance?> GetGrievanceByIdAsync(int id);
        Task<List<Grievance>> GetGrievancesByStudentAsync(string studentId);
        Task<List<Grievance>> GetAllGrievancesAsync(
            GrievanceStatus? status = null, 
            int? departmentId = null, 
            int? categoryId = null, 
            GrievancePriority? priority = null, 
            string? search = null,
            DateTime? startDate = null,
            DateTime? endDate = null);
    }

    public class GrievanceService : IGrievanceService
    {
        private readonly ApplicationDbContext _context;
        private readonly ITicketService _ticketService;
        private readonly IFileUploadService _fileUploadService;
        private readonly INotificationService _notificationService;
        private readonly IRepetitiveDetectionService _repetitiveDetectionService;

        public GrievanceService(
            ApplicationDbContext context, 
            ITicketService ticketService, 
            IFileUploadService fileUploadService, 
            INotificationService notificationService,
            IRepetitiveDetectionService repetitiveDetectionService)
        {
            _context = context;
            _ticketService = ticketService;
            _fileUploadService = fileUploadService;
            _notificationService = notificationService;
            _repetitiveDetectionService = repetitiveDetectionService;
        }

        public async Task<Grievance> SubmitGrievanceAsync(Grievance grievance, List<IFormFile> files, string? sessionEmail)
        {
            // 1. Generate unique ticket number
            grievance.TicketNumber = await _ticketService.GenerateTicketNumberAsync();
            grievance.SubmittedAt = DateTime.UtcNow;
            grievance.LastUpdatedAt = DateTime.UtcNow;
            grievance.Status = GrievanceStatus.Open;
            grievance.Priority = GrievancePriority.Normal;
            grievance.IsOverdue = false;
            grievance.IsRepetitive = false;

            // 2. Fetch Category and Default Department for auto-routing
            var category = await _context.Categories.FindAsync(grievance.CategoryId);
            if (category == null)
                throw new ArgumentException("Selected category is invalid.");

            grievance.DepartmentId = category.DefaultDepartmentId;

            _context.Grievances.Add(grievance);
            await _context.SaveChangesAsync();

            // 3. Process File Uploads (if any)
            if (files != null && files.Count > 0)
            {
                foreach (var file in files)
                {
                    if (_fileUploadService.IsFileValid(file, out string error))
                    {
                        var filePath = await _fileUploadService.UploadFileAsync(file, grievance.TicketNumber);
                        var attachment = new GrievanceAttachment
                        {
                            GrievanceId = grievance.Id,
                            FileName = file.FileName,
                            FilePath = filePath,
                            FileSize = file.Length,
                            UploadedAt = DateTime.UtcNow
                        };
                        _context.GrievanceAttachments.Add(attachment);
                    }
                    else
                    {
                        throw new ArgumentException($"Attachment validation failed: {error}");
                    }
                }
                await _context.SaveChangesAsync();
            }

            // 4. Run Repetitive Grievance Detection
            await _repetitiveDetectionService.DetectAsync(grievance.Id);

            // Reload grievance with department & category details
            var savedGrievance = await _context.Grievances
                .Include(g => g.Category)
                .Include(g => g.Department)
                .ThenInclude(d => d.StaffUser)
                .FirstOrDefaultAsync(g => g.Id == grievance.Id);

            if (savedGrievance == null)
                throw new Exception("Grievance submission could not be verified.");

            // 5. Audit Log entry
            var audit = new AuditLog
            {
                UserId = grievance.IsAnonymous ? "AnonymousStudent" : (grievance.StudentId ?? "Unregistered"),
                Action = "Submit",
                EntityType = "Grievance",
                EntityId = savedGrievance.Id.ToString(),
                Timestamp = DateTime.UtcNow,
                Details = $"Grievance {savedGrievance.TicketNumber} submitted under category {savedGrievance.Category?.Name}."
            };
            _context.AuditLogs.Add(audit);
            await _context.SaveChangesAsync();

            // 6. Trigger student confirmation email
            var emailAddress = grievance.IsAnonymous ? sessionEmail : (await _context.Users.FindAsync(grievance.StudentId))?.Email;
            if (!string.IsNullOrEmpty(emailAddress))
            {
                var body = $@"Hello,

Your grievance has been successfully submitted to GMS.
Ticket Reference: {savedGrievance.TicketNumber}
Title: {savedGrievance.Title}
Category: {savedGrievance.Category?.Name}
Assigned Department: {savedGrievance.Department?.Name}
Status: Open

You will receive updates as the review progresses. If you filed anonymously, please save this ticket reference for verification.

Regards,
GMS Admin Portal";

                await _notificationService.SendStudentEmailAsync(emailAddress, savedGrievance.TicketNumber, "Submission Confirmed", body);
            }

            // 7. Trigger staff assignment email (if department has assigned staff)
            var staffUser = savedGrievance.Department?.StaffUser;
            if (staffUser != null && !string.IsNullOrEmpty(staffUser.Id))
            {
                await _notificationService.SendNotificationAsync(
                    staffUser.Id,
                    savedGrievance.Id,
                    $"A new grievance ({savedGrievance.TicketNumber}) has been auto-routed to your department: {savedGrievance.Department?.Name}.",
                    NotificationType.Submission
                );
            }

            return savedGrievance;
        }

        public async Task UpdateStatusAsync(int grievanceId, GrievanceStatus newStatus, string changedByUserId, string? notes)
        {
            var grievance = await _context.Grievances
                .Include(g => g.Student)
                .Include(g => g.Category)
                .Include(g => g.Department)
                .FirstOrDefaultAsync(g => g.Id == grievanceId);

            if (grievance == null)
                throw new ArgumentException("Grievance not found.");

            var oldStatus = grievance.Status;
            grievance.Status = newStatus;
            grievance.LastUpdatedAt = DateTime.UtcNow;

            if (newStatus == GrievanceStatus.Resolved)
            {
                grievance.ResolutionNotes = notes;
            }

            // Add Status History log
            var history = new GrievanceStatusHistory
            {
                GrievanceId = grievanceId,
                OldStatus = oldStatus,
                NewStatus = newStatus,
                ChangedByUserId = changedByUserId,
                ChangedAt = DateTime.UtcNow,
                Notes = notes
            };
            _context.GrievanceStatusHistories.Add(history);

            // Add Audit log
            var audit = new AuditLog
            {
                UserId = changedByUserId,
                Action = "UpdateStatus",
                EntityType = "Grievance",
                EntityId = grievanceId.ToString(),
                Timestamp = DateTime.UtcNow,
                Details = $"Status updated from {oldStatus} to {newStatus}. Notes: {notes}"
            };
            _context.AuditLogs.Add(audit);

            await _context.SaveChangesAsync();

            // Notify Student
            var studentEmail = grievance.Student?.Email;
            if (!string.IsNullOrEmpty(studentEmail) && !grievance.IsAnonymous)
            {
                string subjectTitle = newStatus == GrievanceStatus.InProgress ? "Under Review" : "Resolved";
                string noteSection = newStatus == GrievanceStatus.Resolved ? $"\nResolution Notes: {notes}" : "";
                
                var body = $@"Hello,

Your grievance {grievance.TicketNumber} has been updated.
New Status: {newStatus}
Updated At: {DateTime.UtcNow} UTC
{noteSection}

Regards,
GMS Admin Portal";

                await _notificationService.SendStudentEmailAsync(studentEmail, grievance.TicketNumber, $"Ticket {subjectTitle}", body);
            }
        }

        public async Task<Grievance?> GetGrievanceByIdAsync(int id)
        {
            return await _context.Grievances
                .Include(g => g.Category)
                .Include(g => g.Department)
                .Include(g => g.Student)
                .Include(g => g.Attachments)
                .Include(g => g.StatusHistory)
                    .ThenInclude(sh => sh.ChangedByUser)
                .FirstOrDefaultAsync(g => g.Id == id);
        }

        public async Task<List<Grievance>> GetGrievancesByStudentAsync(string studentId)
        {
            return await _context.Grievances
                .Include(g => g.Category)
                .Include(g => g.Department)
                .Where(g => g.StudentId == studentId && !g.IsAnonymous)
                .OrderByDescending(g => g.SubmittedAt)
                .ToListAsync();
        }

        public async Task<List<Grievance>> GetAllGrievancesAsync(
            GrievanceStatus? status = null, 
            int? departmentId = null, 
            int? categoryId = null, 
            GrievancePriority? priority = null, 
            string? search = null,
            DateTime? startDate = null,
            DateTime? endDate = null)
        {
            var query = _context.Grievances
                .Include(g => g.Category)
                .Include(g => g.Department)
                .Include(g => g.Student)
                .AsQueryable();

            if (status.HasValue)
            {
                query = query.Where(g => g.Status == status.Value);
            }

            if (departmentId.HasValue)
            {
                query = query.Where(g => g.DepartmentId == departmentId.Value);
            }

            if (categoryId.HasValue)
            {
                query = query.Where(g => g.CategoryId == categoryId.Value);
            }

            if (priority.HasValue)
            {
                query = query.Where(g => g.Priority == priority.Value);
            }

            if (!string.IsNullOrEmpty(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(g => 
                    g.TicketNumber.ToLower().Contains(s) || 
                    g.Title.ToLower().Contains(s) || 
                    (!g.IsAnonymous && g.Student != null && g.Student.FullName.ToLower().Contains(s))
                );
            }

            if (startDate.HasValue)
            {
                query = query.Where(g => g.SubmittedAt >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(g => g.SubmittedAt <= endDate.Value);
            }

            return await query.OrderByDescending(g => g.SubmittedAt).ToListAsync();
        }
    }
}
