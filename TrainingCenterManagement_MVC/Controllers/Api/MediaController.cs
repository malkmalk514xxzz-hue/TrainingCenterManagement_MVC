using Azure;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;
using System.Security.Claims;
using TrainingCenterManagement_MVC.Data;
using TrainingCenterManagement_MVC.Models;

namespace TrainingCenterManagement_MVC.Controllers.Api
{
    // Controllers/MediaController.cs
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using System.Security.Claims;
    using TrainingCenterManagement_MVC.Models;

    [ApiController]
    [Route("api/media")]
    [Authorize]
    public class MediaController : ControllerBase
    {
        private readonly IWebHostEnvironment _env;
        private readonly ApplicationDbContext _context;

        public MediaController(IWebHostEnvironment env, ApplicationDbContext context)
        {
            _env = env;
            _context = context;
        }

        // ============== 1. رفع الملف (يدعم أي كيان + صورة الملف الشخصي) ==============
        [HttpPost("upload")]
        [RequestSizeLimit(1024 * 1024 * 1024)] // 1 جيجا
        public async Task<IActionResult> Upload([FromForm] UploadRequestDto dto)
        {
            if (dto.File == null || dto.File.Length == 0)
                return BadRequest("لم يتم إرسال ملف");

            var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(currentUserId))
                return Unauthorized();

            // التحقق من أنواع الملفات المسموحة
            var allowedTypes = new Dictionary<string, string[]>
            {
                { "image", new[] { "image/jpeg", "image/png", "image/webp", "image/gif" } },
                { "video", new[] { "video/mp4", "video/webm", "video/quicktime" } },
                { "audio", new[] { "audio/mpeg", "audio/wav", "audio/ogg", "audio/mp3" } }
            };

            if (!allowedTypes.ContainsKey(dto.FileType) ||
                !allowedTypes[dto.FileType].Contains(dto.File.ContentType.ToLower()))
            {
                return BadRequest($"نوع الملف {dto.FileType} غير مدعوم");
            }

            // إنشاء المسار
            var basePath = Path.Combine(_env.WebRootPath, "uploads");
            if (!Directory.Exists(basePath))
                Directory.CreateDirectory(basePath);

            var uploadPath = Path.Combine(basePath, dto.FileType, DateTime.UtcNow.ToString("yyyy/MM/dd"));
            Directory.CreateDirectory(uploadPath);

            var extension = Path.GetExtension(dto.File.FileName).ToLowerInvariant();
            var uniqueFileName = $"{Guid.NewGuid()}{extension}";
            var fullPath = Path.Combine(uploadPath, uniqueFileName);

            // حفظ الملف
            await using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await dto.File.CopyToAsync(stream);
            }

            var fileUrl = $"/uploads/{dto.FileType}/{DateTime.UtcNow:yyyy/MM/dd}/{uniqueFileName}";

            // إنشاء سجل الميديا
            var mediaFile = new MediaFile
            {
                Url = fileUrl,
                FileType = dto.FileType,
                ContentType = dto.File.ContentType,
                FileSize = dto.File.Length,
                EntityType = dto.EntityType,
                EntityId = dto.EntityId,
                UserId = currentUserId,
                MessageId = dto.EntityType == "Message" && int.TryParse(dto.EntityId, out int msgId) ? msgId : null
            };

            _context.mediaFiles.Add(mediaFile);

            // تحديث صورة الملف الشخصي إذا كان النوع Profile
            if (dto.EntityType == "Profile" && dto.EntityId == currentUserId)
            {
                var user = await _context.Users.FindAsync(currentUserId);
                if (user != null)
                {
                    user.ProfilePictureUrl = fileUrl;
                }
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                mediaId = mediaFile.Id,
                url = fileUrl,
                entityType = dto.EntityType,
                message = "تم رفع الملف بنجاح"
            });
        }

        // ============== 2. الحصول على صورة الملف الشخصي ==============
        [HttpGet("profile-picture/{userId}")]
        [AllowAnonymous]   // يمكن للجميع رؤية صور الملفات الشخصية
        public async Task<IActionResult> GetProfilePicture(string userId)
        {
            var user = await _context.Users
                .Where(u => u.Id == userId)
                .Select(u => u.ProfilePictureUrl)
                .FirstOrDefaultAsync();

            if (string.IsNullOrEmpty(user))
                return Ok(new { hasPicture = false, url = (string?)null });

            return Ok(new { hasPicture = true, url = user });
        }

        // ============== 3. الحصول على جميع ملفات المستخدم ==============
        [HttpGet("user/{userId}")]
        public async Task<IActionResult> GetUserMedia(string userId)
        {
            var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (currentUserId != userId)
                return Forbid("لا يمكنك الوصول إلى ملفات مستخدم آخر");

            var mediaFiles = await _context.mediaFiles
                .Where(m => m.UserId == userId)
                .OrderByDescending(m => m.UploadedAt)
                .Select(m => new
                {
                    m.Id,
                    m.Url,
                    m.FileType,
                    m.FileSize,
                    m.UploadedAt,
                    m.EntityType,
                    m.EntityId,
                    IsProfilePicture = (m.EntityType == "Profile" && m.EntityId == userId)
                })
                .ToListAsync();

            var profilePictureUrl = await _context.Users
                .Where(u => u.Id == userId)
                .Select(u => u.ProfilePictureUrl)
                .FirstOrDefaultAsync();

            return Ok(new
            {
                success = true,
                profilePictureUrl = profilePictureUrl,
                totalCount = mediaFiles.Count,
                mediaFiles = mediaFiles
            });
        }

        // ============== 4. الحصول على ملفات المستخدم حسب النوع (الإضافة الجديدة) ==============
        [HttpGet("user/{userId}/type/{fileType}")]
        public async Task<IActionResult> GetUserMediaByType(string userId, string fileType)
        {
            var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (currentUserId != userId)
                return Forbid("لا يمكنك الوصول إلى ملفات مستخدم آخر");

            var normalizedFileType = fileType.ToLowerInvariant();

            if (!new[] { "image", "video", "audio" }.Contains(normalizedFileType))
                return BadRequest("نوع الملف غير صالح. الأنواع المدعومة: image, video, audio");

            var mediaFiles = await _context.mediaFiles
                .Where(m => m.UserId == userId && m.FileType == normalizedFileType)
                .OrderByDescending(m => m.UploadedAt)
                .Select(m => new
                {
                    m.Id,
                    m.Url,
                    m.FileType,
                    m.FileSize,
                    m.UploadedAt,
                    m.EntityType,
                    m.EntityId,
                    FileName = Path.GetFileName(m.Url),
                    IsProfilePicture = (m.EntityType == "Profile" && m.EntityId == userId)
                })
                .ToListAsync();

            return Ok(new
            {
                success = true,
                fileType = normalizedFileType,
                count = mediaFiles.Count,
                mediaFiles = mediaFiles
            });
        }
    }
}
