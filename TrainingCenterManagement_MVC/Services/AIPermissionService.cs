using Microsoft.EntityFrameworkCore;
using TrainingCenterManagement_MVC.Data;
using TrainingCenterManagement_MVC.Models;

namespace TrainingCenterManagement_MVC.Services
{
    public class AIPermissionService : IAIPermissionService
    {
        private readonly ApplicationDbContext _context;

        public AIPermissionService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<AIPermissionRole?> GetRolePermissionsAsync(string roleName)
            => await _context.AIPermissionRoles.FirstOrDefaultAsync(r => r.RoleName == roleName);

        public async Task<bool> IsWithinDailyLimitAsync(string userId, int dailyLimit)
        {
            if (dailyLimit < 0) return true; // unlimited (Admin)

            var today = DateTime.UtcNow.Date;
            var count  = await _context.AIChatMessages
                .IgnoreQueryFilters()
                .CountAsync(m => m.UserId == userId
                              && !m.IsDeleted
                              && m.CreatedAt >= today
                              && m.CreatedAt < today.AddDays(1));
            return count < dailyLimit;
        }

        public async Task LogAccessAsync(string userId, string accessType, string resource,
            string? resourceId, bool isAuthorized, string? reason,
            string? ip = null, string? userAgent = null, string? details = null)
        {
            _context.AIAccessLogs.Add(new AIAccessLog
            {
                UserId           = userId,
                AccessType       = accessType,
                ResourceAccessed = resource,
                ResourceId       = resourceId,
                IsAuthorized     = isAuthorized,
                DenialReason     = reason,
                IpAddress        = ip,
                UserAgent        = userAgent,
                Details          = details
            });
            await _context.SaveChangesAsync();
        }

        public Task<bool> CanAccessUserDataAsync(string requesterId, string targetUserId, bool requesterIsAdmin)
            => Task.FromResult(requesterIsAdmin || requesterId == targetUserId);

        public async Task<bool> IsSystemWithinDailyLimitAsync(int systemLimit)
        {
            if (systemLimit <= 0) return true;
            var today = DateTime.UtcNow.Date;
            var count = await _context.AIChatMessages
                .IgnoreQueryFilters()
                .CountAsync(m => m.IsAnswered
                              && m.CreatedAt >= today
                              && m.CreatedAt < today.AddDays(1));
            return count < systemLimit;
        }
    }
}
