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
    public class TrainersApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public TrainersApiController(ApplicationDbContext context)
        {
            _context = context;
        }

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Trainer>>> GetTrainers()
        {
            var trainers = await _context.Trainers.Include(t => t.User).ToListAsync();
            return Ok(trainers);
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<Trainer>> GetTrainer(Guid id)
        {
            var trainer = await _context.Trainers
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.TrainerId == id);

            if (trainer == null) return NotFound();
            return Ok(trainer);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<ActionResult<Trainer>> CreateTrainer([FromBody] Trainer trainer)
        {
            trainer.TrainerId = Guid.NewGuid();
            _context.Trainers.Add(trainer);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetTrainer), new { id = trainer.TrainerId }, trainer);
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> UpdateTrainer(Guid id, [FromBody] Trainer trainer)
        {
            if (id != trainer.TrainerId) return BadRequest();

            _context.Entry(trainer).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if ( _context.Trainers.FirstOrDefaultAsync(t => t.TrainerId == id) is not null) return NotFound();
                throw;
            }

            return NoContent();
        }

        [Authorize(Roles = "Admin")]
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> DeleteTrainer(Guid id)
        {
            var trainer = await _context.Trainers.FindAsync(id);
            if (trainer == null) return NotFound();

            _context.Trainers.Remove(trainer);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [Authorize(Roles = "Trainer,Admin")]
        [HttpGet("MyCourses")]
        public async Task<ActionResult<IEnumerable<Course>>> GetMyCourses()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var trainer = await _context.Trainers.FirstOrDefaultAsync(t => t.UserId == userId);
            if (trainer == null) return NotFound();

            var courses = await _context.CourseTrainers
                .Where(ct => ct.TrainerId == trainer.TrainerId)
                .Select(ct => ct.Course)
                .ToListAsync();

            return Ok(courses);
        }

        [Authorize(Roles = "Trainer")]
        [HttpGet("ViewTrainees/{courseId:guid}")]
        public async Task<ActionResult<IEnumerable<Trainee>>> ViewTrainees(Guid courseId)
        {
            var course = await _context.Courses
                .Include(c => c.CourseTrainees)
                    .ThenInclude(ct => ct.Trainee)
                    .ThenInclude(t => t.User)
                .FirstOrDefaultAsync(c => c.CourseId == courseId);

            if (course == null) return NotFound();

            var trainees = course.CourseTrainees.Select(ct => ct.Trainee).ToList();
            return Ok(trainees);
        }
    }
}