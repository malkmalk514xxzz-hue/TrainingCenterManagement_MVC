using Microsoft.EntityFrameworkCore;
using TrainingCenterManagement_MVC.Data;
using TrainingCenterManagement_MVC.ViewModels;

namespace TrainingCenterManagement_MVC.Helpers
{
    public class TraineeDashboardHelper
    {
        private readonly ApplicationDbContext _context;

        public TraineeDashboardHelper(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<TraineeDashboardViewModel> GetDashboardDataAsync(Guid traineeId, string userId)
        {
            var trainee = await _context.Trainees.FirstOrDefaultAsync(u => u.TraineeId == traineeId);
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (trainee == null) throw new Exception("Trainee not found");

            var model = new TraineeDashboardViewModel
            {
                FullName = user.FullName,
                Email = user.Email ?? string.Empty,
                ProfilePictureUrl = !string.IsNullOrEmpty(user.ProfilePictureUrl)
                    ? user.ProfilePictureUrl
                    : "/images/default-profile.png",
                WelcomeMessage = $"مرحبًا {user.FullName}! استمر في تقدمك الرائع!",
                OverallProgress = await CalculateOverallProgressAsync(traineeId),
                Stats = await GetStatsAsync(traineeId),
                ChartData = await GetChartDataAsync(traineeId),
                CurrentCourses = await GetCurrentCoursesAsync(traineeId),
                RecommendedCourses = await GetRecommendedCoursesAsync(traineeId),
                UpcomingEvents = await GetUpcomingEventsAsync(traineeId),
                Certificates = await GetCertificatesAsync(traineeId),
                Notifications = await GetNotificationsAsync(traineeId),
                UpcomingLiveSessions = await GetUpcomingLiveSessionsAsync(traineeId),
                BalanceUSD = trainee.BalanceUSD,
                BalanceSYP = trainee.BalanceSYP,
                TotalEquivalentUSD = trainee.BalanceUSD + (trainee.BalanceSYP / 130m),
                TransferCode = trainee.TransferCode,
            };

            var shamCashPayments = await GetShamCashPaymentsAsync(traineeId);
            var adminPayments    = await GetAdminPaymentsAsync(traineeId);
            model.ShamCashPayments = shamCashPayments;
            model.AdminPayments    = adminPayments;
            model.TotalShamCashSYP = shamCashPayments.Sum(p => p.Amount);
            model.TotalAdminSYP    = adminPayments.Sum(p => p.Amount);

            return model;
        }

        private async Task<int> CalculateOverallProgressAsync(Guid traineeId)
        {
            var courseTrainees = await _context.CourseTrainees
                .Where(ct => ct.TraineeId == traineeId)
                .Include(ct => ct.Course).ThenInclude(c => c.Lectures)
                .ToListAsync();

            if (!courseTrainees.Any()) return 0;

            int totalLectures = courseTrainees.Sum(ct => ct.Course.Lectures.Count(l => !l.IsDeleted));
            int attended = await _context.Presences
                .CountAsync(p => p.TraineeId == traineeId && p.IsPresent && !p.IsDeleted);

            return totalLectures > 0 ? (attended * 100) / totalLectures : 0;
        }

        private async Task<DashboardStats> GetStatsAsync(Guid traineeId)
        {
            int enrolled = await _context.CourseTrainees.CountAsync(ct => ct.TraineeId == traineeId);
            int certificates = await _context.Certificates.CountAsync(c => c.TraineeId == traineeId && !c.IsDeleted);
            int attended = await _context.Presences.CountAsync(p => p.TraineeId == traineeId && p.IsPresent && !p.IsDeleted);
            int total = await _context.Presences.CountAsync(p => p.TraineeId == traineeId && !p.IsDeleted);
            int rate = total > 0 ? (attended * 100) / total : 0;

            return new DashboardStats
            {
                OverallProgress = await CalculateOverallProgressAsync(traineeId),
                EnrolledCourses = enrolled,
                CompletedHours = attended,
                CompletedExams = certificates,
                AttendanceRate = rate
            };
        }

        private async Task<DashboardChartData> GetChartDataAsync(Guid traineeId)
        {
            var labels = new List<string>();
            var values = new List<decimal>();
            var start = DateTime.UtcNow.AddMonths(-1);

            for (var d = start; d <= DateTime.UtcNow; d = d.AddDays(7))
            {
                labels.Add(d.ToString("MMM dd"));
                var count = await _context.Presences
                    .CountAsync(p => p.TraineeId == traineeId
                        && p.Lecture.LectureDate >= d
                        && p.Lecture.LectureDate < d.AddDays(7)
                        && p.IsPresent && !p.IsDeleted);
                values.Add(count);
            }

            return new DashboardChartData { Analytics = new ChartData { Labels = labels, Values = values } };
        }

        private async Task<List<CurrentCourse>> GetCurrentCoursesAsync(Guid traineeId)
        {
            var list = await _context.CourseTrainees
                .Where(ct => ct.TraineeId == traineeId)
                .Include(ct => ct.Course).ThenInclude(c => c.Lectures)
                .ToListAsync();

            var result = new List<CurrentCourse>();
            foreach (var ct in list)
            {
                int total = ct.Course.Lectures.Count(l => !l.IsDeleted);
                int done = await _context.Presences
                    .CountAsync(p => p.TraineeId == traineeId
                        && p.Lecture.CourseId == ct.CourseId
                        && p.IsPresent && !p.IsDeleted);
                int pct = total > 0 ? (done * 100) / total : 0;

                result.Add(new CurrentCourse
                {
                    Id = ct.CourseId,
                    Name = ct.Course.CourseName,
                    Progress = pct,
                    Remaining = $"{total - done} محاضرات متبقية"
                });
            }
            return result;
        }

        private async Task<List<RecommendedCourse>> GetRecommendedCoursesAsync(Guid traineeId)
        {
            var enrolled = await _context.CourseTrainees
                .Where(ct => ct.TraineeId == traineeId)
                .Select(ct => ct.CourseId)
                .ToListAsync();

            return await _context.Courses
                .Where(c => !c.IsDeleted && !enrolled.Contains(c.CourseId))
                .Take(6)
                .Select(c => new RecommendedCourse
                {
                    Id           = c.CourseId,
                    Name         = c.CourseName,
                    Description  = c.Description ?? "دورة رائعة لتعزيز مهاراتك!",
                    Price        = c.Price,
                    Currency     = c.CourseCurrency.ToString(),
                    ThumbnailUrl = c.ThumbnailUrl,
                    LectureCount = c.NumberOfLectures
                })
                .ToListAsync();
        }

        private async Task<List<UpcomingEvent>> GetUpcomingEventsAsync(Guid traineeId)
        {
            var lectures = await _context.Lectures
                .Where(l => l.LectureDate >= DateTime.UtcNow && !l.IsDeleted
                    && l.Course.CourseTrainees.Any(ct => ct.TraineeId == traineeId))
                .OrderBy(l => l.LectureDate)
                .Take(4)
                .Select(l => new UpcomingEvent
                {
                    Title = l.Title,
                    Date = l.LectureDate.ToString("yyyy-MM-ddTHH:mm:ss"),
                    Link = "#"
                })
                .ToListAsync();

            var exams = await _context.Exams
                .Where(e => e.StartDateTime >= DateTime.UtcNow && !e.IsDeleted
                    && e.Course.CourseTrainees.Any(ct => ct.TraineeId == traineeId))
                .OrderBy(e => e.StartDateTime)
                .Take(4)
                .Select(e => new UpcomingEvent
                {
                    Title = e.ExamName,
                    Date = e.StartDateTime.ToString("yyyy-MM-ddTHH:mm:ss"),
                    Link = "#"
                })
                .ToListAsync();

            return lectures.Concat(exams)
                .OrderBy(e => DateTime.Parse(e.Date))
                .Take(6)
                .ToList();
        }

        private async Task<List<CertificateViewModel>> GetCertificatesAsync(Guid traineeId)
        {
            return await _context.Certificates
                .Where(c => c.TraineeId == traineeId && !c.IsDeleted)
                .Include(c => c.Course)
                .Select(c => new CertificateViewModel { Id = c.CertificateId, Name = c.Course.CourseName })
                .ToListAsync();
        }

        private async Task<List<LiveSessionSummary>> GetUpcomingLiveSessionsAsync(Guid traineeId)
        {
            var enrolledCourseIds = await _context.CourseTrainees
                .Where(ct => ct.TraineeId == traineeId)
                .Select(ct => ct.CourseId)
                .ToListAsync();

            var now = DateTime.UtcNow;

            return await _context.LiveSessions
                .Where(ls => !ls.IsCancelled
                    && enrolledCourseIds.Contains(ls.CourseId)
                    && ls.ScheduledAt >= now.AddMinutes(-60))
                .Include(ls => ls.Course)
                .OrderBy(ls => ls.ScheduledAt)
                .Take(5)
                .Select(ls => new LiveSessionSummary
                {
                    Id          = ls.LiveSessionId,
                    Title       = ls.Title,
                    CourseName  = ls.Course.CourseName,
                    ScheduledAt = ls.ScheduledAt,
                    IsLiveNow   = ls.ScheduledAt <= now.AddMinutes(15)
                })
                .ToListAsync();
        }

        private async Task<List<Notification>> GetNotificationsAsync(Guid traineeId)
        {
            var receiver = await _context.Trainees.FirstOrDefaultAsync(t => t.TraineeId == traineeId);
            if (receiver == null) return new List<Notification>();

            var receptIds = await _context.Receptionists.Select(r => r.UserId).ToListAsync();
            return await _context.Messages
                .Where(m => m.ReceiverId == receiver.UserId && receptIds.Contains(m.SenderId))
                .Select(m => new Notification { Message = m.Content, Time = m.Timestamp })
                .ToListAsync();
        }

        private async Task<List<PaymentHistoryItem>> GetShamCashPaymentsAsync(Guid traineeId)
        {
            return await _context.Payments
                .Where(p => p.TraineeId == traineeId && !p.IsDeleted && p.Notes != null && p.Notes.Contains("[شام كاش]"))
                .Include(p => p.Course)
                .OrderByDescending(p => p.CreatedDate)
                .Select(p => new PaymentHistoryItem
                {
                    Amount     = p.TotalAmount,
                    Currency   = p.Currency.ToString(),
                    CourseName = p.Course != null ? p.Course.CourseName : "—",
                    Notes      = p.Notes ?? "",
                    Date       = p.CreatedDate
                })
                .ToListAsync();
        }

        private async Task<List<PaymentHistoryItem>> GetAdminPaymentsAsync(Guid traineeId)
        {
            return await _context.Payments
                .Where(p => p.TraineeId == traineeId && !p.IsDeleted && (p.Notes == null || !p.Notes.Contains("[شام كاش]")))
                .Include(p => p.Course)
                .OrderByDescending(p => p.CreatedDate)
                .Select(p => new PaymentHistoryItem
                {
                    Amount     = p.TotalAmount,
                    Currency   = p.Currency.ToString(),
                    CourseName = p.Course != null ? p.Course.CourseName : "—",
                    Notes      = p.Notes ?? "",
                    Date       = p.CreatedDate
                })
                .ToListAsync();
        }
    }
}
