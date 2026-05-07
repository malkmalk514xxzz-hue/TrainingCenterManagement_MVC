using Microsoft.EntityFrameworkCore;
using TrainingCenterManagement_MVC.Data;
using TrainingCenterManagement_MVC.ViewModels;

namespace TrainingCenterManagement_MVC.Helpers
{
    public class TrainerDashboardHelper
    {
        private readonly ApplicationDbContext _context;

        public TrainerDashboardHelper(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<TrainerDashboardViewModel> GetTrainerDashboardAsync(Guid trainerId, string fullName)
        {
            var trainer = await _context.Trainers
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.TrainerId == trainerId);

            var model = new TrainerDashboardViewModel
            {
                FullName = fullName,
                ProfilePictureUrl = "/images/default-profile.png",
                Specialization = trainer?.Specialty ?? "General",
                Availability = "Available",
                WelcomeMessage = $"مرحبًا {fullName}! يسعدنا وجودك.",
                OverallProgress = await GetOverallProgressAsync(trainerId)
            };

            model.Stats = new TrainerDashboardStats
            {
                TotalProgress = await GetTotalCoursesAsync(trainerId),
                CurrentCourses = await GetActiveStudentsAsync(trainerId),
                Certificates = await GetCertificatesCountAsync(trainerId),
                UpcomingEvents = await GetUpcomingEventsCountAsync(trainerId)
            };

            model.KPIs = await GetKPIsAsync(trainerId);
            model.CurrentCourses = await GetCurrentCoursesAsync(trainerId);
            model.RecommendedCourses = await GetRecommendedCoursesAsync(trainerId);
            model.UpcomingEvents = await GetUpcomingEventsAsync(trainerId);
            model.Certificates = await GetCertificatesAsync(trainerId);
            model.ChartData = await GetAnalyticsAsync(trainerId);
            model.ScheduleEvents = await GetScheduleEventsAsync(trainerId);
            model.StudentsByCourse = await GetStudentsByCourseAsync(trainerId);
            model.AttendanceRecords = await GetAttendanceRecordsAsync(trainerId);
            model.Assignments = new List<Assignment>();
            model.Reports = new List<Report>();
            model.Resources = new List<Resource>();

            return model;
        }

        private async Task<int> GetOverallProgressAsync(Guid trainerId)
        {
            var courseIds = await _context.CourseTrainers
                .Where(ct => ct.TrainerId == trainerId && !ct.Course.IsDeleted)
                .Select(ct => ct.CourseId).ToListAsync();

            int total = await _context.Presences
                .CountAsync(p => courseIds.Contains(p.Lecture.CourseId) && !p.IsDeleted);
            int present = await _context.Presences
                .CountAsync(p => courseIds.Contains(p.Lecture.CourseId) && p.IsPresent && !p.IsDeleted);

            return total > 0 ? (present * 100) / total : 0;
        }

        private async Task<int> GetTotalCoursesAsync(Guid trainerId) =>
            await _context.CourseTrainers.CountAsync(ct => ct.TrainerId == trainerId && !ct.Course.IsDeleted);

        private async Task<int> GetActiveStudentsAsync(Guid trainerId) =>
            await _context.CourseTrainers
                .Where(ct => ct.TrainerId == trainerId && !ct.Course.IsDeleted)
                .SelectMany(ct => ct.Course.CourseTrainees)
                .Select(ct => ct.TraineeId).Distinct().CountAsync();

        private async Task<int> GetCertificatesCountAsync(Guid trainerId) =>
            await _context.Certificates.CountAsync(c => c.TrainerId == trainerId && !c.IsDeleted);

        private async Task<int> GetUpcomingEventsCountAsync(Guid trainerId)
        {
            int lectures = await _context.Lectures
                .CountAsync(l => l.LectureDate >= DateTime.UtcNow && !l.IsDeleted
                    && l.Course.CourseTrainers.Any(ct => ct.TrainerId == trainerId));
            int exams = await _context.Exams
                .CountAsync(e => e.StartDateTime >= DateTime.UtcNow && !e.IsDeleted
                    && e.Course.CourseTrainers.Any(ct => ct.TrainerId == trainerId));
            return lectures + exams;
        }

        private async Task<TrainerKPIs> GetKPIsAsync(Guid trainerId)
        {
            var courseIds = await _context.CourseTrainers
                .Where(ct => ct.TrainerId == trainerId && !ct.Course.IsDeleted)
                .Select(ct => ct.CourseId).ToListAsync();

            int students = await _context.CourseTrainees
                .Where(ct => courseIds.Contains(ct.CourseId) && !ct.Course.IsDeleted)
                .Select(ct => ct.TraineeId).Distinct().CountAsync();

            var presences = await _context.Presences
                .Include(p => p.Lecture)
                .Where(p => courseIds.Contains(p.Lecture.CourseId)
                    && p.Lecture.LectureDate >= DateTime.UtcNow.AddMonths(-1)
                    && !p.IsDeleted && !p.Lecture.IsDeleted)
                .ToListAsync();

            int total = presences.Count;
            int present = presences.Count(p => p.IsPresent);
            decimal avgAtt = total > 0 ? (present * 100m) / total : 0m;

            // Average rating from CourseRatings
            decimal avgRating = 0m;
            if (courseIds.Any())
            {
                var ratings = await _context.CourseRatings
                    .Where(r => courseIds.Contains(r.CourseId))
                    .Select(r => (decimal)r.Stars)
                    .ToListAsync();
                avgRating = ratings.Any() ? Math.Round(ratings.Average(), 1) : 0m;
            }

            return new TrainerKPIs
            {
                CurrentCourses = courseIds.Count,
                EnrolledStudents = students,
                AverageAttendance = avgAtt,
                UngradedAssignments = 0,
                AverageRating = avgRating
            };
        }

        private async Task<List<CurrentCourse>> GetCurrentCoursesAsync(Guid trainerId)
        {
            var list = await _context.CourseTrainers
                .Where(ct => ct.TrainerId == trainerId && !ct.Course.IsDeleted)
                .Include(ct => ct.Course).ThenInclude(c => c.Lectures.Where(l => !l.IsDeleted))
                .ToListAsync();

            return list.Select(ct =>
            {
                int total = ct.Course.Lectures.Count();
                int done = ct.Course.Lectures.Count(l => l.LectureDate < DateTime.UtcNow);
                int pct = total > 0 ? (done * 100) / total : 0;
                return new CurrentCourse
                {
                    Id = ct.Course.CourseId,
                    Name = ct.Course.CourseName,
                    Progress = pct,
                    Remaining = $"{total - done} محاضرات متبقية"
                };
            }).ToList();
        }

        private async Task<List<RecommendedCourse>> GetRecommendedCoursesAsync(Guid trainerId)
        {
            var assigned = await _context.CourseTrainers
                .Where(ct => ct.TrainerId == trainerId)
                .Select(ct => ct.CourseId).ToListAsync();

            return await _context.Courses
                .Where(c => !c.IsDeleted && !assigned.Contains(c.CourseId))
                .Take(4)
                .Select(c => new RecommendedCourse { Id = c.CourseId, Name = c.CourseName, Description = c.Description ?? "" })
                .ToListAsync();
        }

        private async Task<List<UpcomingEvent>> GetUpcomingEventsAsync(Guid trainerId)
        {
            var lectures = await _context.Lectures
                .Where(l => l.LectureDate >= DateTime.UtcNow && !l.IsDeleted
                    && l.Course.CourseTrainers.Any(ct => ct.TrainerId == trainerId))
                .OrderBy(l => l.LectureDate).Take(5)
                .Select(l => new UpcomingEvent { Title = l.Title, Date = l.LectureDate.ToString("yyyy-MM-ddTHH:mm:ss"), Link = l.VideoUrl })
                .ToListAsync();

            var exams = await _context.Exams
                .Where(e => e.StartDateTime >= DateTime.UtcNow && !e.IsDeleted
                    && e.Course.CourseTrainers.Any(ct => ct.TrainerId == trainerId))
                .OrderBy(e => e.StartDateTime).Take(5)
                .Select(e => new UpcomingEvent { Title = e.ExamName, Date = e.StartDateTime.ToString("yyyy-MM-ddTHH:mm:ss"), Link = null })
                .ToListAsync();

            return lectures.Concat(exams)
                .OrderBy(e => DateTime.Parse(e.Date))
                .Take(5).ToList();
        }

        private async Task<List<CertificateViewModel>> GetCertificatesAsync(Guid trainerId) =>
            await _context.Certificates
                .Where(c => c.TrainerId == trainerId && !c.IsDeleted)
                .Include(c => c.Course)
                .Select(c => new CertificateViewModel { Id = c.CertificateId, Name = c.Course.CourseName })
                .ToListAsync();

        private async Task<DashboardChartData> GetAnalyticsAsync(Guid trainerId)
        {
            var courseIds = await _context.CourseTrainers
                .Where(ct => ct.TrainerId == trainerId && !ct.Course.IsDeleted)
                .Select(ct => ct.CourseId).ToListAsync();

            if (!courseIds.Any())
                return new DashboardChartData { Analytics = new ChartData { Labels = new(), Values = new() } };

            var start = DateTime.UtcNow.AddMonths(-1);
            var presences = await _context.Presences
                .Include(p => p.Lecture)
                .Where(p => courseIds.Contains(p.Lecture.CourseId)
                    && p.Lecture.LectureDate >= start
                    && !p.IsDeleted && !p.Lecture.IsDeleted)
                .ToListAsync();

            var labels = new List<string>();
            var values = new List<decimal>();

            for (var d = start; d <= DateTime.UtcNow; d = d.AddDays(7))
            {
                labels.Add(d.ToString("MMM dd"));
                var week = presences.Where(p => p.Lecture.LectureDate >= d && p.Lecture.LectureDate < d.AddDays(7)).ToList();
                values.Add(week.Count > 0 ? (week.Count(p => p.IsPresent) * 100m) / week.Count : 0m);
            }

            return new DashboardChartData { Analytics = new ChartData { Labels = labels, Values = values } };
        }

        private async Task<List<ScheduleEvent>> GetScheduleEventsAsync(Guid trainerId)
        {
            var lectures = await _context.Lectures
                .Where(l => !l.IsDeleted && l.Course.CourseTrainers.Any(ct => ct.TrainerId == trainerId))
                .Select(l => new ScheduleEvent { Id = l.LectureId, Title = l.Title, Start = l.LectureDate, End = l.LectureDate.AddHours(1), Type = "Lecture", Link = l.VideoUrl })
                .ToListAsync();

            var exams = await _context.Exams
                .Where(e => !e.IsDeleted && e.Course.CourseTrainers.Any(ct => ct.TrainerId == trainerId))
                .Select(e => new ScheduleEvent { Id = e.ExamId, Title = e.ExamName, Start = e.ExamDate, End = e.ExamDate.AddHours(2), Type = "Exam", Link = null })
                .ToListAsync();

            return lectures.Cast<ScheduleEvent>().Concat(exams).ToList();
        }

        private async Task<Dictionary<Guid, List<StudentInfo>>> GetStudentsByCourseAsync(Guid trainerId)
        {
            var courseIds = await _context.CourseTrainers
                .Where(ct => ct.TrainerId == trainerId && !ct.Course.IsDeleted)
                .Select(ct => ct.CourseId).ToListAsync();

            var result = new Dictionary<Guid, List<StudentInfo>>();

            foreach (var courseId in courseIds)
            {
                var course = await _context.Courses.FirstOrDefaultAsync(c => c.CourseId == courseId);
                var students = await _context.CourseTrainees
                    .Where(ct => ct.CourseId == courseId)
                    .Include(ct => ct.Trainee).ThenInclude(t => t.User)
                    .Select(ct => new StudentInfo
                    {
                        Id = ct.TraineeId,
                        Name = ct.Trainee.User.FullName,
                        Contact = ct.Trainee.User.PhoneNumber,
                        Grade = 0,
                        CourseName = course.CourseName,
                        AttendanceStatus = "No Data"
                    })
                    .ToListAsync();

                foreach (var s in students)
                {
                    var p = await _context.Presences
                        .Where(p => p.TraineeId == s.Id && p.Lecture.CourseId == courseId && !p.IsDeleted)
                        .ToListAsync();
                    int t = p.Count, pr = p.Count(x => x.IsPresent);
                    s.AttendanceStatus = t > 0 ? $"{(pr * 100 / t)}%" : "No Data";
                }

                result[courseId] = students;
            }

            return result;
        }

        private async Task<List<AttendanceRecord>> GetAttendanceRecordsAsync(Guid trainerId)
        {
            var courseIds = await _context.CourseTrainers
                .Where(ct => ct.TrainerId == trainerId && !ct.Course.IsDeleted)
                .Select(ct => ct.CourseId).ToListAsync();

            var lectures = await _context.Lectures
                .Where(l => courseIds.Contains(l.CourseId) && !l.IsDeleted)
                .Include(l => l.Presences.Where(p => !p.IsDeleted))
                .ToListAsync();

            return lectures.Select(l => new AttendanceRecord
            {
                LectureId = l.LectureId,
                Date = l.LectureDate,
                Students = l.Presences.Select(p => new StudentAttendance
                {
                    StudentId = p.TraineeId,
                    Present = p.IsPresent,
                    Notes = ""
                }).ToList()
            }).OrderByDescending(r => r.Date).Take(10).ToList();
        }
    }
}
