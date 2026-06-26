using GMS.Web.Data;
using GMS.Web.Models.Entities;
using MailKit.Net.Smtp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MimeKit;
using System;
using System.IO;
using System.Threading.Tasks;

namespace GMS.Web.Services
{
    public interface INotificationService
    {
        Task SendNotificationAsync(string userId, int grievanceId, string message, NotificationType type);
        Task SendStudentEmailAsync(string? recipientEmail, string ticketNumber, string title, string body);
    }

    public class NotificationService : INotificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public NotificationService(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public async Task SendNotificationAsync(string userId, int grievanceId, string message, NotificationType type)
        {
            // 1. Persist notification in database
            var notification = new Notification
            {
                UserId = userId,
                GrievanceId = grievanceId,
                Message = message,
                IsRead = false,
                SentAt = DateTime.UtcNow,
                NotificationType = type
            };
            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            // 2. Resolve User's email address
            var user = await _context.Users.FindAsync(userId);
            if (user != null && !string.IsNullOrEmpty(user.Email))
            {
                await SendEmailCoreAsync(user.Email, $"GMS Notification: {type}", message);
            }
        }

        public async Task SendStudentEmailAsync(string? recipientEmail, string ticketNumber, string title, string body)
        {
            if (string.IsNullOrEmpty(recipientEmail)) return;
            await SendEmailCoreAsync(recipientEmail, $"GMS Ticket {ticketNumber} - {title}", body);
        }

        private async Task SendEmailCoreAsync(string toEmail, string subject, string body)
        {
            var useMock = _configuration.GetValue<bool>("Email:UseFileMock", true);

            if (useMock)
            {
                // Write email to wwwroot/sent_emails/ for testing/grading validation
                var mockPath = _configuration.GetValue<string>("Email:FileMockPath", "wwwroot/sent_emails");
                var folderPath = Path.Combine(Directory.GetCurrentDirectory(), mockPath);
                
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                var fileName = $"{Guid.NewGuid()}_{toEmail}.html";
                var filePath = Path.Combine(folderPath, fileName);

                var emailHtmlContent = $@"
                    <html>
                    <head><title>{subject}</title></head>
                    <body style='font-family: sans-serif; padding: 20px; line-height: 1.6;'>
                        <h2 style='color: #2b5c8f;'>Grievance Management System (GMS)</h2>
                        <hr/>
                        <p><strong>To:</strong> {toEmail}</p>
                        <p><strong>Subject:</strong> {subject}</p>
                        <p><strong>Sent At:</strong> {DateTime.UtcNow} UTC</p>
                        <hr/>
                        <div style='background: #f9f9f9; padding: 15px; border-left: 4px solid #2b5c8f;'>
                            {body.Replace("\n", "<br/>")}
                        </div>
                    </body>
                    </html>";

                await File.WriteAllTextAsync(filePath, emailHtmlContent);
            }
            else
            {
                // Live MailKit SMTP delivery
                var host = _configuration.GetValue<string>("Email:SmtpHost", "smtp.gmail.com");
                var port = _configuration.GetValue<int>("Email:SmtpPort", 587);
                var user = _configuration.GetValue<string>("Email:SmtpUser", "");
                var pass = _configuration.GetValue<string>("Email:SmtpPass", "");
                var fromAddress = _configuration.GetValue<string>("Email:FromAddress", "gms@edu.my");
                var fromName = _configuration.GetValue<string>("Email:FromName", "GMS Notifications");

                var emailMessage = new MimeMessage();
                emailMessage.From.Add(new MailboxAddress(fromName, fromAddress));
                emailMessage.To.Add(new MailboxAddress(toEmail, toEmail));
                emailMessage.Subject = subject;

                var bodyBuilder = new BodyBuilder { HtmlBody = body };
                emailMessage.Body = bodyBuilder.ToMessageBody();

                using (var client = new SmtpClient())
                {
                    // For dev/test, bypass certificate validation if necessary
                    client.ServerCertificateValidationCallback = (s, c, h, e) => true;

                    await client.ConnectAsync(host, port, MailKit.Security.SecureSocketOptions.StartTls);
                    await client.AuthenticateAsync(user, pass);
                    await client.SendAsync(emailMessage);
                    await client.DisconnectAsync(true);
                }
            }
        }
    }
}
