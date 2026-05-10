using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrainingCenterManagement_MVC.Helpers;
using TrainingCenterManagement_MVC.Models;

namespace TrainingCenterManagement_MVC.Controllers
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
            var key             = await _settingsService.GetAsync("JwtKey");
            var defaultCurrency = await _settingsService.GetAsync("DefaultCurrency") ?? "SYP";

            ViewBag.JwtKey          = key ?? string.Empty;
            ViewBag.DefaultCurrency = defaultCurrency;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string JwtKey, string DefaultCurrency)
        {
            if (!string.IsNullOrWhiteSpace(JwtKey))
            {
                if (System.Text.Encoding.UTF8.GetByteCount(JwtKey) < 32)
                {
                    ModelState.AddModelError("JwtKey", "The JWT key must be at least 32 bytes long.");
                    ViewBag.JwtKey          = JwtKey;
                    ViewBag.DefaultCurrency = DefaultCurrency;
                    return View();
                }
                await _settingsService.SetAsync("JwtKey", JwtKey);
            }

            if (DefaultCurrency == "SYP" || DefaultCurrency == "USD" || DefaultCurrency == "EUR")
                await _settingsService.SetAsync("DefaultCurrency", DefaultCurrency);

            TempData["SuccessMessage"] = "تم حفظ الإعدادات بنجاح.";
            return RedirectToAction("Edit");
        }

        // GET /Settings/SetDefaultCurrency — quick AJAX-friendly endpoint for Admin Dashboard
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetDefaultCurrency(string currency)
        {
            if (currency == "SYP" || currency == "USD" || currency == "EUR")
            {
                await _settingsService.SetAsync("DefaultCurrency", currency);
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return Json(new { success = true, currency });
                TempData["SuccessMessage"] = "تم تحديث العملة الافتراضية.";
            }
            return RedirectToAction("Edit");
        }
    }
}
