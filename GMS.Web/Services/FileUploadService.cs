using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace GMS.Web.Services
{
    public interface IFileUploadService
    {
        Task<string> UploadFileAsync(IFormFile file, string ticketNumber);
        bool IsFileValid(IFormFile file, out string errorMessage);
    }

    public class FileUploadService : IFileUploadService
    {
        private readonly string[] _allowedExtensions;
        private readonly long _maxFileSizeLimitBytes;

        public FileUploadService(IConfiguration configuration)
        {
            var allowedExts = configuration.GetSection("FileUpload:AllowedExtensions").Get<string[]>();
            _allowedExtensions = allowedExts ?? new[] { ".pdf", ".jpg", ".jpeg", ".png", ".docx" };

            var maxMB = configuration.GetValue<long>("FileUpload:MaxFileSizeMB", 5);
            _maxFileSizeLimitBytes = maxMB * 1024 * 1024;
        }

        public bool IsFileValid(IFormFile file, out string errorMessage)
        {
            errorMessage = string.Empty;

            if (file == null || file.Length == 0)
            {
                errorMessage = "Selected file is empty.";
                return false;
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!_allowedExtensions.Contains(extension))
            {
                errorMessage = $"File type {extension} is not allowed. Allowed types: {string.Join(", ", _allowedExtensions)}";
                return false;
            }

            if (file.Length > _maxFileSizeLimitBytes)
            {
                errorMessage = $"File exceeds the maximum limit of {_maxFileSizeLimitBytes / (1024 * 1024)}MB.";
                return false;
            }

            return true;
        }

        public async Task<string> UploadFileAsync(IFormFile file, string ticketNumber)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("File is empty.");

            // Create target folder wwwroot/uploads/grievances/{ticketNumber}/
            var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "grievances", ticketNumber);
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            // Sanitize filename to prevent directory traversal
            var fileName = Path.GetFileName(file.FileName);
            var uniqueFileName = $"{Guid.NewGuid()}_{fileName}";
            var filePath = Path.Combine(folderPath, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Return relative URL path for web access
            return $"/uploads/grievances/{ticketNumber}/{uniqueFileName}";
        }
    }
}
