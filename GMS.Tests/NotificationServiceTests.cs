using GMS.Web.Data;
using GMS.Web.Models.Entities;
using GMS.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GMS.Tests
{
    [TestClass]
    public class NotificationServiceTests
    {
        private ApplicationDbContext GetInMemoryContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            return new ApplicationDbContext(options);
        }

        private IConfiguration GetMockConfiguration(bool useMock)
        {
            var configMock = new Mock<IConfiguration>();
            
            var mockSection = new Mock<IConfigurationSection>();
            mockSection.Setup(s => s.Value).Returns(useMock.ToString());
            configMock.Setup(c => c.GetSection("Email:UseFileMock")).Returns(mockSection.Object);

            var pathSection = new Mock<IConfigurationSection>();
            pathSection.Setup(s => s.Value).Returns("wwwroot/sent_emails");
            configMock.Setup(c => c.GetSection("Email:FileMockPath")).Returns(pathSection.Object);

            return configMock.Object;
        }

        [TestMethod]
        public async Task SendNotificationAsync_ShouldPersistRecord_InDatabase()
        {
            // Arrange
            using var context = GetInMemoryContext();
            
            // Seed student user
            var student = new ApplicationUser { Id = "stu-123", Email = "student@test.com", FullName = "Test Student" };
            context.Users.Add(student);
            await context.SaveChangesAsync();

            var config = GetMockConfiguration(true);
            var service = new NotificationService(context, config);

            // Act
            await service.SendNotificationAsync("stu-123", 99, "Your ticket is under review.", NotificationType.StatusChange);

            // Assert
            var dbNotif = await context.Notifications.FirstOrDefaultAsync(n => n.UserId == "stu-123");
            Assert.IsNotNull(dbNotif);
            Assert.AreEqual("Your ticket is under review.", dbNotif.Message);
            Assert.AreEqual(NotificationType.StatusChange, dbNotif.NotificationType);
            Assert.IsFalse(dbNotif.IsRead);
        }
    }
}
