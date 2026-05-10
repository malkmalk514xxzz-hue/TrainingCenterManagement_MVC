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
    public class SalaryController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SalaryController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Salary
        public async Task<IActionResult> Index()
        {
            var now = DateTime.UtcNow;

            var salaries = await _context.EmployeeSalaries
                .Include(es => es.User)
                .Include(es => es.Payments)
                .Where(es => es.IsActive)
                .ToListAsync();

            var rows = new List<EmployeeSalaryRow>();
            foreach (var es in salaries)
            {
                var paidThisMonth = es.Payments
                    .Any(p => p.Month == now.Month && p.Year == now.Year);
                var lastPaid = es.Payments
                    .OrderByDescending(p => p.PaidAt)
                    .FirstOrDefault();

                // Trainer specialty
                string specialty = string.Empty;
                if (es.EmployeeRole == "Trainer")
                {
                    var trainer = await _context.Trainers.FirstOrDefaultAsync(t => t.UserId == es.UserId);
                    specialty = trainer?.Specialty ?? string.Empty;
                }

                rows.Add(new EmployeeSalaryRow
                {
                    SalaryId      = es.SalaryId,
                    UserId        = es.UserId,
                    Name          = es.User?.FullName ?? "—",
                    Email         = es.User?.Email ?? "—",
                    ProfilePic    = es.User?.ProfilePictureUrl,
                    Role          = es.EmployeeRole,
                    Specialty     = specialty,
                    MonthlySalary = es.MonthlySalary,
                    CurrencyLabel = CurrencyHelper.GetSymbol(es.Currency),
                    Currency      = es.Currency,
                    PaidThisMonth = paidThisMonth,
                    LastPaidAt    = lastPaid?.PaidAt,
                    Notes         = es.Notes,
                    IsActive      = es.IsActive
                });
            }

            var totalPayroll = rows.Sum(r => r.MonthlySalary);
            var paidSum      = salaries.Sum(es =>
                es.Payments.Where(p => p.Month == now.Month && p.Year == now.Year)
                           .Sum(p => p.Amount));
            var pendingSum   = salaries.Sum(es =>
                es.Payments.Any(p => p.Month == now.Month && p.Year == now.Year)
                    ? 0
                    : es.MonthlySalary);

            return View(new SalaryIndexViewModel
            {
                Employees          = rows.OrderBy(r => r.Role).ThenBy(r => r.Name).ToList(),
                TotalMonthlyPayroll = totalPayroll,
                PaidThisMonth      = paidSum,
                PendingThisMonth   = pendingSum,
                CurrentMonth       = now.Month,
                CurrentYear        = now.Year
            });
        }

        // GET: Salary/Details/salaryId
        public async Task<IActionResult> Details(Guid id)
        {
            var es = await _context.EmployeeSalaries
                .Include(e => e.User)
                .Include(e => e.Payments).ThenInclude(p => p.PaidByAdmin)
                .FirstOrDefaultAsync(e => e.SalaryId == id);

            if (es == null) return NotFound();

            string specialty = string.Empty;
            if (es.EmployeeRole == "Trainer")
            {
                var trainer = await _context.Trainers.FirstOrDefaultAsync(t => t.UserId == es.UserId);
                specialty = trainer?.Specialty ?? string.Empty;
            }

            var history = es.Payments
                .OrderByDescending(p => p.Year).ThenByDescending(p => p.Month)
                .Select(p => new SalaryPaymentRow
                {
                    Id           = p.Id,
                    Month        = p.Month,
                    Year         = p.Year,
                    Amount       = p.Amount,
                    CurrencyLabel = CurrencyHelper.GetSymbol(p.Currency),
                    PaidAt       = p.PaidAt,
                    PaidByName   = p.PaidByAdmin?.FullName ?? "—",
                    Notes        = p.Notes
                }).ToList();

            return View(new SalaryDetailsViewModel
            {
                SalaryId       = es.SalaryId,
                Name           = es.User?.FullName ?? "—",
                Email          = es.User?.Email ?? "—",
                ProfilePic     = es.User?.ProfilePictureUrl,
                Role           = es.EmployeeRole,
                Specialty      = specialty,
                MonthlySalary  = es.MonthlySalary,
                Currency       = es.Currency,
                CurrencyLabel  = CurrencyHelper.GetSymbol(es.Currency),
                Notes          = es.Notes,
                IsActive       = es.IsActive,
                PaymentHistory = history,
                TotalPaid      = history.Sum(h => h.Amount)
            });
        }

        // GET: Salary/Create
        public async Task<IActionResult> Create()
        {
            await PopulateEmployeesDropdownAsync();
            return View(new CreateSalaryViewModel());
        }

        // POST: Salary/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateSalaryViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await PopulateEmployeesDropdownAsync();
                return View(model);
            }

            var alreadyExists = await _context.EmployeeSalaries
                .AnyAsync(es => es.UserId == model.UserId && es.IsActive);
            if (alreadyExists)
            {
                ModelState.AddModelError("", "يوجد سجل راتب نشط لهذا الموظف بالفعل.");
                await PopulateEmployeesDropdownAsync();
                return View(model);
            }

            var user = await _context.Users.FindAsync(model.UserId);
            if (user == null) return NotFound();

            string role = user.Role switch
            {
                RoleType.Trainer     => "Trainer",
                RoleType.Receptionist => "Receptionist",
                _                    => "Other"
            };

            _context.EmployeeSalaries.Add(new EmployeeSalary
            {
                UserId        = model.UserId,
                EmployeeRole  = role,
                MonthlySalary = model.MonthlySalary,
                Currency      = model.Currency,
                Notes         = model.Notes,
                CreatedAt     = DateTime.UtcNow,
                IsActive      = true
            });

            await _context.SaveChangesAsync();
            TempData["Success"] = "تم إضافة سجل الراتب بنجاح.";
            return RedirectToAction(nameof(Index));
        }

        // POST: Salary/Pay
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Pay(PaySalaryViewModel model)
        {
            var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var salary  = await _context.EmployeeSalaries.FindAsync(model.SalaryId);
            if (salary == null) return NotFound();

            var alreadyPaid = await _context.SalaryPayments
                .AnyAsync(p => p.SalaryId == model.SalaryId &&
                               p.Month == model.Month && p.Year == model.Year);
            if (alreadyPaid)
            {
                TempData["Error"] = "تم صرف الراتب لهذا الشهر بالفعل.";
                return RedirectToAction(nameof(Details), new { id = model.SalaryId });
            }

            _context.SalaryPayments.Add(new SalaryPayment
            {
                SalaryId      = model.SalaryId,
                Month         = model.Month,
                Year          = model.Year,
                Amount        = model.Amount,
                Currency      = model.Currency,
                PaidAt        = DateTime.UtcNow,
                PaidByAdminId = adminId,
                Notes         = model.Notes
            });

            await _context.SaveChangesAsync();
            TempData["Success"] = $"تم صرف راتب {GetMonthName(model.Month)} {model.Year} بنجاح.";
            return RedirectToAction(nameof(Details), new { id = model.SalaryId });
        }

        // POST: Salary/Deactivate
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Deactivate(Guid id)
        {
            var salary = await _context.EmployeeSalaries.FindAsync(id);
            if (salary == null) return NotFound();
            salary.IsActive = false;
            await _context.SaveChangesAsync();
            TempData["Success"] = "تم إيقاف سجل الراتب.";
            return RedirectToAction(nameof(Index));
        }

        // ─── Helpers ─────────────────────────────────────────────────────────
        private async Task PopulateEmployeesDropdownAsync()
        {
            var existingUserIds = await _context.EmployeeSalaries
                .Where(es => es.IsActive)
                .Select(es => es.UserId)
                .ToListAsync();

            var employees = await _context.Users
                .Where(u => (u.Role == RoleType.Trainer || u.Role == RoleType.Receptionist)
                            && !existingUserIds.Contains(u.Id))
                .Select(u => new { u.Id, u.FullName, u.Email, u.Role })
                .ToListAsync();

            ViewBag.Employees = employees.Select(e => new
            {
                Value = e.Id,
                Text  = $"{e.FullName} ({(e.Role == RoleType.Trainer ? "مدرب" : "موظف استقبال")}) — {e.Email}"
            }).ToList();
        }

        private static string GetMonthName(int m) => m switch
        {
            1  => "يناير",  2  => "فبراير", 3  => "مارس",
            4  => "أبريل",  5  => "مايو",   6  => "يونيو",
            7  => "يوليو",  8  => "أغسطس",  9  => "سبتمبر",
            10 => "أكتوبر", 11 => "نوفمبر", _  => "ديسمبر"
        };
    }
}
