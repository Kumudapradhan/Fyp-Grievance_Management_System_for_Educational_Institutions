using GMS.Web.Controllers;
using GMS.Web.Data;
using GMS.Web.Models.Entities;
using GMS.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace GMS.Tests
{
    [TestClass]
    public class AdminUserManagementTests
    {
        private ApplicationDbContext GetInMemoryContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            return new ApplicationDbContext(options);
        }

        private UserManager<ApplicationUser> GetMockUserManager(ApplicationUser? user, bool updateSuccess)
        {
            var storeMock = new Mock<IUserStore<ApplicationUser>>();
            var userManagerMock = new Mock<UserManager<ApplicationUser>>(
                storeMock.Object, null, null, null, null, null, null, null, null);

            userManagerMock.Setup(um => um.GetUserId(It.IsAny<ClaimsPrincipal>()))
                .Returns("admin-1");

            userManagerMock.Setup(um => um.FindByIdAsync(It.IsAny<string>()))
                .ReturnsAsync(user);

            userManagerMock.Setup(um => um.UpdateAsync(It.IsAny<ApplicationUser>()))
                .ReturnsAsync(updateSuccess ? IdentityResult.Success : IdentityResult.Failed());

            return userManagerMock.Object;
        }

        [TestMethod]
        public async Task ToggleUserStatus_ShouldFlipIsActive_AndLogAudit_WhenSucceeded()
        {
            // Arrange
            using var context = GetInMemoryContext();

            var userToToggle = new ApplicationUser 
            { 
                Id = "student-1", 
                FullName = "Student Test", 
                Email = "student@gms.edu",
                IsActive = true 
            };
            context.Users.Add(userToToggle);
            await context.SaveChangesAsync();

            var mockGrievanceService = new Mock<IGrievanceService>();
            var mockRoutingService = new Mock<IRoutingService>();
            var mockNotificationService = new Mock<INotificationService>();
            var userManager = GetMockUserManager(userToToggle, true);

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
            var result = await controller.ToggleUserStatus("student-1");

            // Assert
            Assert.IsFalse(userToToggle.IsActive); // Flips from true to false

            // Audit log check
            var audit = await context.AuditLogs.FirstOrDefaultAsync(a => a.Action == "ToggleUserStatus" && a.EntityId == "student-1");
            Assert.IsNotNull(audit);
            Assert.IsTrue(audit.Details.Contains("Inactive"));

            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));
            var redirect = (RedirectToActionResult)result;
            Assert.AreEqual("Users", redirect.ActionName);
        }
    }
}
