using GMS.Web.Data;
using GMS.Web.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GMS.Web.Services
{
    public class OverdueCheckService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<OverdueCheckService> _logger;
        private readonly int _checkIntervalMinutes;
        private readonly int _overdueDays;
        private readonly bool _slaEnabled;

        public OverdueCheckService(IServiceProvider serviceProvider, ILogger<OverdueCheckService> logger, IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _checkIntervalMinutes = configuration.GetValue<int>("SLA:CheckIntervalMinutes", 1); // Default to check every 1 minute in dev
            _overdueDays = configuration.GetValue<int>("SLA:OverdueDays", 7);
            _slaEnabled = configuration.GetValue<bool>("SLA:Enabled", true);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Overdue Check background worker started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckOverdueTicketsAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during overdue ticket check execution.");
                }

                // Wait for the next check interval
                await Task.Delay(TimeSpan.FromMinutes(_checkIntervalMinutes), stoppingToken);
            }
        }

        private async Task CheckOverdueTicketsAsync()
        {
            if (!_slaEnabled)
            {
                return;
            }

            using (var scope = _serviceProvider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

                var thresholdDate = DateTime.UtcNow.AddDays(-_overdueDays);

                // Find Open or InProgress grievances that are not updated for more than _overdueDays days and not marked overdue yet.
                var overdueGrievances = await context.Grievances
                    .Include(g => g.Department)
                    .ThenInclude(d => d.StaffUser)
                    .Where(g => (g.Status == GrievanceStatus.Open || g.Status == GrievanceStatus.InProgress) 
                                && g.LastUpdatedAt <= thresholdDate
                                && !g.IsOverdue)
                    .ToListAsync();

                if (overdueGrievances.Any())
                {
                    _logger.LogInformation($"Found {overdueGrievances.Count} new overdue grievances.");

                    foreach (var grievance in overdueGrievances)
                    {
                        grievance.IsOverdue = true;
                        grievance.LastUpdatedAt = DateTime.UtcNow;

                        // Create status history log for SLA breach
                        var history = new GrievanceStatusHistory
                        {
                            GrievanceId = grievance.Id,
                            OldStatus = grievance.Status,
                            NewStatus = grievance.Status,
                            ChangedByUserId = "SystemSLA",
                            ChangedAt = DateTime.UtcNow,
                            Notes = $"SLA breached. Overdue flag set automatically by background service."
                        };
                        context.GrievanceStatusHistories.Add(history);

                        // Notify Department Staff if assigned
                        var staff = grievance.Department?.StaffUser;
                        if (staff != null)
                        {
                            await notificationService.SendNotificationAsync(
                                staff.Id,
                                grievance.Id,
                                $"WARNING: Grievance {grievance.TicketNumber} assigned to your department is OVERDUE.",
                                NotificationType.Overdue
                            );
                        }

                        // Also alert Administrators
                        var admins = await userManager.GetUsersInRoleAsync("Administrator");
                        foreach (var admin in admins)
                        {
                            await notificationService.SendNotificationAsync(
                                admin.Id,
                                grievance.Id,
                                $"WARNING: Grievance {grievance.TicketNumber} has breached SLA ({_overdueDays} days without update).",
                                NotificationType.Overdue
                            );
                        }
                    }

                    await context.SaveChangesAsync();
                }
            }
        }
    }
}
