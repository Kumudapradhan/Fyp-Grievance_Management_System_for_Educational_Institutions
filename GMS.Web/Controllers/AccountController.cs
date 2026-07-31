using GMS.Web.Data;
using GMS.Web.Models.Entities;
using GMS.Web.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
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
        private readonly Microsoft.AspNetCore.Identity.UI.Services.IEmailSender _emailSender;
        private readonly ILogger<AccountController> _logger;
        private readonly IWebHostEnvironment _env;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            RoleManager<IdentityRole> roleManager,
            ApplicationDbContext context,
            Microsoft.AspNetCore.Identity.UI.Services.IEmailSender emailSender,
            ILogger<AccountController> logger,
            IWebHostEnvironment env)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _context = context;
            _emailSender = emailSender;
            _logger = logger;
            _env = env;
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
            // Enforce allowed roles on the server; hidden form fields can be changed by a client.
            var allowedRoles = User.IsInRole("Administrator")
                ? new[] { "Student", "Staff" }
                : new[] { "Student" };

            if (!allowedRoles.Contains(model.Role))
            {
                return Forbid();
            }

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
                            user.Programme = model.Programme;
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

                        // Auto login or redirect to Admin Users list
                        if (User.IsInRole("Administrator"))
                        {
                            TempData["SuccessMessage"] = $"Academic staff user {user.FullName} ({user.Email}) registered successfully.";
                            return RedirectToAction("Users", "Admin");
                        }
                        else
                        {
                            await _signInManager.SignInAsync(user, isPersistent: false);
                            return RedirectToDashboard();
                        }
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

                var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, lockoutOnFailure: true);
                if (result.Succeeded)
                {
                    return RedirectToDashboard();
                }
                if (result.IsLockedOut)
                {
                    _logger.LogWarning("User account locked out: {Email}", model.Email);
                    ModelState.AddModelError(string.Empty, "This account is temporarily locked out due to multiple failed login attempts. Please try again in 15 minutes.");
                    return View(model);
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

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByEmailAsync(model.Email);
                if (user == null)
                {
                    // Don't reveal that the user does not exist
                    return View("ForgotPasswordConfirmation");
                }

                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var callbackUrl = Url.Action("ResetPassword", "Account", new { token, email = user.Email }, protocol: Request.Scheme);

                await _emailSender.SendEmailAsync(model.Email, "Reset Password",
                    $"Please reset your password by clicking here: <a href='{callbackUrl}'>link</a>");

                // Save to TempData for demo visibility in case console log is truncated or missing
                TempData["ResetLink"] = callbackUrl;

                return View("ForgotPasswordConfirmation");
            }

            return View(model);
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ForgotPasswordConfirmation()
        {
            return View();
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ResetPassword(string? token = null, string? email = null)
        {
            if (token == null || email == null)
            {
                return RedirectToAction("Login");
            }
            return View(new ResetPasswordViewModel { Token = token, Email = email });
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                // Don't reveal that the user does not exist
                return RedirectToAction("ResetPasswordConfirmation");
            }

            var result = await _userManager.ResetPasswordAsync(user, model.Token, model.Password);
            if (result.Succeeded)
            {
                return RedirectToAction("ResetPasswordConfirmation");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View(model);
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ResetPasswordConfirmation()
        {
            return View();
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Profile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var model = new EditProfileViewModel
            {
                FullName = user.FullName,
                Email = user.Email ?? string.Empty,
                StudentId = user.StudentId,
                Programme = user.Programme,
                Department = user.Department,
                ProfilePicturePath = user.ProfilePicturePath
            };

            var departments = await _context.Departments.Select(d => d.Name).ToListAsync();
            ViewBag.Departments = new SelectList(departments);

            return View(model);
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProfile(EditProfileViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var departments = await _context.Departments.Select(d => d.Name).ToListAsync();
                ViewBag.Departments = new SelectList(departments);
                return View("Profile", model);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            user.FullName = model.FullName;
            
            // Allow Student specific fields updates
            if (User.IsInRole("Student"))
            {
                user.StudentId = model.StudentId;
                user.Programme = model.Programme;
            }
            // Allow Staff specific fields updates
            else if (User.IsInRole("Staff"))
            {
                user.Department = model.Department;
            }

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "Profile updated successfully.";
                return RedirectToAction(nameof(Profile));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            var depts = await _context.Departments.Select(d => d.Name).ToListAsync();
            ViewBag.Departments = new SelectList(depts);
            return View("Profile", model);
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(3_000_000)]
        public async Task<IActionResult> UploadProfilePicture(IFormFile profilePhoto)
        {
            if (profilePhoto == null || profilePhoto.Length == 0)
            {
                TempData["ErrorMessage"] = "Please select a photo to upload.";
                return RedirectToAction(nameof(Profile));
            }

            var allowed = new[] { "image/jpeg", "image/jpg", "image/png" };
            if (!allowed.Contains(profilePhoto.ContentType.ToLower()))
            {
                TempData["ErrorMessage"] = "Only JPG or PNG files are accepted.";
                return RedirectToAction(nameof(Profile));
            }

            if (profilePhoto.Length > 2_097_152) // 2 MB
            {
                TempData["ErrorMessage"] = "Photo must be under 2 MB.";
                return RedirectToAction(nameof(Profile));
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            // Save to wwwroot/uploads/profiles/
            var uploadsDir = Path.Combine(_env.WebRootPath, "uploads", "profiles");
            Directory.CreateDirectory(uploadsDir);

            // Delete old file if exists
            if (!string.IsNullOrWhiteSpace(user.ProfilePicturePath))
            {
                var oldFile = Path.Combine(_env.WebRootPath, user.ProfilePicturePath.TrimStart('/'));
                if (System.IO.File.Exists(oldFile))
                    System.IO.File.Delete(oldFile);
            }

            var ext      = Path.GetExtension(profilePhoto.FileName);
            var fileName = $"{user.Id}{ext}";
            var filePath = Path.Combine(uploadsDir, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
                await profilePhoto.CopyToAsync(stream);

            user.ProfilePicturePath = $"/uploads/profiles/{fileName}";
            await _userManager.UpdateAsync(user);

            TempData["SuccessMessage"] = "Profile photo updated successfully.";
            return RedirectToAction(nameof(Profile));
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Password inputs did not meet requirements.";
                return RedirectToAction(nameof(Profile));
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var result = await _userManager.ChangePasswordAsync(user, model.OldPassword, model.NewPassword);
            if (result.Succeeded)
            {
                await _signInManager.RefreshSignInAsync(user);
                TempData["SuccessMessage"] = "Password changed successfully.";
                return RedirectToAction(nameof(Profile));
            }

            foreach (var error in result.Errors)
            {
                TempData["ErrorMessage"] = error.Description;
            }

            return RedirectToAction(nameof(Profile));
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
