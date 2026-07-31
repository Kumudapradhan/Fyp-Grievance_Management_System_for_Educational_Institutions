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
        public async Task GenerateTicketNumberAsync_ShouldCreateUniqueGuidBackedTicket()
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
            Assert.AreEqual($"GMS-{expectedYear}-".Length + 32, ticketNumber.Length);
            Assert.IsTrue(Guid.TryParseExact(ticketNumber.Substring($"GMS-{expectedYear}-".Length), "N", out _));
        }

        [TestMethod]
        public async Task GenerateTicketNumberAsync_ShouldGenerateDistinctTickets_WhenCalledConcurrently()
        {
            // Arrange
            using var context = GetInMemoryContext();
            var service = new TicketService(context);

            // Act
            var tickets = await Task.WhenAll(
                service.GenerateTicketNumberAsync(),
                service.GenerateTicketNumberAsync(),
                service.GenerateTicketNumberAsync());

            // Assert
            Assert.AreEqual(tickets.Length, tickets.Distinct().Count());
        }
    }
}
