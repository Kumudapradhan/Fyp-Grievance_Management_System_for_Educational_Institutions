using GMS.Web.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GMS.Web.Data
{
    public static class DbInitializer
    {
        public static async Task SeedAsync(ApplicationDbContext context, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            // Ensure Database is Created/Migrated
            await context.Database.MigrateAsync();

            // 1. Seed Roles
            var roles = new[] { "Student", "Administrator", "Staff" };
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            // 2. Seed Default Administrator Account
            var adminEmail = "admin@gms.edu.my";
            var adminUser = await userManager.FindByEmailAsync(adminEmail);
            if (adminUser == null)
            {
                adminUser = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    FullName = "System Administrator",
                    EmailConfirmed = true,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                var result = await userManager.CreateAsync(adminUser, "Admin@123");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "Administrator");
                }
            }

            // 3. Seed Departments (if not existing)
            var seededDepartments = new List<Department>();
            var departmentsData = new[]
            {
                new { Name = "Academic Affairs", Desc = "Handles course registrations, grades, exam schedules and curriculum issues." },
                new { Name = "Finance and Accounts", Desc = "Handles tuition payments, refunds, scholarships, and financial holds." },
                new { Name = "Student Welfare", Desc = "Handles student counseling, housing, club activities, and health facilities." },
                new { Name = "IT Support", Desc = "Handles LMS login issues, campus Wi-Fi access, and software requests." },
                new { Name = "General Administration", Desc = "Handles facilities, general complaints, shuttle service, and admissions." }
            };

            foreach (var dept in departmentsData)
            {
                var existingDept = await context.Departments.FirstOrDefaultAsync(d => d.Name == dept.Name);
                if (existingDept == null)
                {
                    var newDept = new Department
                    {
                        Name = dept.Name,
                        Description = dept.Desc,
                        StaffUserId = null
                    };
                    context.Departments.Add(newDept);
                    seededDepartments.Add(newDept);
                }
                else
                {
                    seededDepartments.Add(existingDept);
                }
            }
            await context.SaveChangesAsync();

            // 4. Seed Categories with Default Department Mappings
            var academicDept = seededDepartments.First(d => d.Name == "Academic Affairs");
            var financeDept = seededDepartments.First(d => d.Name == "Finance and Accounts");
            var welfareDept = seededDepartments.First(d => d.Name == "Student Welfare");
            var itDept = seededDepartments.First(d => d.Name == "IT Support");
            var adminDept = seededDepartments.First(d => d.Name == "General Administration");

            var categoriesData = new[]
            {
                new { Name = "Academic Issue", DeptId = academicDept.Id },
                new { Name = "Financial Issue", DeptId = financeDept.Id },
                new { Name = "Welfare / Personal Issue", DeptId = welfareDept.Id },
                new { Name = "IT / System Issue", DeptId = itDept.Id },
                new { Name = "Administrative Issue", DeptId = adminDept.Id },
                new { Name = "Other", DeptId = adminDept.Id }
            };

            foreach (var cat in categoriesData)
            {
                var existingCat = await context.Categories.FirstOrDefaultAsync(c => c.Name == cat.Name);
                if (existingCat == null)
                {
                    context.Categories.Add(new Category
                    {
                        Name = cat.Name,
                        DefaultDepartmentId = cat.DeptId
                    });
                }
            }
            await context.SaveChangesAsync();
        }
    }
}
