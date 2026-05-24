using Messaging_Chat_Application_MahmoudHakim.Hubs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Rotativa.AspNetCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TrainingCenterManagement_MVC.Data;
using TrainingCenterManagement_MVC.Helpers;
using TrainingCenterManagement_MVC.Models;

namespace TrainingCenterManagement_MVC.Controllers
{
    public class PaymentsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<ChatHub> _hubContext;
        private readonly ISettingsService _settings;

        public PaymentsController(ApplicationDbContext context, IHubContext<ChatHub> hubContext, ISettingsService settings)
        {
            _context = context;
            _hubContext = hubContext;
            _settings = settings;
        }

        // GET: Payments
        public async Task<IActionResult> Index(string search = "")
        {
            var query = _context.Payments
                .Include(p => p.Course)
                .Include(p => p.Trainee).ThenInclude(t => t.User)
                .AsQueryable();

            // ── Search ─────────────────────────────────────────────────────
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(p =>
                    (p.Trainee.User.FullName != null && p.Trainee.User.FullName.ToLower().Contains(term)) ||
                    (p.Course.CourseName != null && p.Course.CourseName.ToLower().Contains(term)) ||
                    (p.Notes != null && p.Notes.ToLower().Contains(term)));
            }

            var payments = await query.ToListAsync();

            // ── Stats ──────────────────────────────────────────────────────
            var allPayments = await _context.Payments.ToListAsync();
            ViewBag.TotalCount = allPayments.Count;
            ViewBag.TotalRevenue = allPayments.Sum(p => p.TotalAmount);
            ViewBag.ActiveMonths = allPayments
                .GroupBy(p => new { p.CreatedDate.Year, p.CreatedDate.Month }).Count();
            ViewBag.UniqueTrainees = allPayments.Select(p => p.TraineeId).Distinct().Count();
            ViewBag.Search = search;

            var grouped = payments
                .GroupBy(p => new { p.CreatedDate.Year, p.CreatedDate.Month })
                .OrderByDescending(g => g.Key.Year)
                .ThenByDescending(g => g.Key.Month)
                .ToList();

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return PartialView("_PaymentsGroupsPartial", grouped);

            return View(grouped);
        }


        // GET: Payments/Details/5
        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var payment = await _context.Payments
                .Include(p => p.Course)
                .Include(p => p.Trainee)
                .FirstOrDefaultAsync(m => m.PaymentId == id);
            if (payment == null)
            {
                return NotFound();
            }

            return View(payment);
        }

        // GET: Payments/Create
        public async Task<IActionResult> Create()
        {
            await PopulateCreateViewDataAsync();
            return View(new Payment());
        }

        // GET: Payments/GetRemainingBalance?traineeId=...&courseId=...
        [HttpGet]
        public async Task<IActionResult> GetRemainingBalance(Guid traineeId, Guid courseId)
        {
            var course = await _context.Courses.FindAsync(courseId);
            if (course == null) return Json(new { error = true });

            var dbRates = await _context.ExchangeRates.ToListAsync();
            var rates   = new Dictionary<PaymentCurrency, decimal>(CurrencyHelper.DefaultRates);
            foreach (var r in dbRates) rates[r.Currency] = r.RateToSYP;

            var existingPayments = await _context.Payments
                .Where(p => p.TraineeId == traineeId && p.CourseId == courseId && !p.IsDeleted)
                .ToListAsync();

            decimal totalPaidSYP = existingPayments.Sum(p => CurrencyHelper.ToSYP(p.TotalAmount, p.Currency, rates));
            decimal remainingSYP = Math.Max(0m, course.Price - totalPaidSYP);

            return Json(new
            {
                coursePrice          = course.Price,
                coursePriceFormatted = $"{course.Price:N0} ل.س",
                totalPaid            = totalPaidSYP,
                totalPaidFormatted   = $"{totalPaidSYP:N0} ل.س",
                remaining            = remainingSYP,
                remainingFormatted   = $"{remainingSYP:N0} ل.س",
                isFullyPaid          = remainingSYP <= 0,
                rates                = new
                {
                    usd = rates.GetValueOrDefault(PaymentCurrency.USD, CurrencyHelper.DefaultRates[PaymentCurrency.USD]),
                    eur = rates.GetValueOrDefault(PaymentCurrency.EUR, CurrencyHelper.DefaultRates[PaymentCurrency.EUR])
                }
            });
        }

        private async Task PopulateCreateViewDataAsync(Guid? selectedCourseId = null, Guid? selectedTraineeId = null)
        {
            ViewData["CourseId"] = new SelectList(_context.Courses, "CourseId", "CourseName", selectedCourseId);
            ViewData["TraineeId"] = new SelectList(
                await _context.Trainees.Include(t => t.User).ToListAsync(),
                "TraineeId",
                "User.FullName",
                selectedTraineeId
            );
            var defaultCurrencyStr = await _settings.GetAsync("DefaultCurrency") ?? "SYP";
            ViewBag.DefaultCurrency = (int)CurrencyHelper.ParseDefault(defaultCurrencyStr);
        }

        // POST: Payments/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Payment payment)
        {
            var course = await _context.Courses.FindAsync(payment.CourseId);
            if (course == null)
            {
                TempData["ErrorMessage"] = "الدورة غير موجودة.";
                await PopulateCreateViewDataAsync(payment.CourseId, payment.TraineeId);
                return View(payment);
            }

            // Load exchange rates — all comparisons happen in SYP (base currency)
            var dbRates = await _context.ExchangeRates.ToListAsync();
            var rates   = new Dictionary<PaymentCurrency, decimal>(CurrencyHelper.DefaultRates);
            foreach (var r in dbRates) rates[r.Currency] = r.RateToSYP;

            // Sum existing payments for this trainee+course, converted to SYP
            var existingPayments = await _context.Payments
                .Where(p => p.TraineeId == payment.TraineeId && p.CourseId == payment.CourseId && !p.IsDeleted)
                .ToListAsync();

            decimal totalPaidSYP  = existingPayments.Sum(p => CurrencyHelper.ToSYP(p.TotalAmount, p.Currency, rates));
            decimal newPaymentSYP = CurrencyHelper.ToSYP(payment.TotalAmount, payment.Currency, rates);

            if (totalPaidSYP >= course.Price)
            {
                TempData["ErrorMessage"] = "تم سداد كامل قيمة الدورة مسبقاً، لا يمكن إضافة دفعة جديدة.";
                await PopulateCreateViewDataAsync(payment.CourseId, payment.TraineeId);
                return View(payment);
            }

            decimal remainingSYP = course.Price - totalPaidSYP;

            if (newPaymentSYP > remainingSYP)
            {
                decimal remainingInChosenCurrency = payment.Currency == PaymentCurrency.SYP
                    ? remainingSYP
                    : Math.Round(remainingSYP / rates[payment.Currency], 2);
                var sym = CurrencyHelper.GetSymbol(payment.Currency);
                TempData["ErrorMessage"] = $"المبلغ يتجاوز المتبقي على الطالب. الحد الأقصى المسموح به: {remainingInChosenCurrency:N0} {sym}";
                await PopulateCreateViewDataAsync(payment.CourseId, payment.TraineeId);
                return View(payment);
            }

            _context.Add(payment);
            await _context.SaveChangesAsync();

            await SendPaymentNotificationsAsync(payment, course, rates);

            TempData["SuccessMessage"] = "تم تسجيل الدفعة بنجاح وتم إشعار الإدارة.";
            return RedirectToAction(nameof(Index));
        }

        private async Task SendPaymentNotificationsAsync(Payment payment, Course course,
            Dictionary<PaymentCurrency, decimal> rates)
        {
            var trainee = await _context.Trainees
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.TraineeId == payment.TraineeId);

            var traineeName  = trainee?.User?.FullName ?? "متدرب";
            var sym          = CurrencyHelper.GetSymbol(payment.Currency);
            var paymentDate  = payment.CreatedDate.ToString("dd/MM/yyyy");
            var notificationTitle = "إيداع دفعة مالية جديدة";

            string amountText;
            if (payment.Currency == PaymentCurrency.SYP)
            {
                amountText = $"{payment.TotalAmount:N0} ل.س";
            }
            else
            {
                var sypEquiv = CurrencyHelper.ToSYP(payment.TotalAmount, payment.Currency, rates);
                amountText = $"{payment.TotalAmount:N0} {sym} (≈ {sypEquiv:N0} ل.س)";
            }

            var notificationMessage = $"الطالب: {traineeName} | المبلغ: {amountText} | الدورة: {course.CourseName} | التاريخ: {paymentDate}";

            var adminUsers = await _context.Users
                .Where(u => u.Role == RoleType.Admin)
                .ToListAsync();

            var notifications = adminUsers.Select(admin => new UserNotification
            {
                NotificationId = Guid.NewGuid(),
                UserId = admin.Id,
                Title = notificationTitle,
                Message = notificationMessage,
                Type = NotificationType.PaymentReceived,
                IsRead = false,
                CreatedAt = DateTime.UtcNow,
                RelatedId = payment.PaymentId.ToString()
            }).ToList();

            if (notifications.Any())
            {
                _context.Notifications.AddRange(notifications);
                await _context.SaveChangesAsync();

                foreach (var admin in adminUsers)
                {
                    var connections = await _context.UserConnections
                        .Where(c => c.UserId == admin.Id && c.IsConnected)
                        .Select(c => c.ConnectionId)
                        .ToListAsync();

                    foreach (var connId in connections)
                    {
                        await _hubContext.Clients.Client(connId).SendAsync(
                            "ReceiveSystemNotification",
                            notificationTitle,
                            notificationMessage);
                    }
                }
            }
        }

        // GET: Payments/Delete/5
        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var payment = await _context.Payments
                .Include(p => p.Course)
                .Include(p => p.Trainee)
                .FirstOrDefaultAsync(m => m.PaymentId == id);
            if (payment == null)
            {
                return NotFound();
            }

            return View(payment);
        }

        // POST: Payments/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var payment = await _context.Payments.FindAsync(id);
            if (payment != null)
            {
                _context.Payments.Remove(payment);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool PaymentExists(Guid id)
        {
            return _context.Payments.Any(e => e.PaymentId == id);
        }

        public async Task<IActionResult> ViewCoursePayments(Guid courseId)
        {
            var course = await _context.Courses.FindAsync(courseId);
            if (course == null) return NotFound();

            ViewBag.CourseName = course.CourseName;
            ViewBag.CourseId = courseId;

            var payments = await _context.Payments
                .Include(p => p.Trainee).ThenInclude(t => t.User)
                .Where(p => p.CourseId == courseId)
                .ToListAsync();

            return View("CoursePayments", payments);
        }

        public async Task<IActionResult> ExportPaymentsPdf(Guid courseId)
        {
            var course = await _context.Courses.FirstOrDefaultAsync(c => c.CourseId == courseId);
            if (course == null) return NotFound();

            var payments = await _context.Payments
                .Where(p => p.CourseId == courseId)
                .Include(p => p.Trainee).ThenInclude(t => t.User)
                .ToListAsync();

            ViewBag.CourseName = course.CourseName;
            ViewBag.IsPdf = true;
            return new ViewAsPdf("ExportPaymentsPdf", payments)
            {
                PageSize = Rotativa.AspNetCore.Options.Size.A4,
                PageOrientation = Rotativa.AspNetCore.Options.Orientation.Portrait
            };
        }
    }
}
