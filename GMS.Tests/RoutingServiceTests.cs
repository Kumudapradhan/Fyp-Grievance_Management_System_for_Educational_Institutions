using GMS.Web.Data;
using GMS.Web.Models.Entities;
using GMS.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Threading.Tasks;

namespace GMS.Tests
{
    [TestClass]
    public class RoutingServiceTests
    {
        private ApplicationDbContext GetInMemoryContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            return new ApplicationDbContext(options);
        }

        [TestMethod]
        public async Task AutoRouteAsync_ShouldUpdateDepartmentToCategoryDefault()
        {
            // Arrange
            using var context = GetInMemoryContext();
            
            // Seed Dept & Category
            var dept = new Department { Id = 1, Name = "IT Support" };
            var cat = new Category { Id = 1, Name = "IT Issue", DefaultDepartmentId = 1 };
            context.Departments.Add(dept);
            context.Categories.Add(cat);

            var grievance = new Grievance
            {
                Id = 123,
                TicketNumber = "GMS-2026-00001",
                Title = "Network Issue",
                Description = "Wi-Fi is down in the main library block for past two hours.",
                CategoryId = 1,
                DepartmentId = 99 // Initially incorrect
            };
            context.Grievances.Add(grievance);
            await context.SaveChangesAsync();

            var mockNotification = new Mock<INotificationService>();
            var service = new RoutingService(context, mockNotification.Object);

            // Act
            await service.AutoRouteAsync(123);

            // Assert
            var updated = await context.Grievances.FindAsync(123);
            Assert.IsNotNull(updated);
            Assert.AreEqual(1, updated.DepartmentId);
        }

        [TestMethod]
        public async Task ReRouteAsync_ShouldChangeDepartmentAndLogHistoryAndNotify()
        {
            // Arrange
            using var context = GetInMemoryContext();
            
            var oldDept = new Department { Id = 1, Name = "IT Support" };
            var newDept = new Department { Id = 2, Name = "Academic Affairs", StaffUserId = "staff-user-id" };
            context.Departments.AddRange(oldDept, newDept);

            var grievance = new Grievance
            {
                Id = 555,
                TicketNumber = "GMS-2026-00005",
                Title = "Course registration problem",
                Description = "Wi-Fi issue description that is long enough to satisfy character length constraints.",
                CategoryId = 1,
                DepartmentId = 1
            };
            context.Grievances.Add(grievance);
            await context.SaveChangesAsync();

            var mockNotification = new Mock<INotificationService>();
            var service = new RoutingService(context, mockNotification.Object);

            // Act
            await service.ReRouteAsync(555, 2, "admin-user", "Re-routing this for course matching review.");

            // Assert
            var updated = await context.Grievances.FindAsync(555);
            Assert.IsNotNull(updated);
            Assert.AreEqual(2, updated.DepartmentId);

            // Check history entry
            var history = await context.GrievanceStatusHistories.FirstOrDefaultAsync(h => h.GrievanceId == 555);
            Assert.IsNotNull(history);
            Assert.IsTrue(history.Notes!.Contains("Department re-routed"));

            // Check notification dispatch call
            mockNotification.Verify(n => n.SendNotificationAsync(
                "staff-user-id", 
                555, 
                It.Is<string>(msg => msg.Contains("re-routed")),
                NotificationType.Assignment
            ), Times.Once);
        }
    }
}
