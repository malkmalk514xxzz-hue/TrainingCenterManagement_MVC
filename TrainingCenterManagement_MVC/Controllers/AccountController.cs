using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
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

        [HttpGet]
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
        [ValidateAntiForgeryToken]
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

            ModelState.AddModelError(string.Empty, "فشل تسجيل الدخول. تحقق من البريد الإلكتروني وكلمة المرور.");
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

        // ── Register (Admin / Receptionist only) ──────────────────────────────

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
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterNewUserViewModel model)
        {
            if (ModelState.IsValid)
            {
                if (model.Role == RoleType.Trainer && string.IsNullOrWhiteSpace(model.ShamCashAccountCode))
                {
                    ModelState.AddModelError(nameof(model.ShamCashAccountCode), "كود حساب الشام كاش مطلوب للمدرب.");
                    return View(model);
                }

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
                        ModelState.AddModelError(string.Empty, "تعذّر إنشاء المستخدم.");
                        return View(model);
                    }

                    string role = model.Role == RoleType.Trainee ? "Trainee" : "Trainer";
                    await _userHelper.AddUserToRoleAsync(user, role);

                    if (model.Role == RoleType.Trainee)
                        await _context.Trainees.AddAsync(new Trainee { User = user, TransferCode = await GenerateUniqueTransferCodeAsync() });
                    else if (model.Role == RoleType.Trainer)
                        await _context.Trainers.AddAsync(new Trainer
                        {
                            User = user,
                            Specialty = "IT",
                            YearsOfExperience = 0,
                            ShamCashAccountCode = model.ShamCashAccountCode.Trim()
                        });

                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "تم إنشاء المستخدم بنجاح.";
                    return RedirectToAction("GetUsers", "Admin");
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "هذا البريد الإلكتروني مسجل مسبقاً.");
                }
            }

            return View(model);
        }

        // ── Public Sign Up (Trainee only) ─────────────────────────────────────

        [HttpGet]
        [AllowAnonymous]
        public IActionResult SignUp()
        {
            if (User.Identity.IsAuthenticated)
                return RedirectToAction("Index", "Home");
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SignUp(SignUpViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var existing = await _userHelper.GetUserByEmailAsync(model.Email);
            if (existing != null)
            {
                ModelState.AddModelError(string.Empty, "هذا البريد الإلكتروني مسجل مسبقاً.");
                return View(model);
            }

            var user = new ApplicationUser
            {
                FullName = model.FullName,
                Email = model.Email,
                UserName = model.Email,
                BirthDate = model.BirthDate,
                PhoneNumber = model.PhoneNumber,
                Gender = model.Gender,
                ProfilePictureUrl = string.Empty,
            };

            var result = await _userHelper.AddUserAsync(user, model.Password);
            if (result != IdentityResult.Success)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);
                return View(model);
            }

            await _userHelper.AddUserToRoleAsync(user, "Trainee");
            await _context.Trainees.AddAsync(new Trainee { User = user, TransferCode = await GenerateUniqueTransferCodeAsync() });
            await _context.SaveChangesAsync();

            // Notify all admins
            var adminUsers = await _userManager.GetUsersInRoleAsync("Admin");
            if (adminUsers.Any())
            {
                var notifications = adminUsers.Select(a => new UserNotification
                {
                    NotificationId = Guid.NewGuid(),
                    UserId = a.Id,
                    Title = "طالب جديد انضم للمنصة",
                    Message = $"سجّل {user.FullName} ({user.Email}) حساباً جديداً كمتدرب.",
                    Type = NotificationType.General,
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow,
                }).ToList();

                _context.Notifications.AddRange(notifications);
                await _context.SaveChangesAsync();
            }

            TempData["SignUpSuccess"] = "تم إنشاء حسابك بنجاح! يمكنك تسجيل الدخول الآن.";
            return RedirectToAction("Login");
        }

        // ── Forgot Password ───────────────────────────────────────────────────

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
            if (!ModelState.IsValid)
                return View(model);

            var user = await _userHelper.GetUserByEmailAsync(model.Email);

            // Always show the same message to prevent user enumeration
            TempData["SignUpSuccess"] = "إذا كان البريد الإلكتروني مسجلاً، ستصلك تعليمات إعادة تعيين كلمة المرور.";

            if (user != null)
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var callbackUrl = Url.Action(
                    "ResetPassword", "Account",
                    new { token, email = user.Email },
                    protocol: Request.Scheme);

                // TODO: Send email with callbackUrl via your email service
                // await _emailService.SendPasswordResetAsync(user.Email, callbackUrl);
            }

            return RedirectToAction("Login");
        }

        // ── Reset Password ────────────────────────────────────────────────────

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ResetPassword(string token = null, string email = null)
        {
            if (token == null)
                return BadRequest("رمز إعادة التعيين مفقود.");

            var model = new ResetPasswordViewModel { Email = email };
            return View(model);
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _userHelper.GetUserByEmailAsync(model.Email);
            if (user != null)
            {
                var result = await _userHelper.ResetPasswordWithoutTokenAsync(user, model.Password);
                if (result.Succeeded)
                {
                    TempData["SignUpSuccess"] = "تم إعادة تعيين كلمة المرور بنجاح.";
                    return RedirectToAction("Login");
                }

                foreach (var error in result.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);

                return View(model);
            }

            ModelState.AddModelError(string.Empty, "المستخدم غير موجود.");
            return View(model);
        }

        // ── Change User Details ───────────────────────────────────────────────

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> ChangeUser()
        {
            var user = await _userHelper.GetUserByEmailAsync(User.Identity.Name);
            if (user == null) return RedirectToAction("NotAuthorized");

            var model = new ChangeUserViewModel
            {
                FullName = user.FullName,
                PhoneNumber = user.PhoneNumber
            };
            return View(model);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeUser(ChangeUserViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _userHelper.GetUserByEmailAsync(User.Identity.Name);
                if (user != null)
                {
                    user.FullName = model.FullName;
                    user.PhoneNumber = model.PhoneNumber;

                    var result = await _userHelper.UpdateUserAsync(user);
                    if (result.Succeeded)
                        return RedirectToAction("Index", "Home");

                    ModelState.AddModelError(string.Empty, "فشل تحديث بيانات المستخدم.");
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "المستخدم غير موجود.");
                }
            }
            return View(model);
        }

        // ── Change Password ───────────────────────────────────────────────────

        [Authorize]
        [HttpGet]
        public IActionResult ChangePassword() => View();

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
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

                    ModelState.AddModelError(string.Empty,
                        result.Errors.FirstOrDefault()?.Description ?? "حدث خطأ أثناء تغيير كلمة المرور.");
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "المستخدم غير موجود.");
                }
            }
            return View(model);
        }

        // ── Access Control ────────────────────────────────────────────────────

        public IActionResult NotAuthorized() => View();

        public IActionResult AccessDenied() => View();

        // ── Private Helpers ───────────────────────────────────────────────────

        private static string GenerateRandomPassword(int length = 8)
        {
            const string chars = "ABCDEFGHJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*()?_-";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        private async Task<string> GenerateUniqueTransferCodeAsync()
        {
            string code;
            do
            {
                code = TransferCodeGenerator.Generate();
            }
            while (await _context.Trainees.AnyAsync(t => t.TransferCode == code));

            return code;
        }
    }
}
