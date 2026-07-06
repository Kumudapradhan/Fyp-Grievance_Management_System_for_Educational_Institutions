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

            // 5. Seed System SLA and System Admin Session Users to avoid constraint checks crash
            var systemSlaEmail = "system.sla@gms.edu.my";
            var slaUser = await userManager.FindByIdAsync("SystemSLA");
            if (slaUser == null)
            {
                slaUser = new ApplicationUser
                {
                    Id = "SystemSLA",
                    UserName = systemSlaEmail,
                    Email = systemSlaEmail,
                    FullName = "System SLA Engine",
                    EmailConfirmed = true,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                await userManager.CreateAsync(slaUser, "SystemSla@123");
                await userManager.AddToRoleAsync(slaUser, "Administrator");
            }

            var systemAdminEmail = "system.admin@gms.edu.my";
            var sysAdminUser = await userManager.FindByIdAsync("Admin");
            if (sysAdminUser == null)
            {
                sysAdminUser = new ApplicationUser
                {
                    Id = "Admin",
                    UserName = systemAdminEmail,
                    Email = systemAdminEmail,
                    FullName = "System Admin Session",
                    EmailConfirmed = true,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                await userManager.CreateAsync(sysAdminUser, "SystemAdmin@123");
                await userManager.AddToRoleAsync(sysAdminUser, "Administrator");
            }

            // 6. Seed default system settings
            var defaultSettings = new[]
            {
                new { Key = "SLA_OverdueDays", Value = "7", Desc = "Default calendar day limits before setting Overdue flags on open tickets" },
                new { Key = "SLA_RepetitiveWindowDays", Value = "30", Desc = "Window in days to scan duplicate complaints categories" },
                new { Key = "SLA_RepetitiveThresholdCount", Value = "3", Desc = "Complaint count limit threshold to escalate category priority" }
            };

            foreach (var setting in defaultSettings)
            {
                var existingSetting = await context.SystemSettings.FirstOrDefaultAsync(s => s.Key == setting.Key);
                if (existingSetting == null)
                {
                    context.SystemSettings.Add(new SystemSetting
                    {
                        Key = setting.Key,
                        Value = setting.Value,
                        Description = setting.Desc,
                        LastUpdatedAt = DateTime.UtcNow
                    });
                }
            }
            await context.SaveChangesAsync();

            // 7. Seed Subcategories
            var categories = await context.Categories.ToListAsync();
            var academicCat = categories.FirstOrDefault(c => c.Name == "Academic Issue");
            var financeCat = categories.FirstOrDefault(c => c.Name == "Financial Issue");
            var welfareCat = categories.FirstOrDefault(c => c.Name == "Welfare / Personal Issue");
            var itCat = categories.FirstOrDefault(c => c.Name == "IT / System Issue");
            var adminCat = categories.FirstOrDefault(c => c.Name == "Administrative Issue");

            if (academicCat != null)
            {
                await SeedSubcategoryIfNotExists(context, "Module Registration", academicCat.Id);
                await SeedSubcategoryIfNotExists(context, "Exam Timetable", academicCat.Id);
                await SeedSubcategoryIfNotExists(context, "Grade Discrepancy", academicCat.Id);
            }
            if (financeCat != null)
            {
                await SeedSubcategoryIfNotExists(context, "Tuition Fee Payments", financeCat.Id);
                await SeedSubcategoryIfNotExists(context, "Refund Delays", financeCat.Id);
                await SeedSubcategoryIfNotExists(context, "Scholarship Disbursal", financeCat.Id);
            }
            if (welfareCat != null)
            {
                await SeedSubcategoryIfNotExists(context, "Student Housing", welfareCat.Id);
                await SeedSubcategoryIfNotExists(context, "Mental Health Counseling", welfareCat.Id);
            }
            if (itCat != null)
            {
                await SeedSubcategoryIfNotExists(context, "Campus Wi-Fi Outage", itCat.Id);
                await SeedSubcategoryIfNotExists(context, "LMS Login Failure", itCat.Id);
            }
            if (adminCat != null)
            {
                await SeedSubcategoryIfNotExists(context, "Facilities/Maintenance", adminCat.Id);
                await SeedSubcategoryIfNotExists(context, "Shuttle Bus Schedule", adminCat.Id);
            }

            await context.SaveChangesAsync();
        }

        private static async Task SeedSubcategoryIfNotExists(ApplicationDbContext context, string name, int categoryId)
        {
            var exists = await context.Subcategories.AnyAsync(s => s.Name == name && s.CategoryId == categoryId);
            if (!exists)
            {
                context.Subcategories.Add(new Subcategory { Name = name, CategoryId = categoryId });
            }
        }
    }
}
