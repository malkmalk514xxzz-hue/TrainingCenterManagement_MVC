using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrainingCenterManagement_MVC.Data;
using TrainingCenterManagement_MVC.Models;

namespace TrainingCenterManagement_MVC.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MediaController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _env;

        public MediaController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IWebHostEnvironment env)
        {
            _context = context;
            _userManager = userManager;
            _env = env;
        }

        /// <summary>
        /// الحصول على صورة من رسالة خاصة مع فحص الصلاحيات
        /// </summary>
        [HttpGet("private/{messageId}")]
        [Authorize]
        public async Task<IActionResult> GetPrivateMessageMedia(int messageId)
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null)
                    return Unauthorized("المستخدم غير مسجل دخول");

                var message = await _context.Messages
                    .FirstOrDefaultAsync(m => m.Id == messageId);

                if (message == null)
                    return NotFound("الرسالة غير موجودة");

                // فحص الصلاحيات - يجب أن يكون المستخدم إما المرسل أو المستقبل
                if (message.SenderId != currentUser.Id && message.ReceiverId != currentUser.Id)
                    return Forbid();

                if (string.IsNullOrWhiteSpace(message.MediaUrl))
                    return NotFound("الملف غير موجود");

                return RedirectToMediaFile(message.MediaUrl);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"خطأ في الخادم: {ex.Message}");
            }
        }

        /// <summary>
        /// الحصول على صورة من رسالة جماعية مع فحص الصلاحيات
        /// </summary>
        [HttpGet("group/{messageId}")]
        [Authorize]
        public async Task<IActionResult> GetGroupMessageMedia(int messageId)
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null)
                    return Unauthorized("المستخدم غير مسجل دخول");

                var message = await _context.GroupMessages
                    .Include(m => m.Course)
                    .FirstOrDefaultAsync(m => m.Id == messageId);

                if (message == null)
                    return NotFound("الرسالة غير موجودة");

                // فحص ما إذا كان المستخدم عضواً في الكورس
                var isCourseMember = await _context.CourseTrainees
                    .Where(ct => ct.Course.CourseId == message.CourseId)
                    .Include(ct => ct.Trainee)
                    .AnyAsync(ct => ct.Trainee.UserId == currentUser.Id);

                if (!isCourseMember)
                {
                    isCourseMember = await _context.CourseTrainers
                        .Where(ct => ct.Course.CourseId == message.CourseId)
                        .Include(ct => ct.Trainer)
                        .AnyAsync(ct => ct.Trainer.UserId == currentUser.Id);
                }

                // السماح للمدير بالوصول لأي ملف
                if (currentUser.Role != RoleType.Admin && !isCourseMember)
                    return Forbid();

                if (string.IsNullOrWhiteSpace(message.Content))
                    return NotFound("الملف غير موجود");

                // استخراج URL من المحتوى
                var urlMatch = System.Text.RegularExpressions.Regex.Match(message.Content, @"\[media:[^:]+:(.+)\]");
                if (!urlMatch.Success)
                    return NotFound("صيغة الملف غير صحيحة");

                return RedirectToMediaFile(urlMatch.Groups[1].Value);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"خطأ في الخادم: {ex.Message}");
            }
        }

        /// <summary>
        /// الحصول على محتوى الملف من النظام
        /// </summary>
        private IActionResult RedirectToMediaFile(string mediaUrl)
        {
            try
            {
                // تنظيف URL من الأحرف غير المرغوبة
                var cleanUrl = mediaUrl
                    .Replace("\u200f", string.Empty)  // RTL mark
                    .Replace("\u200e", string.Empty)  // LTR mark
                    .Replace("\u061c", string.Empty); // ALM mark

                // إذا كان URL مطلق، استخراج المسار النسبي
                if (cleanUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    // في حالة الـ CDN أو URL خارجي
                    return Redirect(cleanUrl);
                }

                // المسار النسبي - يجب أن يبدأ بـ /uploads
                if (!cleanUrl.StartsWith("/uploads", StringComparison.OrdinalIgnoreCase))
                    cleanUrl = "/uploads" + (cleanUrl.StartsWith("/") ? string.Empty : "/") + cleanUrl;

                // بناء المسار الفيزيائي
                var filePath = Path.Combine(_env.WebRootPath, cleanUrl.TrimStart('/'));

                // التحقق من أمان المسار
                var fullPath = Path.GetFullPath(filePath);
                var webRootPath = Path.GetFullPath(_env.WebRootPath);

                if (!fullPath.StartsWith(webRootPath, StringComparison.OrdinalIgnoreCase))
                    return BadRequest("مسار الملف غير صحيح");

                if (!System.IO.File.Exists(fullPath))
                    return NotFound("الملف غير موجود");

                // خدمة الملف
                var contentType = GetContentType(fullPath);
                return PhysicalFile(fullPath, contentType);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"خطأ في الوصول للملف: {ex.Message}");
            }
        }

        private static string GetContentType(string path)
        {
            return Path.GetExtension(path).ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".bmp" => "image/bmp",
                ".svg" => "image/svg+xml",
                ".mp4" => "video/mp4",
                ".webm" => "video/webm",
                ".ogg" => "video/ogg",
                ".mp3" => "audio/mpeg",
                ".wav" => "audio/wav",
                ".aac" => "audio/aac",
                ".m4a" => "audio/mp4",
                _ => "application/octet-stream"
            };
        }
    }
}
