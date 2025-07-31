using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;


using TrainingCenterManagement_MVC.Models;
using TrainingCenterManagement_MVC.Resources;
using TrainingCenterManagement_MVC.ViewModels;


namespace TrainingCenterManagement_MVC.Controllers
{
    public class AdminController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;

        public AdminController(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager
            
            )
        {
            _signInManager = signInManager;
            _userManager = userManager;
        }
        public IActionResult Dashboard()
        {
            return View();
        }
        [HttpGet]
        public IActionResult AdminAuth()
        {
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Dashboard", "Admin");
            }
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> AdminAuth(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Dashboard", "Admin");
            }

            var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, false, false);

            if (result.Succeeded)
            {
                var user = await _userManager.FindByEmailAsync(model.Email);
                if (user != null)
                {
                    HttpContext.Session.SetString("UserId", user.Id);
                    return RedirectToAction("Dashboard", "Admin");
                }
            }

            ModelState.AddModelError(string.Empty, SharedResource.InvalidLoginCredentials);
            return View(model);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            HttpContext.Session.Clear();
            await _signInManager.SignOutAsync();
            return RedirectToAction("Login", "Account");
        }

        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}
