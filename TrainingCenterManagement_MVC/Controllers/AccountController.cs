using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using TrainingCenterManagement_MVC.Data;
using TrainingCenterManagement_MVC.Helpers;
using TrainingCenterManagement_MVC.Models;
using TrainingCenterManagement_MVC.ViewModels;

namespace TrainingCenterManagement_MVC.Controllers
{
    public class AccountController : Controller
    {
        private readonly IUserHelper _userHelper;
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly UserManager<ApplicationUser> _userManager;

        public AccountController(
            IUserHelper userHelper,
            ApplicationDbContext context,
            IConfiguration configuration,
            UserManager<ApplicationUser> userManager)
        {
            _userHelper = userHelper;
            _context = context;
            _configuration = configuration;
            _userManager = userManager;
        }

        // ── Login ─────────────────────────────────────────────────────────────

        public IActionResult Login(string returnUrl = null)
        {
            if (User.Identity.IsAuthenticated)
            {
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    return Redirect(returnUrl);

                return RedirectToAction("Index", "Home");
            }
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model, string returnUrl = null)
        {
            if (ModelState.IsValid)
            {
                var result = await _userHelper.LoginAsync(model);

                if (result.Succeeded)
                {
                    var user = await _userHelper.GetUserByEmailAsync(model.Email);

                    HttpContext.Session.SetString("Username", user.Email);
                    HttpContext.Session.SetString("UserId", user.Id);

                    if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                        return Redirect(returnUrl);

                    var userRole = await _userHelper.GetRoleAsync(user);
                    return userRole switch
                    {
                        "Admin" => RedirectToAction("AdminDashboard", "Dashboard"),
                        "Trainer" => RedirectToAction("TrainerDashboard", "Dashboard"),
                        "Trainee" => RedirectToAction("TraineeDashboard", "Dashboard"),
                        "Receptionist" => RedirectToAction("ReceptionistDashboard", "Dashboard"),
                        _ => RedirectToAction("Index", "Home")
                    };
                }
            }

            ModelState.AddModelError(string.Empty, "Failed to log in. Check your email and password.");
            ViewData["ReturnUrl"] = returnUrl;
            return View(model);
        }

        // ── Logout ────────────────────────────────────────────────────────────

        public async Task<IActionResult> Logout()
        {
            HttpContext.Session.Remove("Username");
            HttpContext.Session.Remove("UserId");
            await _userHelper.LogoutAsync();
            return RedirectToAction("Index", "Home");
        }

        // ── Register ──────────────────────────────────────────────────────────

        [Authorize(Roles = "Receptionist,Admin")]
        [HttpGet]
        public IActionResult Register()
        {
            var model = new RegisterNewUserViewModel
            {
                TemporaryPassword = GenerateRandomPassword()
            };
            model.Password = model.TemporaryPassword;
            model.Confirm = model.TemporaryPassword;
            return View(model);
        }

        [Authorize(Roles = "Receptionist,Admin")]
        [HttpPost]
        public async Task<IActionResult> Register(RegisterNewUserViewModel model)
        {
            if (ModelState.IsValid)
            {
                var existingUser = await _userHelper.GetUserByEmailAsync(model.Username);

                if (existingUser == null)
                {
                    var user = new ApplicationUser
                    {
                        FullName = model.FullName,
                        Email = model.Username,
                        UserName = model.Username,
                        BirthDate = model.BirthDate,
                        PhoneNumber = model.PhoneNumber,
                        Gender = model.Gender,
                        ProfilePictureUrl = model.ProfilePictureUrl,
                    };

                    var result = await _userHelper.AddUserAsync(user, model.Password);
                    if (result != IdentityResult.Success)
                    {
                        ModelState.AddModelError(string.Empty, "The user could not be created.");
                        return View(model);
                    }

                    // Assign role and create corresponding entity
                    string role = model.Role == RoleType.Trainee ? "Trainee" : "Trainer";
                    await _userHelper.AddUserToRoleAsync(user, role);

                    if (model.Role == RoleType.Trainee)
                    {
                        await _context.Trainees.AddAsync(new Trainee { User = user });
                    }
                    else if (model.Role == RoleType.Trainer)
                    {
                        await _context.Trainers.AddAsync(new Trainer { User = user });
                    }

                    await _context.SaveChangesAsync();

                    ViewBag.Message = "User created successfully.";
                    return RedirectToAction("Login");
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "This email is already registered.");
                }
            }

            return View(model);
        }

        // ── Change User Details ───────────────────────────────────────────────

        [Authorize]
        public async Task<IActionResult> ChangeUser()
        {
            var user = await _userHelper.GetUserByEmailAsync(User.Identity.Name);

            if (user == null)
                return RedirectToAction("NotAuthorized");

            var model = new ChangeUserViewModel
            {
                FullName = user.FullName,
                PhoneNumber = user.PhoneNumber
            };

            return View(model);
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> ChangeUser(ChangeUserViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _userHelper.GetUserByEmailAsync(User.Identity.Name);

                if (user != null)
                {
                    user.FullName = model.FullName;
                    user.PhoneNumber = model.PhoneNumber;

                    // FIX: Removed duplicate UpdateUserAsync call
                    var result = await _userHelper.UpdateUserAsync(user);

                    if (result.Succeeded)
                        return RedirectToAction("Index", "Home");

                    ModelState.AddModelError(string.Empty, "Failed to update user details.");
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "User not found.");
                }
            }

            return View(model);
        }

        // ── Reset Password ────────────────────────────────────────────────────

        [Authorize(Roles = "Admin,Trainer,Trainee,Receptionist")]
        public IActionResult ResetPassword(string token)
        {
            return View();
        }

        [Authorize(Roles = "Admin,Trainer,Trainee,Receptionist")]
        [HttpPost]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            var user = await _userHelper.GetUserByEmailAsync(model.Email);
            if (user != null)
            {
                var result = await _userHelper.ResetPasswordWithoutTokenAsync(user, model.Password);
                if (result.Succeeded)
                {
                    ViewBag.Message = "Password reset successfully.";
                    return View();
                }

                ViewBag.Message = "Error resetting the password.";
                return View(model);
            }

            ViewBag.Message = "User not found.";
            return View(model);
        }

        // ── Change Password ───────────────────────────────────────────────────

        [Authorize]
        public IActionResult ChangePassword()
        {
            return View();
        }

        [Authorize(Roles = "Receptionist,Admin")]
        [HttpPost]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _userHelper.GetUserByEmailAsync(User.Identity.Name);
                if (user != null)
                {
                    var result = await _userHelper.ChangePasswordAsync(user, model.OldPassword, model.NewPassword);
                    if (result.Succeeded)
                        return RedirectToAction("ChangeUser");

                    ModelState.AddModelError(string.Empty, result.Errors.FirstOrDefault()?.Description ?? "Error changing password.");
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "User not found.");
                }
            }

            return View(model);
        }

        // ── Access Control Pages ──────────────────────────────────────────────

        public IActionResult NotAuthorized() => View();

        public IActionResult AccessDenied() => View();

        // ── Private Helpers ───────────────────────────────────────────────────

        private string GenerateRandomPassword(int length = 8)
        {
            const string validChars = "ABCDEFGHJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*()?_-";
            var random = new Random();
            return new string(Enumerable.Repeat(validChars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }
    }
}
