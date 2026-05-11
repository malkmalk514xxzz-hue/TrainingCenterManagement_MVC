using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using TrainingCenterManagement_MVC.Data;
using TrainingCenterManagement_MVC.Models;
using TrainingCenterManagement_MVC.Services;

namespace TrainingCenterManagement_MVC.Controllers
{
    [Authorize]
    public class LectureResourcesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILectureResourceService _resourceService;

        public LectureResourcesController(
            ApplicationDbContext context,
            ILectureResourceService resourceService)
        {
            _context         = context;
            _resourceService = resourceService;
        }

        // ── Manage (Trainer/Admin) ────────────────────────────────────

        [Authorize(Roles = "Trainer,Admin")]
        public async Task<IActionResult> Manage(Guid lectureId)
        {
            var lecture = await _context.Lectures
                .Include(l => l.Course)
                .FirstOrDefaultAsync(l => l.LectureId == lectureId);

            if (lecture == null) return NotFound();

            var resources = await _resourceService.GetLectureResourcesAsync(lectureId, includeHidden: true);

            ViewBag.LectureId   = lectureId;
            ViewBag.LectureTitle = lecture.Title;
            ViewBag.CourseName  = lecture.Course?.CourseName;
            ViewBag.Resources   = resources;

            return View();
        }

        // ── Upload ────────────────────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Trainer,Admin")]
        public async Task<IActionResult> Upload(
            Guid lectureId,
            IFormFile resourceFile,
            ResourceType resourceType,
            string? description,
            bool isRequired = false)
        {
            try
            {
                var userId  = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var trainer = await _context.Trainers.FirstOrDefaultAsync(t => t.UserId == userId);

                await _resourceService.UploadResourceAsync(
                    resourceFile,
                    lectureId,
                    trainer?.TrainerId,
                    resourceType,
                    description,
                    isRequired);

                TempData["Success"] = "تم رفع الملف بنجاح.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction(nameof(Manage), new { lectureId });
        }

        // ── Toggle Visibility ─────────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Trainer,Admin")]
        public async Task<IActionResult> ToggleVisibility(Guid resourceId, Guid lectureId)
        {
            var resource = await _resourceService.GetResourceAsync(resourceId);
            if (resource == null) return NotFound();

            await _resourceService.UpdateResourceAsync(
                resourceId,
                newType: null,
                newDescription: null,
                isRequired: null,
                isVisible: !resource.IsVisible);

            TempData["Success"] = resource.IsVisible
                ? "تم إخفاء الملف عن الطلاب."
                : "تم إظهار الملف للطلاب.";

            return RedirectToAction(nameof(Manage), new { lectureId });
        }

        // ── Delete ────────────────────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Trainer,Admin")]
        public async Task<IActionResult> Delete(Guid resourceId, Guid lectureId)
        {
            var success = await _resourceService.DeleteResourceAsync(resourceId);
            TempData[success ? "Success" : "Error"] = success
                ? "تم حذف الملف بنجاح."
                : "لم يتم العثور على الملف.";

            return RedirectToAction(nameof(Manage), new { lectureId });
        }

        // ── Download (Trainee) ────────────────────────────────────────

        public async Task<IActionResult> Download(Guid resourceId)
        {
            try
            {
                var userId  = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var trainee = await _context.Trainees.FirstOrDefaultAsync(t => t.UserId == userId);

                if (trainee == null)
                    return Forbid();

                var ip        = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "";
                var userAgent = Request.Headers["User-Agent"].ToString();

                var (fileBytes, fileName, contentType) = await _resourceService.DownloadResourceAsync(
                    resourceId, trainee.TraineeId, ip, userAgent);

                return File(fileBytes, contentType, fileName);
            }
            catch (UnauthorizedAccessException)
            {
                TempData["Error"] = "هذا الملف غير متاح حالياً.";
                return RedirectToAction("Index", "Lectures");
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("Index", "Lectures");
            }
        }

        // ── Admin Download (no tracking) ──────────────────────────────

        [Authorize(Roles = "Trainer,Admin")]
        public async Task<IActionResult> AdminDownload(Guid resourceId)
        {
            var resource = await _resourceService.GetResourceAsync(resourceId);
            if (resource == null) return NotFound();

            if (!System.IO.File.Exists(resource.FilePath))
                return NotFound();

            var bytes = await System.IO.File.ReadAllBytesAsync(resource.FilePath);
            var ext   = resource.FileExtension;
            var mime  = ext switch
            {
                ".pdf"  => "application/pdf",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".doc"  => "application/msword",
                ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                ".ppt"  => "application/vnd.ms-powerpoint",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".xls"  => "application/vnd.ms-excel",
                ".txt"  => "text/plain",
                ".zip"  => "application/zip",
                ".rar"  => "application/x-rar-compressed",
                _       => "application/octet-stream"
            };

            return File(bytes, mime, resource.FileName);
        }
    }
}
