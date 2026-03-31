using Microsoft.AspNetCore.Authentication.JwtBearer;
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
    public class ReceptionistsApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ReceptionistsApiController(ApplicationDbContext context)
        {
            _context = context;
        }

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Receptionist>>> GetReceptionists()
        {
            var receptionists = await _context.Receptionists
                .Include(r => r.User)
                .ToListAsync();
            return Ok(receptionists);
        }

        
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Receptionist,Admin")]
        [HttpPost("RegisterTraineeToCourse")]
        public async Task<IActionResult> RegisterTraineeToCourse([FromBody] RegisterToCourseDto dto)
        {
            if (!_context.Courses.Any(c => c.CourseId == dto.CourseId) ||
                !_context.Trainees.Any(t => t.TraineeId == dto.TraineeId))
            {
                return BadRequest(new { message = "الدورة أو الطالب غير موجود." });
            }

            var existing = await _context.CourseTrainees
                .AnyAsync(ct => ct.CourseId == dto.CourseId && ct.TraineeId == dto.TraineeId);

            if (existing)
                return BadRequest(new { message = "الطالب مسجل بالفعل في هذه الدورة." });

            _context.CourseTrainees.Add(new CourseTrainee
            {
                CourseId = dto.CourseId,
                TraineeId = dto.TraineeId
            });

            await _context.SaveChangesAsync();
            return Ok(new { message = "تم تسجيل الطالب في الدورة بنجاح." });
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Receptionist,Admin")]
        [HttpPost("UnregisterTraineeFromCourse")]
        public async Task<IActionResult> UnregisterTraineeFromCourse([FromBody] RegisterToCourseDto dto)
        {
            var registration = await _context.CourseTrainees
                .FirstOrDefaultAsync(ct => ct.CourseId == dto.CourseId && ct.TraineeId == dto.TraineeId);

            if (registration == null)
                return BadRequest(new { message = "الطالب غير مسجل في هذه الدورة." });

            _context.CourseTrainees.Remove(registration);
            await _context.SaveChangesAsync();

            return Ok(new { message = "تم إلغاء تسجيل الطالب من الدورة بنجاح." });
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Receptionist,Admin")]
        [HttpGet("PaymentReports")]
        public async Task<IActionResult> GetPaymentReports()
        {
            var payments = await _context.Payments
                .Include(p => p.Course)
                .Include(p => p.Trainee).ThenInclude(t => t.User)
                .ToListAsync();

            return Ok(payments);
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Receptionist,Admin")]
        [HttpGet("AttendanceReports")]
        public async Task<IActionResult> GetAttendanceReports()
        {
            var reports = await _context.Presences
                .Include(p => p.Trainee).ThenInclude(t => t.User)
                .Include(p => p.Lecture).ThenInclude(l => l.Course)
                .ToListAsync();

            return Ok(reports);
        }
    }

    // DTO بسيط للتسجيل
    public class RegisterToCourseDto
    {
        public Guid CourseId { get; set; }
        public Guid TraineeId { get; set; }
    }
}