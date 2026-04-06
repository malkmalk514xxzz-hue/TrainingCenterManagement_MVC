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

       
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpPost("MarkAttendance")]
        public async Task<IActionResult> MarkAttendance([FromBody] MarkAttendanceViewModel model)
        {
            var lecture = await _context.Lectures.FindAsync(model.LectureId);
            if (lecture == null) return NotFound();
          

            foreach (var item in model.Trainees)
            {
                var TraineeId = _context.Trainees.FirstOrDefaultAsync(t => t.UserId == item.TraineeId.ToString()).Result.TraineeId;
                _context.Presences.Add(new Presence
                {
                    PresenceId = Guid.NewGuid(),
                    LectureId = model.LectureId,
                    TraineeId = TraineeId,
                    IsPresent = true,
                    IsDeleted = false
                });
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Attendance saved successfully." });
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpPost("GetTraineeAttendance/{courseId}/{traineeId}")]
        public async Task<IActionResult> GetTraineeAttendance(string courseId, string traineeId)
        {
            // 1. التحقق من صحة الـ IDs
            if (!Guid.TryParse(courseId, out Guid courseGuid))
                return BadRequest("Invalid Course ID format");

            if (!Guid.TryParse(traineeId, out Guid traineeGuid))
                return BadRequest("Invalid Trainee ID format");
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null) return NotFound(new { message = "User not found" });
            var trainee = await _context.Trainees.FirstOrDefaultAsync(t => t.UserId == userId);

            if (trainee == null) return NotFound(new { message = "Trainee not found" });

            // var TraineeId = _context.Trainees.FirstOrDefaultAsync(t => t.UserId == traineeId.ToString()).Result.TraineeId;
            var TraineeId = trainee.TraineeId;
             // 2. جلب المحاضرات + معلومات الحضور للطالب في نفس الاستعلام (Left Join)
             var result = await _context.Lectures
                .Where(l => l.CourseId == courseGuid)
                .OrderBy(l => l.LectureDate)           // أو حسب التاريخ إذا كان موجود
                .Select(l => new TraineeLectureAttendanceDto
                {
                    LectureId = l.LectureId,
                    LectureTitle = l.Title,
                    LectureDate = l.LectureDate,     // إذا كان عندك حقل تاريخ
                   

                    // إذا وجد سجل حضور → نأخذ قيمة IsPresent، وإلا false
                    IsPresent = _context.Presences
                        .Any(p => p.LectureId == l.LectureId && p.TraineeId == TraineeId && !p.IsDeleted && p.IsPresent),

                    // يمكنك إضافة PresenceId إذا أردت
                    PresenceId = _context.Presences
                        .Where(p => p.LectureId == l.LectureId && p.TraineeId == TraineeId && !p.IsDeleted)
                        .Select(p => (Guid?)p.PresenceId)
                        .FirstOrDefault()
                })
                .ToListAsync();

            if (result.Count == 0)
                return NotFound("No lectures found for this course");

            return Ok(new
            {
                courseId = courseGuid,
                traineeId = traineeGuid,
                totalLectures = result.Count,
                attendanceList = result
            });
        }
        [HttpGet("LectureAttendance/{lectureId:guid}")]
        public async Task<IActionResult> GetLectureAttendance(Guid lectureId)
        {
            var lecture = await _context.Lectures
                .Include(l => l.Course)
                .Include(l => l.Presences)
                    .ThenInclude(p => p.Trainee)
                    .ThenInclude(t => t.User)
                .FirstOrDefaultAsync(l => l.LectureId == lectureId);

            if (lecture == null) return NotFound();

            return Ok(lecture.Presences);
        }
    }
    public class TraineeLectureAttendanceDto
    {
        public Guid LectureId { get; set; }
        public string LectureTitle { get; set; } = string.Empty;
        public DateTime? LectureDate { get; set; }
       

        public bool IsPresent { get; set; }
        public Guid? PresenceId { get; set; }
    }
}