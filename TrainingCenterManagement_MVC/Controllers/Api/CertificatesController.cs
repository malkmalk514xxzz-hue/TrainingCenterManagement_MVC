using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Rotativa.AspNetCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TrainingCenterManagement_MVC.Data;
using TrainingCenterManagement_MVC.Models;

namespace TrainingCenterManagement_MVC.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class CertificatesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public CertificatesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/Certificates
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var list = await _context.Certificates
                    .Include(c => c.Course)
                    .Include(c => c.Exam)
                    .Include(c => c.Trainee).ThenInclude(t => t.User)
                    .Include(c => c.Trainer)
                    .ToListAsync();
            return Ok(list);
        }

        // GET: api/Certificates/{id}
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> Details(Guid id)
        {
            var certificate = await _context.Certificates
                .Include(c => c.Course)
                .Include(c => c.Exam)
                .Include(c => c.Trainee).ThenInclude(t => t.User)
                .Include(c => c.Trainer)
                .FirstOrDefaultAsync(m => m.CertificateId == id);

            if (certificate == null) return NotFound();
            return Ok(certificate);
        }

        // GET: api/Certificates/create (lookup data)
        [Authorize(Roles = "Admin")]
        [HttpGet("create")]
        public async Task<IActionResult> Create()
        {
            var courses = await _context.Courses.Select(c => new { c.CourseId, c.CourseName }).ToListAsync();
            var exams = await _context.Exams.Select(e => new { e.ExamId, e.ExamName }).ToListAsync();
            var trainees = await _context.Trainees.Include(t => t.User).Select(t => new { t.TraineeId, Email = t.User.Email }).ToListAsync();
            var trainers = await _context.Trainers.Include(tr => tr.User).Select(tr => new { tr.TrainerId, Email = tr.User.Email }).ToListAsync();
            return Ok(new { courses, exams, trainees, trainers });
        }

        // POST: api/Certificates
        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Certificate certificate)
        {
            certificate.CertificateId = Guid.NewGuid();
            _context.Add(certificate);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(Details), new { id = certificate.CertificateId }, certificate);
        }

        // GET: api/Certificates/{id}/edit
        [Authorize(Roles = "Admin")]
        [HttpGet("{id:guid}/edit")]
        public async Task<IActionResult> Edit(Guid id)
        {
            var certificate = await _context.Certificates.Include(c => c.Trainee).ThenInclude(t => t.User)
                .FirstOrDefaultAsync(c => c.CertificateId == id);
            if (certificate == null) return NotFound();

            var courses = await _context.Courses.Select(c => new { c.CourseId, c.CourseName }).ToListAsync();
            var exams = await _context.Exams.Select(e => new { e.ExamId, e.ExamName }).ToListAsync();
            var trainees = await _context.Trainees.Include(t => t.User).Select(t => new { t.TraineeId, Email = t.User.Email }).ToListAsync();
            var trainers = await _context.Trainers.Include(tr => tr.User).Select(tr => new { tr.TrainerId, Email = tr.User.Email }).ToListAsync();
            return Ok(new { certificate, courses, exams, trainees, trainers });
        }

        // PUT: api/Certificates/{id}
        [Authorize(Roles = "Admin")]
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Edit(Guid id, [FromBody] Certificate certificate)
        {
            if (id != certificate.CertificateId) return BadRequest();
            try
            {
                _context.Update(certificate);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!CertificateExists(certificate.CertificateId)) return NotFound();
                throw;
            }
            return NoContent();
        }

        // GET: api/Certificates/{id}/delete
        [Authorize(Roles = "Admin")]
        [HttpGet("{id:guid}/delete")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var certificate = await _context.Certificates
                .Include(c => c.Course)
                .Include(c => c.Exam)
                .Include(c => c.Trainee).ThenInclude(t => t.User)
                .Include(c => c.Trainer)
                .FirstOrDefaultAsync(m => m.CertificateId == id);
            if (certificate == null) return NotFound();
            return Ok(certificate);
        }

        // DELETE: api/Certificates/{id}
        [Authorize(Roles = "Admin")]
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var certificate = await _context.Certificates.FindAsync(id);
            if (certificate != null) _context.Certificates.Remove(certificate);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        private bool CertificateExists(Guid id)
        {
            return _context.Certificates.Any(e => e.CertificateId == id);
        }

        // Public verification: GET api/Certificates/{id}/verify
        [AllowAnonymous]
        [HttpGet("{id:guid}/verify")]
        public async Task<IActionResult> Verify(Guid id)
        {
            var cert = await _context.Certificates
                .Include(c => c.Course)
                .Include(c => c.Trainee).ThenInclude(t => t.User)
                .Include(c => c.Trainer).ThenInclude(t => t.User)
                .FirstOrDefaultAsync(c => c.CertificateId == id);
            if (cert == null) return NotFound();
            return Ok(cert);
        }

        // GET: api/Certificates/search?traineeName=&courseName=
        [AllowAnonymous]
        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] string traineeName, [FromQuery] string courseName)
        {
            var results = await _context.Certificates
                .Include(c => c.Course)
                .Include(c => c.Trainee).ThenInclude(t => t.User)
                .Include(c => c.Trainer).ThenInclude(t => t.User)
                .Where(c => (string.IsNullOrEmpty(traineeName) || c.Trainee.User.FullName.Contains(traineeName)) && (string.IsNullOrEmpty(courseName) || c.Course.CourseName.Contains(courseName)))
                .ToListAsync();
            return Ok(results);
        }

        [Authorize(Roles = "Admin, Trainer,Trainee")]
        [HttpGet("by-course/{courseId:guid}")]
        public async Task<IActionResult> CertificatesByCourse(Guid courseId)
        {
            var certificates = await _context.Certificates
                .Include(c => c.Course)
                .Include(c => c.Trainee).ThenInclude(t => t.User)
                .Include(c => c.Trainer).ThenInclude(t => t.User)
                .Where(c => c.CourseId == courseId)
                .ToListAsync();
            return Ok(certificates);
        }

        // GET: api/Certificates/{certificateId}/pdf - PDF generation not supported in API controller
        [HttpGet("{certificateId:guid}/pdf")]
        public async Task<IActionResult> CertificatePdf(Guid certificateId)
        {
            var certificate = await _context.Certificates
                .Include(c => c.Course)
                .Include(c => c.Trainee).ThenInclude(t => t.User)
                .Include(c => c.Trainer).ThenInclude(t => t.User)
                .FirstOrDefaultAsync(c => c.CertificateId == certificateId);
            if (certificate == null) return NotFound();
            return Ok(new { message = "PDF generation is available via web UI endpoints only.", certificateId = certificate.CertificateId });
        }

    }
}
