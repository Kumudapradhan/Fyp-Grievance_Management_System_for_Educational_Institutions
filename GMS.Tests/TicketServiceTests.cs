using GMS.Web.Data;
using GMS.Web.Models.Entities;
using GMS.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading.Tasks;

namespace GMS.Tests
{
    [TestClass]
    public class TicketServiceTests
    {
        private ApplicationDbContext GetInMemoryContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            return new ApplicationDbContext(options);
        }

        [TestMethod]
        public async Task GenerateTicketNumberAsync_ShouldCreateFirstTicket_CorrectFormat()
        {
            // Arrange
            using var context = GetInMemoryContext();
            var service = new TicketService(context);
            var expectedYear = DateTime.UtcNow.Year;

            // Act
            var ticketNumber = await service.GenerateTicketNumberAsync();

            // Assert
            Assert.IsNotNull(ticketNumber);
            Assert.IsTrue(ticketNumber.StartsWith($"GMS-{expectedYear}-"));
            Assert.AreEqual($"GMS-{expectedYear}-00001", ticketNumber);
        }

        [TestMethod]
        public async Task GenerateTicketNumberAsync_ShouldIncrementSequence_WhenTicketsExist()
        {
            // Arrange
            using var context = GetInMemoryContext();
            var expectedYear = DateTime.UtcNow.Year;

            // Seed existing tickets
            context.Grievances.Add(new Grievance
            {
                TicketNumber = $"GMS-{expectedYear}-00045",
                Title = "Test 1",
                Description = "Test 1 description with more than fifty characters to pass validations.",
                IncidentDate = DateTime.Today,
                CategoryId = 1,
                DepartmentId = 1
            });
            await context.SaveChangesAsync();

            var service = new TicketService(context);

            // Act
            var ticketNumber = await service.GenerateTicketNumberAsync();

            // Assert
            Assert.AreEqual($"GMS-{expectedYear}-00046", ticketNumber);
        }
    }
}
