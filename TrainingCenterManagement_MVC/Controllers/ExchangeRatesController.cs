using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TrainingCenterManagement_MVC.Data;
using TrainingCenterManagement_MVC.Models;
using TrainingCenterManagement_MVC.ViewModels;

namespace TrainingCenterManagement_MVC.Controllers
{
    [Authorize(Roles = "Admin")]
    public class ExchangeRatesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ExchangeRatesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: ExchangeRates
        public async Task<IActionResult> Index()
        {
            var rates = await _context.ExchangeRates
                .Include(er => er.UpdatedBy)
                .OrderBy(er => er.Currency)
                .ToListAsync();

            var rows = rates.Select(r => new ExchangeRateRow
            {
                Id             = r.Id,
                Currency       = r.Currency,
                CurrencyLabel  = GetCurrencyLabel(r.Currency),
                CurrencySymbol = GetCurrencySymbol(r.Currency),
                RateToSAR      = r.RateToSAR,
                RateFromSAR    = r.RateToSAR > 0 ? Math.Round(1m / r.RateToSAR, 6) : 0,
                UpdatedAt      = r.UpdatedAt,
                UpdatedByName  = r.UpdatedBy?.FullName ?? "—"
            }).ToList();

            // Add missing currencies with default rates
            var existingCurrencies = rates.Select(r => r.Currency).ToHashSet();
            foreach (var c in Enum.GetValues<PaymentCurrency>().Where(c => c != PaymentCurrency.SAR))
            {
                if (!existingCurrencies.Contains(c))
                {
                    rows.Add(new ExchangeRateRow
                    {
                        Id             = Guid.Empty,
                        Currency       = c,
                        CurrencyLabel  = GetCurrencyLabel(c),
                        CurrencySymbol = GetCurrencySymbol(c),
                        RateToSAR      = GetDefaultRate(c),
                        RateFromSAR    = GetDefaultRate(c) > 0 ? Math.Round(1m / GetDefaultRate(c), 6) : 0,
                        UpdatedAt      = DateTime.UtcNow,
                        UpdatedByName  = "—"
                    });
                }
            }

            return View(new ExchangeRateIndexViewModel
            {
                Rates   = rows.OrderBy(r => r.Currency).ToList(),
                SarRate = 1m
            });
        }

        // POST: ExchangeRates/Update
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(PaymentCurrency currency, decimal rateToSar)
        {
            if (currency == PaymentCurrency.SAR || rateToSar <= 0)
            {
                TempData["Error"] = "قيمة غير صالحة.";
                return RedirectToAction(nameof(Index));
            }

            var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var existing = await _context.ExchangeRates
                .FirstOrDefaultAsync(er => er.Currency == currency);

            if (existing != null)
            {
                existing.RateToSAR      = rateToSar;
                existing.UpdatedAt      = DateTime.UtcNow;
                existing.UpdatedByUserId = adminId;
            }
            else
            {
                _context.ExchangeRates.Add(new ExchangeRate
                {
                    Currency       = currency,
                    RateToSAR      = rateToSar,
                    UpdatedAt      = DateTime.UtcNow,
                    UpdatedByUserId = adminId
                });
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = $"تم تحديث سعر صرف {GetCurrencyLabel(currency)} بنجاح.";
            return RedirectToAction(nameof(Index));
        }

        // ─── Helpers ─────────────────────────────────────────────────────────
        public static string GetCurrencyLabel(PaymentCurrency c) => c switch
        {
            PaymentCurrency.USD => "دولار أمريكي (USD)",
            PaymentCurrency.EUR => "يورو (EUR)",
            PaymentCurrency.EGP => "جنيه مصري (EGP)",
            _                   => "ريال سعودي (SAR)"
        };

        public static string GetCurrencySymbol(PaymentCurrency c) => c switch
        {
            PaymentCurrency.USD => "$",
            PaymentCurrency.EUR => "€",
            PaymentCurrency.EGP => "ج.م",
            _                   => "ر.س"
        };

        private static decimal GetDefaultRate(PaymentCurrency c) => c switch
        {
            PaymentCurrency.USD => 3.75m,
            PaymentCurrency.EUR => 4.08m,
            PaymentCurrency.EGP => 0.071m,
            _                   => 1m
        };
    }
}
