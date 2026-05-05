using Messaging_Chat_Application_MahmoudHakim.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TrainingCenterManagement_MVC.Data;
using TrainingCenterManagement_MVC.Models;
using TrainingCenterManagement_MVC.ViewModels;
using static QRCoder.PayloadGenerator.SwissQrCode;

namespace TrainingCenterManagement_MVC.Controllers
{
    public class ChatController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHubContext<ChatHub> _hubContext;
        private readonly IWebHostEnvironment _env;

        public ChatController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IHubContext<ChatHub> hubContext, IWebHostEnvironment env)
        {
            _context = context;
            _userManager = userManager;
            _hubContext = hubContext;
            _env = env;
        }
        

        public async Task<IActionResult> Index()
        {
            var userId = await GetUserIdAsync();
            ViewBag.UserId = userId; // تمرير Id المستخدم الحالي

            var query = _context.Courses as IQueryable<Course>;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);

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
            else
            {
                myCourses = new List<Course>();
            }

            // جلب المحادثات الخاصة
            var privateContacts = await _context.Messages
                .Where(m => m.SenderId == userId || m.ReceiverId == userId)
                .Include(m => m.Sender)
                .Include(m => m.Receiver)
                .GroupBy(m => m.SenderId == userId ? m.ReceiverId : m.SenderId)
                .Select(g => new RecentContact
                {
                    UserId = g.Key,
                    Username = g.First().SenderId == userId ? g.First().Receiver.UserName : g.First().Sender.UserName,
                    LastMessage = g.OrderByDescending(m => m.Timestamp).First().Content,
                    LastMessageTime = g.OrderByDescending(m => m.Timestamp).First().Timestamp ,
                    IsGroup = false
                })
                .ToListAsync();

            // جلب المحادثات الجماعية
            var groupContacts = await _context.GroupMessages
                .Where(gm => myCourses.Select(c => c.CourseId).Contains(gm.CourseId))
                .Include(gm => gm.Course)
                .GroupBy(gm => gm.CourseId)
                .Select(g => new RecentContact
                {
                    UserId = g.Key.ToString(),
                    Username = g.First().Course.CourseName,
                    LastMessage = g.OrderByDescending(m => m.Timestamp).First().Content,
                    LastMessageTime = g.OrderByDescending(m => m.Timestamp).First().Timestamp ,
                     IsGroup = true
                })
                .ToListAsync();

            // دمج المحادثات الخاصة والجماعية وترتيبها
            var allContacts = privateContacts.Concat(groupContacts)
                .OrderByDescending(c => c.LastMessageTime)
                .ToList();

            var model = new ChatIndexViewModel
            {
                Courses = myCourses,
                RecentContacts = allContacts,
                AllUsers = await _context.Users
                    .Where(u => u.Id != userId)
                    .ToListAsync()
            };
            return View(model);
        }

        public async Task<IActionResult> GroupChat(Guid courseId)
        {
            var userId = await GetUserIdAsync();
            var messages = await _context.GroupMessages
                .Where(m => m.CourseId == courseId)
                .Include(m => m.Sender)
                .OrderBy(m => m.Timestamp)
                .ThenBy(m => m.Id)
                .ToListAsync();
            var course = await _context.Courses.FindAsync(courseId);
            ViewBag.CourseId = courseId;
            ViewBag.CourseName = course?.CourseName ?? "Group Chat";
            ViewBag.UserId = userId;
            return View(messages);
        }

        public async Task<IActionResult> PrivateChat(string receiverId)
        {
            var userId = await GetUserIdAsync();
            var messages = await _context.Messages
                .Where(m => (m.SenderId == userId && m.ReceiverId == receiverId) ||
                            (m.SenderId == receiverId && m.ReceiverId == userId))
                .Include(m => m.Sender)
                .Include(m => m.Receiver)
                .OrderBy(m => m.Timestamp)
                .ThenBy(m => m.Id)
                .ToListAsync();

            // Mark received messages as read on open
            var unread = messages.Where(m => m.SenderId == receiverId && !m.IsRead).ToList();
            if (unread.Any())
            {
                unread.ForEach(m => m.IsRead = true);
                await _context.SaveChangesAsync();
            }

            ViewBag.ReceiverId = receiverId;
            ViewBag.UserId = userId;
            var receiver = await _context.Users.FindAsync(receiverId);
            ViewBag.ReceiverUsername = receiver?.UserName;
            return View(messages);
        }

        [HttpPost]
        public async Task<IActionResult> MarkRead(string partnerId)
        {
            var userId = await GetUserIdAsync();
            var unread = await _context.Messages
                .Where(m => m.SenderId == partnerId && m.ReceiverId == userId && !m.IsRead)
                .ToListAsync();
            if (unread.Any())
            {
                unread.ForEach(m => m.IsRead = true);
                await _context.SaveChangesAsync();

                // Notify sender that messages were read
                var senderConnections = await _context.UserConnections
                    .Where(c => c.UserId == partnerId && c.IsConnected)
                    .Select(c => c.ConnectionId).ToListAsync();
                var msgIds = unread.Select(m => m.Id).ToList();
                foreach (var conn in senderConnections)
                    await _hubContext.Clients.Client(conn).SendAsync("MessagesRead", msgIds);
            }
            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> SendMedia(IFormFile file, string? receiverId, string? courseId, string mediaType)
        {
            if (file == null || file.Length == 0)
                return Json(new { success = false, message = "No file provided" });
            if (file.Length > 50 * 1024 * 1024)
                return Json(new { success = false, message = "File exceeds 50 MB limit" });

            var userId = await GetUserIdAsync();
            var user = await _context.Users.FindAsync(userId);
            var now = DateTime.Now;

            var origExt = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!string.IsNullOrEmpty(origExt))
            {
                origExt = origExt.Split(';')[0];
            }
            var ext = string.IsNullOrEmpty(origExt)
                ? (mediaType == "audio" ? ".webm" : mediaType == "video" ? ".mp4" : ".jpg")
                : origExt;

            var yearPart = now.Year.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var monthPart = now.Month.ToString("00", System.Globalization.CultureInfo.InvariantCulture);
            var folder = Path.Combine(_env.WebRootPath, "uploads", mediaType, yearPart, monthPart);
            Directory.CreateDirectory(folder);
            var fileName = $"{Guid.NewGuid()}{ext}";
            using (var stream = System.IO.File.Create(Path.Combine(folder, fileName)))
                await file.CopyToAsync(stream);

            var url = $"/uploads/{mediaType}/{yearPart}/{monthPart}/{fileName}";
            var senderName = user?.UserName ?? "User";

            if (!string.IsNullOrEmpty(receiverId))
            {
                var msg = new Message
                {
                    SenderId = userId,
                    ReceiverId = receiverId,
                    Content = $"[{mediaType} message]",
                    MediaUrl = url,
                    Timestamp = DateTime.Now
                };
                _context.Messages.Add(msg);
                await _context.SaveChangesAsync();

                var connections = await _context.UserConnections
                    .Where(c => c.UserId == receiverId && c.IsConnected)
                    .Select(c => c.ConnectionId)
                    .ToListAsync();

                var notifText = mediaType == "audio" ? "🎙️ رسالة صوتية" : mediaType == "video" ? "🎥 مقطع فيديو" : "🖼️ صورة";
                foreach (var conn in connections)
                {
                    await _hubContext.Clients.Client(conn).SendAsync(
                        "ReceivePrivateMessage",
                        userId,
                        senderName,
                        $"[media:{mediaType}:{url}]",
                        msg.Timestamp.ToString("o"),
                        msg.Id,
                        false);
                    await _hubContext.Clients.Client(conn).SendAsync(
                        "ReceiveNotification",
                        senderName,
                        notifText);
                }

                return Json(new { success = true, url, messageId = msg.Id, timestamp = msg.Timestamp.ToString("o") });
            }
            else if (!string.IsNullOrEmpty(courseId) && Guid.TryParse(courseId, out var courseGuid))
            {
                var msg = new GroupMessage
                {
                    CourseId = courseGuid,
                    SenderId = userId,
                    Content = $"[media:{mediaType}:{url}]",
                    Timestamp = DateTime.Now
                };
                _context.GroupMessages.Add(msg);
                await _context.SaveChangesAsync();

                var groupNotifText = mediaType == "audio" ? "🎙️ رسالة صوتية" : mediaType == "video" ? "🎥 مقطع فيديو" : "🖼️ صورة";
                await _hubContext.Clients.Group($"Course_{courseGuid}")
                    .SendAsync("ReceiveGroupMessage", userId, senderName, $"[media:{mediaType}:{url}]", msg.Timestamp.ToString("o"));
                await _hubContext.Clients.Group($"Course_{courseGuid}")
                    .SendAsync("ReceiveNotification", senderName, groupNotifText);

                return Json(new { success = true, url, messageId = msg.Id, timestamp = msg.Timestamp.ToString("o") });
            }

            return Json(new { success = false, message = "Invalid target" });
        }

        [HttpPost]
        public async Task<IActionResult> SendGroupMessage(Guid courseId, string content)
        {
            try
            {
                content = (content ?? string.Empty).Trim();

                if (string.IsNullOrEmpty(content))
                    return Json(new { success = false, message = "Message cannot be empty" });

                var userId = await GetUserIdAsync();
                if (string.IsNullOrEmpty(userId))
                    return Json(new { success = false, message = "User not authenticated" });

                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                    return Json(new { success = false, message = "User not found" });

                var course = await _context.Courses.FindAsync(courseId);
                if (course == null)
                    return Json(new { success = false, message = "Course not found" });

                var now = DateTime.Now;
                var groupMessage = new GroupMessage
                {
                    CourseId = courseId,
                    SenderId = userId,
                    Content = content,
                    Timestamp = now
                };

                await _context.GroupMessages.AddAsync(groupMessage);
                await _context.SaveChangesAsync();

                await _hubContext.Clients.Group($"Course_{courseId}")
                    .SendAsync("ReceiveGroupMessage", userId, user.UserName, content, groupMessage.Timestamp.ToString("o"));

                var courseConnections = await _context.UserConnections
                    .Where(c => c.IsConnected && c.UserId != userId)
                    .Join(
                        _context.CourseTrainees.Where(ct => ct.CourseId == courseId).Select(ct => ct.Trainee.UserId)
                            .Union(_context.CourseTrainers.Where(ct => ct.CourseId == courseId).Select(ct => ct.Trainer.UserId))
                            .Distinct(),
                        connection => connection.UserId,
                        memberId => memberId,
                        (connection, memberId) => connection.ConnectionId)
                    .Distinct()
                    .ToListAsync();

                foreach (var connectionId in courseConnections)
                {
                    await _hubContext.Clients.Client(connectionId)
                        .SendAsync("ReceiveNotification", user.UserName, content);
                }

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in SendGroupMessage: {ex.Message}");
                return Json(new { success = false, message = $"Failed to send message: {ex.Message}" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> SendPrivateMessage(string receiverId, string content)
        {
            content = (content ?? string.Empty).Trim();

            if (string.IsNullOrEmpty(content))
                return Json(new { success = false, message = "Message cannot be empty" });

            var userId = await GetUserIdAsync();
            var user = await _context.Users.FindAsync(userId);
            var receiver = await _context.Users.FindAsync(receiverId);
            if (receiver == null)
                return Json(new { success = false, message = "Receiver not found" });

            var privateMessage = new Message
            {
                SenderId = userId,
                ReceiverId = receiverId,
                Content = content,
                Timestamp = DateTime.Now
            };
            _context.Messages.Add(privateMessage);
            await _context.SaveChangesAsync();

            var senderName = user?.UserName ?? SenderFallback(userId);
            var receiverConnections = await _context.UserConnections
                .Where(c => c.UserId == receiverId && c.IsConnected)
                .Select(c => c.ConnectionId)
                .ToListAsync();

            foreach (var connectionId in receiverConnections)
            {
                await _hubContext.Clients.Client(connectionId).SendAsync(
                    "ReceivePrivateMessage",
                    userId,
                    senderName,
                    content,
                    privateMessage.Timestamp.ToString("o"),
                    privateMessage.Id,
                    false);

                await _hubContext.Clients.Client(connectionId).SendAsync(
                    "ReceiveNotification",
                    senderName,
                    content);
            }

            return Json(new { success = true, messageId = privateMessage.Id, timestamp = privateMessage.Timestamp.ToString("o") });
        }

        private static string SenderFallback(string userId)
        {
            return string.IsNullOrWhiteSpace(userId) ? "User" : $"User {userId[..Math.Min(4, userId.Length)]}";
        }

        private async Task<string> GetUserIdAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                string guestId = Request.Cookies["guestId"];
                if (string.IsNullOrEmpty(guestId) || !Guid.TryParse(guestId, out var guid))
                {
                    guid = Guid.NewGuid();
                    Response.Cookies.Append("guestId", guid.ToString(), new CookieOptions
                    {
                        Expires = DateTime.UtcNow.AddYears(2),
                        HttpOnly = true,
                        Secure = true
                    });
                }
                return guid.ToString();
            }
            return user.Id;

        }


        [HttpGet]
        public async Task<IActionResult> CustomerSupportChat()
        {
            var guestId = await GetGuestIdAsync();
            var contactUs = await _context.ContactUs.FirstOrDefaultAsync(cu => cu.GuestId == guestId.ToString());
            if (contactUs == null)
            {
                contactUs = new ContactUs
                {
                    GuestId = guestId.ToString(),
                    GusetMessages = new List<GusetMessage>()
                };
                _context.ContactUs.Add(contactUs);
                await _context.SaveChangesAsync();
            }

            var adminsIds = await _context.Users
                .Where(u => u.Role == RoleType.Admin)
                .Select(u => u.Id)
                .ToListAsync();

            var messages = await _context.GusetMessages
                .Where(m => (adminsIds.Any(id => m.SenderId == id) && m.ReceiverId == guestId.ToString()) ||
                            (m.SenderId == guestId.ToString() && adminsIds.Any(id => m.ReceiverId == id)))
               
                .OrderBy(m => m.Timestamp)
                .ThenBy(m => m.Id)
                .ToListAsync();

            ViewBag.ReceiverId = adminsIds.FirstOrDefault() ?? "admin";
            ViewBag.ReceiverUsername = "دعم الإدارة";
            ViewBag.SenderId = guestId.ToString();
            ViewBag.SenderUsername = $"ضيف_{guestId.ToString().Substring(0, 8)}";
            ViewBag.IsGuestChat = true;

            return View("ContactUsChat", messages);
        }

        [HttpGet]
        public async Task<IActionResult> AdminCustomerSupportChats()
        {
            var userId = await GetUserIdAsync();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user?.Role != RoleType.Admin)
            {
                return Unauthorized();
            }

            var contactUsList = await _context.ContactUs
                .Include(cu => cu.GusetMessages)
                .Where(cu => cu.GusetMessages.Any())
                .Select(cu => new RecentContact
                {
                    UserId = cu.GuestId.ToString(),
                    Username = $"ضيف_{cu.GuestId.ToString().Substring(0, 8)}",
                    LastMessage = cu.GusetMessages.OrderByDescending(m => m.Timestamp).FirstOrDefault().Content ?? "لا توجد رسائل بعد",
                    LastMessageTime = cu.GusetMessages.OrderByDescending(m => m.Timestamp).FirstOrDefault().Timestamp 
                })
                .OrderByDescending(c => c.LastMessageTime)
                .ToListAsync();

            return View("AdminCustomerSupportChats", contactUsList);
        }

        [HttpGet]
        public async Task<IActionResult> AdminCustomerSupportChat(string guestId)
        {
            var userId = await GetUserIdAsync();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user?.Role != RoleType.Admin)
            {
                return Unauthorized();
            }

            var adminsIds = await _context.Users
                .Where(u => u.Role == RoleType.Admin)
                .Select(u => u.Id)
                .ToListAsync();

            var messages = await _context.GusetMessages
                .Where(m => (adminsIds.Any(id => m.SenderId == id) && m.ReceiverId == guestId) ||
                            (m.SenderId == guestId && adminsIds.Any(id => m.ReceiverId == id)))
                
                .OrderBy(m => m.Timestamp)
                .ThenBy(m => m.Id)
                .ToListAsync();

            ViewBag.ReceiverId = guestId;
            ViewBag.ReceiverUsername = $"ضيف_{guestId.Substring(0, 8)}";
            ViewBag.SenderId = userId;
            ViewBag.SenderUsername = user?.UserName ?? "الإدارة";
            ViewBag.IsGuestChat = true;

            return View("ContactUsChat", messages);
        }

        [HttpPost]
        public async Task<IActionResult> SendCustomerSupportMessage(string receiverId, string content)
        {
            if (string.IsNullOrEmpty(content))
                return Json(new { success = false, message = "الرسالة لا يمكن أن تكون فارغة" });

            var userId = await GetUserIdAsync();
            var isGuest = await IsGuestAsync();
            string targetReceiverId;

            if (isGuest)
            {
                var guestId = await GetGuestIdAsync();
                var adminsIds = await _context.Users
                    .Where(u => u.Role == RoleType.Admin)
                    .Select(u => u.Id)
                    .ToListAsync();
                var connectedAdmins = await _context.UserConnections
                    .Where(uc => uc.IsConnected && adminsIds.Contains(uc.UserId))
                    .Select(uc => uc.UserId)
                    .ToListAsync();
                targetReceiverId = connectedAdmins.FirstOrDefault() ?? adminsIds.FirstOrDefault();
                if (targetReceiverId == null)
                    return Json(new { success = false, message = "لا يوجد مدراء متاحون" });

                var contactUs = await _context.ContactUs.FirstOrDefaultAsync(cu => cu.GuestId == guestId.ToString());
                if (contactUs == null)
                {
                    contactUs = new ContactUs
                    {
                        GuestId = guestId.ToString(),
                        GusetMessages = new List<GusetMessage>()
                    };
                    _context.ContactUs.Add(contactUs);
                }

                var message = new GusetMessage
                {
                    SenderId = guestId.ToString(),
                    ReceiverId = targetReceiverId,
                    Content = content,
                    Timestamp = DateTime.Now
                };
                contactUs.GusetMessages.Add(message);
                _context.GusetMessages.Add(message);
                await _context.SaveChangesAsync();

                var senderName = $"ضيف_ {guestId.ToString().Substring(0, 4)}";
                await _hubContext.Clients.User(targetReceiverId).SendAsync("ReceivePrivateMessage", senderName, content, DateTime.Now);

                // إشعار الأدمن بالرسالة الجديدة
                await SendAdminMessageNotificationAsync(targetReceiverId, senderName, content);

                return Json(new { success = true });
            }
            else
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
                if (user?.Role != RoleType.Admin)
                    return Json(new { success = false, message = "غير مصرح" });

                targetReceiverId = receiverId;
                var contactUs = await _context.ContactUs.FirstOrDefaultAsync(cu => cu.GuestId == receiverId);
                if (contactUs == null)
                {
                    contactUs = new ContactUs
                    {
                        GuestId = receiverId,
                        GusetMessages = new List<GusetMessage>()
                    };
                    _context.ContactUs.Add(contactUs);
                }

                var message = new GusetMessage
                {
                    SenderId = userId,
                    ReceiverId = receiverId,
                    Content = content,
                    Timestamp = DateTime.Now
                };
                contactUs.GusetMessages.Add(message);
                _context.GusetMessages.Add(message);
                await _context.SaveChangesAsync();

                var senderName = user.UserName;
                var gustconn = await _context.UserConnections.Where(uc=>uc.UserId == receiverId).ToListAsync();
                foreach (var conn in gustconn)
                {
                    await _hubContext.Clients.Client(conn.ConnectionId).SendAsync("ReceivePrivateMessage", senderName, content, DateTime.Now);
                }
               
                //await _hubContext.Clients.User(userId).SendAsync("ReceivePrivateMessage", senderName, content, DateTime.Now);
                ViewBag.senderUsername = "دعم الإدارة";
                return Json(new { success = true });
            }
        }

        private async Task<string> GetGuestIdAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                string guestId = Request.Cookies["guestId"];
                if (string.IsNullOrEmpty(guestId) || !Guid.TryParse(guestId, out var guid))
                {
                    guid = Guid.NewGuid();
                    Response.Cookies.Append("guestId", guid.ToString(), new CookieOptions
                    {
                        Expires = DateTime.UtcNow.AddYears(2),
                        HttpOnly = true,
                        Secure = true
                    });
                }
                return guid.ToString();
            }
            return user.Id;
           
        }

        private async Task<bool> IsGuestAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            return user == null || false;
        }

        private async Task SendAdminMessageNotificationAsync(string adminUserId, string senderName, string content)
        {
            var preview = content.Length > 50 ? content.Substring(0, 50) + "..." : content;
            var notification = new UserNotification
            {
                NotificationId = Guid.NewGuid(),
                UserId = adminUserId,
                Title = "رسالة جديدة",
                Message = $"رسالة من {senderName}: {preview}",
                Type = NotificationType.MessageReceived,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            var connections = await _context.UserConnections
                .Where(c => c.UserId == adminUserId && c.IsConnected)
                .Select(c => c.ConnectionId)
                .ToListAsync();

            foreach (var connId in connections)
            {
                await _hubContext.Clients.Client(connId).SendAsync(
                    "ReceiveSystemNotification",
                    "رسالة جديدة",
                    $"رسالة من {senderName}: {preview}");
            }
        }
    }
}










