using GMS.Web.Data;
using System;
using System.Threading.Tasks;

namespace GMS.Web.Services
{
    public interface ITicketService
    {
        Task<string> GenerateTicketNumberAsync();
    }

    public class TicketService : ITicketService
    {
        public TicketService(ApplicationDbContext context)
        {
        }

        public Task<string> GenerateTicketNumberAsync()
        {
            var year = DateTime.UtcNow.Year;
            return Task.FromResult($"GMS-{year}-{Guid.NewGuid():N}");
        }
    }
}
