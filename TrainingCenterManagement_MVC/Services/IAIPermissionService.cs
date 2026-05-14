using TrainingCenterManagement_MVC.Models;

namespace TrainingCenterManagement_MVC.Services
{
    public interface IAIPermissionService
    {
        Task<AIPermissionRole?> GetRolePermissionsAsync(string roleName);
        Task<bool> IsWithinDailyLimitAsync(string userId, int dailyLimit);
        Task LogAccessAsync(string userId, string accessType, string resource,
            string? resourceId, bool isAuthorized, string? reason,
            string? ip = null, string? userAgent = null, string? details = null);
        Task<bool> CanAccessUserDataAsync(string requesterId, string targetUserId, bool requesterIsAdmin);
        Task<bool> IsSystemWithinDailyLimitAsync(int systemLimit);
    }
}
