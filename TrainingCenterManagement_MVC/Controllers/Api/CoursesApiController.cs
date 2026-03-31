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
        public async Task<ActionResult<Course>> CreateCourse([FromBody] Course course)
        {
            var exists = await _context.Courses
                .AnyAsync(c => c.CourseName == course.CourseName && c.BatchNumber == course.BatchNumber);

            if (exists)
                return BadRequest(new { message = "Course with the same name and batch number already exists." });

            course.CourseId = Guid.NewGuid();
            _context.Courses.Add(course);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetCourse), new { id = course.CourseId }, course);
        }

        [HttpPut("{id:guid}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
        public async Task<IActionResult> UpdateCourse(Guid id, [FromBody] Course course)
        {
            if (id != course.CourseId) return BadRequest();

            _context.Entry(course).State = EntityState.Modified;

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

        [Authorize(Roles = "Trainee")]
        [HttpPost("{id:guid}/Enroll")]
        public async Task<IActionResult> Enroll(Guid id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var trainee = await _context.Trainees.FirstOrDefaultAsync(t => t.UserId == userId);
            if (trainee == null) return NotFound(new { message = "Trainee not found" });

            var alreadyEnrolled = await _context.CourseTrainees
                .AnyAsync(ct => ct.CourseId == id && ct.TraineeId == trainee.TraineeId);

            if (alreadyEnrolled)
                return BadRequest(new { message = "أنت مسجل بالفعل في هذه الدورة." });

            _context.CourseTrainees.Add(new CourseTrainee { CourseId = id, TraineeId = trainee.TraineeId });
            await _context.SaveChangesAsync();

            return Ok(new { message = "تم التسجيل في الدورة بنجاح." });
        }
    }
}