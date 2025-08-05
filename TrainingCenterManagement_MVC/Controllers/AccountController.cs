using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TrainingCenterManagement_MVC.Helpers;
using TrainingCenterManagement_MVC.Models;
using TrainingCenterManagement_MVC.ViewModels;

namespace TrainingCenterManagement_MVC.Controllers
{
    public class AccountController : Controller
    {
        private readonly IUserHelper _userHelper;
        //private readonly IMailHelper _mailHelper;
        private readonly IConfiguration _configuration;

        public AccountController(
            IUserHelper userHelper,
           // IMailHelper mailHelper,
            IConfiguration configuration)
        {
            _userHelper = userHelper;
            //_mailHelper = mailHelper;
            _configuration = configuration;
        }

        // Displays the login page
        public IActionResult Login()
        {
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }
            return View();
        }

        // Processes the login
        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (ModelState.IsValid)
            {
                var result = await _userHelper.LoginAsync(model);

                if (result.Succeeded)
                {
                    // Check the user's role to determine the redirection
                    var user = await _userHelper.GetUserByEmailAsync(model.Email);
                    var userRole = await _userHelper.GetRoleAsync(user);
                    // Store user info in session
                    HttpContext.Session.SetString("Username", user.Email);
                    HttpContext.Session.SetString("UserId", user.Id);
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
                        //Address = model.Address,
                        PhoneNumber = model.PhoneNumber,
                       // DateCreated = DateTime.UtcNow
                    };

                    string temporaryPassword = GenerateRandomPassword();
                    model.TemporaryPassword = temporaryPassword;

                    // Create user with temporary password
                    var result = await _userHelper.AddUserAsync(user, temporaryPassword);
                    if (result != IdentityResult.Success)
                    {
                        ModelState.AddModelError(string.Empty, "The user could not be created.");
                        return View(model);
                    }

                    // Assign the role "Pending"
                    await _userHelper.AddUserToRoleAsync(user, "Pending");

                    // Generates the email activation token
                    string myToken = await _userHelper.GenerateEmailConfirmationTokenAsync(user);
                    string tokenLink = Url.Action("ConfirmEmail", "Account", new { userid = user.Id, token = myToken }, protocol: HttpContext.Request.Scheme);

                 

                  
                        ViewBag.Message = "User created successfully. An email was sent with further instructions.";

                        ViewBag.Links = new Dictionary<string, string>
                        {
                            { "Create Admin", Url.Action("Create", "Admin") },
                            { "Create Trainer", Url.Action("Create", "Trainer") },
                             { "Create Trainee", Url.Action("Create", "Trainee") },
                            { "Create Receptionist", Url.Action("Create", "Receptionist") }
                        };
                    
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
        public IActionResult ResetPassword(string token)
        {
            return View();
        }

        // Processes the password reset request
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
