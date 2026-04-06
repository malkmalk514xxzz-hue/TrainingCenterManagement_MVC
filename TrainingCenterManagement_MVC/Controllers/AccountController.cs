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
        private readonly ApplicationDbContext context;
        private readonly IConfiguration _configuration;
        private readonly UserManager<ApplicationUser> _userManager;

        public AccountController(
            IUserHelper userHelper,
            ApplicationDbContext context,
            IConfiguration configuration,
            UserManager<ApplicationUser> userManager)
        {
            _userHelper = userHelper;
            this.context = context;
            _configuration = configuration;
            _userManager = userManager;
        }

        // Displays the login page
        public IActionResult Login(string returnUrl = null)
        {
            if (User.Identity.IsAuthenticated)
            {
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }
                return RedirectToAction("Index", "Home");
            }
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        // Processes the login
        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model, string returnUrl = null)
        {
            if (ModelState.IsValid)
            {
                var result = await _userHelper.LoginAsync(model);

                if (result.Succeeded)
                {
                    var user = await _userHelper.GetUserByEmailAsync(model.Email);

                    // Store user info in session
                    HttpContext.Session.SetString("Username", user.Email);
                    HttpContext.Session.SetString("UserId", user.Id);

                    // Handle ReturnUrl
                    if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    {
                        return Redirect(returnUrl);
                    }

                    var userRole = await _userHelper.GetRoleAsync(user);
                    switch (userRole)
                    {
                        case "Admin":
                            return RedirectToAction("AdminDashboard", "Dashboard");
                        case "Trainer":
                            return RedirectToAction("TrainerDashboard", "Dashboard");
                        case "Trainee":
                            return RedirectToAction("TraineeDashboard", "Dashboard");
                        case "Receptionist":
                            return RedirectToAction("ReceptionistDashboard", "Dashboard");
                        default:
                            return RedirectToAction("Index", "Home");
                    }
                }
            }

            ModelState.AddModelError(string.Empty, "Failed to log in.");
            ViewData["ReturnUrl"] = returnUrl;
            return View(model);
        }

        // Logs out the user
        public async Task<IActionResult> Logout()
        {
            // Store user info in session
            HttpContext.Session.Remove("Username");
            HttpContext.Session.Remove("UserId");
            await _userHelper.LogoutAsync();
            return RedirectToAction("Index", "Home");
        }

        // Displays the registration page
        [Authorize(Roles = "Receptionist,Admin")]
        [HttpGet]
        public IActionResult Register()
        {
            var model = new RegisterNewUserViewModel
            {
                TemporaryPassword = GenerateRandomPassword()
            };

            // Fill in temporary password automatically
            model.Password = model.TemporaryPassword;
            model.Confirm = model.TemporaryPassword;

            return View(model);
        }

        // Processes the registration
        [Authorize(Roles = "Receptionist,Admin")]
        [HttpPost]

            public async Task<IActionResult> Register(RegisterNewUserViewModel model)
            {
                if (ModelState.IsValid)
                {
                    var user = await _userHelper.GetUserByEmailAsync(model.Username);

                    if (user == null)
                    {
                        user = new ApplicationUser
                        {
                            FullName = model.FullName,
                            Email = model.Username,
                            UserName = model.Username,
                            BirthDate = model.BirthDate,
                            PhoneNumber = model.PhoneNumber,
                        };

                        // Create user with password
                        var result = await _userHelper.AddUserAsync(user, model.Password);
                        if (result != IdentityResult.Success)
                        {
                            ModelState.AddModelError(string.Empty, "The user could not be created.");
                            return View(model);
                        }

                        // Assign the selected role
                        string role = model.Role == RoleType.Trainee ? "Trainee" : "Trainer";
                        await _userHelper.AddUserToRoleAsync(user, role);

                        // Add to appropriate table based on role
                        if (model.Role == RoleType.Trainee)
                        {
                            await context.Trainees.AddAsync(new Trainee
                            {
                                User = user,
                            });
                        }
                        else if (model.Role == RoleType.Trainer)
                        {
                            await context.Trainers.AddAsync(new Trainer
                            {
                                User = user,
                            });
                        }

                        await context.SaveChangesAsync();

                        ViewBag.Message = "User created successfully. An email was sent with further instructions.";
                        return RedirectToAction("Login");
                    }
                    else
                    {
                        ModelState.AddModelError(string.Empty, "This email is already registered.");
                        return View(model);
                    }
                }

                return View(model);
            
        }

        // Displays the change user details page
        public async Task<IActionResult> ChangeUser()
        {
            var user = await _userHelper.GetUserByEmailAsync(User.Identity.Name);

            if (user == null)
            {
                return RedirectToAction("NotAuthorized");
            }

            var model = new ChangeUserViewModel
            {
                FullName = user.FullName,
                
               
                PhoneNumber = user.PhoneNumber
            };

            return View(model);
        }

        // Processes the change user details request
        [HttpPost]
        public async Task<IActionResult> ChangeUser(ChangeUserViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _userHelper.GetUserByEmailAsync(User.Identity.Name);

                if (user != null)
                {
                    // Update the User entity
                    user.FullName = model.FullName;
                   
                    
                    user.PhoneNumber = model.PhoneNumber;

                    // Save changes to the user
                    var result = await _userHelper.UpdateUserAsync(user);

                    if (result.Succeeded)
                    {
                        // Call the method to update associated entities (Employee, Student, Teacher)
                        await _userHelper.UpdateUserAsync(user);

                        return RedirectToAction("Index", "Home");
                    }

                    ModelState.AddModelError(string.Empty, "Failed to update user details.");
                }
                else
                {
                    return RedirectToAction("NotAuthorized");
                }
            }

            return View(model);
        }

        // Displays the not authorized page
        public IActionResult NotAuthorized()
        {
            return View();
        }



        // Displays the password reset page
        [Authorize(Roles = "Admin, Trainer, Trainee, Receptionist")]
        public IActionResult ResetPassword(string token)
        {
            return View();
        }

        // Processes the password reset request
        [Authorize(Roles = "Admin, Trainer, Trainee, Receptionist")]
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

        public IActionResult AccessDenied()
        {
            return View();
        }



        private string GenerateRandomPassword(int length = 8)
        {
            const string validChars = "ABCDEFGHJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*()?_-";
            Random random = new Random();
            return new string(Enumerable.Repeat(validChars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }



        public IActionResult ChangePassword()
        {
            return View();
        }

        // Processes the change password request
        [Authorize(Roles = "Receptionist,Admin")]
        [HttpPost]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _userHelper.GetUserByEmailAsync(this.User.Identity.Name);
                if (user != null)
                {
                    var result = await _userHelper.ChangePasswordAsync(user, model.OldPassword, model.NewPassword);
                    if (result.Succeeded)
                    {
                        return RedirectToAction("ChangeUser");
                    }
                    ModelState.AddModelError(string.Empty, result.Errors.FirstOrDefault().Description);
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "User not found.");
                }
            }

            return View(model);
        }


      

    }
}
