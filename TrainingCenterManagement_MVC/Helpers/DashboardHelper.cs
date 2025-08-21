using System;
using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml.InkML;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.EntityFrameworkCore;
using TrainingCenterManagement_MVC.Data;
using TrainingCenterManagement_MVC.Models;
using TrainingCenterManagement_MVC.ViewModels;

namespace TrainingCenterManagement_MVC.Helpers
{
    public class DashboardHelper
    {
        private readonly ApplicationDbContext context;
       

        public DashboardHelper(ApplicationDbContext context
            )
        {
            this.context = context;
            
        }

        // حساب إجمالي عدد الدورات غير المحذوفة
        public int GetTotalCourses()
        {
            return context.Courses
                .Count(c => !c.IsDeleted);
        }

        // حساب التغيير في عدد الدورات بين الشهر الحالي والشهر الماضي
        public int GetCoursesChange()
        {
            var currentDate = DateTime.UtcNow;
            var currentMonthStart = new DateTime(currentDate.Year, currentDate.Month, 1);
            var nextMonthStart = currentMonthStart.AddMonths(1);
            var previousMonthStart = currentMonthStart.AddMonths(-1);
            var previousMonthEnd = currentMonthStart.AddTicks(-1);

            var currentMonthCourses = context.Courses
                .Count(c => !c.IsDeleted && c.CreatedDate >= currentMonthStart && c.CreatedDate < nextMonthStart);

            var previousMonthCourses = context.Courses
                .Count(c => !c.IsDeleted && c.CreatedDate >= previousMonthStart && c.CreatedDate <= previousMonthEnd);

            return currentMonthCourses - previousMonthCourses;
        }

        // حساب عدد الطلاب النشطين (المسجلين في دورات غير محذوفة)
        public int GetActiveStudents()
        {
            return context.CourseTrainees
                .Where(ct => !ct.Course.IsDeleted)
                .Select(ct => ct.TraineeId)
                .Distinct()
                .Count();
        }

        // حساب التغيير في عدد الطلاب بين الشهر الحالي والشهر الماضي
        public int GetStudentsChange()
        {
            var currentDate = DateTime.UtcNow;
            var currentMonthStart = new DateTime(currentDate.Year, currentDate.Month, 1);
            var nextMonthStart = currentMonthStart.AddMonths(1);
            var previousMonthStart = currentMonthStart.AddMonths(-1);
            var previousMonthEnd = currentMonthStart.AddTicks(-1);

            var currentMonthStudents = context.Payments
                .Where(p => !p.IsDeleted && !p.Course.IsDeleted && p.CreatedDate >= currentMonthStart && p.CreatedDate < nextMonthStart)
                .Select(p => p.TraineeId)
                .Distinct()
                .Count();

            var previousMonthStudents = context.Payments
                .Where(p => !p.IsDeleted && !p.Course.IsDeleted && p.CreatedDate >= previousMonthStart && p.CreatedDate <= previousMonthEnd)
                .Select(p => p.TraineeId)
                .Distinct()
                .Count();

            return currentMonthStudents - previousMonthStudents;
        }

        // حساب إجمالي الإيرادات في الشهر الحالي
        public decimal GetMonthlyRevenue()
        {
            var currentDate = DateTime.UtcNow;
            var currentMonthStart = new DateTime(currentDate.Year, currentDate.Month, 1);
            var nextMonthStart = currentMonthStart.AddMonths(1);

            return context.Payments
                .Where(p => !p.IsDeleted && !p.Course.IsDeleted && p.CreatedDate >= currentMonthStart && p.CreatedDate < nextMonthStart)
                .Sum(p => p.TotalAmount);
        }

        // حساب نسبة التغيير في الإيرادات بين الشهر الحالي والشهر الماضي
        public decimal GetRevenueChange()
        {
            var currentDate = DateTime.UtcNow;
            var currentMonthStart = new DateTime(currentDate.Year, currentDate.Month, 1);
            var nextMonthStart = currentMonthStart.AddMonths(1);
            var previousMonthStart = currentMonthStart.AddMonths(-1);
            var previousMonthEnd = currentMonthStart.AddTicks(-1);

            var currentMonthRevenue = context.Payments
                .Where(p => !p.IsDeleted && !p.Course.IsDeleted && p.CreatedDate >= currentMonthStart && p.CreatedDate < nextMonthStart)
                .Sum(p => p.TotalAmount);

            var previousMonthRevenue = context.Payments
                .Where(p => !p.IsDeleted && !p.Course.IsDeleted && p.CreatedDate >= previousMonthStart && p.CreatedDate <= previousMonthEnd)
                .Sum(p => p.TotalAmount);

            if (previousMonthRevenue == 0)
                return currentMonthRevenue > 0 ? 100m : 0m;

            return ((currentMonthRevenue - previousMonthRevenue) / previousMonthRevenue) * 100m;
        }

        // حساب عدد المعاهد النشطة (يُفترض أنها عدد المدربين النشطين)
        public int GetActiveInstitutes()
        {
            return context.Trainers
                .Count(t => t.CourseTrainers.Any(ct => !ct.Course.IsDeleted));
        }

        // حساب التغيير في عدد المعاهد بين الشهر الحالي والشهر الماضي
        public int GetInstitutesChange()
        {
            var currentDate = DateTime.UtcNow;
            var currentMonthStart = new DateTime(currentDate.Year, currentDate.Month, 1);
            var nextMonthStart = currentMonthStart.AddMonths(1);
            var previousMonthStart = currentMonthStart.AddMonths(-1);
            var previousMonthEnd = currentMonthStart.AddTicks(-1);

            var currentMonthTrainers = context.CourseTrainers
                .Where(ct => !ct.Course.IsDeleted && ct.Course.CreatedDate >= currentMonthStart && ct.Course.CreatedDate < nextMonthStart)
                .Select(ct => ct.TrainerId)
                .Distinct()
                .Count();

            var previousMonthTrainers = context.CourseTrainers
                .Where(ct => !ct.Course.IsDeleted && ct.Course.CreatedDate >= previousMonthStart && ct.Course.CreatedDate <= previousMonthEnd)
                .Select(ct => ct.TrainerId)
                .Distinct()
                .Count();

            return currentMonthTrainers - previousMonthTrainers;
        }

        // دالة مساعدة للحصول على الأشهر الستة الماضية
        private List<(DateTime Start, DateTime End, string Label)> GetLastSixMonths()
        {
            var months = new List<(DateTime, DateTime, string)>();
            var currentDate = DateTime.UtcNow;

            for (int i = 5; i >= 0; i--)
            {
                var monthStart = new DateTime(currentDate.Year, currentDate.Month, 1).AddMonths(-i);
                var monthEnd = monthStart.AddMonths(1).AddTicks(-1);
                var monthLabel = monthStart.ToString("MMMM");
                months.Add((monthStart, monthEnd, monthLabel));
            }

            return months;
        }

        // حساب بيانات الرسم البياني للنمو العام (Overview)
        public ChartData GetOverviewChartData()
        {
            var months = GetLastSixMonths();
            var values = new List<decimal>();

            foreach (var (start, end, _) in months)
            {
                var count = context.Courses
                    .Count(c => !c.IsDeleted && c.CreatedDate >= start && c.CreatedDate <= end);
                values.Add(count);
            }

            return new ChartData
            {
                Labels = months.Select(m => m.Label).ToList(),
                Values = values
            };
        }

        // حساب بيانات الرسم البياني للدورات
        public ChartData GetCoursesChartData()
        {
            var months = GetLastSixMonths();
            var values = new List<decimal>();

            foreach (var (start, end, _) in months)
            {
                var count = context.Courses
                    .Count(c => !c.IsDeleted && c.CreatedDate >= start && c.CreatedDate <= end);
                values.Add(count);
            }

            return new ChartData
            {
                Labels = months.Select(m => m.Label).ToList(),
                Values = values
            };
        }

        // حساب بيانات الرسم البياني للطلاب
        public ChartData GetStudentsChartData()
        {
            var months = GetLastSixMonths();
            var values = new List<decimal>();

            foreach (var (start, end, _) in months)
            {
                var count = context.Payments
                    .Where(p => !p.IsDeleted && !p.Course.IsDeleted && p.CreatedDate >= start && p.CreatedDate <= end)
                    .Select(p => p.TraineeId)
                    .Distinct()
                    .Count();
                values.Add(count);
            }

            return new ChartData
            {
                Labels = months.Select(m => m.Label).ToList(),
                Values = values
            };
        }

        // حساب بيانات الرسم البياني للإيرادات
        public ChartData GetRevenueChartData()
        {
            var months = GetLastSixMonths();
            var values = new List<decimal>();

            foreach (var (start, end, _) in months)
            {
                var sum = context.Payments
                    .Where(p => !p.IsDeleted && !p.Course.IsDeleted && p.CreatedDate >= start && p.CreatedDate <= end)
                    .Sum(p => p.TotalAmount);
                values.Add(sum);
            }

            return new ChartData
            {
                Labels = months.Select(m => m.Label).ToList(),
                Values = values
            };
        }

        // دالة شاملة لإرجاع جميع بيانات الرسوم البيانية
        public Dictionary<string, ChartData> GetChartData()
        {
            return new Dictionary<string, ChartData>
            {
                { "overview", GetOverviewChartData() },
                { "courses", GetCoursesChartData() },
                { "students", GetStudentsChartData() },
                { "revenue", GetRevenueChartData() }
            };
        }

        // دالة شاملة لإرجاع كائن Stats
        public Stats GetDashboardStats()
        {
            return new Stats
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
        }
        //////////////////////////////////////////////////////////////////////

        /// <summary>
        /// ///////////////////////////////////
        /// </summary>
        /// <param name="_traineeId"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task<TraineeDashboardViewModel> GetDashboardDataAsync(Guid _traineeId,string userid)
        {
          
            var trainee = await context.Trainees
                
                .FirstOrDefaultAsync(u => u.TraineeId == _traineeId );
            var user = await context .Users.FirstOrDefaultAsync(u => u.Id == userid);
            if (trainee == null)
                throw new Exception("Trainee not found");

            var viewModel = new TraineeDashboardViewModel
            {
                FullName = user.FullName,
                ProfilePictureUrl = //trainee.Trainee?.ProfilePictureUrl ?? 
                "/images/default-profile.png",
                WelcomeMessage = GenerateWelcomeMessage(user.FullName),
                OverallProgress = await CalculateOverallProgressAsync( _traineeId),
                Stats = await GetDashboardStatsAsync(_traineeId),
                ChartData = await GetChartDataAsync(_traineeId),
                CurrentCourses = await GetCurrentCoursesAsync(_traineeId),
                RecommendedCourses = await GetRecommendedCoursesAsync(_traineeId),
                UpcomingEvents = await GetUpcomingEventsAsync(_traineeId),
                Certificates = await GetCertificatesAsync(_traineeId)
            };

            return viewModel;
        }

        private string GenerateWelcomeMessage(string fullName)
        {
            return $"مرحبًا {fullName}! استمر في تقدمك الرائع!";
        }

        private async Task<int> CalculateOverallProgressAsync(Guid _traineeId)
        {
            var courseTrainees = await context.CourseTrainees
                .Where(ct => ct.TraineeId == _traineeId)
                .Include(ct => ct.Course)
                .ThenInclude(c => c.Lectures)
                .ToListAsync();

            if (!courseTrainees.Any())
                return 0;

            int totalLectures = courseTrainees.Sum(ct => ct.Course.Lectures.Count(l => !l.IsDeleted));
            var attendedLectures = await context.Presences
                .Where(p => p.TraineeId == _traineeId && p.IsPresent && !p.IsDeleted)
                .CountAsync();

            return totalLectures > 0 ? (attendedLectures * 100) / totalLectures : 0;
        }

        private async Task<DashboardStats> GetDashboardStatsAsync(Guid _traineeId)
        {
            var enrolledCourses = await context.CourseTrainees
                .Where(ct => ct.TraineeId == _traineeId)
            .CountAsync();

            var completedExams = await context.Certificates
                .Where(c => c.TraineeId == _traineeId && !c.IsDeleted)
            .CountAsync();

            var attendedLectures = await context.Presences
                .Where(p => p.TraineeId == _traineeId && p.IsPresent && !p.IsDeleted)
            .CountAsync();

            var totalLectures = await context.Presences
                .Where(p => p.TraineeId == _traineeId && !p.IsDeleted)
                .CountAsync();

            var attendanceRate = totalLectures > 0 ? (attendedLectures * 100) / totalLectures : 0;

            // Placeholder for completed hours (assuming each lecture is 1 hour for simplicity)
            var completedHours = attendedLectures;

            return new DashboardStats
            {
                OverallProgress = await CalculateOverallProgressAsync( _traineeId),
                EnrolledCourses = enrolledCourses,
                CompletedHours = completedHours,
                CompletedExams = completedExams,
                AttendanceRate = attendanceRate
            };
        }

        private async Task<DashboardChartData> GetChartDataAsync(Guid _traineeId)
        {
            var labels = new List<string>();
            var values = new List<decimal>();
            var startDate = DateTime.UtcNow.AddMonths(-1);
            var endDate = DateTime.UtcNow;

            for (var date = startDate; date <= endDate; date = date.AddDays(7))
            {
                labels.Add(date.ToString("MMM dd"));
                var presences = await context.Presences
                    .Where(p => p.TraineeId == _traineeId && p.Lecture.LectureDate >= date && p.Lecture.LectureDate < date.AddDays(7) && p.IsPresent && !p.IsDeleted)
                    .CountAsync();
                values.Add(presences);
            }

            return new DashboardChartData
            {
                Analytics = new ChartData
                {
                    Labels = labels,
                    Values = values
                }
            };
        }

        private async Task<List<CurrentCourse>> GetCurrentCoursesAsync(Guid _traineeId)
        {
            var courseTrainees = await context.CourseTrainees
                .Where(ct => ct.TraineeId == _traineeId)
                .Include(ct => ct.Course)
                .ThenInclude(c => c.Lectures)
                .ToListAsync();

            var currentCourses = new List<CurrentCourse>();
            foreach (var ct in courseTrainees)
            {
                var totalLectures = ct.Course.Lectures.Count(l => !l.IsDeleted);
                var attendedLectures = await context.Presences
                    .Where(p => p.TraineeId == _traineeId && p.Lecture.CourseId == ct.CourseId && p.IsPresent && !p.IsDeleted)
                    .CountAsync();

                var progress = totalLectures > 0 ? (attendedLectures * 100) / totalLectures : 0;
                var remainingLectures = totalLectures - attendedLectures;

                currentCourses.Add(new CurrentCourse
                {
                    Id = ct.CourseId,
                    Name = ct.Course.CourseName,
                    Progress = progress,
                    Remaining = $"{remainingLectures} محاضرات متبقية"
                });
            }

            return currentCourses;
        }

        private async Task<List<RecommendedCourse>> GetRecommendedCoursesAsync(Guid _traineeId)
        {
            // Simple recommendation logic: suggest courses not yet enrolled by the trainee
            var enrolledCourseIds = await context.CourseTrainees
                .Where(ct => ct.TraineeId == _traineeId)
                .Select(ct => ct.CourseId)
            .ToListAsync();

            var recommendedCourses = await context.Courses
                .Where(c => !c.IsDeleted && !enrolledCourseIds.Contains(c.CourseId))
                .Take(4)
                .Select(c => new RecommendedCourse
                {
                    Id = c.CourseId,
                    Name = c.CourseName,
                    Description = c.Description ?? "دورة رائعة لتعزيز مهاراتك!"
                })
                .ToListAsync();

            return recommendedCourses;
        }

        private async Task<List<UpcomingEvent>> GetUpcomingEventsAsync(Guid _traineeId)
        {
            var upcomingLectures = await context.Lectures
                .Where(l => l.LectureDate >= DateTime.UtcNow && !l.IsDeleted)
                .Include(l => l.Course)
                .ThenInclude(c => c.CourseTrainees)
                .Where(l => l.Course.CourseTrainees.Any(ct => ct.TraineeId == _traineeId))
                .Take(5)
                .Select(l => new UpcomingEvent
                {
                    Title = l.Title,
                    Date = l.LectureDate.ToString("MMM dd, yyyy HH:mm"),
                    Link = l.VideoUrl ?? "https://zoom.us/j/123456789" // Placeholder link
                })
            .ToListAsync();

            var upcomingExams = await context.Exams
                .Where(e => e.ExamDate >= DateTime.UtcNow && !e.IsDeleted)
                .Include(e => e.Course)
                .ThenInclude(c => c.CourseTrainees)
                .Where(e => e.Course.CourseTrainees.Any(ct => ct.TraineeId == _traineeId))
                .Take(5)
                .Select(e => new UpcomingEvent
                {
                    Title = e.ExamName,
                    Date = e.ExamDate.ToString("MMM dd, yyyy HH:mm"),
                    Link = "https://zoom.us/j/123456789" // Placeholder link
                })
                .ToListAsync();

            return upcomingLectures.Concat(upcomingExams)
                .OrderBy(e => DateTime.Parse(e.Date))
                .Take(5)
                .ToList();
        }

        private async Task<List<CertificateViewModel>> GetCertificatesAsync(Guid _traineeId)
        {
            return await context.Certificates
                .Where(c => c.TraineeId == _traineeId && !c.IsDeleted)
                .Include(c => c.Course)
                .Select(c => new CertificateViewModel
                {
                    Id = c.CertificateId,
                    Name = c.Course.CourseName
                })
                .ToListAsync();
        }

    }
}