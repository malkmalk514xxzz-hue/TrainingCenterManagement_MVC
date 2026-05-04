using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TrainingCenterManagement_MVC.Data;
namespace TrainingCenterManagement_MVC.Controllers
{
    [Authorize]
    public class NotificationsController : Controller
    {
        private readonly ApplicationDbContext _context;
        public NotificationsController(ApplicationDbContext context)
        {
            _context = context;
        }
        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Challenge();
            }
            var notifications = await _context.Notifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();
            return View(notifications);
        }
        [HttpGet]
        public async Task<IActionResult> GetLatest(int take = 10)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized();
            }
            take = Math.Clamp(take, 1, 50);
            var notifications = await _context.Notifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .Take(take)
                .Select(n => new
                {
                    n.NotificationId,
                    n.Title,
                    n.Message,
                    Type = n.Type.ToString(),
                    n.IsRead,
                    n.CreatedAt,
                    n.RelatedId
                })
                .ToListAsync();
            var unreadCount = await _context.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);
            return Json(new { unreadCount, notifications });
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAsRead(Guid id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized();
            }
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.NotificationId == id && n.UserId == userId);
            if (notification == null)
            {
                return NotFound();
            }
            if (!notification.IsRead)
            {
                notification.IsRead = true;
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized();
            }
            var notifications = await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ToListAsync();
            if (notifications.Count > 0)
            {
                foreach (var notification in notifications)
                {
                    notification.IsRead = true;
                }
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
