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
    public class LecturesApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public LecturesApiController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetLectures()
        {
            var data = await _context.Courses
                .Include(c => c.Lectures)
                .OrderBy(c => c.CourseName)
                .ToListAsync();
            return Ok(data);
        }

        [HttpGet("/{courseId}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]

        public async Task<IActionResult> GetLectures(string courseId)
        {
            var data = await _context.Courses.Where(c=>c.CourseId == Guid.Parse(courseId))
                .Include(c => c.Lectures)
                .OrderBy(c => c.CourseName)
                .ToListAsync();
            return Ok(data);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetLecture(Guid id)
        {
            var lecture = await _context.Lectures
                .Include(l => l.Course)
                .FirstOrDefaultAsync(l => l.LectureId == id);

            if (lecture == null) return NotFound();
            return Ok(lecture);
        }

        [HttpPost]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Trainer,Admin")]
        public async Task<ActionResult<Lecture>> CreateLecture([FromBody] Lecture lecture)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            lecture.LectureId = Guid.NewGuid();
            _context.Lectures.Add(lecture);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetLecture), new { id = lecture.LectureId }, lecture);
        }

        [HttpPut("{id:guid}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Trainer,Admin")]
        public async Task<IActionResult> UpdateLecture(Guid id, [FromBody] Lecture lecture)
        {
            if (id != lecture.LectureId) return BadRequest();

            if (!ModelState.IsValid) return BadRequest(ModelState);

            _context.Entry(lecture).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!LectureExists(id)) return NotFound();
                throw;
            }

            return NoContent();
        }

        [HttpDelete("{id:guid}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Trainer,Admin")]
        public async Task<IActionResult> DeleteLecture(Guid id)
        {
            var lecture = await _context.Lectures.FindAsync(id);
            if (lecture == null) return NotFound();

            _context.Lectures.Remove(lecture);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [Authorize(Roles = "Trainee")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Trainee")]
        [HttpGet("ViewLecture/{id:guid}")]
        public async Task<IActionResult> ViewLecture(Guid id)
        {
            var lecture = await _context.Lectures
                .Include(l => l.Course)
                .Include(l => l.Presences).ThenInclude(p => p.Trainee).ThenInclude(t => t.User)
                .FirstOrDefaultAsync(l => l.LectureId == id);

            if (lecture == null) return NotFound();

            // تسجيل حضور تلقائي للطالب
            if (User.IsInRole("Trainee"))
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var trainee = await _context.Trainees.FirstOrDefaultAsync(t => t.UserId == userId);
                if (trainee != null)
                {
                    var alreadyPresent = await _context.Presences.AnyAsync(p => p.TraineeId == trainee.TraineeId && p.LectureId == id);
                    if (!alreadyPresent)
                    {
                        _context.Presences.Add(new Presence
                        {
                            PresenceId = Guid.NewGuid(),
                            LectureId = id,
                            TraineeId = trainee.TraineeId,
                            IsPresent = true
                        });
                        await _context.SaveChangesAsync();
                    }
                }
            }

            return Ok(lecture);
        }

        private bool LectureExists(Guid id)
        {
            return _context.Lectures.Any(e => e.LectureId == id);
        }
    }
}