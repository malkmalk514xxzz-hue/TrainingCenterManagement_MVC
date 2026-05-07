using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using TrainingCenterManagement_MVC.Data;
using TrainingCenterManagement_MVC.Models;

namespace TrainingCenterManagement_MVC.Controllers
{
    [Authorize]
    public class LectureMaterialsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        private static readonly string[] AllowedExtensions =
            { ".pdf", ".docx", ".doc", ".pptx", ".ppt", ".xlsx", ".xls", ".txt", ".zip" };

        private static readonly Dictionary<string, string> MimeTypes = new()
        {
            [".pdf"]  = "application/pdf",
            [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            [".doc"]  = "application/msword",
            [".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            [".ppt"]  = "application/vnd.ms-powerpoint",
            [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            [".xls"]  = "application/vnd.ms-excel",
            [".txt"]  = "text/plain",
            [".zip"]  = "application/zip",
        };

        public LectureMaterialsController(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        [Authorize(Roles = "Trainer,Admin")]
        public async Task<IActionResult> Manage(Guid lectureId)
        {
            var lecture = await _context.Lectures
                .Include(l => l.Course)
                .Include(l => l.Materials.OrderBy(m => m.CreatedAt))
                .FirstOrDefaultAsync(l => l.LectureId == lectureId);

            if (lecture == null) return NotFound();
            return View(lecture);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Trainer,Admin")]
        public async Task<IActionResult> Upload(Guid lectureId, IFormFile materialFile, string title)
        {
            var lecture = await _context.Lectures.FindAsync(lectureId);
            if (lecture == null) return NotFound();

            if (materialFile == null || materialFile.Length == 0)
            {
                TempData["Error"] = "يرجى اختيار ملف.";
                return RedirectToAction(nameof(Manage), new { lectureId });
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                TempData["Error"] = "عنوان المادة مطلوب.";
                return RedirectToAction(nameof(Manage), new { lectureId });
            }

            var ext = Path.GetExtension(materialFile.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(ext))
            {
                TempData["Error"] = "صيغة الملف غير مدعومة. الصيغ المقبولة: PDF, Word, PowerPoint, Excel, TXT, ZIP";
                return RedirectToAction(nameof(Manage), new { lectureId });
            }

            if (materialFile.Length > 50L * 1024 * 1024)
            {
                TempData["Error"] = "حجم الملف يتجاوز الحد المسموح (50 ميغابايت).";
                return RedirectToAction(nameof(Manage), new { lectureId });
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var trainer = await _context.Trainers.FirstOrDefaultAsync(t => t.UserId == userId);
            if (trainer == null) return Forbid();

            var fileName = $"{Guid.NewGuid()}{ext}";
            var uploadDir = Path.Combine(_env.WebRootPath, "uploads", "materials", lectureId.ToString());
            Directory.CreateDirectory(uploadDir);
            var fullPath = Path.Combine(uploadDir, fileName);

            using (var stream = new FileStream(fullPath, FileMode.Create))
                await materialFile.CopyToAsync(stream);

            MimeTypes.TryGetValue(ext, out var contentType);

            _context.LectureMaterials.Add(new LectureMaterial
            {
                MaterialId = Guid.NewGuid(),
                LectureId = lectureId,
                Title = title.Trim(),
                FileName = materialFile.FileName,
                FilePath = $"/uploads/materials/{lectureId}/{fileName}",
                LocalFilePath = fullPath,
                ContentType = contentType ?? "application/octet-stream",
                FileSizeInBytes = materialFile.Length,
                UploadedByTrainerId = trainer.TrainerId,
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            TempData["Success"] = "تم رفع المادة بنجاح.";
            return RedirectToAction(nameof(Manage), new { lectureId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Trainer,Admin")]
        public async Task<IActionResult> Delete(Guid materialId)
        {
            var material = await _context.LectureMaterials
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(m => m.MaterialId == materialId);

            if (material == null) return NotFound();

            var lectureId = material.LectureId;

            if (!string.IsNullOrEmpty(material.LocalFilePath) && System.IO.File.Exists(material.LocalFilePath))
                System.IO.File.Delete(material.LocalFilePath);

            material.IsDeleted = true;
            material.DeletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            TempData["Success"] = "تم حذف المادة بنجاح.";
            return RedirectToAction(nameof(Manage), new { lectureId });
        }

        public async Task<IActionResult> Download(Guid materialId)
        {
            var material = await _context.LectureMaterials
                .FirstOrDefaultAsync(m => m.MaterialId == materialId);

            if (material == null) return NotFound();

            if (!string.IsNullOrEmpty(material.LocalFilePath) && System.IO.File.Exists(material.LocalFilePath))
            {
                var bytes = await System.IO.File.ReadAllBytesAsync(material.LocalFilePath);
                return File(bytes, material.ContentType, material.FileName);
            }

            var webPath = Path.Combine(_env.WebRootPath, material.FilePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (System.IO.File.Exists(webPath))
            {
                var bytes = await System.IO.File.ReadAllBytesAsync(webPath);
                return File(bytes, material.ContentType, material.FileName);
            }

            return NotFound();
        }
    }
}
