using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TrainingCenterManagement_MVC.Data;
using TrainingCenterManagement_MVC.Helpers;
using TrainingCenterManagement_MVC.Models;
using TrainingCenterManagement_MVC.ViewModels;

namespace TrainingCenterManagement_MVC.Controllers
{
    [Authorize(Roles = "Admin")]
    public class ExchangeRatesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ISettingsService    _settings;

        public ExchangeRatesController(ApplicationDbContext context, ISettingsService settings)
        {
            _context  = context;
            _settings = settings;
        }

        // GET: ExchangeRates
        public async Task<IActionResult> Index()
        {
            var dbRates = await _context.ExchangeRates
                .Include(er => er.UpdatedBy)
                .ToListAsync();

            var defaultCurrency = await _settings.GetAsync("DefaultCurrency") ?? "SYP";

            var rows = new List<ExchangeRateRow>();

            foreach (var c in new[] { PaymentCurrency.USD, PaymentCurrency.EUR })
            {
                var dbEntry = dbRates.FirstOrDefault(r => r.Currency == c);
                var rate    = dbEntry?.RateToSYP ?? CurrencyHelper.DefaultRates[c];

                rows.Add(new ExchangeRateRow
                {
                    Id             = dbEntry?.Id ?? Guid.Empty,
                    Currency       = c,
                    CurrencyLabel  = CurrencyHelper.GetLabel(c),
                    CurrencySymbol = CurrencyHelper.GetSymbol(c),
                    RateToSYP      = rate,
                    RateFromSYP    = rate > 0 ? Math.Round(1m / rate, 8) : 0,
                    UpdatedAt      = dbEntry?.UpdatedAt ?? DateTime.UtcNow,
                    UpdatedByName  = dbEntry?.UpdatedBy?.FullName ?? "—"
                });
            }

            ViewBag.DefaultCurrency = defaultCurrency;
            return View(new ExchangeRateIndexViewModel { Rates = rows });
        }

        // POST: ExchangeRates/Update
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(PaymentCurrency currency, decimal rateToSar)
        {
            if (currency == PaymentCurrency.SYP || rateToSar <= 0)
            {
                TempData["Error"] = "قيمة غير صالحة.";
                return RedirectToAction(nameof(Index));
            }

            var adminId  = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var existing = await _context.ExchangeRates.FirstOrDefaultAsync(er => er.Currency == currency);

            if (existing != null)
            {
                existing.RateToSYP       = rateToSar;
                existing.UpdatedAt       = DateTime.UtcNow;
                existing.UpdatedByUserId = adminId;
            }
            else
            {
                _context.ExchangeRates.Add(new ExchangeRate
                {
                    Currency        = currency,
                    RateToSYP       = rateToSar,
                    UpdatedAt       = DateTime.UtcNow,
                    UpdatedByUserId = adminId
                });
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = $"تم تحديث سعر صرف {CurrencyHelper.GetLabel(currency)} بنجاح.";
            return RedirectToAction(nameof(Index));
        }

        // POST: ExchangeRates/SetDefault
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetDefault(string currency)
        {
            if (currency == "SYP" || currency == "USD" || currency == "EUR")
            {
                await _settings.SetAsync("DefaultCurrency", currency);
                TempData["Success"] = $"تم تعيين {CurrencyHelper.GetLabel(CurrencyHelper.ParseDefault(currency))} كعملة افتراضية.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
