using GMS.Web.Data;
using GMS.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace GMS.Web.Services
{
    public interface IRoutingService
    {
        Task AutoRouteAsync(int grievanceId);
        Task ReRouteAsync(int grievanceId, int newDepartmentId, string changedByUserId, string notes);
    }

    public class RoutingService : IRoutingService
    {
        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notificationService;

        public RoutingService(ApplicationDbContext context, INotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        public async Task AutoRouteAsync(int grievanceId)
        {
            var grievance = await _context.Grievances
                .Include(g => g.Category)
                .FirstOrDefaultAsync(g => g.Id == grievanceId);

            if (grievance == null)
                throw new ArgumentException($"Grievance with ID {grievanceId} not found.");

            if (grievance.Category == null)
                throw new ArgumentException($"Grievance category is missing.");

            grievance.DepartmentId = grievance.Category.DefaultDepartmentId;
            grievance.LastUpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }

        public async Task ReRouteAsync(int grievanceId, int newDepartmentId, string changedByUserId, string notes)
        {
            var grievance = await _context.Grievances
                .Include(g => g.Department)
                .FirstOrDefaultAsync(g => g.Id == grievanceId);

            if (grievance == null)
                throw new ArgumentException($"Grievance with ID {grievanceId} not found.");

            var newDept = await _context.Departments
                .Include(d => d.StaffUser)
                .FirstOrDefaultAsync(d => d.Id == newDepartmentId);

            if (newDept == null)
                throw new ArgumentException($"Target department with ID {newDepartmentId} not found.");

            var oldDeptId = grievance.DepartmentId;
            var oldDeptName = grievance.Department?.Name ?? "Unassigned";

            // Update Department
            grievance.DepartmentId = newDepartmentId;
            grievance.LastUpdatedAt = DateTime.UtcNow;

            // Log status history (department change)
            var history = new GrievanceStatusHistory
            {
                GrievanceId = grievanceId,
                OldStatus = grievance.Status,
                NewStatus = grievance.Status, // Status remains the same, but logged for re-routing
                ChangedByUserId = changedByUserId,
                ChangedAt = DateTime.UtcNow,
                Notes = $"Department re-routed from '{oldDeptName}' to '{newDept.Name}'. Action note: {notes}"
            };
            _context.GrievanceStatusHistories.Add(history);

            // Audit Log entry
            var audit = new AuditLog
            {
                UserId = changedByUserId,
                Action = "ReRoute",
                EntityType = "Grievance",
                EntityId = grievanceId.ToString(),
                Timestamp = DateTime.UtcNow,
                Details = $"Re-routed ticket {grievance.TicketNumber} from department {oldDeptId} to {newDepartmentId}."
            };
            _context.AuditLogs.Add(audit);

            await _context.SaveChangesAsync();

            // Trigger email notification to the newly assigned department's staff (if assigned)
            if (!string.IsNullOrEmpty(newDept.StaffUserId))
            {
                await _notificationService.SendNotificationAsync(
                    newDept.StaffUserId, 
                    grievanceId, 
                    $"A grievance ({grievance.TicketNumber}) has been re-routed to your department: {newDept.Name}.",
                    NotificationType.Assignment
                );
            }
        }
    }
}
