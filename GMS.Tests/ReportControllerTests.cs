using GMS.Web.Controllers;
using GMS.Web.Data;
using GMS.Web.Models.Entities;
using GMS.Web.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading.Tasks;

namespace GMS.Tests
{
    [TestClass]
    public class ReportControllerTests
    {
        private ApplicationDbContext GetInMemoryContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            return new ApplicationDbContext(options);
        }

        [TestMethod]
        public async Task Index_ShouldReturnReportViewModel_WithNonNullLists()
        {
            // Arrange
            using var context = GetInMemoryContext();

            var dept = new Department { Id = 1, Name = "IT Support" };
            var cat = new Category { Id = 1, Name = "IT Issue", DefaultDepartmentId = 1 };
            context.Departments.Add(dept);
            context.Categories.Add(cat);

            var grievance = new Grievance
            {
                Id = 1,
                TicketNumber = "T-100",
                Title = "Test title that must meet the character count criteria which is minimum fifty characters.",
                Description = "Test description that must meet the character count criteria which is minimum fifty characters.",
                CategoryId = 1,
                DepartmentId = 1,
                StudentId = "student-1",
                IsAnonymous = false,
                Status = GrievanceStatus.Resolved,
                SubmittedAt = DateTime.UtcNow.AddMonths(-1),
                LastUpdatedAt = DateTime.UtcNow
            };
            context.Grievances.Add(grievance);
            await context.SaveChangesAsync();

            var controller = new ReportController(context);

            // Act
            var result = await controller.Index();

            // Assert
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            var viewResult = (ViewResult)result;

            Assert.IsInstanceOfType(viewResult.Model, typeof(ReportViewModel));
            var model = (ReportViewModel)viewResult.Model;

            Assert.IsNotNull(model);
            Assert.IsTrue(model.TotalSubmitted >= 1);
            Assert.IsTrue(model.TotalResolved >= 1);
            Assert.IsNotNull(model.GrievancesByDepartment);
            Assert.IsNotNull(model.GrievancesByStatus);
            Assert.IsNotNull(model.GrievancesByCategory);
            Assert.IsNotNull(model.MonthlyVolume);
            Assert.IsNotNull(model.AvgResolutionByDept);
        }
    }
}
