using Microsoft.EntityFrameworkCore;
using TrainingCenterManagement_MVC.Data;
using TrainingCenterManagement_MVC.Models;
using TrainingCenterManagement_MVC.ViewModels;

namespace TrainingCenterManagement_MVC.Helpers
{
    public class ReceptionistDashboardHelper
    {
        private readonly ApplicationDbContext _context;

        public ReceptionistDashboardHelper(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<ReceptionistDashboardViewModel> GetDashboardAsync(string userId)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);

            return new ReceptionistDashboardViewModel
            {
                ReceptionistName  = user?.FullName ?? "موظف استقبال",
                ReceptionistEmail = user?.Email ?? string.Empty,
                ProfilePic        = !string.IsNullOrEmpty(user?.ProfilePictureUrl)
                                        ? user.ProfilePictureUrl
                                        : "/images/default-profile.png",
                Stats            = await GetStatsAsync(),
                RecentPayments   = await GetRecentPaymentsAsync(),
                TodayEnrollments = await GetTodayEnrollmentsAsync(),
                ActiveCourses    = await GetActiveCoursesAsync()
            };
        }

        private async Task<ReceptionistStats> GetStatsAsync()
        {
            var now       = DateTime.UtcNow;
            var thisMonth = new DateTime(now.Year, now.Month, 1);
            var nextMonth = thisMonth.AddMonths(1);
            var prevMonth = thisMonth.AddMonths(-1);
            var today     = now.Date;
            var tomorrow  = today.AddDays(1);

            int totalStudents = await _context.CourseTrainees
                .Where(ct => !ct.Course.IsDeleted)
                .Select(ct => ct.TraineeId)
                .Distinct()
                .CountAsync();

            int curStudents = await _context.Payments
                .Where(p => !p.IsDeleted && !p.Course.IsDeleted && p.CreatedDate >= thisMonth && p.CreatedDate < nextMonth)
                .Select(p => p.TraineeId).Distinct().CountAsync();
            int prevStudents = await _context.Payments
                .Where(p => !p.IsDeleted && !p.Course.IsDeleted && p.CreatedDate >= prevMonth && p.CreatedDate < thisMonth)
                .Select(p => p.TraineeId).Distinct().CountAsync();

            int totalCourses = await _context.Courses.CountAsync(c => !c.IsDeleted);
            int curCourses   = await _context.Courses.CountAsync(c => !c.IsDeleted && c.CreatedDate >= thisMonth && c.CreatedDate < nextMonth);
            int prevCourses  = await _context.Courses.CountAsync(c => !c.IsDeleted && c.CreatedDate >= prevMonth && c.CreatedDate < thisMonth);

            decimal curRevenue  = await _context.Payments.Where(p => !p.IsDeleted && !p.Course.IsDeleted && p.CreatedDate >= thisMonth && p.CreatedDate < nextMonth).SumAsync(p => p.TotalAmount);
            decimal prevRevenue = await _context.Payments.Where(p => !p.IsDeleted && !p.Course.IsDeleted && p.CreatedDate >= prevMonth && p.CreatedDate < thisMonth).SumAsync(p => p.TotalAmount);
            decimal revenueChange = prevRevenue > 0 ? Math.Round(((curRevenue - prevRevenue) / prevRevenue) * 100m, 1) : (curRevenue > 0 ? 100m : 0m);

            int todayPayments = await _context.Payments
                .CountAsync(p => !p.IsDeleted && p.CreatedDate >= today && p.CreatedDate < tomorrow);

            return new ReceptionistStats
            {
                TotalStudents     = totalStudents,
                StudentsChange    = curStudents - prevStudents,
                TotalCourses      = totalCourses,
                CoursesChange     = curCourses - prevCourses,
                MonthlyRevenue    = curRevenue,
                RevenueChange     = revenueChange,
                TodayPayments     = todayPayments,
                PendingEnrollments = 0
            };
        }

        private async Task<List<RecentPaymentEntry>> GetRecentPaymentsAsync()
        {
            var payments = await _context.Payments
                .Where(p => !p.IsDeleted && !p.Course.IsDeleted)
                .Include(p => p.Trainee).ThenInclude(t => t.User)
                .Include(p => p.Course)
                .OrderByDescending(p => p.CreatedDate)
                .Take(10)
                .ToListAsync();

            return payments.Select(p => new RecentPaymentEntry
            {
                PaymentId   = p.PaymentId,
                TraineeName = p.Trainee?.User?.FullName ?? "—",
                CourseName  = p.Course?.CourseName ?? "—",
                Amount      = p.TotalAmount,
                Currency    = p.Currency switch
                {
                    PaymentCurrency.USD => "USD",
                    PaymentCurrency.EUR => "EUR",
                    PaymentCurrency.EGP => "ج.م",
                    _                   => "ر.س"
                },
                Notes = p.Notes,
                Date  = p.CreatedDate
            }).ToList();
        }

        private async Task<List<TodayEnrollment>> GetTodayEnrollmentsAsync()
        {
            var today    = DateTime.UtcNow.Date;
            var tomorrow = today.AddDays(1);

            return await _context.Payments
                .Where(p => !p.IsDeleted && !p.Course.IsDeleted && p.CreatedDate >= today && p.CreatedDate < tomorrow)
                .Include(p => p.Trainee).ThenInclude(t => t.User)
                .Include(p => p.Course)
                .OrderByDescending(p => p.CreatedDate)
                .Take(5)
                .Select(p => new TodayEnrollment
                {
                    TraineeName = p.Trainee.User.FullName,
                    CourseName  = p.Course.CourseName,
                    EnrolledAt  = p.CreatedDate
                })
                .ToListAsync();
        }

        private async Task<List<CourseQuickInfo>> GetActiveCoursesAsync()
        {
            var courses = await _context.Courses
                .Where(c => !c.IsDeleted)
                .Include(c => c.CourseTrainees)
                .OrderByDescending(c => c.CourseTrainees.Count)
                .Take(8)
                .ToListAsync();

            return courses.Select(c => new CourseQuickInfo
            {
                CourseId      = c.CourseId,
                CourseName    = c.CourseName,
                EnrolledCount = c.CourseTrainees.Count,
                Price         = c.Price,
                IsActive      = true
            }).ToList();
        }
    }
}
