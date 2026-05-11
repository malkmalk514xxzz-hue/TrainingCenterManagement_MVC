using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TrainingCenterManagement_MVC.Data;
using TrainingCenterManagement_MVC.Models;

namespace TrainingCenterManagement_MVC.Services
{
    public class LectureResourceService : ILectureResourceService
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<LectureResourceService> _logger;

        private const long MaxFileSize = 500L * 1024 * 1024; // 500 MB

        private static readonly string[] AllowedExtensions =
        {
            ".pdf", ".docx", ".doc", ".pptx", ".ppt", ".xlsx", ".xls",
            ".txt", ".zip", ".rar", ".jpg", ".jpeg", ".png", ".gif", ".mp4", ".avi"
        };

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
            [".rar"]  = "application/x-rar-compressed",
            [".jpg"]  = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".png"]  = "image/png",
            [".gif"]  = "image/gif",
            [".mp4"]  = "video/mp4",
            [".avi"]  = "video/x-msvideo",
        };

        public LectureResourceService(
            ApplicationDbContext context,
            IWebHostEnvironment environment,
            ILogger<LectureResourceService> logger)
        {
            _context = context;
            _environment = environment;
            _logger = logger;
        }

        public async Task<LectureResource> UploadResourceAsync(
            IFormFile file,
            Guid lectureId,
            Guid? trainerId,
            ResourceType resourceType,
            string? description = null,
            bool isRequired = false)
        {
            var lecture = await _context.Lectures.FindAsync(lectureId);
            if (lecture == null)
                throw new InvalidOperationException("المحاضرة غير موجودة");

            if (file == null || file.Length == 0)
                throw new InvalidOperationException("الملف مطلوب");

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(ext))
                throw new InvalidOperationException($"نوع الملف '{ext}' غير مسموح به");

            if (file.Length > MaxFileSize)
                throw new InvalidOperationException("حجم الملف يتجاوز 500 ميغابايت");

            var uploadDir = Path.Combine(_environment.WebRootPath, "uploads", "resources", lectureId.ToString());
            Directory.CreateDirectory(uploadDir);

            var storedName = $"{Guid.NewGuid()}{ext}";
            var fullPath = Path.Combine(uploadDir, storedName);

            using (var stream = new FileStream(fullPath, FileMode.Create))
                await file.CopyToAsync(stream);

            var nextOrder = await _context.LectureResources
                .IgnoreQueryFilters()
                .Where(r => r.LectureId == lectureId)
                .MaxAsync(r => (int?)r.DisplayOrder) ?? 0;

            var resource = new LectureResource
            {
                LectureId            = lectureId,
                FileName             = file.FileName,
                FilePath             = fullPath,
                FileExtension        = ext,
                FileSizeInBytes      = file.Length,
                ResourceType         = resourceType,
                Description          = description,
                IsRequired           = isRequired,
                DisplayOrder         = nextOrder + 1,
                UploadedByTrainerId  = trainerId
            };

            _context.LectureResources.Add(resource);
            await _context.SaveChangesAsync();

            _logger.LogInformation("تم رفع ملف: {ResourceId}", resource.ResourceId);
            return resource;
        }

        public async Task<List<LectureResource>> GetLectureResourcesAsync(Guid lectureId, bool includeHidden = false)
        {
            var query = _context.LectureResources
                .Where(r => r.LectureId == lectureId);

            if (!includeHidden)
                query = query.Where(r => r.IsVisible);

            return await query
                .OrderBy(r => r.DisplayOrder)
                .ThenBy(r => r.CreatedAt)
                .ToListAsync();
        }

        public async Task<LectureResource?> GetResourceAsync(Guid resourceId)
        {
            return await _context.LectureResources
                .FirstOrDefaultAsync(r => r.ResourceId == resourceId);
        }

        public async Task<bool> DeleteResourceAsync(Guid resourceId)
        {
            var resource = await _context.LectureResources
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(r => r.ResourceId == resourceId);

            if (resource == null) return false;

            resource.IsDeleted  = true;
            resource.DeletedAt  = DateTime.UtcNow;

            if (File.Exists(resource.FilePath))
            {
                try { File.Delete(resource.FilePath); }
                catch (Exception ex) { _logger.LogWarning("لم يتم حذف الملف من القرص: {Ex}", ex.Message); }
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> UpdateResourceAsync(
            Guid resourceId,
            ResourceType? newType,
            string? newDescription,
            bool? isRequired,
            bool? isVisible)
        {
            var resource = await _context.LectureResources.FindAsync(resourceId);
            if (resource == null) return false;

            if (newType.HasValue)      resource.ResourceType = newType.Value;
            if (newDescription != null) resource.Description  = newDescription;
            if (isRequired.HasValue)   resource.IsRequired    = isRequired.Value;
            if (isVisible.HasValue)    resource.IsVisible     = isVisible.Value;
            resource.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<(byte[] fileBytes, string fileName, string contentType)> DownloadResourceAsync(
            Guid resourceId,
            Guid traineeId,
            string ipAddress,
            string userAgent)
        {
            var resource = await _context.LectureResources.FindAsync(resourceId)
                ?? throw new InvalidOperationException("الملف غير موجود");

            if (!resource.IsVisible)
                throw new UnauthorizedAccessException("هذا الملف غير متاح");

            if (!File.Exists(resource.FilePath))
                throw new FileNotFoundException("الملف غير موجود على الخادم");

            var fileBytes = await File.ReadAllBytesAsync(resource.FilePath);

            _context.ResourceDownloads.Add(new ResourceDownload
            {
                ResourceId    = resourceId,
                TraineeId     = traineeId,
                IpAddress     = ipAddress,
                UserAgent     = userAgent
            });

            resource.DownloadCount++;
            await _context.SaveChangesAsync();

            MimeTypes.TryGetValue(resource.FileExtension, out var mime);
            return (fileBytes, resource.FileName, mime ?? "application/octet-stream");
        }

        public async Task<Dictionary<ResourceType, int>> GetResourceStatisticsAsync(Guid lectureId)
        {
            var stats = await _context.LectureResources
                .IgnoreQueryFilters()
                .Where(r => r.LectureId == lectureId && !r.IsDeleted)
                .GroupBy(r => r.ResourceType)
                .Select(g => new { Type = g.Key, Count = g.Count() })
                .ToListAsync();

            return stats.ToDictionary(x => x.Type, x => x.Count);
        }
    }
}
