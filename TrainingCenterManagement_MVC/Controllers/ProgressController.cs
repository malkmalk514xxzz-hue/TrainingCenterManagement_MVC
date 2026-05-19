using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using TrainingCenterManagement_MVC.Data;
using TrainingCenterManagement_MVC.ViewModels;

namespace TrainingCenterManagement_MVC.Controllers
{
    [Authorize(Roles = "Trainee")]
    public class ProgressController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ProgressController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> CourseProgress(Guid courseId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var trainee = await _context.Trainees.FirstOrDefaultAsync(t => t.UserId == userId);
            if (trainee == null) return Forbid();

            var isEnrolled = await _context.CourseTrainees
                .AnyAsync(ct => ct.CourseId == courseId && ct.TraineeId == trainee.TraineeId);
            if (!isEnrolled) return Forbid();

            var course = await _context.Courses
                .Include(c => c.Lectures.Where(l => !l.IsDeleted))
                    .ThenInclude(l => l.Videos)
                .Include(c => c.Lectures.Where(l => !l.IsDeleted))
                    .ThenInclude(l => l.Materials)
                .Include(c => c.Exams)
                .AsSplitQuery()
                .FirstOrDefaultAsync(c => c.CourseId == courseId);

            if (course == null) return NotFound();

            var lectures = course.Lectures.OrderBy(l => l.LectureDate).ToList();

            var attendedIds = (await _context.Presences
                .Where(p => p.TraineeId == trainee.TraineeId && p.IsPresent &&
                            lectures.Select(l => l.LectureId).Contains(p.LectureId))
                .Select(p => p.LectureId)
                .ToListAsync()).ToHashSet();

            var watchedVideoIds = (await _context.VideoViews
                .Where(vv => vv.TraineeId == trainee.TraineeId && vv.IsCompleted)
                .Select(vv => vv.VideoId)
                .ToListAsync()).ToHashSet();

            var examIds = course.Exams.Select(e => e.ExamId).ToList();
            var examAttempts = await _context.ExamAttempts
                .Where(a => a.TraineeId == trainee.TraineeId && examIds.Contains(a.ExamId))
                .ToListAsync();

            var hasCertificate = await _context.Certificates
                .AnyAsync(c => c.TraineeId == trainee.TraineeId && c.CourseId == courseId);

            var totalVideos = lectures.Sum(l => l.Videos.Count);
            var watchedVideos = lectures.Sum(l => l.Videos.Count(v => watchedVideoIds.Contains(v.VideoId)));

            var vm = new CourseProgressViewModel
            {
                CourseId = courseId,
                CourseName = course.CourseName,
                TotalLectures = lectures.Count,
                AttendedLectures = attendedIds.Count,
                TotalVideos = totalVideos,
                WatchedVideos = watchedVideos,
                HasCertificate = hasCertificate,
                Lectures = lectures.Select(l => new LectureProgressItem
                {
                    LectureId = l.LectureId,
                    Title = l.Title,
                    Date = l.LectureDate,
                    IsAttended = attendedIds.Contains(l.LectureId),
                    TotalVideos = l.Videos.Count,
                    WatchedVideos = l.Videos.Count(v => watchedVideoIds.Contains(v.VideoId)),
                    TotalMaterials = l.Materials.Count
                }).ToList(),
                Exams = course.Exams.Select(e =>
                {
                    var best = examAttempts
                        .Where(a => a.ExamId == e.ExamId)
                        .OrderByDescending(a => a.ScorePercentage)
                        .FirstOrDefault();

                    return new ExamProgressItem
                    {
                        ExamId = e.ExamId,
                        ExamTitle = e.ExamName,
                        BestScorePercentage = best?.ScorePercentage,
                        BestTotalScore = best?.TotalScore,
                        MaxScore = best?.MaxScore,
                        AttemptDate = best?.SubmittedAt
                    };
                }).ToList()
            };

            return View(vm);
        }
    }
}
