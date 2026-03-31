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
    public class TraineesApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public TraineesApiController(ApplicationDbContext context)
        {
            _context = context;
        }


        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Trainee>>> GetTrainees()
        {
            var trainees = await _context.Trainees.Include(t => t.User).ToListAsync();
            return Ok(trainees);
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<Trainee>> GetTrainee(Guid id)
        {
            var trainee = await _context.Trainees
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.TraineeId == id);

            if (trainee == null) return NotFound();
            return Ok(trainee);
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
        [HttpPost]
        public async Task<ActionResult<Trainee>> CreateTrainee([FromBody] TraineeCreateViewModel model)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FullName = model.FullName
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var trainee = new Trainee
            {
                TraineeId = Guid.NewGuid(),
                UserId = user.Id
            };

            _context.Trainees.Add(trainee);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetTrainee), new { id = trainee.TraineeId }, trainee);
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> DeleteTrainee(Guid id)
        {
            var trainee = await _context.Trainees.FindAsync(id);
            if (trainee == null) return NotFound();

            _context.Trainees.Remove(trainee);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Trainee")]
        [HttpGet("MyCertificates")]
        public async Task<ActionResult<IEnumerable<Certificate>>> GetMyCertificates()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var trainee = await _context.Trainees.FirstOrDefaultAsync(t => t.UserId == userId);
            if (trainee == null) return NotFound();

            var certificates = await _context.Certificates
                .Where(c => c.TraineeId == trainee.TraineeId)
                .Include(c => c.Course)
                .Include(c => c.Trainer).ThenInclude(t => t.User)
                .ToListAsync();

            return Ok(certificates);
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Trainee")]
        [HttpGet("MyCourses")]
        public async Task<ActionResult<IEnumerable<Course>>> GetMyCourses()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var trainee = await _context.Trainees.FirstOrDefaultAsync(t => t.UserId == userId);
            if (trainee == null) return NotFound();

            var courses = await _context.CourseTrainees
                .Where(ct => ct.TraineeId == trainee.TraineeId)
                .Include(ct => ct.Course)
                .Select(ct => ct.Course)
                .ToListAsync();

            return Ok(courses);
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Trainee")]
        [HttpGet("Profile")]
        public async Task<ActionResult<Trainee>> GetProfile()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var trainee = await _context.Trainees
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.UserId == userId);

            if (trainee == null) return NotFound();
            return Ok(trainee);
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Trainee")]
        [HttpGet("TrackAttendance")]
        public async Task<ActionResult<IEnumerable<TraineeAttendanceViewModel>>> TrackAttendance()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var trainee = await _context.Trainees.FirstOrDefaultAsync(t => t.UserId == userId);
            if (trainee == null) return NotFound();

            var data = await _context.CourseTrainees
                .Where(ct => ct.TraineeId == trainee.TraineeId)
                .Select(ct => new TraineeAttendanceViewModel
                {
                    CourseName = ct.Course.CourseName,
                    TotalLectures = ct.Course.NumberOfLectures,
                    AttendedLectures = ct.Course.Lectures.Count(l => l.Presences.Any(p => p.TraineeId == trainee.TraineeId && p.IsPresent))
                })
                .ToListAsync();

            return Ok(data);
        }
    }
}