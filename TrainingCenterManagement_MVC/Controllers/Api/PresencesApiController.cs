using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
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

        [Authorize(Roles = "Trainer")]
        [HttpPost("MarkAttendance")]
        public async Task<IActionResult> MarkAttendance([FromBody] MarkAttendanceViewModel model)
        {
            var lecture = await _context.Lectures.FindAsync(model.LectureId);
            if (lecture == null) return NotFound();

            // حذف السجلات القديمة إن وجدت
            var existing = await _context.Presences.Where(p => p.LectureId == model.LectureId).ToListAsync();
            _context.Presences.RemoveRange(existing);

            foreach (var item in model.Trainees)
            {
                _context.Presences.Add(new Presence
                {
                    PresenceId = Guid.NewGuid(),
                    LectureId = model.LectureId,
                    TraineeId = item.TraineeId,
                    IsPresent = item.IsPresent
                });
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Attendance saved successfully." });
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
}