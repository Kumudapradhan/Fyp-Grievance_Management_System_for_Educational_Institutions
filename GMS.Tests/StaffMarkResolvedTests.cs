using GMS.Web.Controllers;
using GMS.Web.Data;
using GMS.Web.Models.Entities;
using GMS.Web.Models.ViewModels;
using GMS.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace GMS.Tests
{
    [TestClass]
    public class StaffMarkResolvedTests
    {
        private ApplicationDbContext GetInMemoryContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            return new ApplicationDbContext(options);
        }

        private UserManager<ApplicationUser> GetMockUserManager(ApplicationUser? user)
        {
            var storeMock = new Mock<IUserStore<ApplicationUser>>();
            var userManagerMock = new Mock<UserManager<ApplicationUser>>(
                storeMock.Object, null, null, null, null, null, null, null, null);
            
            userManagerMock.Setup(um => um.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync(user);

            return userManagerMock.Object;
        }

        [TestMethod]
        public async Task MarkResolved_ShouldResolveAndNotifyStudent_WhenNotAnonymous()
        {
            // Arrange
            using var context = GetInMemoryContext();

            var staffUser = new ApplicationUser { Id = "staff-1", FullName = "Staff Officer", Email = "staff@gms.edu", Department = "IT Support" };
            var studentUser = new ApplicationUser { Id = "student-1", FullName = "Student One", Email = "student@gms.edu" };
            context.Users.AddRange(staffUser, studentUser);

            var dept = new Department { Id = 1, Name = "IT Support", StaffUserId = "staff-1" };
            context.Departments.Add(dept);

            var cat = new Category { Id = 1, Name = "IT Issue", DefaultDepartmentId = 1 };
            context.Categories.Add(cat);

            var grievance = new Grievance
            {
                Id = 1,
                TicketNumber = "T-100",
                Title = "Test title that must meet the character count criteria which is minimum fifty characters.",
                Description = "Test description that must meet the character count criteria which is minimum fifty characters.",
                CategoryId = 1,
                DepartmentId = 1,
                StudentId = "student-1",
                IsAnonymous = false,
                Status = GrievanceStatus.Open
            };
            context.Grievances.Add(grievance);
            await context.SaveChangesAsync();

            var mockGrievanceService = new Mock<IGrievanceService>();
            var mockNotificationService = new Mock<INotificationService>();
            var userManager = GetMockUserManager(staffUser);

            var httpContext = new DefaultHttpContext();
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "staff-1"),
                new Claim(ClaimTypes.Role, "Staff")
            }));

            var controller = new StaffController(mockGrievanceService.Object, userManager, context, mockNotificationService.Object)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = httpContext
                },
                TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>())
            };

            // Act
            var result = await controller.MarkResolved(1, "Resolution complete.");

            // Assert
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));

            var updated = await context.Grievances.FindAsync(1);
            Assert.IsNotNull(updated);
            Assert.AreEqual(GrievanceStatus.Resolved, updated.Status);
            Assert.AreEqual("Resolution complete.", updated.ResolutionNotes);

            // Audit Log
            var audit = await context.AuditLogs.AnyAsync(a => a.Action == "MarkResolved" && a.EntityId == "1");
            Assert.IsTrue(audit);

            // Notification for student
            var notification = await context.Notifications.FirstOrDefaultAsync(n => n.UserId == "student-1");
            Assert.IsNotNull(notification);
            Assert.IsTrue(notification.Message.Contains("resolved"));

            // Email check
            mockNotificationService.Verify(n => n.SendStudentEmailAsync(
                "student@gms.edu", 
                "T-100", 
                "Ticket Resolved", 
                It.Is<string>(body => body.Contains("Resolution:") || body.Contains("Resolution complete"))
            ), Times.Once);
        }

        [TestMethod]
        public async Task MarkResolved_ShouldResolveAndSkipNotification_WhenAnonymous()
        {
            // Arrange
            using var context = GetInMemoryContext();

            var staffUser = new ApplicationUser { Id = "staff-1", FullName = "Staff Officer", Email = "staff@gms.edu", Department = "IT Support" };
            context.Users.Add(staffUser);

            var dept = new Department { Id = 1, Name = "IT Support", StaffUserId = "staff-1" };
            context.Departments.Add(dept);

            var cat = new Category { Id = 1, Name = "IT Issue", DefaultDepartmentId = 1 };
            context.Categories.Add(cat);

            var grievance = new Grievance
            {
                Id = 2,
                TicketNumber = "T-200",
                Title = "Test title that must meet the character count criteria which is minimum fifty characters.",
                Description = "Test description that must meet the character count criteria which is minimum fifty characters.",
                CategoryId = 1,
                DepartmentId = 1,
                StudentId = "student-1",
                IsAnonymous = true,
                Status = GrievanceStatus.Open
            };
            context.Grievances.Add(grievance);
            await context.SaveChangesAsync();

            var mockGrievanceService = new Mock<IGrievanceService>();
            var mockNotificationService = new Mock<INotificationService>();
            var userManager = GetMockUserManager(staffUser);

            var httpContext = new DefaultHttpContext();
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "staff-1"),
                new Claim(ClaimTypes.Role, "Staff")
            }));

            var controller = new StaffController(mockGrievanceService.Object, userManager, context, mockNotificationService.Object)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = httpContext
                },
                TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>())
            };

            // Act
            var result = await controller.MarkResolved(2, "Resolution complete anonymous.");

            // Assert
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));

            var updated = await context.Grievances.FindAsync(2);
            Assert.IsNotNull(updated);
            Assert.AreEqual(GrievanceStatus.Resolved, updated.Status);

            // Skip notification
            var notification = await context.Notifications.AnyAsync(n => n.GrievanceId == 2);
            Assert.IsFalse(notification);

            mockNotificationService.Verify(n => n.SendStudentEmailAsync(
                It.IsAny<string>(), 
                It.IsAny<string>(), 
                It.IsAny<string>(), 
                It.IsAny<string>()
            ), Times.Never);
        }
    }
}
