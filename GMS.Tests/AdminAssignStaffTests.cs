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
    public class AdminAssignStaffTests
    {
        private ApplicationDbContext GetInMemoryContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            return new ApplicationDbContext(options);
        }

        private UserManager<ApplicationUser> GetMockUserManager(ApplicationUser? staffUser, string staffUserId, bool isInRole)
        {
            var storeMock = new Mock<IUserStore<ApplicationUser>>();
            var userManagerMock = new Mock<UserManager<ApplicationUser>>(
                storeMock.Object, null, null, null, null, null, null, null, null);

            userManagerMock.Setup(um => um.GetUserId(It.IsAny<ClaimsPrincipal>()))
                .Returns("admin-1");

            userManagerMock.Setup(um => um.FindByIdAsync(staffUserId))
                .ReturnsAsync(staffUser);

            userManagerMock.Setup(um => um.IsInRoleAsync(It.IsAny<ApplicationUser>(), "Staff"))
                .ReturnsAsync(isInRole);

            return userManagerMock.Object;
        }

        [TestMethod]
        public async Task AssignStaff_ShouldSetAssignedStaffUserId_AndNotifyWithAnonymity_WhenAnonymous()
        {
            // Arrange
            using var context = GetInMemoryContext();

            var staffUser = new ApplicationUser { Id = "staff-1", FullName = "Staff Officer", Email = "staff@gms.edu" };
            context.Users.Add(staffUser);

            var dept = new Department { Id = 1, Name = "IT Support", StaffUserId = "staff-1" };
            var cat = new Category { Id = 1, Name = "IT Issue", DefaultDepartmentId = 1 };
            context.Departments.Add(dept);
            context.Categories.Add(cat);

            var grievance = new Grievance
            {
                Id = 10,
                TicketNumber = "T-100",
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
            var mockRoutingService = new Mock<IRoutingService>();
            var mockNotificationService = new Mock<INotificationService>();
            var userManager = GetMockUserManager(staffUser, "staff-1", true);

            var httpContext = new DefaultHttpContext();
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "admin-1"),
                new Claim(ClaimTypes.Role, "Administrator")
            }));

            var controller = new AdminController(mockGrievanceService.Object, mockRoutingService.Object, userManager, context, mockNotificationService.Object)
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext },
                TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>())
            };

            // Act
            var result = await controller.AssignStaff(10, "staff-1", "Please review this immediately.");

            // Assert
            var updated = await context.Grievances.FindAsync(10);
            Assert.IsNotNull(updated);
            Assert.AreEqual("staff-1", updated.AssignedStaffUserId);

            // History Log
            var history = await context.GrievanceStatusHistories.FirstOrDefaultAsync(h => h.GrievanceId == 10);
            Assert.IsNotNull(history);
            Assert.IsTrue(history.Notes!.Contains("Assigned to staff member Staff Officer"));

            // Audit Log
            var audit = await context.AuditLogs.FirstOrDefaultAsync(a => a.Action == "AssignStaff" && a.EntityId == "10");
            Assert.IsNotNull(audit);

            // Notification
            mockNotificationService.Verify(n => n.SendNotificationAsync(
                "staff-1",
                10,
                It.Is<string>(msg => msg.Contains("Anonymous Filing") && !msg.Contains("Student One") && !msg.Contains("student-1")),
                NotificationType.Assignment
            ), Times.Once);

            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));
        }

        [TestMethod]
        public async Task AssignStaff_ShouldNotifyWithAnonymity_WhenNotAnonymous()
        {
            // Arrange
            using var context = GetInMemoryContext();

            var staffUser = new ApplicationUser { Id = "staff-1", FullName = "Staff Officer", Email = "staff@gms.edu" };
            context.Users.Add(staffUser);

            var dept = new Department { Id = 1, Name = "IT Support", StaffUserId = "staff-1" };
            var cat = new Category { Id = 1, Name = "IT Issue", DefaultDepartmentId = 1 };
            context.Departments.Add(dept);
            context.Categories.Add(cat);

            var grievance = new Grievance
            {
                Id = 11,
                TicketNumber = "T-111",
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
            var mockRoutingService = new Mock<IRoutingService>();
            var mockNotificationService = new Mock<INotificationService>();
            var userManager = GetMockUserManager(staffUser, "staff-1", true);

            var httpContext = new DefaultHttpContext();
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "admin-1"),
                new Claim(ClaimTypes.Role, "Administrator")
            }));

            var controller = new AdminController(mockGrievanceService.Object, mockRoutingService.Object, userManager, context, mockNotificationService.Object)
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext },
                TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>())
            };

            // Act
            var result = await controller.AssignStaff(11, "staff-1", "Review details.");

            // Assert
            mockNotificationService.Verify(n => n.SendNotificationAsync(
                "staff-1",
                11,
                It.Is<string>(msg => !msg.Contains("Student One") && !msg.Contains("student-1")),
                NotificationType.Assignment
            ), Times.Once);

            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));
        }

        [TestMethod]
        public async Task AssignStaff_ShouldRejectStaffFromAnotherDepartment()
        {
            using var context = GetInMemoryContext();

            var staffUser = new ApplicationUser { Id = "staff-2", FullName = "Finance Officer", Email = "finance@gms.edu", Department = "Finance" };
            context.Users.Add(staffUser);
            context.Departments.AddRange(
                new Department { Id = 1, Name = "IT Support" },
                new Department { Id = 2, Name = "Finance", StaffUserId = "staff-2" });
            context.Categories.Add(new Category { Id = 1, Name = "IT Issue", DefaultDepartmentId = 1 });
            context.Grievances.Add(new Grievance
            {
                Id = 12,
                TicketNumber = "T-112",
                Title = "Test title that must meet the character count criteria which is minimum fifty characters.",
                Description = "Test description that must meet the character count criteria which is minimum fifty characters.",
                CategoryId = 1,
                DepartmentId = 1,
                Status = GrievanceStatus.Open
            });
            await context.SaveChangesAsync();

            var httpContext = new DefaultHttpContext();
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "admin-1"),
                new Claim(ClaimTypes.Role, "Administrator")
            }));
            var notificationService = new Mock<INotificationService>();
            var controller = new AdminController(new Mock<IGrievanceService>().Object, new Mock<IRoutingService>().Object,
                GetMockUserManager(staffUser, "staff-2", true), context, notificationService.Object)
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext },
                TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>())
            };

            var result = await controller.AssignStaff(12, "staff-2", null);

            Assert.IsNull((await context.Grievances.FindAsync(12))!.AssignedStaffUserId);
            notificationService.Verify(n => n.SendNotificationAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<NotificationType>()), Times.Never);
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));
        }
    }
}
