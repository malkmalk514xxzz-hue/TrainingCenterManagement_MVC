using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrainingCenterManagement_MVC.Data;
using TrainingCenterManagement_MVC.Models;
using TrainingCenterManagement_MVC.ViewModels;

namespace TrainingCenterManagement_MVC.Controllers
{
    [Authorize(Roles = "Admin,Receptionist")]
    public class PaymentManagementController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PaymentManagementController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: PaymentManagement
        public async Task<IActionResult> Index()
        {
            var rates = await GetRatesAsync();

            var trainees = await _context.Trainees
                .Include(t => t.User)
                .Include(t => t.CourseTrainees).ThenInclude(ct => ct.Course)
                .Include(t => t.Payments.Where(p => !p.IsDeleted))
                    .ThenInclude(p => p.Course)
                .ToListAsync();

            var summaries = new List<StudentAccountSummary>();
            decimal totalRevenue = 0, totalPending = 0;
            int withDebt = 0;

            foreach (var t in trainees)
            {
                var totalOwed = t.CourseTrainees
                    .Where(ct => !ct.Course.IsDeleted)
                    .Sum(ct => ct.Course.Price);

                var totalPaid = t.Payments
                    .Where(p => !p.IsDeleted && !p.Course.IsDeleted)
                    .Sum(p => ConvertToSAR(p.TotalAmount, p.Currency, rates));

                var remaining = totalOwed - totalPaid;
                totalRevenue += totalPaid;
                if (remaining > 0) { totalPending += remaining; withDebt++; }

                summaries.Add(new StudentAccountSummary
                {
                    TraineeId        = t.TraineeId,
                    Name             = t.User?.FullName ?? "—",
                    Email            = t.User?.Email ?? "—",
                    ProfilePic       = t.User?.ProfilePictureUrl,
                    EnrolledCourses  = t.CourseTrainees.Count(ct => !ct.Course.IsDeleted),
                    TotalOwed        = totalOwed,
                    TotalPaid        = totalPaid,
                    RemainingBalance = remaining
                });
            }

            return View(new StudentAccountsIndexViewModel
            {
                Students         = summaries.OrderByDescending(s => s.RemainingBalance).ToList(),
                TotalRevenue     = totalRevenue,
                TotalPending     = totalPending,
                StudentsWithDebt = withDebt
            });
        }

        // GET: PaymentManagement/StudentAccount/traineeId
        public async Task<IActionResult> StudentAccount(Guid id)
        {
            var rates = await GetRatesAsync();

            var trainee = await _context.Trainees
                .Include(t => t.User)
                .Include(t => t.CourseTrainees).ThenInclude(ct => ct.Course)
                .Include(t => t.Payments.Where(p => !p.IsDeleted))
                    .ThenInclude(p => p.Course)
                .FirstOrDefaultAsync(t => t.TraineeId == id);

            if (trainee == null) return NotFound();

            var courses = trainee.CourseTrainees
                .Where(ct => !ct.Course.IsDeleted)
                .Select(ct =>
                {
                    var paidSAR = trainee.Payments
                        .Where(p => !p.IsDeleted && p.CourseId == ct.CourseId)
                        .Sum(p => ConvertToSAR(p.TotalAmount, p.Currency, rates));

                    return new CourseDebtEntry
                    {
                        CourseId      = ct.CourseId,
                        CourseName    = ct.Course.CourseName,
                        CoursePrice   = ct.Course.Price,
                        TotalPaidSAR  = paidSAR,
                        RemainingDebt = ct.Course.Price - paidSAR
                    };
                }).ToList();

            var history = trainee.Payments
                .Where(p => !p.IsDeleted && !p.Course.IsDeleted)
                .OrderByDescending(p => p.CreatedDate)
                .Select(p => new PaymentHistoryEntry
                {
                    PaymentId        = p.PaymentId,
                    CourseName       = p.Course?.CourseName ?? "—",
                    OriginalAmount   = p.TotalAmount,
                    OriginalCurrency = GetSymbol(p.Currency),
                    AmountInSAR      = ConvertToSAR(p.TotalAmount, p.Currency, rates),
                    Notes            = p.Notes,
                    Date             = p.CreatedDate
                }).ToList();

            var totalOwed = courses.Sum(c => c.CoursePrice);
            var totalPaid = history.Sum(p => p.AmountInSAR);

            return View(new StudentAccountDetailsViewModel
            {
                TraineeId        = trainee.TraineeId,
                Name             = trainee.User?.FullName ?? "—",
                Email            = trainee.User?.Email ?? "—",
                ProfilePic       = trainee.User?.ProfilePictureUrl,
                Courses          = courses,
                PaymentHistory   = history,
                TotalOwed        = totalOwed,
                TotalPaid        = totalPaid,
                RemainingBalance = totalOwed - totalPaid
            });
        }

        // ─── Helpers ─────────────────────────────────────────────────────────
        private async Task<Dictionary<PaymentCurrency, decimal>> GetRatesAsync()
        {
            var dbRates = await _context.ExchangeRates.ToListAsync();
            var dict = new Dictionary<PaymentCurrency, decimal>
            {
                [PaymentCurrency.SAR] = 1m,
                [PaymentCurrency.USD] = 3.75m,
                [PaymentCurrency.EUR] = 4.08m,
                [PaymentCurrency.EGP] = 0.071m
            };
            foreach (var r in dbRates)
                dict[r.Currency] = r.RateToSAR;
            return dict;
        }

        private static decimal ConvertToSAR(decimal amount, PaymentCurrency currency,
            Dictionary<PaymentCurrency, decimal> rates)
        {
            if (currency == PaymentCurrency.SAR) return amount;
            return rates.TryGetValue(currency, out var rate) ? amount * rate : amount;
        }

        private static string GetSymbol(PaymentCurrency c) => c switch
        {
            PaymentCurrency.USD => "USD",
            PaymentCurrency.EUR => "EUR",
            PaymentCurrency.EGP => "ج.م",
            _                   => "ر.س"
        };
    }
}
