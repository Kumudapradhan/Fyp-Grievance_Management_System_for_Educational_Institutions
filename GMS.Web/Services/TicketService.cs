using GMS.Web.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace GMS.Web.Services
{
    public interface ITicketService
    {
        Task<string> GenerateTicketNumberAsync();
    }

    public class TicketService : ITicketService
    {
        private readonly ApplicationDbContext _context;
        private static readonly object _lock = new object();

        public TicketService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<string> GenerateTicketNumberAsync()
        {
            var year = DateTime.UtcNow.Year;
            int nextSeq = 1;

            // Simple lock block is synchronous; for async DB call we retrieve latest from DB, and fallback to lock if needed.
            // Retrieve all ticket numbers starting with "GMS-{year}-"
            var prefix = $"GMS-{year}-";
            var latestTicket = await _context.Grievances
                .Where(g => g.TicketNumber.StartsWith(prefix))
                .OrderByDescending(g => g.TicketNumber)
                .Select(g => g.TicketNumber)
                .FirstOrDefaultAsync();

            if (latestTicket != null && latestTicket.Length > prefix.Length)
            {
                var suffix = latestTicket.Substring(prefix.Length);
                if (int.TryParse(suffix, out int currentSeq))
                {
                    nextSeq = currentSeq + 1;
                }
            }

            return $"{prefix}{nextSeq:D5}";
        }
    }
}
