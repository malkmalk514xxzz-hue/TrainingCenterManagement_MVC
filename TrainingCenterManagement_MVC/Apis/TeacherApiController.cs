using System;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrainingCenterManagement_MVC.Data;
using TrainingCenterManagement_MVC.Models;

namespace TrainingCenterManagement_MVC.Apis
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class TeacherApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public TeacherApiController(ApplicationDbContext context)
        {
            _context = context;
        }

        public class GenerateQrRequest
        {
            public Guid? CourseId { get; set; }
            public int ValidMinutes { get; set; } = 15;
        }

        [HttpPost("generate-qr")]
        public async Task<IActionResult> GenerateQr([FromBody] GenerateQrRequest req)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var qr = new QrLoginToken
            {
                Token = Guid.NewGuid().ToString(),
                TeacherUserId = userId,
                CourseId = req.CourseId,
                ExpiresAt = DateTime.UtcNow.AddMinutes(req.ValidMinutes),
                Used = false
            };
            _context.QrLoginTokens.Add(qr);
            await _context.SaveChangesAsync();

            // Return the token (clients can render it as a QR code)
            return Ok(new { token = qr.Token, expiresAt = qr.ExpiresAt, courseId = qr.CourseId });
        }

        [HttpGet("tokens")]
        public async Task<IActionResult> GetMyTokens()
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var tokens = await _context.QrLoginTokens
                .Where(q => q.TeacherUserId == userId)
                .OrderByDescending(q => q.ExpiresAt)
                .Select(q => new { q.Id, q.Token, q.CourseId, q.ExpiresAt, q.Used })
                .ToListAsync();

            return Ok(tokens);
        }
    }
}
