using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
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
    public class AdminApiController : ControllerBase
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public AdminApiController(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _context = context;
        }

        [HttpGet("Dashboard")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme,Roles = "Admin")]
        public IActionResult Dashboard()
        {
            return Ok(new { message = "Admin dashboard endpoint - Use frontend to consume data" });
        }

        [HttpPost("Login")]
        public async Task<IActionResult> AdminLogin([FromBody] LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, false, false);

            if (result.Succeeded)
            {
                var user = await _userManager.FindByEmailAsync(model.Email);
                if (user != null)
                {
                    return Ok(new
                    {
                        message = "Login successful",
                        userId = user.Id,
                        redirectTo = "/api/Admin/Dashboard"
                    });
                }
            }

            return Unauthorized(new { error = "Invalid credentials" });
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpPost("Logout")]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return Ok(new { message = "Logged out successfully" });
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
        [HttpPost("IssueCertificate")]
        public async Task<IActionResult> IssueCertificate([FromBody] IssueCertificateDto dto)
        {
            var trainee = await _context.Trainees
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.TraineeId == dto.TraineeId);

            var course = await _context.Courses
                .Include(c => c.Exam)
                .Include(c => c.CourseTrainers)
                .FirstOrDefaultAsync(c => c.CourseId == dto.CourseId);

            if (trainee == null || course == null)
                return NotFound(new { message = "Trainee or Course not found" });

            var trainerId = course.CourseTrainers.FirstOrDefault()?.TrainerId;

            var certificate = new Certificate
            {
                CertificateId = Guid.NewGuid(),
                Average = (float)(dto.Average ?? 0),
                CourseId = dto.CourseId,
                TraineeId = dto.TraineeId,
                TrainerId = trainerId ?? Guid.Empty,
                ExamId = course.Exam?.ExamId ?? Guid.Empty,
                Url = "" // يمكن توليده لاحقاً
            };

            _context.Certificates.Add(certificate);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Certificate issued successfully",
                certificateId = certificate.CertificateId
            });
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
        [HttpGet("SearchCertificates")]
        public async Task<IActionResult> SearchCertificates(Guid? courseId, Guid? traineeId)
        {
            var query = _context.Certificates
                .Include(c => c.Course)
                .Include(c => c.Trainee).ThenInclude(t => t.User)
                .Include(c => c.Trainer).ThenInclude(t => t.User)
                .AsQueryable();

            if (courseId.HasValue)
                query = query.Where(c => c.CourseId == courseId.Value);

            if (traineeId.HasValue)
                query = query.Where(c => c.TraineeId == traineeId.Value);

            var certificates = await query.ToListAsync();

            return Ok(new
            {
                certificates,
                totalCount = certificates.Count
            });
        }

        // يمكن إضافة المزيد من الدوال الإدارية حسب الحاجة
    }

    // DTO لإصدار الشهادة
    public class IssueCertificateDto
    {
        public Guid TraineeId { get; set; }
        public Guid CourseId { get; set; }
        public decimal? Average { get; set; }
    }
}