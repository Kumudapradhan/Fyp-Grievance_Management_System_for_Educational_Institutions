using GMS.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;

namespace GMS.Tests
{
    [TestClass]
    public class SecurityTests
    {
        private IConfiguration GetMockConfiguration()
        {
            var inMemorySettings = new Dictionary<string, string?> {
                {"FileUpload:MaxFileSizeMB", "5"},
                {"FileUpload:AllowedExtensions:0", ".pdf"},
                {"FileUpload:AllowedExtensions:1", ".png"},
                {"FileUpload:AllowedExtensions:2", ".jpg"},
                {"FileUpload:AllowedExtensions:3", ".jpeg"},
                {"FileUpload:AllowedExtensions:4", ".docx"}
            };

            return new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();
        }

        [TestMethod]
        public void IsFileValid_ValidPdfSignature_ShouldReturnTrue()
        {
            // Arrange
            var config = GetMockConfiguration();
            var service = new FileUploadService(config);

            var fileMock = new Mock<IFormFile>();
            fileMock.Setup(f => f.FileName).Returns("evidence.pdf");
            fileMock.Setup(f => f.Length).Returns(100);

            // Valid PDF magic bytes: %PDF
            byte[] bytes = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x31, 0x32, 0x33, 0x34 };
            var stream = new MemoryStream(bytes);
            fileMock.Setup(f => f.OpenReadStream()).Returns(stream);

            // Act
            var isValid = service.IsFileValid(fileMock.Object, out string error);

            // Assert
            Assert.IsTrue(isValid, $"File validation failed: {error}");
            Assert.AreEqual(string.Empty, error);
        }

        [TestMethod]
        public void IsFileValid_InvalidPdfSignature_ShouldReturnFalse()
        {
            // Arrange
            var config = GetMockConfiguration();
            var service = new FileUploadService(config);

            var fileMock = new Mock<IFormFile>();
            fileMock.Setup(f => f.FileName).Returns("evidence.pdf");
            fileMock.Setup(f => f.Length).Returns(100);

            // Invalid PDF magic bytes: plain text
            byte[] bytes = new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F, 0x20, 0x57, 0x6F };
            var stream = new MemoryStream(bytes);
            fileMock.Setup(f => f.OpenReadStream()).Returns(stream);

            // Act
            var isValid = service.IsFileValid(fileMock.Object, out string error);

            // Assert
            Assert.IsFalse(isValid);
            Assert.IsTrue(error.Contains("Invalid PDF file signature"));
        }

        [TestMethod]
        public void IsFileValid_ValidPngSignature_ShouldReturnTrue()
        {
            // Arrange
            var config = GetMockConfiguration();
            var service = new FileUploadService(config);

            var fileMock = new Mock<IFormFile>();
            fileMock.Setup(f => f.FileName).Returns("evidence.png");
            fileMock.Setup(f => f.Length).Returns(100);

            // Valid PNG magic bytes: 89 50 4E 47
            byte[] bytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
            var stream = new MemoryStream(bytes);
            fileMock.Setup(f => f.OpenReadStream()).Returns(stream);

            // Act
            var isValid = service.IsFileValid(fileMock.Object, out string error);

            // Assert
            Assert.IsTrue(isValid, $"File validation failed: {error}");
        }

        [TestMethod]
        public void IsFileValid_InvalidPngSignature_ShouldReturnFalse()
        {
            // Arrange
            var config = GetMockConfiguration();
            var service = new FileUploadService(config);

            var fileMock = new Mock<IFormFile>();
            fileMock.Setup(f => f.FileName).Returns("evidence.png");
            fileMock.Setup(f => f.Length).Returns(100);

            // Invalid PNG magic bytes
            byte[] bytes = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x0D, 0x0A, 0x1A, 0x0A };
            var stream = new MemoryStream(bytes);
            fileMock.Setup(f => f.OpenReadStream()).Returns(stream);

            // Act
            var isValid = service.IsFileValid(fileMock.Object, out string error);

            // Assert
            Assert.IsFalse(isValid);
            Assert.IsTrue(error.Contains("Invalid PNG image file signature"));
        }
    }
}
