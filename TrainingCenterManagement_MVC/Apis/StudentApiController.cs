using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrainingCenterManagement_MVC.Data;
using TrainingCenterManagement_MVC.Models;

namespace TrainingCenterManagement_MVC.Apis
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class StudentApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public StudentApiController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpGet("me")]
        public async Task<IActionResult> Me()
        {
            var userId = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) return NotFound();

            // If trainee - return their courses and lectures
            if (user.Role == RoleType.Trainee)
            {
                var trainee = await _context.Trainees
                    .Include(t => t.CourseTrainees)
                    .ThenInclude(ct => ct.Course)
                    .ThenInclude(c => c.Lectures)
                    .FirstOrDefaultAsync(t => t.UserId == userId);

                var courses = trainee?.CourseTrainees.Select(ct => new
                {
                    ct.Course.CourseId,
                    ct.Course.CourseName,
                    ct.Course.Description,
                    Lectures = ct.Course.Lectures.Select(l => new { l.LectureId, l.Title, l.VideoUrl, l.LectureDate })
                });

                return Ok(new { user.Id, user.FullName, Role = user.Role.ToString(), Courses = courses });
            }

            // For trainers - return courses they teach
            if (user.Role == RoleType.Trainer)
            {
                var trainer = await _context.Trainers
                    .Include(t => t.CourseTrainers)
                    .ThenInclude(ct => ct.Course)
                    .ThenInclude(c => c.Lectures)
                    .FirstOrDefaultAsync(t => t.UserId == userId);

                var courses = trainer?.CourseTrainers.Select(ct => new
                {
                    ct.Course.CourseId,
                    ct.Course.CourseName,
                    ct.Course.Description,
                    Lectures = ct.Course.Lectures.Select(l => new { l.LectureId, l.Title, l.VideoUrl, l.LectureDate })
                });

                return Ok(new { user.Id, user.FullName, Role = user.Role.ToString(), Courses = courses });
            }

            return Ok(new { user.Id, user.FullName, Role = user.Role.ToString() });
        }
    }
}
