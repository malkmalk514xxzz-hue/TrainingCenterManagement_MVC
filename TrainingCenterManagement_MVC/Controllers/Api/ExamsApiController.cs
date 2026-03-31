using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
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
        public class ExamsApiController : ControllerBase
        {
            private readonly ApplicationDbContext _context;

            public ExamsApiController(ApplicationDbContext context)
            {
                _context = context;
            }

            [HttpGet]
            public async Task<ActionResult<IEnumerable<Exam>>> GetExams()
            {
                var exams = await _context.Exams.Include(e => e.Course).ToListAsync();
                return Ok(exams);
            }

            [HttpGet("{id:guid}")]
            public async Task<ActionResult<Exam>> GetExam(Guid id)
            {
                var exam = await _context.Exams
                    .Include(e => e.Course)
                    .FirstOrDefaultAsync(e => e.ExamId == id);

                if (exam == null) return NotFound();
                return Ok(exam);
            }

            [HttpPost]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
        public async Task<ActionResult<Exam>> CreateExam([FromBody] Exam exam)
            {
                if (!ModelState.IsValid) return BadRequest(ModelState);

                exam.ExamId = Guid.NewGuid();
                _context.Exams.Add(exam);
                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(GetExam), new { id = exam.ExamId }, exam);
            }

            [HttpPut("{id:guid}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
        public async Task<IActionResult> UpdateExam(Guid id, [FromBody] Exam exam)
            {
                if (id != exam.ExamId) return BadRequest();

                if (!ModelState.IsValid) return BadRequest(ModelState);

                _context.Entry(exam).State = EntityState.Modified;

                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ExamExists(id)) return NotFound();
                    throw;
                }

                return NoContent();
            }

            [HttpDelete("{id:guid}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
        public async Task<IActionResult> DeleteExam(Guid id)
            {
                var exam = await _context.Exams.FindAsync(id);
                if (exam == null) return NotFound();

                _context.Exams.Remove(exam);
                await _context.SaveChangesAsync();
                return NoContent();
            }

            
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Trainee,Admin")]
        [HttpGet("MyExams")]
            public async Task<ActionResult<IEnumerable<Exam>>> GetMyExams()
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var trainee = await _context.Trainees.FirstOrDefaultAsync(t => t.UserId == userId);
                if (trainee == null) return NotFound(new { message = "Trainee not found" });

                var exams = await _context.Exams
                    .Include(e => e.Course)
                    .Where(e => e.Course.CourseTrainees.Any(ct => ct.TraineeId == trainee.TraineeId))
                    .OrderByDescending(e => e.ExamDate)
                    .ToListAsync();

                return Ok(exams);
            }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Trainee,Admin")]
        [HttpGet("TakeExam/{examId:guid}")]
            public async Task<IActionResult> TakeExam(Guid examId)
            {
                var exam = await _context.Exams
                    .Include(e => e.Course)
                    .FirstOrDefaultAsync(e => e.ExamId == examId);

                if (exam == null) return NotFound(new { message = "الامتحان غير موجود." });

                return Ok(new { message = "Exam is ready to start", exam });
            }

            private bool ExamExists(Guid id)
            {
                return _context.Exams.Any(e => e.ExamId == id);
            }
        }
    }

