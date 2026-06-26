using GMS.Web.Data;
using GMS.Web.Models.Entities;
using GMS.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GMS.Tests
{
    [TestClass]
    public class RepetitiveDetectionServiceTests
    {
        private ApplicationDbContext GetInMemoryContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            return new ApplicationDbContext(options);
        }

        private IConfiguration GetMockConfiguration()
        {
            var myConfiguration = new Dictionary<string, string>
            {
                { "SLA:RepetitiveWindowDays", "30" },
                { "SLA:RepetitiveThresholdCount", "3" }
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
        public async Task DetectAsync_ShouldNOTFlagRepetitive_WhenUnderThreshold()
        {
            // Arrange
            using var context = GetInMemoryContext();
            var config = GetMockConfiguration();
            var mockNotif = new Mock<INotificationService>();
            var adminsList = new List<ApplicationUser>();
            var userManager = GetMockUserManager(adminsList);

            // Seed only 2 tickets under Category 1 in the 30-day window
            context.Grievances.AddRange(
                new Grievance
                {
                    Id = 101, TicketNumber = "GMS-01", Title = "Title 1",
                    Description = "Validating descriptions lengths for sample test logs.",
                    CategoryId = 1, DepartmentId = 1, SubmittedAt = DateTime.UtcNow.AddDays(-5),
                    Priority = GrievancePriority.Normal
                },
                new Grievance
                {
                    Id = 102, TicketNumber = "GMS-02", Title = "Title 2",
                    Description = "Validating descriptions lengths for sample test logs.",
                    CategoryId = 1, DepartmentId = 1, SubmittedAt = DateTime.UtcNow,
                    Priority = GrievancePriority.Normal
                }
            );
            await context.SaveChangesAsync();

            var service = new RepetitiveDetectionService(context, config, userManager, mockNotif.Object);

            // Act
            await service.DetectAsync(102);

            // Assert
            var ticket1 = await context.Grievances.FindAsync(101);
            var ticket2 = await context.Grievances.FindAsync(102);

            Assert.IsFalse(ticket1!.IsRepetitive);
            Assert.IsFalse(ticket2!.IsRepetitive);
            Assert.AreEqual(GrievancePriority.Normal, ticket1.Priority);
            Assert.AreEqual(GrievancePriority.Normal, ticket2.Priority);
        }

        [TestMethod]
        public async Task DetectAsync_ShouldFlagRepetitiveAndEscalate_WhenThresholdReached()
        {
            // Arrange
            using var context = GetInMemoryContext();
            var config = GetMockConfiguration();
            var mockNotif = new Mock<INotificationService>();
            
            var adminUser = new ApplicationUser { Id = "admin-1", Email = "admin@gms.com" };
            var adminsList = new List<ApplicationUser> { adminUser };
            var userManager = GetMockUserManager(adminsList);

            // Seed 3 tickets under Category 2 in the 30-day window
            context.Grievances.AddRange(
                new Grievance
                {
                    Id = 201, TicketNumber = "GMS-21", Title = "Grievance 1",
                    Description = "Validating descriptions lengths for sample test logs.",
                    CategoryId = 2, DepartmentId = 1, SubmittedAt = DateTime.UtcNow.AddDays(-10),
                    Priority = GrievancePriority.Normal, IsRepetitive = false
                },
                new Grievance
                {
                    Id = 202, TicketNumber = "GMS-22", Title = "Grievance 2",
                    Description = "Validating descriptions lengths for sample test logs.",
                    CategoryId = 2, DepartmentId = 1, SubmittedAt = DateTime.UtcNow.AddDays(-2),
                    Priority = GrievancePriority.Normal, IsRepetitive = false
                },
                new Grievance
                {
                    Id = 203, TicketNumber = "GMS-23", Title = "Grievance 3",
                    Description = "Validating descriptions lengths for sample test logs.",
                    CategoryId = 2, DepartmentId = 1, SubmittedAt = DateTime.UtcNow,
                    Priority = GrievancePriority.Normal, IsRepetitive = false
                }
            );
            await context.SaveChangesAsync();

            var service = new RepetitiveDetectionService(context, config, userManager, mockNotif.Object);

            // Act
            await service.DetectAsync(203);

            // Assert
            var t1 = await context.Grievances.FindAsync(201);
            var t2 = await context.Grievances.FindAsync(202);
            var t3 = await context.Grievances.FindAsync(203);

            Assert.IsTrue(t1!.IsRepetitive);
            Assert.IsTrue(t2!.IsRepetitive);
            Assert.IsTrue(t3!.IsRepetitive);
            Assert.AreEqual(GrievancePriority.High, t1.Priority);
            Assert.AreEqual(GrievancePriority.High, t2.Priority);
            Assert.AreEqual(GrievancePriority.High, t3.Priority);

            // Verify admin notification sent
            mockNotif.Verify(n => n.SendNotificationAsync(
                "admin-1",
                203,
                It.Is<string>(msg => msg.Contains("Repetitive grievance pattern")),
                NotificationType.RepetitiveFlag
            ), Times.Once);
        }
    }
}
