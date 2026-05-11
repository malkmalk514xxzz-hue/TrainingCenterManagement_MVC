using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using TrainingCenterManagement_MVC.Models;

namespace TrainingCenterManagement_MVC.Services
{
    public interface ILectureResourceService
    {
        Task<LectureResource> UploadResourceAsync(
            IFormFile file,
            Guid lectureId,
            Guid? trainerId,
            ResourceType resourceType,
            string? description = null,
            bool isRequired = false);

        Task<List<LectureResource>> GetLectureResourcesAsync(Guid lectureId, bool includeHidden = false);

        Task<LectureResource?> GetResourceAsync(Guid resourceId);

        Task<bool> DeleteResourceAsync(Guid resourceId);

        Task<bool> UpdateResourceAsync(
            Guid resourceId,
            ResourceType? newType,
            string? newDescription,
            bool? isRequired,
            bool? isVisible);

        Task<(byte[] fileBytes, string fileName, string contentType)> DownloadResourceAsync(
            Guid resourceId,
            Guid traineeId,
            string ipAddress,
            string userAgent);

        Task<Dictionary<ResourceType, int>> GetResourceStatisticsAsync(Guid lectureId);
    }
}
