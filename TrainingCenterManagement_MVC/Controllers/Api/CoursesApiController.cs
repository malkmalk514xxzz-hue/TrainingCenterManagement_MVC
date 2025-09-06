using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TrainingCenterManagement_MVC.Data;
using TrainingCenterManagement_MVC.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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

        // GET: api/Courses
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Course>>> Get()
        {
            var courses = await _context.Courses
                .Include(c => c.Admin).ThenInclude(a => a.User)
                .ToListAsync();
            return Ok(courses);
        }

        // GET: api/Courses/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<Course>> Get(Guid id)
        {
            var course = await _context.Courses
                .Include(c => c.Admin)
                .Include(c => c.CourseTrainers).ThenInclude(ct => ct.Trainer).ThenInclude(t => t.User)
                .Include(c => c.CourseTrainees).ThenInclude(ct => ct.Trainee).ThenInclude(t => t.User)
                .FirstOrDefaultAsync(m => m.CourseId == id);

            if (course == null) return NotFound();
            return Ok(course);
        }

        // POST: api/Courses
        [HttpPost]
        public async Task<ActionResult<Course>> Create([FromBody] Course course)
        {
            var courseExists = await _context.Courses
                .AnyAsync(c => c.CourseName == course.CourseName && c.BatchNumber == course.BatchNumber);

            if (courseExists)
            {
                return BadRequest(new { message = "Course with the same name and batch number already exists." });
            }

            course.CourseId = Guid.NewGuid();
            _context.Courses.Add(course);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(Get), new { id = course.CourseId }, course);
        }

        // PUT: api/Courses/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> Edit(Guid id, [FromBody] Course course)
        {
            if (id != course.CourseId) return BadRequest();

            try
            {
                _context.Entry(course).State = EntityState.Modified;
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!CourseExists(id)) return NotFound();
                throw;
            }

            return NoContent();
        }

        // DELETE: api/Courses/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var course = await _context.Courses.FindAsync(id);
            if (course == null) return NotFound();

            _context.Courses.Remove(course);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // POST: api/Courses/{id}/assign-trainer
        [HttpPost("{id}/assign-trainer")]
        public async Task<IActionResult> AssignTrainer(Guid id, [FromBody] Guid trainerId)
        {
            bool alreadyAssigned = await _context.CourseTrainers
                .AnyAsync(ct => ct.CourseId == id && ct.TrainerId == trainerId);

            if (!alreadyAssigned)
            {
                var courseTrainer = new CourseTrainer { CourseId = id, TrainerId = trainerId };
                _context.CourseTrainers.Add(courseTrainer);
                await _context.SaveChangesAsync();
            }

            return Ok();
        }

        // POST: api/Courses/{id}/enroll
        [Authorize(Roles = "Trainee")]
        [HttpPost("{id}/enroll")]
        public async Task<IActionResult> Enroll(Guid id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var trainee = await _context.Trainees.FirstOrDefaultAsync(t => t.UserId == userId);
            if (trainee == null) return NotFound(new { message = "المتدرب غير موجود." });

            var course = await _context.Courses.FindAsync(id);
            if (course == null) return NotFound(new { message = "الدورة غير موجودة." });

            var alreadyEnrolled = await _context.CourseTrainees
                .AnyAsync(ct => ct.CourseId == id && ct.TraineeId == trainee.TraineeId);

            if (alreadyEnrolled) return BadRequest(new { message = "أنت مسجل بالفعل في هذه الدورة." });

            var courseTrainee = new CourseTrainee { CourseId = id, TraineeId = trainee.TraineeId };
            _context.CourseTrainees.Add(courseTrainee);
            await _context.SaveChangesAsync();

            return Ok(new { message = "تم التسجيل في الدورة بنجاح." });
        }

        // GET: api/Courses/{id}/preview
        [AllowAnonymous]
        [HttpGet("{id}/preview")]
        public async Task<IActionResult> Preview(Guid id)
        {
            var course = await _context.Courses
                .Include(c => c.Lectures)
                .FirstOrDefaultAsync(c => c.CourseId == id && !c.IsDeleted);

            if (course == null) return NotFound(new { message = "الدورة غير موجودة." });

            return Ok(course);
        }

        // GET: api/Courses/{id}/resume
        [Authorize(Roles = "Trainee")]
        [HttpGet("{id}/resume")]
        public async Task<IActionResult> Resume(Guid id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var trainee = await _context.Trainees.FirstOrDefaultAsync(t => t.UserId == userId);
            if (trainee == null) return NotFound("المتدرب غير موجود.");

            var course = await _context.Courses
                .Include(c => c.Lectures)
                .Include(c => c.CourseTrainees)
                .FirstOrDefaultAsync(c => c.CourseId == id && c.CourseTrainees.Any(ct => ct.TraineeId == trainee.TraineeId));

            if (course == null) return NotFound("الدورة غير موجودة أو أنت غير مسجل فيها.");

            var latestLecture = course.Lectures
                .Where(l => !l.IsDeleted && l.LectureDate <= DateTime.UtcNow)
                .Where(l => !_context.Presences.Any(p => p.LectureId == l.LectureId && p.TraineeId == trainee.TraineeId))
                .OrderByDescending(l => l.LectureDate)
                .FirstOrDefault();

            if (latestLecture != null)
            {
                return Ok(new { redirectTo = Url.Action("ViewLecture", "Lectures", new { id = latestLecture.LectureId }) });
            }

            return Ok(course);
        }

        // POST: api/Courses/{courseId}/assign-trainee
        [HttpPost("{courseId}/assign-trainee")]
        public async Task<IActionResult> AssignTraineeToCourse(Guid courseId, [FromBody] Guid traineeId)
        {
            bool alreadyAssigned = await _context.CourseTrainees
                .AnyAsync(ct => ct.CourseId == courseId && ct.TraineeId == traineeId);

            if (!alreadyAssigned)
            {
                var ct = new CourseTrainee { CourseId = courseId, TraineeId = traineeId };
                _context.CourseTrainees.Add(ct);
                await _context.SaveChangesAsync();
            }

            return Ok();
        }

        // POST: api/Courses/{courseId}/assign-trainees
        [HttpPost("{courseId}/assign-trainees")]
        public async Task<IActionResult> AssignTrainees(Guid courseId, [FromBody] List<Guid> traineeIds)
        {
            foreach (var traineeId in traineeIds)
            {
                bool alreadyAssigned = await _context.CourseTrainees
                    .AnyAsync(ct => ct.CourseId == courseId && ct.TraineeId == traineeId);

                if (!alreadyAssigned)
                {
                    _context.CourseTrainees.Add(new CourseTrainee { CourseId = courseId, TraineeId = traineeId });
                }
            }

            await _context.SaveChangesAsync();
            return Ok();
        }

        private bool CourseExists(Guid id)
        {
            return _context.Courses.Any(e => e.CourseId == id);
        }
    }
}
