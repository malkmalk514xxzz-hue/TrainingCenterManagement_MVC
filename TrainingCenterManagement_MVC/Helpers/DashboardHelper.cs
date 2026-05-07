using Microsoft.EntityFrameworkCore;
using TrainingCenterManagement_MVC.Data;
using TrainingCenterManagement_MVC.ViewModels;

namespace TrainingCenterManagement_MVC.Helpers
{
    /// <summary>
    /// Admin and Receptionist dashboard statistics.
    /// </summary>
    public class DashboardHelper
    {
        private readonly ApplicationDbContext _context;

        public DashboardHelper(ApplicationDbContext context)
        {
            _context = context;
        }

        public int GetTotalCourses() =>
            _context.Courses.Count(c => !c.IsDeleted);

        public int GetCoursesChange()
        {
            var now = DateTime.UtcNow;
            var thisMonth = new DateTime(now.Year, now.Month, 1);
            var nextMonth = thisMonth.AddMonths(1);
            var prevMonth = thisMonth.AddMonths(-1);

            int current = _context.Courses.Count(c => !c.IsDeleted && c.CreatedDate >= thisMonth && c.CreatedDate < nextMonth);
            int previous = _context.Courses.Count(c => !c.IsDeleted && c.CreatedDate >= prevMonth && c.CreatedDate < thisMonth);
            return current - previous;
        }

        public int GetActiveStudents() =>
            _context.CourseTrainees
                .Where(ct => !ct.Course.IsDeleted)
                .Select(ct => ct.TraineeId)
                .Distinct()
                .Count();

        public int GetStudentsChange()
        {
            var now = DateTime.UtcNow;
            var thisMonth = new DateTime(now.Year, now.Month, 1);
            var nextMonth = thisMonth.AddMonths(1);
            var prevMonth = thisMonth.AddMonths(-1);

            int current = _context.Payments
                .Where(p => !p.IsDeleted && !p.Course.IsDeleted && p.CreatedDate >= thisMonth && p.CreatedDate < nextMonth)
                .Select(p => p.TraineeId).Distinct().Count();
            int previous = _context.Payments
                .Where(p => !p.IsDeleted && !p.Course.IsDeleted && p.CreatedDate >= prevMonth && p.CreatedDate < thisMonth)
                .Select(p => p.TraineeId).Distinct().Count();
            return current - previous;
        }

        public decimal GetMonthlyRevenue()
        {
            var now = DateTime.UtcNow;
            var start = new DateTime(now.Year, now.Month, 1);
            return _context.Payments
                .Where(p => !p.IsDeleted && !p.Course.IsDeleted && p.CreatedDate >= start)
                .Sum(p => p.TotalAmount);
        }

        public decimal GetRevenueChange()
        {
            var now = DateTime.UtcNow;
            var thisMonth = new DateTime(now.Year, now.Month, 1);
            var nextMonth = thisMonth.AddMonths(1);
            var prevMonth = thisMonth.AddMonths(-1);

            decimal cur = _context.Payments.Where(p => !p.IsDeleted && !p.Course.IsDeleted && p.CreatedDate >= thisMonth && p.CreatedDate < nextMonth).Sum(p => p.TotalAmount);
            decimal prev = _context.Payments.Where(p => !p.IsDeleted && !p.Course.IsDeleted && p.CreatedDate >= prevMonth && p.CreatedDate < thisMonth).Sum(p => p.TotalAmount);
            if (prev == 0) return cur > 0 ? 100m : 0m;
            return ((cur - prev) / prev) * 100m;
        }

        public int GetActiveInstitutes() =>
            _context.Trainers.Count(t => t.CourseTrainers.Any(ct => !ct.Course.IsDeleted));

        public int GetInstitutesChange()
        {
            var now = DateTime.UtcNow;
            var thisMonth = new DateTime(now.Year, now.Month, 1);
            var nextMonth = thisMonth.AddMonths(1);
            var prevMonth = thisMonth.AddMonths(-1);

            int current = _context.CourseTrainers
                .Where(ct => !ct.Course.IsDeleted && ct.Course.CreatedDate >= thisMonth && ct.Course.CreatedDate < nextMonth)
                .Select(ct => ct.TrainerId).Distinct().Count();
            int previous = _context.CourseTrainers
                .Where(ct => !ct.Course.IsDeleted && ct.Course.CreatedDate >= prevMonth && ct.Course.CreatedDate < thisMonth)
                .Select(ct => ct.TrainerId).Distinct().Count();
            return current - previous;
        }

        public Stats GetDashboardStats() => new Stats
        {
            TotalCourses = GetTotalCourses(),
            CoursesChange = GetCoursesChange(),
            ActiveStudents = GetActiveStudents(),
            StudentsChange = GetStudentsChange(),
            MonthlyRevenue = GetMonthlyRevenue(),
            RevenueChange = GetRevenueChange(),
            ActiveInstitutes = GetActiveInstitutes(),
            InstitutesChange = GetInstitutesChange()
        };

        public Dictionary<string, ChartData> GetChartData() => new()
        {
            { "overview", GetMonthlyChartData(start => _context.Courses.Count(c => !c.IsDeleted && c.CreatedDate >= start && c.CreatedDate < start.AddMonths(1))) },
            { "courses",  GetMonthlyChartData(start => _context.Courses.Count(c => !c.IsDeleted && c.CreatedDate >= start && c.CreatedDate < start.AddMonths(1))) },
            { "students", GetMonthlyChartData(start => _context.Payments.Where(p => !p.IsDeleted && !p.Course.IsDeleted && p.CreatedDate >= start && p.CreatedDate < start.AddMonths(1)).Select(p => p.TraineeId).Distinct().Count()) },
            { "revenue",  GetMonthlyChartData(start => (int)_context.Payments.Where(p => !p.IsDeleted && !p.Course.IsDeleted && p.CreatedDate >= start && p.CreatedDate < start.AddMonths(1)).Sum(p => p.TotalAmount)) }
        };

        private ChartData GetMonthlyChartData(Func<DateTime, int> valueSelector)
        {
            var now = DateTime.UtcNow;
            var labels = new List<string>();
            var values = new List<decimal>();

            for (int i = 5; i >= 0; i--)
            {
                var start = new DateTime(now.Year, now.Month, 1).AddMonths(-i);
                labels.Add(start.ToString("MMMM"));
                values.Add(valueSelector(start));
            }

            return new ChartData { Labels = labels, Values = values };
        }
    }
}
