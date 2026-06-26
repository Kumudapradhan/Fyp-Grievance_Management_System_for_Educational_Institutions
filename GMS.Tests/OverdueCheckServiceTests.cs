using GMS.Web.Data;
using GMS.Web.Models.Entities;
using GMS.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace GMS.Tests
{
    [TestClass]
    public class OverdueCheckServiceTests
    {
        private ApplicationDbContext GetInMemoryContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            return new ApplicationDbContext(options);
        }

        private IServiceProvider GetMockServiceProvider(ApplicationDbContext context, INotificationService notificationService, UserManager<ApplicationUser> userManager)
        {
            var services = new ServiceCollection();
            services.AddSingleton(context);
            services.AddSingleton(notificationService);
            services.AddSingleton(userManager);
            return services.BuildServiceProvider();
        }

        private IConfiguration GetMockConfiguration(int overdueDays)
        {
            var myConfiguration = new Dictionary<string, string>
            {
                { "SLA:CheckIntervalMinutes", "1" },
                { "SLA:OverdueDays", overdueDays.ToString() }
            };
            return new ConfigurationBuilder()
                .AddInMemoryCollection(myConfiguration)
                .Build();
        }

        private UserManager<ApplicationUser> GetMockUserManager(List<ApplicationUser> admins)
        {
            var storeMock = new Mock<IUserStore<ApplicationUser>>();
            var userManagerMock = new Mock<UserManager<ApplicationUser>>(
                storeMock.Object, null, null, null, null, null, null, null, null);
            
            userManagerMock.Setup(um => um.GetUsersInRoleAsync("Administrator"))
                .ReturnsAsync(admins);

            return userManagerMock.Object;
        }

        [TestMethod]
        public async Task CheckOverdueTicketsAsync_ShouldFlagTicketsOverdue_WhenSLAExceeded()
        {
            // Arrange
            using var context = GetInMemoryContext();
            var overdueDays = 7;
            var config = GetMockConfiguration(overdueDays);
            
            // Seed Dept & Category
            var dept = new Department { Id = 1, Name = "General Administration" };
            var cat = new Category { Id = 1, Name = "Other", DefaultDepartmentId = 1 };
            context.Departments.Add(dept);
            context.Categories.Add(cat);
            await context.SaveChangesAsync();

            // Grievance older than 7 days, status Open
            var oldTicket = new Grievance
            {
                Id = 1,
                TicketNumber = "GMS-OLD",
                Title = "Old Ticket",
                Description = "Wi-Fi issue description that is long enough to satisfy character length constraints.",
                Status = GrievanceStatus.Open,
                CategoryId = 1,
                DepartmentId = 1,
                IncidentDate = DateTime.UtcNow.AddDays(-10),
                SubmittedAt = DateTime.UtcNow.AddDays(-10),
                LastUpdatedAt = DateTime.UtcNow.AddDays(-10), // Exceeded SLA
                IsOverdue = false
            };

            // Grievance older than 7 days, status Resolved (should NOT be flagged)
            var oldResolvedTicket = new Grievance
            {
                Id = 2,
                TicketNumber = "GMS-RESOLVED",
                Title = "Resolved Ticket",
                Description = "Wi-Fi issue description that is long enough to satisfy character length constraints.",
                Status = GrievanceStatus.Resolved,
                CategoryId = 1,
                DepartmentId = 1,
                IncidentDate = DateTime.UtcNow.AddDays(-10),
                SubmittedAt = DateTime.UtcNow.AddDays(-10),
                LastUpdatedAt = DateTime.UtcNow.AddDays(-10),
                IsOverdue = false
            };

            // Grievance newer than 7 days (should NOT be flagged)
            var newTicket = new Grievance
            {
                Id = 3,
                TicketNumber = "GMS-NEW",
                Title = "New Ticket",
                Description = "Wi-Fi issue description that is long enough to satisfy character length constraints.",
                Status = GrievanceStatus.Open,
                CategoryId = 1,
                DepartmentId = 1,
                IncidentDate = DateTime.UtcNow.AddDays(-2),
                SubmittedAt = DateTime.UtcNow.AddDays(-2),
                LastUpdatedAt = DateTime.UtcNow.AddDays(-2),
                IsOverdue = false
            };

            context.Grievances.AddRange(oldTicket, oldResolvedTicket, newTicket);
            await context.SaveChangesAsync();

            var mockNotif = new Mock<INotificationService>();
            var adminUser = new ApplicationUser { Id = "admin-1", Email = "admin@gms.com" };
            var userManager = GetMockUserManager(new List<ApplicationUser> { adminUser });

            var serviceProvider = GetMockServiceProvider(context, mockNotif.Object, userManager);
            var logger = new Mock<ILogger<OverdueCheckService>>().Object;

            var service = new OverdueCheckService(serviceProvider, logger, config);

            // Act: Invoke the private CheckOverdueTicketsAsync method using Reflection
            var method = typeof(OverdueCheckService).GetMethod("CheckOverdueTicketsAsync", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, "CheckOverdueTicketsAsync method not found.");
            
            var task = (Task)method.Invoke(service, null)!;
            await task;

            // Assert
            var ticket1 = await context.Grievances.FindAsync(1);
            var ticket2 = await context.Grievances.FindAsync(2);
            var ticket3 = await context.Grievances.FindAsync(3);

            Assert.IsTrue(ticket1!.IsOverdue, "Older open ticket should be flagged as overdue.");
            Assert.IsFalse(ticket2!.IsOverdue, "Older resolved ticket should not be flagged as overdue.");
            Assert.IsFalse(ticket3!.IsOverdue, "Newer open ticket should not be flagged as overdue.");

            // Check if status history logged
            var history = await context.GrievanceStatusHistories.FirstOrDefaultAsync(h => h.GrievanceId == 1 && h.ChangedByUserId == "SystemSLA");
            Assert.IsNotNull(history, "History record for SLA breach should exist.");

            // Verify notifications sent to admin
            mockNotif.Verify(n => n.SendNotificationAsync(
                "admin-1",
                1,
                It.Is<string>(msg => msg.Contains("breached SLA")),
                NotificationType.Overdue
            ), Times.Once);
        }
    }
}
