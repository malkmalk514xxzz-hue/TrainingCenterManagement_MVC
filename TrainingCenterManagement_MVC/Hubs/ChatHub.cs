using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using TrainingCenterManagement_MVC.Data;
using TrainingCenterManagement_MVC.Models;

namespace Messaging_Chat_Application_MahmoudHakim.Hubs
{
    public class ChatHub : Hub
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ChatHub(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public override async Task OnConnectedAsync()
        {
            try
            {
                var userId = await GetCurrentUserId();
                if (!string.IsNullOrEmpty(userId))
                {
                    var connection = new UserConnection
                    {
                        UserId = userId,
                        ConnectionId = Context.ConnectionId,
                        ConnectedAt = DateTime.Now,
                        IsConnected = true
                    };
                    _context.UserConnections.Add(connection);
                    await _context.SaveChangesAsync();
                    await Clients.All.SendAsync("UserStatusChanged", userId, true);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"خطأ في OnConnectedAsync: {ex.Message}");
                await Clients.Caller.SendAsync("ReceiveError", "فشل الاتصال");
            }
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            try
            {
                var connection = await _context.UserConnections
                    .FirstOrDefaultAsync(c => c.ConnectionId == Context.ConnectionId && c.IsConnected);
                if (connection != null)
                {
                    _context.UserConnections.Remove(connection);
                    await _context.SaveChangesAsync();
                    await Clients.All.SendAsync("UserStatusChanged", connection.UserId, false);
                }
                if (exception != null)
                    Console.WriteLine($"خطأ في OnDisconnectedAsync: {exception.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"خطأ في OnDisconnectedAsync: {ex.Message}");
            }
            await base.OnDisconnectedAsync(exception);
        }
        public async Task SendGroupMessage(Guid courseId, string user, string message)
        {
            var userId = await GetCurrentUserId();
            //var groupMessage = new GroupMessage
            //{
            //    CourseId = courseId,
            //    SenderId = userId,
            //    Content = message,
            //    Timestamp = DateTime.Now
            //};
            //_context.GroupMessages.Add(groupMessage);
            //await _context.SaveChangesAsync();
            await Clients.Group($"Course_{courseId}").SendAsync("ReceiveGroupMessage", user, message, DateTime.Now);

            // إرسال قائمة المحادثات الأخيرة لجميع أعضاء المجموعة
            var recentContacts = await GetRecentContacts(userId);
            await Clients.Group($"Course_{courseId}").SendAsync("UpdateRecentContacts", recentContacts);
            await Clients.Group($"Course_{courseId}").SendAsync("ReceiveNotification", user, message);
        }

        public async Task SendPrivateMessage(string receiverId, string sender, string message)
        {
            var receiver = await _context.Users.FirstOrDefaultAsync(u => u.Id == receiverId);
            if (receiver == null) return;

            var userId = await GetCurrentUserId();
            //var privateMessage = new Message
            //{
            //    SenderId = userId,
            //    ReceiverId = receiver.Id,
            //    Content = message,
            //    Timestamp = DateTime.Now
            //};
            //_context.Messages.Add(privateMessage);
            //await _context.SaveChangesAsync();

            // إرسال الرسالة إلى المستلم
            var receiverConnections = await _context.UserConnections
                .Where(c => c.UserId == receiver.Id && c.IsConnected)
                .Select(c => c.ConnectionId)
                .ToListAsync();
            foreach (var connectionId in receiverConnections)
            {
                await Clients.Client(connectionId).SendAsync("ReceivePrivateMessage", sender, message, DateTime.Now);
                await Clients.Client(connectionId).SendAsync("ReceiveNotification", sender, message);
            }

            // إرسال قائمة المحادثات الأخيرة للمرسل
            var senderRecentContacts = await GetRecentContacts(userId);
            var senderConnections = await _context.UserConnections
                .Where(c => c.UserId == userId && c.IsConnected)
                .Select(c => c.ConnectionId)
                .ToListAsync();
            foreach (var connectionId in senderConnections)
            {
                await Clients.Client(connectionId).SendAsync("UpdateRecentContacts", senderRecentContacts);
            }

            // إرسال قائمة المحادثات الأخيرة للمستلم
            var receiverRecentContacts = await GetRecentContacts(receiver.Id);
            foreach (var connectionId in receiverConnections)
            {
                await Clients.Client(connectionId).SendAsync("UpdateRecentContacts", receiverRecentContacts);
            }
        }

        public async Task JoinCourseGroup(Guid courseId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Course_{courseId}");
        }

        public async Task LeaveCourseGroup(Guid courseId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Course_{courseId}");
        }
        //public async Task SendGroupMessage(Guid courseId, string user, string message)
        //{
        //    await Clients.Group($"Course_{courseId}").SendAsync("ReceiveGroupMessage", user, message, DateTime.Now);
        //    var recentContacts = await GetRecentContacts(await GetCurrentUserId());
        //    await Clients.Group($"Course_{courseId}").SendAsync("UpdateRecentContacts", recentContacts);
        //    await Clients.Group($"Course_{courseId}").SendAsync("ReceiveNotification", user, message);
        //}

        public async Task SendCustomerSupportMessage(string receiverId, string sender, string message)
        {
            var userId = await GetCurrentUserId();
            var isGuest = await IsGuestAsync();
            var senderName = isGuest ? $"ضيف_{userId.Substring(0, 8)}" : sender;

            await Clients.User(receiverId).SendAsync("ReceivePrivateMessage", senderName, message, DateTime.Now);
            await Clients.User(userId).SendAsync("ReceivePrivateMessage", senderName, message, DateTime.Now);

            var senderRecentContacts = await GetRecentContacts(userId);
            var receiverRecentContacts = await GetRecentContacts(receiverId);
            await Clients.User(userId).SendAsync("UpdateRecentContacts", senderRecentContacts);
            await Clients.User(receiverId).SendAsync("UpdateRecentContacts", receiverRecentContacts);
        }

        private async Task<string> GetCurrentUserId()
        {
            var user = await _userManager.GetUserAsync(Context.User);
            if (user != null)
            {
                return user.Id;
            }

            var queryGuestId = Context.GetHttpContext()?.Request.Query["guestId"].FirstOrDefault();
            if (!string.IsNullOrEmpty(queryGuestId))
            {
                return queryGuestId;
            }

            var httpContext = Context.GetHttpContext();
            if (httpContext != null)
            {
                string guestId = httpContext.Request.Cookies["guestId"];
                if (string.IsNullOrEmpty(guestId))
                {
                    guestId = Guid.NewGuid().ToString();
                    httpContext.Response.Cookies.Append("guestId", guestId, new CookieOptions
                    {
                        Expires = DateTime.UtcNow.AddYears(2),
                        HttpOnly = true,
                        Secure = true
                    });
                }
                return guestId;
            }

            return Guid.NewGuid().ToString();
        }

        private async Task<bool> IsGuestAsync()
        {
            var user = await _userManager.GetUserAsync(Context.User);
            return user == null || false;
        }

        private async Task<List<object>> GetRecentContacts(string userId)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) return new List<object>();

            var query = _context.Courses as IQueryable<Course>;
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

            // جلب المحادثات الخاصة
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

            // جلب المحادثات الجماعية
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

            // دمج المحادثات وترتيبها
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
}