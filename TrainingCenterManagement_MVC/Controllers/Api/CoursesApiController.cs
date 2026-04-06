using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using TrainingCenterManagement_MVC.Data;
using TrainingCenterManagement_MVC.Models;
using TrainingCenterManagement_MVC.ViewModels;

namespace TrainingCenterManagement_MVC.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    public class CoursesApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public CoursesApiController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Course>>> GetCourses()
        {
            var courses = await _context.Courses
                .Include(c => c.Admin).ThenInclude(a => a.User)
                .ToListAsync();
            return Ok(courses);
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<Course>> GetCourse(Guid id)
        {
            var course = await _context.Courses
                .Include(c => c.Admin)
                .Include(c => c.CourseTrainers).ThenInclude(ct => ct.Trainer).ThenInclude(t => t.User)
                .Include(c => c.CourseTrainees).ThenInclude(ct => ct.Trainee).ThenInclude(t => t.User)
                .Include(c => c.Lectures)
                .FirstOrDefaultAsync(c => c.CourseId == id);

            if (course == null) return NotFound();
            return Ok(course);
        }

        [HttpPost]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
        public async Task<ActionResult<Course>> CreateCourse([FromBody] CourseViewModel2 course)
        {
            var exists = await _context.Courses
                .AnyAsync(c => c.CourseName == course.CourseName && c.BatchNumber == course.BatchNumber);

            if (exists)
                return BadRequest(new { message = "Course with the same name and batch number already exists." });
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var admin = await _context.Admins.FirstOrDefaultAsync(a => a.UserId == userId);
            Course course1 = new Course
            {
                CourseName = course.CourseName,
                BatchNumber = course.BatchNumber,
                NumberOfLectures = course.NumberOfLectures,
                Price = course.Price,
                Description = course.Description,
                VideoUrl = course.VideoUrl,
                ThumbnailUrl = course.ThumbnailUrl,
                CreatedDate = course.CreatedDate,
                ReleaseDate = course.ReleaseDate,
                AdminId = admin.AdminId
            };
            
            _context.Courses.Add(course1);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetCourse), new { id = course1.CourseId }, course);
        }

        [HttpPut("{id:guid}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
        public async Task<IActionResult> UpdateCourse(Guid id, [FromBody] CourseViewModel2 course)
        {
            Course Localcourse = await _context.Courses.FirstOrDefaultAsync(c => c.CourseId == id);
            if (course == null) return NotFound();

            Localcourse.CourseName = course.CourseName;
            Localcourse.BatchNumber = course.BatchNumber;
            Localcourse.NumberOfLectures = course.NumberOfLectures;
            Localcourse.Price = course.Price;
            Localcourse.Description = course.Description;
            Localcourse.VideoUrl = course.VideoUrl;
            Localcourse.ThumbnailUrl = course.ThumbnailUrl;
            Localcourse.CreatedDate = course.CreatedDate;
            Localcourse.ReleaseDate = course.ReleaseDate;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (_context.Courses.FirstOrDefaultAsync(c=> c.CourseId == id ) is not null ) return NotFound();
                throw;
            }

            return NoContent();
        }

        [HttpDelete("{id:guid}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
        public async Task<IActionResult> DeleteCourse(Guid id)
        {
            var course = await _context.Courses.FindAsync(id);
            if (course == null) return NotFound();

            _context.Courses.Remove(course);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpPost("courseId/{courseId:guid}/traineeId/{userId:guid}/Enroll")]
        public async Task<IActionResult> Enroll(Guid courseId,string userId)
        {
           var trainee = await _context.Trainees.FirstOrDefaultAsync(t => t.UserId == userId);
           
            if (trainee == null) return NotFound(new { message = "Trainee not found" });

            var alreadyEnrolled = await _context.CourseTrainees
                .AnyAsync(ct => ct.CourseId == courseId && ct.TraineeId == trainee.TraineeId);

            if (alreadyEnrolled)
                return BadRequest(new { message = "أنت مسجل بالفعل في هذه الدورة." });

            _context.CourseTrainees.Add(new CourseTrainee { CourseId = courseId, TraineeId = trainee.TraineeId });
            await _context.SaveChangesAsync();

            return Ok(new { message = "تم التسجيل في الدورة بنجاح." });
        }
    }
}