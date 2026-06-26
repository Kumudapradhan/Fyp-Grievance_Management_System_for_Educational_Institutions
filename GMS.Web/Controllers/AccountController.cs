using GMS.Web.Data;
using GMS.Web.Models.Entities;
using GMS.Web.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace GMS.Web.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _context;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            RoleManager<IdentityRole> roleManager,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _context = context;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Register()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToDashboard();
            }

            // Populate departments dropdown for staff selection
            var departments = await _context.Departments.Select(d => d.Name).ToListAsync();
            ViewBag.Departments = new SelectList(departments);
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = new ApplicationUser
                {
                    UserName = model.Email,
                    Email = model.Email,
                    FullName = model.FullName,
                    IsActive = true
                };

                if (model.Role == "Student")
                {
                    if (string.IsNullOrWhiteSpace(model.StudentId))
                    {
                        ModelState.AddModelError("StudentId", "Student ID is required for student registration.");
                    }
                    else
                    {
                        // Check if student ID is unique
                        var duplicateId = await _userManager.Users.AnyAsync(u => u.StudentId == model.StudentId);
                        if (duplicateId)
                        {
                            ModelState.AddModelError("StudentId", "This Student ID is already registered.");
                        }
                        else
                        {
                            user.StudentId = model.StudentId;
                            // Custom property mapping for Student
                        }
                    }
                }
                else if (model.Role == "Staff")
                {
                    if (string.IsNullOrWhiteSpace(model.Department))
                    {
                        ModelState.AddModelError("Department", "Department selection is required for staff registration.");
                    }
                    else
                    {
                        user.Department = model.Department;
                    }
                }

                if (ModelState.ErrorCount == 0)
                {
                    var result = await _userManager.CreateAsync(user, model.Password);
                    if (result.Succeeded)
                    {
                        // Add to role
                        await _userManager.AddToRoleAsync(user, model.Role);

                        // If user is Staff, automatically assign them as staff user for their department if it doesn't have one
                        if (model.Role == "Staff" && !string.IsNullOrEmpty(model.Department))
                        {
                            var dept = await _context.Departments.FirstOrDefaultAsync(d => d.Name == model.Department);
                            if (dept != null && string.IsNullOrEmpty(dept.StaffUserId))
                            {
                                dept.StaffUserId = user.Id;
                                await _context.SaveChangesAsync();
                            }
                        }

                        // Auto login
                        await _signInManager.SignInAsync(user, isPersistent: false);
                        return RedirectToDashboard();
                    }

                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                }
            }

            var departments = await _context.Departments.Select(d => d.Name).ToListAsync();
            ViewBag.Departments = new SelectList(departments);
            return View(model);
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToDashboard();
            }
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByEmailAsync(model.Email);
                if (user != null && !user.IsActive)
                {
                    ModelState.AddModelError(string.Empty, "This account is inactive. Please contact administration.");
                    return View(model);
                }

                var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, lockoutOnFailure: false);
                if (result.Succeeded)
                {
                    return RedirectToDashboard();
                }

                ModelState.AddModelError(string.Empty, "Invalid login credentials.");
            }
            return View(model);
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Login", "Account");
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            Response.StatusCode = 403;
            return View();
        }

        private IActionResult RedirectToDashboard()
        {
            if (User.IsInRole("Administrator"))
            {
                return RedirectToAction("Index", "Admin");
            }
            else if (User.IsInRole("Staff"))
            {
                return RedirectToAction("Index", "Staff");
            }
            else
            {
                return RedirectToAction("Index", "Grievance");
            }
        }
    }
}
