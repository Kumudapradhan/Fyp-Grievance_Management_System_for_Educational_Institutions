using GMS.Web.Data;
using GMS.Web.Models.Entities;
using GMS.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace GMS.Tests
{
    [TestClass]
    public class GrievanceServiceTests
    {
        private ApplicationDbContext GetInMemoryContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            return new ApplicationDbContext(options);
        }

        [TestMethod]
        public async Task SubmitGrievanceAsync_ShouldSaveGrievanceAndRouteAndNotify()
        {
            // Arrange
            using var context = GetInMemoryContext();
            
            // Seed Category and Department
            var dept = new Department { Id = 10, Name = "IT Support", StaffUserId = "staff-1" };
            var cat = new Category { Id = 5, Name = "IT Issue", DefaultDepartmentId = 10 };
            context.Departments.Add(dept);
            context.Categories.Add(cat);
            
            var staffUser = new ApplicationUser { Id = "staff-1", Email = "staff@gms.com", FullName = "Staff Officer" };
            context.Users.Add(staffUser);
            await context.SaveChangesAsync();

            var grievance = new Grievance
            {
                Id = 1,
                Title = "Library Wi-Fi down",
                Description = "Wi-Fi issue description that is long enough to satisfy character length constraints.",
                CategoryId = 5,
                IsAnonymous = false,
                StudentId = "student-1"
            };

            var mockTicket = new Mock<ITicketService>();
            mockTicket.Setup(t => t.GenerateTicketNumberAsync()).ReturnsAsync("GMS-2026-99999");

            var mockUpload = new Mock<IFileUploadService>();
            var mockNotif = new Mock<INotificationService>();
            var mockRepetitive = new Mock<IRepetitiveDetectionService>();
            var mockLogger = new Mock<ILogger<GrievanceService>>();

            var service = new GrievanceService(context, mockTicket.Object, mockUpload.Object, mockNotif.Object, mockRepetitive.Object, mockLogger.Object);

            // Act
            var result = await service.SubmitGrievanceAsync(grievance, new List<IFormFile>(), "student@test.com");

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("GMS-2026-99999", result.TicketNumber);
            Assert.AreEqual(10, result.DepartmentId); // Auto-routed to IT Support (DefaultDepartmentId = 10)
            Assert.AreEqual(GrievanceStatus.Open, result.Status);

            // Verify repetitive check triggered
            mockRepetitive.Verify(r => r.DetectAsync(1), Times.Once);

            // Verify notifications sent to staff
            mockNotif.Verify(n => n.SendNotificationAsync(
                "staff-1",
                1,
                It.Is<string>(msg => msg.Contains("auto-routed")),
                NotificationType.Submission
            ), Times.Once);
        }

        [TestMethod]
        public async Task UpdateStatusAsync_ShouldChangeStatusAndNotifyStudent()
        {
            // Arrange
            using var context = GetInMemoryContext();
            
            var studentUser = new ApplicationUser { Id = "student-12", Email = "student12@test.com" };
            context.Users.Add(studentUser);

            var dept = new Department { Id = 1, Name = "Finance" };
            var cat = new Category { Id = 1, Name = "Finance Issue", DefaultDepartmentId = 1 };
            context.Departments.Add(dept);
            context.Categories.Add(cat);

            var grievance = new Grievance
            {
                Id = 2,
                TicketNumber = "GMS-TEST-02",
                Title = "Finance Payment Issue",
                Description = "Payment failed but money was deducted from my account yesterday.",
                CategoryId = 1,
                DepartmentId = 1,
                StudentId = "student-12",
                Status = GrievanceStatus.Open
            };
            context.Grievances.Add(grievance);
            await context.SaveChangesAsync();

            var mockTicket = new Mock<ITicketService>();
            var mockUpload = new Mock<IFileUploadService>();
            var mockNotif = new Mock<INotificationService>();
            var mockRepetitive = new Mock<IRepetitiveDetectionService>();
            var mockLogger = new Mock<ILogger<GrievanceService>>();

            var service = new GrievanceService(context, mockTicket.Object, mockUpload.Object, mockNotif.Object, mockRepetitive.Object, mockLogger.Object);

            // Act
            await service.UpdateStatusAsync(2, GrievanceStatus.Resolved, "admin-1", "Funds credited back to student card.");

            // Assert
            var updated = await context.Grievances.FindAsync(2);
            Assert.IsNotNull(updated);
            Assert.AreEqual(GrievanceStatus.Resolved, updated.Status);
            Assert.AreEqual("Funds credited back to student card.", updated.ResolutionNotes);

            // Check if status history logged
            var history = await context.GrievanceStatusHistories.FirstOrDefaultAsync(h => h.GrievanceId == 2);
            Assert.IsNotNull(history);
            Assert.AreEqual(GrievanceStatus.Open, history.OldStatus);
            Assert.AreEqual(GrievanceStatus.Resolved, history.NewStatus);

            // Check if student notification email was sent
            mockNotif.Verify(n => n.SendStudentEmailAsync(
                "student12@test.com",
                "GMS-TEST-02",
                It.Is<string>(subj => subj.Contains("Resolved")),
                It.Is<string>(body => body.Contains("Resolved"))
            ), Times.Once);
        }
    }
}
