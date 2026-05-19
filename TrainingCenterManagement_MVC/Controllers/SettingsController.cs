using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrainingCenterManagement_MVC.Helpers;
using TrainingCenterManagement_MVC.Models;

namespace TrainingCenterManagement_MVC.Controllers
{
    [Authorize(Roles = "Admin")]
    public class SettingsController : Controller
    {
        private readonly ISettingsService  _settingsService;
        private readonly IWebHostEnvironment _env;

        public SettingsController(ISettingsService settingsService, IWebHostEnvironment env)
        {
            _settingsService = settingsService;
            _env             = env;
        }

        [HttpGet]
        public async Task<IActionResult> Edit()
        {
            var key             = await _settingsService.GetAsync("JwtKey");
            var defaultCurrency = await _settingsService.GetAsync("DefaultCurrency") ?? "SYP";

            ViewBag.JwtKey          = key ?? string.Empty;
            ViewBag.DefaultCurrency = defaultCurrency;

            // Payment gateway settings
            ViewBag.ShamCashAccountName   = await _settingsService.GetAsync("ShamCash:AccountName")   ?? "";
            ViewBag.ShamCashAccountNumber = await _settingsService.GetAsync("ShamCash:AccountNumber") ?? "";
            ViewBag.ShamCashQrCodeUrl     = await _settingsService.GetAsync("ShamCash:QrCodeUrl")     ?? "";
            ViewBag.BinanceWalletAddress  = await _settingsService.GetAsync("Binance:WalletAddress")  ?? "";
            ViewBag.BinanceNetwork        = await _settingsService.GetAsync("Binance:Network")         ?? "TRC20";
            ViewBag.BinanceQrCodeUrl      = await _settingsService.GetAsync("Binance:QrCodeUrl")       ?? "";

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            string JwtKey, string DefaultCurrency,
            string? ShamCashAccountName, string? ShamCashAccountNumber,
            IFormFile? ShamCashQrFile,
            string? BinanceWalletAddress, string? BinanceNetwork,
            IFormFile? BinanceQrFile)
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

            await _settingsService.SetAsync("ShamCash:AccountName",   ShamCashAccountName?.Trim()   ?? "");
            await _settingsService.SetAsync("ShamCash:AccountNumber", ShamCashAccountNumber?.Trim() ?? "");
            await _settingsService.SetAsync("Binance:WalletAddress",  BinanceWalletAddress?.Trim()  ?? "");
            await _settingsService.SetAsync("Binance:Network",        BinanceNetwork?.Trim()         ?? "TRC20");

            // Handle QR code file uploads
            var qrDir = Path.Combine(_env.WebRootPath, "uploads", "qrcodes");
            Directory.CreateDirectory(qrDir);

            if (ShamCashQrFile != null && ShamCashQrFile.Length > 0)
            {
                var ext  = Path.GetExtension(ShamCashQrFile.FileName).ToLowerInvariant();
                var name = $"shamcash_qr{ext}";
                await using var fs = new FileStream(Path.Combine(qrDir, name), FileMode.Create);
                await ShamCashQrFile.CopyToAsync(fs);
                await _settingsService.SetAsync("ShamCash:QrCodeUrl", $"/uploads/qrcodes/{name}");
            }

            if (BinanceQrFile != null && BinanceQrFile.Length > 0)
            {
                var ext  = Path.GetExtension(BinanceQrFile.FileName).ToLowerInvariant();
                var name = $"binance_qr{ext}";
                await using var fs = new FileStream(Path.Combine(qrDir, name), FileMode.Create);
                await BinanceQrFile.CopyToAsync(fs);
                await _settingsService.SetAsync("Binance:QrCodeUrl", $"/uploads/qrcodes/{name}");
            }

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
