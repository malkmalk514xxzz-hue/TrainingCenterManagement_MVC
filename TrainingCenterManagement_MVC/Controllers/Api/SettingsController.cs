using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrainingCenterManagement_MVC.Helpers;
using System.Threading.Tasks;

namespace TrainingCenterManagement_MVC.Controllers.Api
{
    [Authorize(Roles = "Admin")]
    public class SettingsController : Controller
    {
        private readonly ISettingsService _settingsService;

        public SettingsController(ISettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        [HttpGet]
        public async Task<IActionResult> Edit()
        {
            var key = await _settingsService.GetAsync("JwtKey");
            ViewBag.JwtKey = key ?? string.Empty;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string JwtKey)
        {
            if (string.IsNullOrWhiteSpace(JwtKey) || System.Text.Encoding.UTF8.GetByteCount(JwtKey) < 32)
            {
                ModelState.AddModelError("JwtKey", "The JWT key must be at least 32 bytes long.");
                ViewBag.JwtKey = JwtKey;
                return View();
            }

            await _settingsService.SetAsync("JwtKey", JwtKey);
            TempData["SuccessMessage"] = "JWT key updated successfully. Restart may be required for Bearer validation depending on configuration.";
            return RedirectToAction("Edit");
        }
    }
}
