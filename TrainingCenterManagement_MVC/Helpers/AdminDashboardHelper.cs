using Microsoft.EntityFrameworkCore;
using TrainingCenterManagement_MVC.Data;
using TrainingCenterManagement_MVC.ViewModels;

namespace TrainingCenterManagement_MVC.Helpers
{
    public class AdminDashboardHelper
    {
        private readonly ApplicationDbContext _context;

        public AdminDashboardHelper(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<AdminDashboardViewModelModel> GetAdminDashboardAsync(string userId)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);

            var model = new AdminDashboardViewModelModel
            {
                AdminName = user?.FullName ?? "Admin",
                AdminEmail = user?.Email ?? string.Empty,
                AdminProfilePic = !string.IsNullOrEmpty(user?.ProfilePictureUrl)
                    ? user.ProfilePictureUrl
                    : "/images/default-profile.png",
                Stats = GetStats(),
                ChartData = GetChartData(),
                Health = await GetPlatformHealthAsync(),
                RecentActivity = await GetRecentActivityAsync(),
                TopCourses = await GetTopCoursesAsync(),
                RecentPayments = await GetRecentPaymentsAsync(),
                TopTrainers = await GetTopTrainersAsync()
            };

            return model;
        }

        // ─── Core Stats ──────────────────────────────────────────────────────────
        public Stats GetStats()
        {
            var now = DateTime.UtcNow;
            var thisMonth = new DateTime(now.Year, now.Month, 1);
            var nextMonth = thisMonth.AddMonths(1);
            var prevMonth = thisMonth.AddMonths(-1);

            int curCourses = _context.Courses.Count(c => !c.IsDeleted && c.CreatedDate >= thisMonth && c.CreatedDate < nextMonth);
            int prevCourses = _context.Courses.Count(c => !c.IsDeleted && c.CreatedDate >= prevMonth && c.CreatedDate < thisMonth);

            int curStudents = _context.Payments.Where(p => !p.IsDeleted && !p.Course.IsDeleted && p.CreatedDate >= thisMonth && p.CreatedDate < nextMonth).Select(p => p.TraineeId).Distinct().Count();
            int prevStudents = _context.Payments.Where(p => !p.IsDeleted && !p.Course.IsDeleted && p.CreatedDate >= prevMonth && p.CreatedDate < thisMonth).Select(p => p.TraineeId).Distinct().Count();

            decimal curRevenue = _context.Payments.Where(p => !p.IsDeleted && !p.Course.IsDeleted && p.CreatedDate >= thisMonth && p.CreatedDate < nextMonth).Sum(p => p.TotalAmount);
            decimal prevRevenue = _context.Payments.Where(p => !p.IsDeleted && !p.Course.IsDeleted && p.CreatedDate >= prevMonth && p.CreatedDate < thisMonth).Sum(p => p.TotalAmount);
            decimal revenueChange = prevRevenue > 0 ? Math.Round(((curRevenue - prevRevenue) / prevRevenue) * 100m, 1) : (curRevenue > 0 ? 100m : 0m);

            int curTrainers = _context.CourseTrainers.Where(ct => !ct.Course.IsDeleted && ct.Course.CreatedDate >= thisMonth && ct.Course.CreatedDate < nextMonth).Select(ct => ct.TrainerId).Distinct().Count();
            int prevTrainers = _context.CourseTrainers.Where(ct => !ct.Course.IsDeleted && ct.Course.CreatedDate >= prevMonth && ct.Course.CreatedDate < thisMonth).Select(ct => ct.TrainerId).Distinct().Count();

            return new Stats
            {
                TotalCourses = _context.Courses.Count(c => !c.IsDeleted),
                CoursesChange = curCourses - prevCourses,
                ActiveStudents = _context.CourseTrainees.Where(ct => !ct.Course.IsDeleted).Select(ct => ct.TraineeId).Distinct().Count(),
                StudentsChange = curStudents - prevStudents,
                MonthlyRevenue = curRevenue,
                RevenueChange = revenueChange,
                ActiveInstitutes = _context.Trainers.Count(t => t.CourseTrainers.Any(ct => !ct.Course.IsDeleted)),
                InstitutesChange = curTrainers - prevTrainers
            };
        }

        public Dictionary<string, ChartData> GetChartData()
        {
            return new Dictionary<string, ChartData>
            {
                { "overview", BuildMonthlyChart(s => _context.Courses.Count(c => !c.IsDeleted && c.CreatedDate >= s && c.CreatedDate < s.AddMonths(1))) },
                { "courses",  BuildMonthlyChart(s => _context.Courses.Count(c => !c.IsDeleted && c.CreatedDate >= s && c.CreatedDate < s.AddMonths(1))) },
                { "students", BuildMonthlyChart(s => _context.Payments.Where(p => !p.IsDeleted && !p.Course.IsDeleted && p.CreatedDate >= s && p.CreatedDate < s.AddMonths(1)).Select(p => p.TraineeId).Distinct().Count()) },
                { "revenue",  BuildMonthlyChart(s => (int)_context.Payments.Where(p => !p.IsDeleted && !p.Course.IsDeleted && p.CreatedDate >= s && p.CreatedDate < s.AddMonths(1)).Sum(p => p.TotalAmount)) }
            };
        }

        private ChartData BuildMonthlyChart(Func<DateTime, int> valueSelector)
        {
            var now = DateTime.UtcNow;
            var labels = new List<string>();
            var values = new List<decimal>();
            for (int i = 5; i >= 0; i--)
            {
                var start = new DateTime(now.Year, now.Month, 1).AddMonths(-i);
                labels.Add(start.ToString("MMM yyyy"));
                values.Add(valueSelector(start));
            }
            return new ChartData { Labels = labels, Values = values };
        }

        // ─── Platform Health ─────────────────────────────────────────────────────
        private async Task<PlatformHealth> GetPlatformHealthAsync() => new PlatformHealth
        {
            TotalCertificates = await _context.Certificates.CountAsync(c => !c.IsDeleted),
            TotalLectures = await _context.Lectures.CountAsync(l => !l.IsDeleted),
            TotalExams = await _context.Exams.CountAsync(e => !e.IsDeleted),
            TotalPresences = await _context.Presences.CountAsync(p => p.IsPresent && !p.IsDeleted)
        };

        // ─── Recent Activity ─────────────────────────────────────────────────────
        private async Task<List<RecentActivityItem>> GetRecentActivityAsync()
        {
            var items = new List<RecentActivityItem>();

            // Last 4 enrollments
            var enrollments = await _context.CourseTrainees
                .Include(ct => ct.Trainee).ThenInclude(t => t.User)
                .Include(ct => ct.Course)
                .OrderByDescending(ct => ct.Course.CreatedDate)
                .Take(4)
                .ToListAsync();
            foreach (var e in enrollments)
                items.Add(new RecentActivityItem
                {
                    Type = "enrollment",
                    Title = "تسجيل جديد",
                    Description = $"{e.Trainee.User.FullName} سجّل في {e.Course.CourseName}",
                    Time = e.Course.CreatedDate,
                    Icon = "fas fa-user-plus",
                    Color = "#10b981"
                });

            // Last 4 payments
            var payments = await _context.Payments
                .Include(p => p.Trainee).ThenInclude(t => t.User)
                .Include(p => p.Course)
                .Where(p => !p.IsDeleted)
                .OrderByDescending(p => p.CreatedDate)
                .Take(4)
                .ToListAsync();
            foreach (var p in payments)
                items.Add(new RecentActivityItem
                {
                    Type = "payment",
                    Title = "دفعة جديدة",
                    Description = $"{p.Trainee.User.FullName} دفع {p.TotalAmount:N0} ريال لـ {p.Course.CourseName}",
                    Time = p.CreatedDate,
                    Icon = "fas fa-credit-card",
                    Color = "#3b82f6"
                });

            // Last 3 new courses
            var courses = await _context.Courses
                .Where(c => !c.IsDeleted)
                .OrderByDescending(c => c.CreatedDate)
                .Take(3)
                .ToListAsync();
            foreach (var c in courses)
                items.Add(new RecentActivityItem
                {
                    Type = "course",
                    Title = "دورة جديدة",
                    Description = $"أُضيفت دورة \"{c.CourseName}\"",
                    Time = c.CreatedDate,
                    Icon = "fas fa-book-open",
                    Color = "#8b5cf6"
                });

            return items.OrderByDescending(i => i.Time).Take(10).ToList();
        }

        // ─── Top Courses ─────────────────────────────────────────────────────────
        private async Task<List<TopCourseItem>> GetTopCoursesAsync()
        {
            var courses = await _context.Courses
                .Where(c => !c.IsDeleted)
                .Include(c => c.CourseTrainees)
                .Include(c => c.Lectures.Where(l => !l.IsDeleted))
                .Include(c => c.Ratings)
                .OrderByDescending(c => c.CourseTrainees.Count)
                .Take(6)
                .ToListAsync();

            return courses.Select(c => new TopCourseItem
            {
                CourseId = c.CourseId,
                CourseName = c.CourseName,
                EnrollmentCount = c.CourseTrainees.Count,
                AverageRating = c.Ratings.Any() ? Math.Round((decimal)c.Ratings.Average(r => r.Stars), 1) : 0m,
                LectureCount = c.Lectures.Count,
                Price = c.Price
            }).ToList();
        }

        // ─── Recent Payments ─────────────────────────────────────────────────────
        private async Task<List<RecentPaymentItem>> GetRecentPaymentsAsync()
        {
            return await _context.Payments
                .Where(p => !p.IsDeleted && !p.Course.IsDeleted)
                .Include(p => p.Trainee).ThenInclude(t => t.User)
                .Include(p => p.Course)
                .OrderByDescending(p => p.CreatedDate)
                .Take(8)
                .Select(p => new RecentPaymentItem
                {
                    TraineeName = p.Trainee.User.FullName,
                    CourseName = p.Course.CourseName,
                    Amount = p.TotalAmount,
                    Date = p.CreatedDate
                })
                .ToListAsync();
        }

        // ─── Top Trainers ────────────────────────────────────────────────────────
        private async Task<List<TopTrainerItem>> GetTopTrainersAsync()
        {
            var trainers = await _context.Trainers
                .Include(t => t.User)
                .Include(t => t.CourseTrainers)
                    .ThenInclude(ct => ct.Course)
                        .ThenInclude(c => c.CourseTrainees)
                .Take(20)
                .ToListAsync();

            return trainers
                .Select(t => new TopTrainerItem
                {
                    TrainerId = t.TrainerId,
                    Name = t.User.FullName,
                    Specialty = t.Specialty,
                    ProfilePic = t.User.ProfilePictureUrl ?? string.Empty,
                    CourseCount = t.CourseTrainers.Count(ct => !ct.Course.IsDeleted),
                    StudentCount = t.CourseTrainers
                        .Where(ct => !ct.Course.IsDeleted)
                        .SelectMany(ct => ct.Course.CourseTrainees)
                        .Select(ct => ct.TraineeId)
                        .Distinct().Count()
                })
                .OrderByDescending(t => t.StudentCount)
                .Take(5)
                .ToList();
        }
    }
}
