using GMS.Web.Data;
using GMS.Web.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace GMS.Web.Services
{
    public interface IRepetitiveDetectionService
    {
        Task DetectAsync(int grievanceId);
    }

    public class RepetitiveDetectionService : IRepetitiveDetectionService
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly INotificationService _notificationService;

        public RepetitiveDetectionService(
            ApplicationDbContext context, 
            IConfiguration configuration,
            UserManager<ApplicationUser> userManager,
            INotificationService notificationService)
        {
            _context = context;
            _configuration = configuration;
            _userManager = userManager;
            _notificationService = notificationService;
        }

        public async Task DetectAsync(int grievanceId)
        {
            var grievance = await _context.Grievances.FindAsync(grievanceId);
            if (grievance == null) return;

            var categoryId = grievance.CategoryId;
            var windowDays = _configuration.GetValue<int>("SLA:RepetitiveWindowDays", 30);
            var thresholdCount = _configuration.GetValue<int>("SLA:RepetitiveThresholdCount", 3);

            var limitDate = DateTime.UtcNow.AddDays(-windowDays);

            // Fetch grievances with same Category submitted within the last X days
            var recentGrievances = await _context.Grievances
                .Where(g => g.CategoryId == categoryId && g.SubmittedAt >= limitDate)
                .ToListAsync();

            if (recentGrievances.Count >= thresholdCount)
            {
                foreach (var g in recentGrievances)
                {
                    g.IsRepetitive = true;
                    g.Priority = GrievancePriority.High;
                    g.LastUpdatedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();

                // Alert the Administrator role users
                var admins = await _userManager.GetUsersInRoleAsync("Administrator");
                foreach (var admin in admins)
                {
                    await _notificationService.SendNotificationAsync(
                        admin.Id,
                        grievanceId,
                        $"ALERT: Repetitive grievance pattern detected for category. {recentGrievances.Count} tickets filed within {windowDays} days. All flagged as HIGH priority.",
                        NotificationType.RepetitiveFlag
                    );
                }
            }
        }
    }
}
