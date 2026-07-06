using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace GMS.Web.Services
{
    public class EmailSender : IEmailSender
    {
        private readonly ILogger<EmailSender> _logger;

        public EmailSender(ILogger<EmailSender> logger)
        {
            _logger = logger;
        }

        public Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            // For educational project sandboxing/demo, we print the notification output to Console logs.
            // This structure is ready for SMTP deployment.
            _logger.LogInformation("================ EMAIL DISPATCH STUB ================");
            _logger.LogInformation("Recipient: {Email}", email);
            _logger.LogInformation("Subject: {Subject}", subject);
            _logger.LogInformation("Body:\n{HtmlMessage}", htmlMessage);
            _logger.LogInformation("=====================================================");

            return Task.CompletedTask;
        }
    }
}
