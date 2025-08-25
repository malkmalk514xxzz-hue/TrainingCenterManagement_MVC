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
        ////////////////////////////////Admin/////////////////////////////////

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
        ////////////////////////////////////////////////////////////////////// Trainee  /////////////////////////////////

        /// <summary>
        /// ///////////////////////////////////
        /// </summary>
        /// <param name="_traineeId"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task<TraineeDashboardViewModel> GetDashboardDataAsync(Guid _traineeId, string userid)
        {

            var trainee = await context.Trainees

                .FirstOrDefaultAsync(u => u.TraineeId == _traineeId);
            var user = await context.Users.FirstOrDefaultAsync(u => u.Id == userid);
            if (trainee == null)
                throw new Exception("Trainee not found");

            var viewModel = new TraineeDashboardViewModel
            {
                FullName = user.FullName,
                ProfilePictureUrl = //trainee.Trainee?.ProfilePictureUrl ?? 
                "/images/team2.png",
                WelcomeMessage = GenerateWelcomeMessage(user.FullName),
                OverallProgress = await CalculateOverallProgressAsync(_traineeId),
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
                OverallProgress = await CalculateOverallProgressAsync(_traineeId),
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


        /////////////////////////////////////// Trainer //////////////////////////////////////////
        public async Task<TrainerDashboardViewModel> GetTrainerDashboardAsync(Guid trainerId, string fullName)
        {
            var trainer = await context.Trainers
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.TrainerId == trainerId);

            var model = new TrainerDashboardViewModel
            {
                FullName = fullName,
                ProfilePictureUrl = "/images/default-profile.png", // Assume static; add field to User if needed
                Specialization = trainer?.Specialty ?? "General",
                Availability = "Available", // Implement logic, e.g., based on schedule
                WelcomeMessage = "Welcome back to your Trainer Dashboard!",
                OverallProgress = await GetTrainerOverallProgressAsync(trainerId)
               
            };

            model.Stats = new TrainerDashboardStats
            {
                TotalProgress = await GetTrainerTotalCoursesAsync(trainerId),
                CurrentCourses = await GetTrainerActiveStudentsAsync(trainerId),
                Certificates = await GetTrainerIssuedCertificatesCountAsync(trainerId),
                UpcomingEvents = await GetTrainerUpcomingEventsCountAsync(trainerId)
            };

            model.CurrentCourses = await GetTrainerCurrentCoursesAsync(trainerId);
            model.RecommendedCourses = await GetTrainerRecommendedCoursesAsync(trainerId);
            model.UpcomingEvents = await GetTrainerUpcomingEventsAsync(trainerId);
            model.Certificates = await GetTrainerCertificatesAsync(trainerId);
            model.ChartData = await GetTrainerAnalyticsAsync(trainerId);
            Console.WriteLine("/***************************************************///////////////////////////////////////////***************//////////");
            foreach (var x in model.ChartData.Analytics.Labels)
            {  Console.WriteLine(x); }
            Console.WriteLine("/***************************************************///////////////////////////////////////////***************//////////");
            foreach (var x in model.ChartData.Analytics.Values)
            { Console.WriteLine(x); }
           
            model.KPIs = await GetTrainerKPIsAsync(trainerId);
            
            model.ScheduleEvents = await GetTrainerScheduleEventsAsync(trainerId);
            model.StudentsByCourse = await GetStudentsByCourseAsync(trainerId);
            model.Assignments = await GetTrainerAssignmentsAsync(trainerId);
            model.AttendanceRecords = await GetTrainerAttendanceRecordsAsync(trainerId);
            model.Reports = await GetTrainerReportsAsync(trainerId);
            model.Resources = await GetTrainerResourcesAsync(trainerId);

            return model;
        }

        private async Task<int> GetTrainerOverallProgressAsync(Guid trainerId)
        {
            var courseIds = await context.CourseTrainers
                .Where(ct => ct.TrainerId == trainerId && !ct.Course.IsDeleted)
                .Select(ct => ct.CourseId)
                .ToListAsync();

            var totalPresences = await context.Presences
                .Where(p => courseIds.Contains(p.Lecture.CourseId) && !p.IsDeleted)
                .CountAsync();

            var presentPresences = await context.Presences
                .Where(p => courseIds.Contains(p.Lecture.CourseId) && p.IsPresent && !p.IsDeleted)
                .CountAsync();

            return totalPresences > 0 ? (presentPresences * 100) / totalPresences : 0;
        }

        private async Task<int> GetTrainerTotalCoursesAsync(Guid trainerId)
        {
            return await context.CourseTrainers
                .CountAsync(ct => ct.TrainerId == trainerId && !ct.Course.IsDeleted);
        }

        private async Task<int> GetTrainerActiveStudentsAsync(Guid trainerId)
        {
            return await context.CourseTrainers
                .Where(ct => ct.TrainerId == trainerId && !ct.Course.IsDeleted)
                .SelectMany(ct => ct.Course.CourseTrainees)
                .Select(ct => ct.TraineeId)
                .Distinct()
                .CountAsync();
        }

        private async Task<int> GetTrainerIssuedCertificatesCountAsync(Guid trainerId)
        {
            return await context.Certificates
                .CountAsync(c => c.TrainerId == trainerId && !c.IsDeleted);
        }

        private async Task<int> GetTrainerUpcomingEventsCountAsync(Guid trainerId)
        {
            var upcomingLectures = await context.Lectures
                .CountAsync(l => l.LectureDate >= DateTime.UtcNow && !l.IsDeleted && l.Course.CourseTrainers.Any(ct => ct.TrainerId == trainerId));

            var upcomingExams = await context.Exams
                .CountAsync(e => e.ExamDate >= DateTime.UtcNow && !e.IsDeleted && e.Course.CourseTrainers.Any(ct => ct.TrainerId == trainerId));

            return upcomingLectures + upcomingExams;
        }

        private async Task<List<CurrentCourse>> GetTrainerCurrentCoursesAsync(Guid trainerId)
        {
            var courseTrainers = await context.CourseTrainers
                .Where(ct => ct.TrainerId == trainerId && !ct.Course.IsDeleted)
                .Include(ct => ct.Course)
                .ThenInclude(c => c.Lectures.Where(l => !l.IsDeleted))
                .ToListAsync();

            var currentCourses = new List<CurrentCourse>();
            foreach (var ct in courseTrainers)
            {
                var course = ct.Course;
                var totalLectures = course.Lectures.Count();
                var completedLectures = course.Lectures.Count(l => l.LectureDate < DateTime.UtcNow);

                var progress = totalLectures > 0 ? (completedLectures * 100) / totalLectures : 0;
                var remainingLectures = totalLectures - completedLectures;

                currentCourses.Add(new CurrentCourse
                {
                    Id = course.CourseId,
                    Name = course.CourseName,
                    Progress = progress,
                    Remaining = $"{remainingLectures} lectures remaining"
                });
            }

            return currentCourses;
        }

        private async Task<List<RecommendedCourse>> GetTrainerRecommendedCoursesAsync(Guid trainerId)
        {
            var assignedCourseIds = await context.CourseTrainers
                .Where(ct => ct.TrainerId == trainerId)
                .Select(ct => ct.CourseId)
                .ToListAsync();

            return await context.Courses
                .Where(c => !c.IsDeleted && !assignedCourseIds.Contains(c.CourseId))
                .Take(4)
                .Select(c => new RecommendedCourse
                {
                    Id = c.CourseId,
                    Name = c.CourseName,
                    Description = c.Description ?? "Great course to teach!"
                })
                .ToListAsync();
        }

        private async Task<List<UpcomingEvent>> GetTrainerUpcomingEventsAsync(Guid trainerId)
        {
            var upcomingLectures = await context.Lectures
                .Where(l => l.LectureDate >= DateTime.UtcNow && !l.IsDeleted && l.Course.CourseTrainers.Any(ct => ct.TrainerId == trainerId))
                .OrderBy(l => l.LectureDate)
                .Take(5)
                .Select(l => new UpcomingEvent
                {
                    Title = l.Title,
                    Date = l.LectureDate.ToString("MMM dd, yyyy HH:mm"),
                    Link = l.VideoUrl ?? "https://zoom.us/j/123456789"
                })
                .ToListAsync();

            var upcomingExams = await context.Exams
                .Where(e => e.ExamDate >= DateTime.UtcNow && !e.IsDeleted && e.Course.CourseTrainers.Any(ct => ct.TrainerId == trainerId))
                .OrderBy(e => e.ExamDate)
                .Take(5)
                .Select(e => new UpcomingEvent
                {
                    Title = e.ExamName,
                    Date = e.ExamDate.ToString("MMM dd, yyyy HH:mm"),
                    Link = "https://zoom.us/j/123456789"
                })
                .ToListAsync();

            return upcomingLectures.Concat(upcomingExams)
                .OrderBy(e => DateTime.Parse(e.Date))
                .Take(5)
                .ToList();
        }

        private async Task<List<CertificateViewModel>> GetTrainerCertificatesAsync(Guid trainerId)
        {
            return await context.Certificates
                .Where(c => c.TrainerId == trainerId && !c.IsDeleted)
                .Include(c => c.Course)
                .Select(c => new CertificateViewModel
                {
                    Id = c.CertificateId,
                    Name = c.Course.CourseName
                })
                .ToListAsync();
        }

        // دالة لجلب بيانات الرسم البياني لتحليلات المدرب (نسبة الحضور الأسبوعية للدورات المرتبطة به)
        private async Task<DashboardChartData> GetTrainerAnalyticsAsync(Guid trainerId)
        {
            // إنشاء قائمتين لتخزين تسميات الأسابيع (مثل "Jul 25") وقيم نسب الحضور (مثل 80%)
            var labels = new List<string>();
            var values = new List<decimal>();

            // تحديد النطاق الزمني للبيانات: منذ شهر مضى حتى الآن
            var startDate = DateTime.UtcNow.AddMonths(-1);
            var endDate = DateTime.UtcNow;

            // جلب معرفات الدورات المرتبطة بالمدرب (trainerId) من جدول CourseTrainers، مع استبعاد الدورات المحذوفة
            var courseIds = await context.CourseTrainers
                .Where(ct => ct.TrainerId == trainerId && !ct.Course.IsDeleted)
                .Select(ct => ct.CourseId)
                .ToListAsync();

            // التحقق مما إذا كان المدرب ليس لديه دورات، وإرجاع كائن فارغ إذا لم توجد دورات
            if (!courseIds.Any())
            {
                return new DashboardChartData
                {
                    Analytics = new ChartData
                    {
                        Labels = new List<string>(),
                        Values = new List<decimal>()
                    }
                };
            }

            // جلب جميع سجلات الحضور للدورات المرتبطة بالمدرب خلال الشهر الماضي، مع التأكد من أن المحاضرات غير محذوفة وأن LectureDate ليست null
            var presences = await context.Presences
                .Include(p => p.Lecture)
                .Where(p => courseIds.Contains(p.Lecture.CourseId)
                         && p.Lecture.LectureDate != null
                         && p.Lecture.LectureDate >= startDate
                         && p.Lecture.LectureDate <= endDate
                         && !p.IsDeleted
                         && !p.Lecture.IsDeleted)
                .ToListAsync();

            // حلقة لمعالجة كل أسبوع في النطاق الزمني (بفاصل 7 أيام)
            for (var date = startDate; date <= endDate; date = date.AddDays(7))
            {
                // إضافة تسمية الأسبوع (مثل "Jul 25") إلى قائمة التسميات
                labels.Add(date.ToString("MMM dd"));

                // تصفية سجلات الحضور للأسبوع الحالي (من date إلى date + 7 أيام)
                var presencesInWeek = presences
                    .Where(p => p.Lecture.LectureDate >= date && p.Lecture.LectureDate < date.AddDays(7))
                    .ToList();

                // حساب عدد سجلات الحضور الكلية في الأسبوع
                var total = presencesInWeek.Count;

                // حساب عدد الحاضرين (IsPresent = true) في الأسبوع
                var present = presencesInWeek.Count(p => p.IsPresent);

                // حساب نسبة الحضور (الحاضرون / الإجمالي * 100)، أو 0 إذا لم يكن هناك سجلات
                var attendance = total > 0 ? (present * 100m) / total : 0m;

                // إضافة نسبة الحضور إلى قائمة القيم
                values.Add(attendance);
            }

            // إرجاع كائن DashboardChartData يحتوي على التسميات ونسب الحضور لاستخدامها في الرسم البياني
            return new DashboardChartData
            {
                Analytics = new ChartData
                {
                    Labels = labels,
                    Values = values
                }
            };
        }

        private async Task<TrainerKPIs> GetTrainerKPIsAsync(Guid trainerId)
        {
            var courseIds = await context.CourseTrainers
                .Where(ct => ct.TrainerId == trainerId && !ct.Course.IsDeleted)
                .Select(ct => ct.CourseId)
                .ToListAsync();

            var currentCourses = courseIds.Count;

            var enrolledStudents = await context.CourseTrainees
                .Where(ct => courseIds.Contains(ct.CourseId) && !ct.Course.IsDeleted)
                .Select(ct => ct.TraineeId)
                .Distinct()
                .CountAsync();

            // Average attendance last month
            var lastMonthStart = DateTime.UtcNow.AddMonths(-1);
            var presencesLastMonth = await context.Presences
                .Include(p => p.Lecture)
                .Where(p => courseIds.Contains(p.Lecture.CourseId)
                         && p.Lecture.LectureDate >= lastMonthStart
                         && !p.IsDeleted
                         && !p.Lecture.IsDeleted)
                .ToListAsync();

            var totalPresences = presencesLastMonth.Count;
            var presentCount = presencesLastMonth.Count(p => p.IsPresent);
            var averageAttendance = totalPresences > 0 ? (presentCount * 100m) / totalPresences : 0m;

            // Ungraded assignments: Assume no Assignment model, placeholder 0
            var ungradedAssignments = 0; // Implement if Assignment model added

            // Average rating: Assume no Rating model, placeholder
            var averageRating = 4.5m; // Query if added

            return new TrainerKPIs
            {
                CurrentCourses = currentCourses,
                EnrolledStudents = enrolledStudents,
                AverageAttendance = averageAttendance,
                UngradedAssignments = ungradedAssignments,
                AverageRating = averageRating
            };
        }
     


        private async Task<List<ScheduleEvent>> GetTrainerScheduleEventsAsync(Guid trainerId)
        {
            var lectures = await context.Lectures
                .Where(l => !l.IsDeleted && l.Course.CourseTrainers.Any(ct => ct.TrainerId == trainerId))
                .Select(l => new ScheduleEvent
                {
                    Id = l.LectureId,
                    Title = l.Title,
                    Start = l.LectureDate,
                    End = l.LectureDate.AddHours(1), // Assume 1 hour duration
                    Type = "Lecture",
                    Link = l.VideoUrl
                })
                .ToListAsync();

            var exams = await context.Exams
                .Where(e => !e.IsDeleted && e.Course.CourseTrainers.Any(ct => ct.TrainerId == trainerId))
                .Select(e => new ScheduleEvent
                {
                    Id = e.ExamId,
                    Title = e.ExamName,
                    Start = e.ExamDate,
                    End = e.ExamDate.AddHours(2), // Assume 2 hours
                    Type = "Exam",
                    Link = null // Add if available
                })
                .ToListAsync();

            // Add meetings if model exists

            var events = lectures.Cast<ScheduleEvent>().Concat(exams).ToList();
            return events;
        }

        private async Task<Dictionary<Guid, List<StudentInfo>>> GetStudentsByCourseAsync(Guid trainerId)
        {
            var courseIds = await context.CourseTrainers
                .Where(ct => ct.TrainerId == trainerId && !ct.Course.IsDeleted)
                .Select(ct => ct.CourseId)
                .ToListAsync();

            var studentsByCourse = new Dictionary<Guid, List<StudentInfo>>();

            foreach (var courseId in courseIds)
            {
                        
                var Courseob = await context.Courses.FirstOrDefaultAsync(c=>c.CourseId == courseId);
                var students = await context.CourseTrainees
                    .Where(ct => ct.CourseId == courseId)
                    .Include(ct => ct.Trainee)
                    .ThenInclude(t => t.User)
                    .Select(ct => new StudentInfo
                    {
                        Id = ct.TraineeId,
                        Name = ct.Trainee.User.FullName,
                        AttendanceStatus = "Present", // Calculate based on recent presence
                        Grade = 0, // Assume from certificates or add model
                        Contact = ct.Trainee.User.PhoneNumber,
                        CourseName = Courseob.CourseName
                    })
                    .ToListAsync();

                // Enhance with actual attendance status, e.g., average presence
                foreach (var student in students)
                {
                    var presences = await context.Presences
                        .Where(p => p.TraineeId == student.Id
                                 && p.Lecture.CourseId == courseId
                                 && !p.IsDeleted)
                        .ToListAsync();

                    var total = presences.Count;
                    var present = presences.Count(p => p.IsPresent);
                    student.AttendanceStatus = total > 0 ? $"{(present * 100 / total)}% Attendance" : "No Data";
                }

                studentsByCourse[courseId] = students;
            }

            return studentsByCourse;
        }

        private async Task<List<Assignment>> GetTrainerAssignmentsAsync(Guid trainerId)
        {
            // No Assignment model provided; placeholder
            // If added, query like:
            // return await context.Assignments
            //     .Where(a => a.TrainerId == trainerId)
            //     .ToListAsync();
            return new List<Assignment>();
        }

        private async Task<List<AttendanceRecord>> GetTrainerAttendanceRecordsAsync(Guid trainerId)
        {
            var courseIds = await context.CourseTrainers
                .Where(ct => ct.TrainerId == trainerId && !ct.Course.IsDeleted)
                .Select(ct => ct.CourseId)
                .ToListAsync();

            var lectures = await context.Lectures
                .Where(l => courseIds.Contains(l.CourseId) && !l.IsDeleted)
                .Include(l => l.Presences.Where(p => !p.IsDeleted))
                .ThenInclude(p => p.Trainee)
                .ThenInclude(t => t.User)
                .ToListAsync();

            var records = new List<AttendanceRecord>();

            foreach (var lecture in lectures)
            {
                var record = new AttendanceRecord
                {
                    LectureId = lecture.LectureId,
                    Date = lecture.LectureDate,
                    Students = lecture.Presences.Select(p => new StudentAttendance
                    {
                        StudentId = p.TraineeId,
                        Present = p.IsPresent,
                        Notes = "" // Add if field exists
                    }).ToList()
                };
                records.Add(record);
            }

            return records.OrderByDescending(r => r.Date).Take(10).ToList(); // Recent 10
        }

        private async Task<List<Report>> GetTrainerReportsAsync(Guid trainerId)
        {
            // Placeholder; generate reports based on data
            // For example, student performance report
            var reports = new List<Report>
            {
                new Report { Title = "Attendance Report", Data = "JSON chart data" },
                // Query and serialize
            };
            return reports;
        }

        private async Task<List<Resource>> GetTrainerResourcesAsync(Guid trainerId)
        {
            // No Resource model; placeholder
            // If added, query resources linked to trainer or courses
            return new List<Resource>();
        }
    }
    }