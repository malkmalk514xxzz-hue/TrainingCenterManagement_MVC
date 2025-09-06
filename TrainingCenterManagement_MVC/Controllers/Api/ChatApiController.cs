using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TrainingCenterManagement_MVC.Data;
using TrainingCenterManagement_MVC.Models;
using Messaging_Chat_Application_MahmoudHakim.Hubs;

namespace TrainingCenterManagement_MVC.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHubContext<ChatHub> _hubContext;

        public ChatApiController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IHubContext<ChatHub> hubContext)
        {
            _context = context;
            _userManager = userManager;
            _hubContext = hubContext;
        }

        // GET: api/chat/recent-contacts
        [HttpGet("recent-contacts")]
        [Authorize]
        public async Task<IActionResult> GetRecentContacts()
        {
            var userId = await GetCurrentUserIdAsync();
            var contacts = await BuildRecentContacts(userId);
            return Ok(contacts);
        }

        // GET: api/chat/group/{courseId}/messages
        [HttpGet("group/{courseId:guid}/messages")]
        public async Task<IActionResult> GetGroupMessages(Guid courseId)
        {
            var messages = await _context.GroupMessages
                .Where(m => m.CourseId == courseId)
                .Include(m => m.Sender)
                .OrderBy(m => m.Timestamp)
                .Select(m => new { m.Id, m.CourseId, SenderId = m.SenderId, SenderName = m.Sender.UserName, m.Content, m.Timestamp })
                .ToListAsync();
            return Ok(messages);
        }

        // POST: api/chat/group/{courseId}/send
        [HttpPost("group/{courseId:guid}/send")]
        [Authorize]
        public async Task<IActionResult> SendGroupMessage(Guid courseId, [FromBody] GroupMessageDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Content)) return BadRequest(new { message = "Message cannot be empty" });

            var userId = await GetCurrentUserIdAsync();
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return Unauthorized();

            var course = await _context.Courses.FindAsync(courseId);
            if (course == null) return NotFound(new { message = "Course not found" });

            var gm = new GroupMessage
            {
                CourseId = courseId,
                SenderId = userId,
                Content = dto.Content,
                Timestamp = DateTime.UtcNow
            };
            _context.GroupMessages.Add(gm);
            await _context.SaveChangesAsync();

            // notify via SignalR if available
            await _hubContext.Clients.Group($"Course_{courseId}").SendAsync("ReceiveGroupMessage", user.UserName, dto.Content, gm.Timestamp);

            // update recent contacts for group members
            var recent = await BuildRecentContacts(userId);
            await _hubContext.Clients.Group($"Course_{courseId}").SendAsync("UpdateRecentContacts", recent);
            await _hubContext.Clients.Group($"Course_{courseId}").SendAsync("ReceiveNotification", user.UserName, dto.Content);

            return Ok(new { success = true });
        }

        // GET: api/chat/private/{otherId}/messages
        [HttpGet("private/{otherId}/messages")]
        [Authorize]
        public async Task<IActionResult> GetPrivateMessages(string otherId)
        {
            var userId = await GetCurrentUserIdAsync();
            var messages = await _context.Messages
                .Where(m => (m.SenderId == userId && m.ReceiverId == otherId) || (m.SenderId == otherId && m.ReceiverId == userId))
                .Include(m => m.Sender)
                .Include(m => m.Receiver)
                .OrderBy(m => m.Timestamp)
                .Select(m => new { m.Id, m.SenderId, SenderName = m.Sender.UserName, m.ReceiverId, ReceiverName = m.Receiver.UserName, m.Content, m.Timestamp })
                .ToListAsync();
            return Ok(messages);
        }

        // POST: api/chat/private/send
        [HttpPost("private/send")]
        [Authorize]
        public async Task<IActionResult> SendPrivateMessage([FromBody] PrivateMessageDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Content)) return BadRequest(new { message = "Message cannot be empty" });
            var userId = await GetCurrentUserIdAsync();
            var user = await _context.Users.FindAsync(userId);
            var receiver = await _context.Users.FindAsync(dto.ReceiverId);
            if (receiver == null) return NotFound(new { message = "Receiver not found" });

            var msg = new Message
            {
                SenderId = userId,
                ReceiverId = dto.ReceiverId,
                Content = dto.Content,
                Timestamp = DateTime.UtcNow
            };
            _context.Messages.Add(msg);
            await _context.SaveChangesAsync();

            // send to receiver connections
            var receiverConnections = await _context.UserConnections
                .Where(c => c.UserId == dto.ReceiverId && c.IsConnected)
                .Select(c => c.ConnectionId)
                .ToListAsync();
            foreach (var conn in receiverConnections)
            {
                await _hubContext.Clients.Client(conn).SendAsync("ReceivePrivateMessage", user.UserName, dto.Content, msg.Timestamp);
                await _hubContext.Clients.Client(conn).SendAsync("ReceiveNotification", user.UserName, dto.Content);
            }

            // update recent contacts for sender and receiver
            var senderRecent = await BuildRecentContacts(userId);
            var receiverRecent = await BuildRecentContacts(dto.ReceiverId);

            var senderConns = await _context.UserConnections.Where(c => c.UserId == userId && c.IsConnected).Select(c => c.ConnectionId).ToListAsync();
            foreach (var conn in senderConns)
            {
                await _hubContext.Clients.Client(conn).SendAsync("UpdateRecentContacts", senderRecent);
            }
            foreach (var conn in receiverConnections)
            {
                await _hubContext.Clients.Client(conn).SendAsync("UpdateRecentContacts", receiverRecent);
            }

            return Ok(new { success = true });
        }

        // Customer support: send message (guest or admin)
        // POST: api/chat/support/send
        [HttpPost("support/send")]
        public async Task<IActionResult> SendCustomerSupportMessage([FromBody] SupportMessageDto dto)
        {
            var userId = await GetCurrentUserIdAsync();
            var isGuest = await IsGuestAsync();

            if (isGuest)
            {
                // guest sending to admin
                var adminsIds = await _context.Users.Where(u => u.Role == RoleType.Admin).Select(u => u.Id).ToListAsync();
                var targetReceiverId = await _context.UserConnections.Where(uc => uc.IsConnected && adminsIds.Contains(uc.UserId)).Select(uc => uc.UserId).FirstOrDefaultAsync();
                targetReceiverId ??= adminsIds.FirstOrDefault();
                if (targetReceiverId == null) return BadRequest(new { message = "No admins available" });

                var guestId = userId;
                var contactUs = await _context.ContactUs.FirstOrDefaultAsync(cu => cu.GuestId == guestId);
                if (contactUs == null)
                {
                    contactUs = new ContactUs { GuestId = guestId };
                    _context.ContactUs.Add(contactUs);
                }

                var message = new GusetMessage
                {
                    SenderId = guestId,
                    ReceiverId = targetReceiverId,
                    Content = dto.Content,
                    Timestamp = DateTime.UtcNow
                };
                _context.GusetMessages.Add(message);
                await _context.SaveChangesAsync();

                var senderName = $"ضيف_{guestId.Substring(0, 8)}";
                // notify connected admin clients
                var adminConns = await _context.UserConnections.Where(uc => uc.UserId == targetReceiverId && uc.IsConnected).Select(uc => uc.ConnectionId).ToListAsync();
                foreach (var conn in adminConns)
                {
                    await _hubContext.Clients.Client(conn).SendAsync("ReceivePrivateMessage", senderName, dto.Content, message.Timestamp);
                }

                return Ok(new { success = true });
            }
            else
            {
                // admin replying to guest
                var user = await _context.Users.FindAsync(userId);
                if (user?.Role != RoleType.Admin) return Unauthorized();
                var targetGuestId = dto.TargetGuestId;
                if (string.IsNullOrEmpty(targetGuestId)) return BadRequest(new { message = "Guest id required" });

                var contactUs = await _context.ContactUs.FirstOrDefaultAsync(cu => cu.GuestId == targetGuestId);
                if (contactUs == null)
                {
                    contactUs = new ContactUs { GuestId = targetGuestId };
                    _context.ContactUs.Add(contactUs);
                }

                var message = new GusetMessage
                {
                    SenderId = userId,
                    ReceiverId = targetGuestId,
                    Content = dto.Content,
                    Timestamp = DateTime.UtcNow
                };
                _context.GusetMessages.Add(message);
                await _context.SaveChangesAsync();

                // notify guest connections
                var guestConns = await _context.UserConnections.Where(uc => uc.UserId == targetGuestId && uc.IsConnected).Select(uc => uc.ConnectionId).ToListAsync();
                foreach (var conn in guestConns)
                {
                    await _hubContext.Clients.Client(conn).SendAsync("ReceivePrivateMessage", user.UserName, dto.Content, message.Timestamp);
                }

                return Ok(new { success = true });
            }
        }

        // GET: api/chat/support/{guestId}/messages
        [HttpGet("support/{guestId}/messages")]
        [Authorize]
        public async Task<IActionResult> GetSupportMessages(string guestId)
        {
            var userId = await GetCurrentUserIdAsync();
            var user = await _context.Users.FindAsync(userId);
            if (user?.Role != RoleType.Admin)
            {
                // allow guest to get own messages
                if (guestId != userId) return Unauthorized();
            }

            var adminsIds = await _context.Users.Where(u => u.Role == RoleType.Admin).Select(u => u.Id).ToListAsync();
            var messages = await _context.GusetMessages
                .Where(m => (adminsIds.Contains(m.SenderId) && m.ReceiverId == guestId) || (m.SenderId == guestId && adminsIds.Contains(m.ReceiverId)))
                .OrderBy(m => m.Timestamp)
                .ToListAsync();

            return Ok(messages);
        }

        // GET: api/chat/online
        [HttpGet("online")]
        public async Task<IActionResult> GetOnlineUsers()
        {
            var conns = await _context.UserConnections.Where(uc => uc.IsConnected).Select(uc => new { uc.UserId, uc.ConnectionId, uc.ConnectedAt }).ToListAsync();
            return Ok(conns);
        }

        // Helpers
        private async Task<string> GetCurrentUserIdAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null) return user.Id;

            // check query or cookies for guestId
            var queryGuestId = HttpContext.Request.Query["guestId"].FirstOrDefault();
            if (!string.IsNullOrEmpty(queryGuestId)) return queryGuestId;

            var cookie = HttpContext.Request.Cookies["guestId"];
            if (!string.IsNullOrEmpty(cookie)) return cookie;

            // create guest id cookie
            var guestId = Guid.NewGuid().ToString();
            HttpContext.Response.Cookies.Append("guestId", guestId, new CookieOptions { Expires = DateTime.UtcNow.AddYears(2), HttpOnly = true, Secure = true });
            return guestId;
        }

        private async Task<bool> IsGuestAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            return user == null;
        }

        private async Task<List<object>> BuildRecentContacts(string userId)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) return new List<object>();

            var query = _context.Courses.AsQueryable();
            List<Course> myCourses = new List<Course>();

            if (user.Role == RoleType.Trainee)
            {
                var trainee = await _context.Trainees.FirstOrDefaultAsync(t => t.UserId == userId);
                if (trainee != null)
                {
                    myCourses = await query
                        .Include(c => c.CourseTrainees)
                        .SelectMany(c => c.CourseTrainees)
                        .Where(ct => ct.TraineeId == trainee.TraineeId)
                        .Select(ct => ct.Course)
                        .ToListAsync();
                }
            }
            else if (user.Role == RoleType.Trainer)
            {
                var trainer = await _context.Trainers.FirstOrDefaultAsync(t => t.UserId == userId);
                if (trainer != null)
                {
                    myCourses = await query
                        .Include(c => c.CourseTrainers)
                        .SelectMany(c => c.CourseTrainers)
                        .Where(ct => ct.TrainerId == trainer.TrainerId)
                        .Select(ct => ct.Course)
                        .ToListAsync();
                }
            }
            else if (user.Role == RoleType.Admin)
            {
                myCourses = await query.ToListAsync();
            }

            var privateContacts = await _context.Messages
                .Where(m => m.SenderId == userId || m.ReceiverId == userId)
                .Include(m => m.Sender)
                .Include(m => m.Receiver)
                .GroupBy(m => m.SenderId == userId ? m.ReceiverId : m.SenderId)
                .Select(g => new
                {
                    Id = g.Key,
                    DisplayName = g.First().SenderId == userId ? g.First().Receiver.UserName : g.First().Sender.UserName,
                    LastMessage = g.OrderByDescending(m => m.Timestamp).First().Content,
                    LastMessageTime = g.OrderByDescending(m => m.Timestamp).First().Timestamp,
                    IsGroup = false
                })
                .ToListAsync();

            var groupContacts = await _context.GroupMessages
                .Where(gm => myCourses.Select(c => c.CourseId).Contains(gm.CourseId))
                .Include(gm => gm.Course)
                .GroupBy(gm => gm.CourseId)
                .Select(g => new
                {
                    Id = g.Key.ToString(),
                    DisplayName = g.First().Course.CourseName,
                    LastMessage = g.OrderByDescending(m => m.Timestamp).First().Content,
                    LastMessageTime = g.OrderByDescending(m => m.Timestamp).First().Timestamp,
                    IsGroup = true
                })
                .ToListAsync();

            var allContacts = privateContacts.Concat(groupContacts)
                .OrderByDescending(c => c.LastMessageTime)
                .Select(c => new
                {
                    Id = c.Id,
                    DisplayName = c.DisplayName,
                    LastMessage = c.LastMessage,
                    LastMessageTime = c.LastMessageTime.ToString("o"),
                    IsGroup = c.IsGroup
                })
                .ToList();

            return allContacts.Cast<object>().ToList();
        }
    }

    // DTOs
    public record GroupMessageDto(string Content);
    public record PrivateMessageDto(string ReceiverId, string Content);
    public record SupportMessageDto(string Content, string TargetGuestId = null);
}
