using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Security.Claims;
using System.Threading.Tasks;
using TrainingCenterManagement_MVC.Data;
using TrainingCenterManagement_MVC.Models;

namespace TrainingCenterManagement_MVC.Controllers
{
    [Authorize(Roles = "Trainee")]
    public class CourseRatingsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CourseRatingsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Rate(Guid courseId, int stars, string? comment)
        {
            if (stars < 1 || stars > 5)
            {
                TempData["RatingError"] = "التقييم يجب أن يكون بين 1 و 5 نجوم.";
                return RedirectToAction("Details", "Courses", new { id = courseId });
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var trainee = await _context.Trainees.FirstOrDefaultAsync(t => t.UserId == userId);
            if (trainee == null) return Forbid();

            var isEnrolled = await _context.CourseTrainees
                .AnyAsync(ct => ct.CourseId == courseId && ct.TraineeId == trainee.TraineeId);

            if (!isEnrolled)
            {
                TempData["RatingError"] = "يجب أن تكون مسجلاً في هذا الكورس لتتمكن من التقييم.";
                return RedirectToAction("Details", "Courses", new { id = courseId });
            }

            var existing = await _context.CourseRatings
                .FirstOrDefaultAsync(r => r.CourseId == courseId && r.TraineeId == trainee.TraineeId);

            if (existing != null)
            {
                existing.Stars = stars;
                existing.Comment = comment?.Trim();
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                _context.CourseRatings.Add(new CourseRating
                {
                    RatingId = Guid.NewGuid(),
                    CourseId = courseId,
                    TraineeId = trainee.TraineeId,
                    Stars = stars,
                    Comment = comment?.Trim(),
                    CreatedAt = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();
            TempData["RatingSuccess"] = "شكراً على تقييمك!";
            return RedirectToAction("Details", "Courses", new { id = courseId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(Guid courseId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var trainee = await _context.Trainees.FirstOrDefaultAsync(t => t.UserId == userId);
            if (trainee == null) return Forbid();

            var rating = await _context.CourseRatings
                .FirstOrDefaultAsync(r => r.CourseId == courseId && r.TraineeId == trainee.TraineeId);

            if (rating != null)
            {
                _context.CourseRatings.Remove(rating);
                await _context.SaveChangesAsync();
                TempData["RatingSuccess"] = "تم حذف تقييمك.";
            }

            return RedirectToAction("Details", "Courses", new { id = courseId });
        }
    }
}
