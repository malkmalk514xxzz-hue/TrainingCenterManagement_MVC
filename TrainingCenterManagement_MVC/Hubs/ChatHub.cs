using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
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
            var userId = await GetCurrentUserId();
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
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            var connection = await _context.UserConnections
                .FirstOrDefaultAsync(c => c.ConnectionId == Context.ConnectionId && c.IsConnected);
            if (connection != null)
            {
                _context.UserConnections.Remove(connection);
                await _context.SaveChangesAsync();
                await Clients.All.SendAsync("UserStatusChanged", connection.UserId, false);
            }
            await base.OnDisconnectedAsync(exception);
        }

        public async Task SendGroupMessage(Guid courseId, string user, string message)
        {
            var userId = await GetCurrentUserId();
            var groupMessage = new GroupMessage
            {
                CourseId = courseId,
                SenderId = userId,
                Content = message,
                Timestamp = DateTime.Now
            };
            _context.GroupMessages.Add(groupMessage);
            await _context.SaveChangesAsync();
            await Clients.Group($"Course_{courseId}").SendAsync("ReceiveGroupMessage", user, message, DateTime.Now);
        }

        public async Task SendPrivateMessage(string receiverUsername, string sender, string message)
        {
            var receiver = await _context.Users.FirstOrDefaultAsync(u => u.UserName == receiverUsername);
            if (receiver == null) return;

            var userId = await GetCurrentUserId();
            var privateMessage = new Message
            {
                SenderId = userId,
                ReceiverId = receiver.Id,
                Content = message,
                Timestamp = DateTime.Now
            };
            _context.Messages.Add(privateMessage);
            await _context.SaveChangesAsync();

            // استرجاع جميع ConnectionIds النشطة للمستخدم المستلم
            var connections = await _context.UserConnections
                .Where(c => c.UserId == receiver.Id && c.IsConnected)
                .Select(c => c.ConnectionId)
                .ToListAsync();

            // إرسال الرسالة إلى كل ConnectionId
            foreach (var connectionId in connections)
            {
                await Clients.Client(connectionId).SendAsync("ReceivePrivateMessage", sender, message, DateTime.Now);
            }
        }

        public async Task JoinCourseGroup(int courseId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Course_{courseId}");
        }

        public async Task LeaveCourseGroup(int courseId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Course_{courseId}");
        }

        private async Task<string> GetCurrentUserId()
        {
            var user = await _userManager.GetUserAsync(Context.User);
            return user?.Id ?? "";
        }
    }
}