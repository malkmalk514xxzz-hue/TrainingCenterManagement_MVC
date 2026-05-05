using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Security.Claims;
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

        //public override async Task OnConnectedAsync()
        //{
        //    try
        //    {
        //        var userId = await GetCurrentUserId();
        //        if (!string.IsNullOrEmpty(userId))
        //        {
        //            var connection = new UserConnection
        //            {
        //                UserId = userId,
        //                ConnectionId = Context.ConnectionId,
        //                ConnectedAt = DateTime.Now,
        //                IsConnected = true
        //            };
        //            _context.UserConnections.Add(connection);
        //            await _context.SaveChangesAsync();
        //            await Clients.All.SendAsync("UserStatusChanged", userId, true);
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"خطأ في OnConnectedAsync: {ex.Message}");
        //        await Clients.Caller.SendAsync("ReceiveError", "فشل الاتصال");
        //    }
        //    await base.OnConnectedAsync();
        //}
        public override async Task OnConnectedAsync()
        {
            try
            {
                var isAndroidApp = IsAndroidClient();

                if (isAndroidApp)
                {
                    await HandleAndroidConnection();
                }
                else
                {
                    await HandleBrowserOrGuestConnection();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"خطأ في OnConnectedAsync: {ex.Message}");
            }

            await base.OnConnectedAsync();
        }

        // دالة مساعدة للكشف
        private bool IsAndroidClient()
        {
            var httpContext = Context.GetHttpContext();
            if (httpContext == null) return false;

            // طريقة 1: من Query Parameter (موصى بها)
            var clientType = httpContext.Request.Query["client"].FirstOrDefault();
            if (clientType?.Equals("android", StringComparison.OrdinalIgnoreCase) == true)
                return true;

            // طريقة 2: من User-Agent
            var userAgent = httpContext.Request.Headers["User-Agent"].ToString();
            return userAgent.Contains("Android", StringComparison.OrdinalIgnoreCase) ||
                   userAgent.Contains(" okhttp", StringComparison.OrdinalIgnoreCase); // OkHttp شائع في Android
        }

        // المنطق الخاص بتطبيق Android (يجب توكن + مستخدم مسجل)
        // المنطق الخاص بتطبيق Android (يجب توكن + مستخدم مسجل)
        private async Task HandleAndroidConnection()
        {
            Console.WriteLine("[OnConnectedAsync] Android App detected");

            var userId = await GetCurrentUserId();

            // تحقق صارم: إذا لم يحصل على UserId حقيقي → نرفض الاتصال
            if (string.IsNullOrEmpty(userId) ||
                userId.Length < 10 ||                    // GUID عادة أطول من 10
                userId.StartsWith("guest", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("[HandleAndroidConnection] ❌ No valid UserId from JWT token");
                await Clients.Caller.SendAsync("ReceiveError", "يجب تسجيل الدخول للاتصال من التطبيق");
                Context.Abort();   // يقطع الاتصال فوراً
                return;
            }

            Console.WriteLine($"[HandleAndroidConnection] ✅ Android User connected successfully → UserId: {userId}");
            await SaveUserConnection(userId, isAndroid: true);
        }

        // المنطق الخاص بالمتصفح والضيوف
        private async Task HandleBrowserOrGuestConnection()
        {
            Console.WriteLine("[OnConnectedAsync] Browser or Guest detected");

            var userId = await GetCurrentUserId();   // يمكن أن يكون Guest

            await SaveUserConnection(userId, isAndroid: false);
        }

        // دالة مشتركة لحفظ الاتصال
        private async Task SaveUserConnection(string userId, bool isAndroid)
        {
            var connection = new UserConnection
            {
                UserId = userId,
                ConnectionId = Context.ConnectionId,
                ConnectedAt = DateTime.UtcNow,
                IsConnected = true,
                // يمكنك إضافة عمود IsAndroid إذا أردت
            };

            _context.UserConnections.Add(connection);
            await _context.SaveChangesAsync();

            await Clients.All.SendAsync("UserStatusChanged", userId, true);
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
        public async Task SendGroupMessage(Guid courseId, string userId, string user, string message)
        {
            if (userId is null || userId == string.Empty)
                userId = await GetCurrentUserId();

            await Clients.Group($"Course_{courseId}").SendAsync("ReceiveGroupMessage", userId, user, message, DateTime.Now);

            var recentContacts = await GetRecentContacts(userId);
            await Clients.Group($"Course_{courseId}").SendAsync("UpdateRecentContacts", recentContacts);

            // إرسال التوست للأعضاء الآخرين فقط (بدون المرسل)
            await Clients.OthersInGroup($"Course_{courseId}").SendAsync("ReceiveNotification", user, message);

            // حفظ إشعار في قاعدة البيانات لكل عضو في المجموعة (ما عدا المرسل)
            await SaveGroupMessageNotificationsAsync(courseId, userId, user, message);
        }

        private async Task SaveGroupMessageNotificationsAsync(Guid courseId, string senderId, string senderName, string message)
        {
            try
            {
                var course = await _context.Courses.FindAsync(courseId);
                if (course == null) return;

                var traineeUserIds = await _context.CourseTrainees
                    .Where(ct => ct.CourseId == courseId)
                    .Include(ct => ct.Trainee)
                    .Where(ct => ct.Trainee.UserId != senderId)
                    .Select(ct => ct.Trainee.UserId)
                    .ToListAsync();

                var trainerUserIds = await _context.CourseTrainers
                    .Where(ct => ct.CourseId == courseId)
                    .Include(ct => ct.Trainer)
                    .Where(ct => ct.Trainer.UserId != senderId)
                    .Select(ct => ct.Trainer.UserId)
                    .ToListAsync();

                var allUserIds = traineeUserIds.Union(trainerUserIds).Distinct().ToList();
                if (!allUserIds.Any()) return;

                var truncated = message.Length > 100 ? message.Substring(0, 100) + "..." : message;

                // 1. حفظ إشعار في قاعدة البيانات لكل عضو
                var notifications = allUserIds.Select(uid => new UserNotification
                {
                    NotificationId = Guid.NewGuid(),
                    UserId = uid,
                    Title = $"رسالة في {course.CourseName}",
                    Message = $"{senderName}: {truncated}",
                    Type = NotificationType.MessageReceived,
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow,
                    RelatedId = courseId.ToString()
                }).ToList();

                _context.Notifications.AddRange(notifications);
                await _context.SaveChangesAsync();

                // 2. إرسال SignalR مباشرة لكل اتصالات الأعضاء (يشمل الـ notification widget)
                // الـ OthersInGroup في SendGroupMessage يغطي من هو على صفحة الدردشة
                // هذا الإرسال المباشر يغطي من هو على صفحات أخرى (Dashboard, Lectures...)
                foreach (var uid in allUserIds)
                {
                    var connections = await _context.UserConnections
                        .Where(c => c.UserId == uid && c.IsConnected)
                        .Select(c => c.ConnectionId)
                        .ToListAsync();

                    foreach (var connId in connections)
                    {
                        await Clients.Client(connId).SendAsync("ReceiveNotification", senderName, message);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SaveGroupMessageNotifications] ERROR: {ex.Message}");
            }
        }
        public async Task SendPrivateMessage(string receiverId, string userId, string sender, string message)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(receiverId) || string.IsNullOrWhiteSpace(message))
                {
                    await Clients.Caller.SendAsync("ReceiveError", "البيانات غير مكتملة");
                    return;
                }

                if (string.IsNullOrEmpty(userId))
                    userId = await GetCurrentUserId();

                var receiver = await _context.Users.FirstOrDefaultAsync(u => u.Id == receiverId);
                if (receiver == null)
                {
                    await Clients.Caller.SendAsync("ReceiveError", "المستلم غير موجود");
                    return;
                }

                var senderUser = await _context.Users.FindAsync(userId);
                var senderName = senderUser?.UserName ?? sender ?? "User";

                var privateMessage = new Message
                {
                    SenderId = userId,
                    ReceiverId = receiverId,
                    Content = message,
                    Timestamp = DateTime.UtcNow
                };
                _context.Messages.Add(privateMessage);

                var truncatedMsg = message.Length > 120 ? message.Substring(0, 120) + "..." : message;
                var notification = new UserNotification
                {
                    NotificationId = Guid.NewGuid(),
                    UserId = receiverId,
                    Title = senderName,
                    Message = truncatedMsg,
                    Type = NotificationType.MessageReceived,
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow,
                    RelatedId = userId
                };
                _context.Notifications.Add(notification);

                await _context.SaveChangesAsync();

                // جلب الاتصالات
                var receiverConnections = await _context.UserConnections
                    .Where(c => c.UserId == receiverId && c.IsConnected)
                    .Select(c => c.ConnectionId)
                    .ToListAsync();

                Console.WriteLine($"[SignalR] Sending private message to {receiverConnections.Count} connections for user {receiverId}");

                foreach (var connectionId in receiverConnections)
                {
                    // إرسال بطريقة واضحة وآمنة
                    await Clients.Client(connectionId).SendAsync("ReceivePrivateMessage",
                        userId,
                        senderName,
                        message,
                        privateMessage.Timestamp.ToString("o"));   // ISO string

                    await Clients.Client(connectionId).SendAsync("ReceiveNotification",
                        senderName, message);
                }

                Console.WriteLine($"[SignalR] Private message sent successfully to {receiverId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SignalR] ERROR in SendPrivateMessage: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                await Clients.Caller.SendAsync("ReceiveError", "حدث خطأ أثناء إرسال الرسالة");
            }
        }
        //public async Task SendPrivateMessage(string receiverId, string userId, string sender, string message)
        //{
        //    try
        //    {
        //        if (string.IsNullOrWhiteSpace(receiverId) || string.IsNullOrWhiteSpace(message))
        //        {
        //            throw new HubException("ReceiverId and message are required");
        //        }

        //        if (string.IsNullOrEmpty(userId))
        //            userId = await GetCurrentUserId();

        //        var receiver = await _context.Users
        //            .FirstOrDefaultAsync(u => u.Id == receiverId);

        //        if (receiver == null)
        //        {
        //            await Clients.Caller.SendAsync("ReceiveError", "المستلم غير موجود");
        //            return;
        //        }

        //        var senderUser = await _context.Users.FindAsync(userId);
        //        var senderName = senderUser?.UserName ?? sender ?? "User";

        //        // حفظ الرسالة (أزل التعليقات عندما تكون جاهزاً)
        //        var privateMessage = new Message
        //        {
        //            SenderId = userId,
        //            ReceiverId = receiverId,
        //            Content = message,
        //            Timestamp = DateTime.UtcNow
        //        };

        //        _context.Messages.Add(privateMessage);
        //        await _context.SaveChangesAsync();

        //        // جلب اتصالات المستلم (الأندرويد + المتصفحات)
        //        var receiverConnections = await _context.UserConnections
        //            .Where(c => c.UserId == receiverId && c.IsConnected)
        //            .Select(c => c.ConnectionId)
        //            .ToListAsync();

        //        if (receiverConnections.Count == 0)
        //        {
        //            Console.WriteLine($"No active connections found for receiver: {receiverId}");
        //            await Clients.Caller.SendAsync("ReceiveError", "المستلم غير متصل حالياً");
        //            return;
        //        }

        //        // إرسال الرسالة لكل اتصال نشط للمستلم
        //        foreach (var connectionId in receiverConnections)
        //        {
        //            await Clients.Client(connectionId).SendAsync("ReceivePrivateMessage",
        //                userId,
        //                senderName,
        //                message,
        //                privateMessage.Timestamp);

        //            await Clients.Client(connectionId).SendAsync("ReceiveNotification",
        //                senderName,
        //                message);
        //        }

        //        // تحديث Recent Contacts للمرسل والمستلم
        //        var senderRecent = await GetRecentContacts(userId);
        //        var receiverRecent = await GetRecentContacts(receiverId);

        //        // للمرسل
        //        var senderConnections = await _context.UserConnections
        //            .Where(c => c.UserId == userId && c.IsConnected)
        //            .Select(c => c.ConnectionId)
        //            .ToListAsync();

        //        foreach (var conn in senderConnections)
        //            await Clients.Client(conn).SendAsync("UpdateRecentContacts", senderRecent);

        //        // للمستلم
        //        foreach (var conn in receiverConnections)
        //            await Clients.Client(conn).SendAsync("UpdateRecentContacts", receiverRecent);

        //        Console.WriteLine($"Message sent successfully from {userId} to {receiverId} | Connections: {receiverConnections.Count}");
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"Error in SendPrivateMessage Hub: {ex.Message}\n{ex.StackTrace}");
        //        await Clients.Caller.SendAsync("ReceiveError", $"فشل إرسال الرسالة: {ex.Message}");
        //        throw;
        //    }
        //}


        public async Task JoinCourseGroup(Guid courseId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Course_{courseId}");
        }

        public async Task LeaveCourseGroup(Guid courseId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Course_{courseId}");
        }


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

        //private async Task<string> GetCurrentUserId()
        //{
        //    var user = await _userManager.GetUserAsync(Context.User);
        //    if (user != null)
        //    {
        //        return user.Id;
        //    }

        //    var queryGuestId = Context.GetHttpContext()?.Request.Query["guestId"].FirstOrDefault();
        //    if (!string.IsNullOrEmpty(queryGuestId))
        //    {
        //        return queryGuestId;
        //    }

        //    var httpContext = Context.GetHttpContext();
        //    if (httpContext != null)
        //    {
        //        string guestId = httpContext.Request.Cookies["guestId"];
        //        if (string.IsNullOrEmpty(guestId))
        //        {
        //            guestId = Guid.NewGuid().ToString();
        //            httpContext.Response.Cookies.Append("guestId", guestId, new CookieOptions
        //            {
        //                Expires = DateTime.UtcNow.AddYears(2),
        //                HttpOnly = true,
        //                Secure = true
        //            });
        //        }
        //        return guestId;
        //    }

        //    return Guid.NewGuid().ToString();
        //}
        private async Task<string> GetCurrentUserId()
        {
            try
            {
                var principal = Context.User;
                var httpContext = Context.GetHttpContext();

                if (principal != null)
                {
                    // البحث عن UserId بكل الأسماء الممكنة (مهم جدًا)
                    string userId =
                        principal.FindFirstValue(ClaimTypes.NameIdentifier) ??   // الأكثر شيوعًا
                        principal.FindFirstValue("sub") ??                       // موجود في توكنك
                        principal.FindFirstValue("Id") ??
                        principal.FindFirstValue("id") ??
                        principal.FindFirstValue("user_id") ??
                        principal.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");

                    if (!string.IsNullOrEmpty(userId))
                    {
                        Console.WriteLine($"[GetCurrentUserId] ✅ SUCCESS → UserId: {userId} | IsAndroid: {IsAndroidClient()}");
                        return userId;
                    }

                    // طباعة كل الـ Claims للتصحيح (مفيدة جدًا الآن)
                    Console.WriteLine("[GetCurrentUserId] Claims in principal:");
                    foreach (var claim in principal.Claims)
                    {
                        Console.WriteLine($"   - {claim.Type} = {claim.Value}");
                    }
                }

                // إذا كان Android ولم نجد UserId → نرفض الاتصال
                if (IsAndroidClient())
                {
                    Console.WriteLine("[GetCurrentUserId] ❌ Android client - No valid UserId found in token");
                    return string.Empty;
                }

                // Guest Mode (للمتصفح والضيوف)
                if (httpContext != null)
                {
                    var guestFromQuery = httpContext.Request.Query["guestId"].FirstOrDefault();
                    if (!string.IsNullOrEmpty(guestFromQuery))
                        return guestFromQuery;

                    var guestFromCookie = httpContext.Request.Cookies["guestId"];
                    if (!string.IsNullOrEmpty(guestFromCookie))
                        return guestFromCookie;
                }

                Console.WriteLine("[GetCurrentUserId] ⚠️ No user found → Generating Guest ID");
                return Guid.NewGuid().ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetCurrentUserId] ❌ Exception: {ex.Message}");
                return string.Empty;
            }
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