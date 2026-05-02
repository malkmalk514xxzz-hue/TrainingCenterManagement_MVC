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
    public class PresencesApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public PresencesApiController(ApplicationDbContext context)
        {
            _context = context;
        }

        // POST: api/Presences/MarkAttendance
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpPost("MarkAttendance")]
        public async Task<IActionResult> MarkAttendance([FromBody] MarkAttendanceViewModel model)
        {
            var lecture = await _context.Lectures.FindAsync(model.LectureId);
            if (lecture == null)
                return NotFound(new { message = "Lecture not found." });

            foreach (var item in model.Trainees)
            {
                // FIX: Replaced blocking .Result with proper await to prevent deadlocks
                var traineeRecord = await _context.Trainees
                    .FirstOrDefaultAsync(t => t.UserId == item.TraineeId.ToString());

                if (traineeRecord == null)
                    continue; // Skip if trainee not found instead of crashing

                // Avoid duplicate attendance entries
                bool alreadyMarked = await _context.Presences
                    .AnyAsync(p => p.LectureId == model.LectureId && p.TraineeId == traineeRecord.TraineeId);

                if (!alreadyMarked)
                {
                    _context.Presences.Add(new Presence
                    {
                        PresenceId = Guid.NewGuid(),
                        LectureId = model.LectureId,
                        TraineeId = traineeRecord.TraineeId,
                        IsPresent = true,
                        IsDeleted = false
                    });
                }
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Attendance saved successfully." });
        }

        // POST: api/Presences/GetTraineeAttendance/{courseId}/{traineeId}
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpPost("GetTraineeAttendance/{courseId}/{traineeId}")]
        public async Task<IActionResult> GetTraineeAttendance(string courseId, string traineeId)
        {
            if (!Guid.TryParse(courseId, out Guid courseGuid))
                return BadRequest("Invalid Course ID format");

            if (!Guid.TryParse(traineeId, out Guid traineeGuid))
                return BadRequest("Invalid Trainee ID format");

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
                return NotFound(new { message = "User not found" });

            var trainee = await _context.Trainees.FirstOrDefaultAsync(t => t.UserId == userId);
            if (trainee == null)
                return NotFound(new { message = "Trainee not found" });

            var resolvedTraineeId = trainee.TraineeId;

            var result = await _context.Lectures
                .Where(l => l.CourseId == courseGuid && !l.IsDeleted)
                .OrderBy(l => l.LectureDate)
                .Select(l => new TraineeLectureAttendanceDto
                {
                    LectureId = l.LectureId,
                    LectureTitle = l.Title,
                    LectureDate = l.LectureDate,
                    IsPresent = _context.Presences
                        .Any(p => p.LectureId == l.LectureId && p.TraineeId == resolvedTraineeId && !p.IsDeleted && p.IsPresent),
                    PresenceId = _context.Presences
                        .Where(p => p.LectureId == l.LectureId && p.TraineeId == resolvedTraineeId && !p.IsDeleted)
                        .Select(p => (Guid?)p.PresenceId)
                        .FirstOrDefault()
                })
                .ToListAsync();

            if (result.Count == 0)
                return NotFound("No lectures found for this course.");

            return Ok(new
            {
                courseId = courseGuid,
                traineeId = traineeGuid,
                totalLectures = result.Count,
                attendedCount = result.Count(r => r.IsPresent),
                attendanceList = result
            });
        }

        // GET: api/Presences/LectureAttendance/{lectureId}
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpGet("LectureAttendance/{lectureId:guid}")]
        public async Task<IActionResult> GetLectureAttendance(Guid lectureId)
        {
            var lecture = await _context.Lectures
                .Include(l => l.Course)
                .Include(l => l.Presences)
                    .ThenInclude(p => p.Trainee)
                        .ThenInclude(t => t.User)
                .FirstOrDefaultAsync(l => l.LectureId == lectureId);

            if (lecture == null)
                return NotFound();

            var attendance = lecture.Presences.Select(p => new
            {
                p.PresenceId,
                p.IsPresent,
                TraineeName = p.Trainee?.User?.FullName ?? "Unknown"
            });

            return Ok(attendance);
        }
    }

    // DTO for attendance response
    public class TraineeLectureAttendanceDto
    {
        public Guid LectureId { get; set; }
        public string LectureTitle { get; set; } = string.Empty;
        public DateTime? LectureDate { get; set; }
        public bool IsPresent { get; set; }
        public Guid? PresenceId { get; set; }
    }
}
