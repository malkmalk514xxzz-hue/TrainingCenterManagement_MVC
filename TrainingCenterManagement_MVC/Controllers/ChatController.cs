using Messaging_Chat_Application_MahmoudHakim.Hubs;
using Microsoft.AspNetCore.Authorization;
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

        public ChatController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IHubContext<ChatHub> hubContext)
        {
            _context = context;
            _userManager = userManager;
            _hubContext = hubContext;
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
            var messages = await _context.GroupMessages
                .Where(m => m.CourseId == courseId)
                .Include(m => m.Sender)
                .OrderBy(m => m.Timestamp)
                .ToListAsync();
            ViewBag.CourseId = courseId;
            ViewBag.user   = await _context.Users.FindAsync(await GetUserIdAsync());
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
                .ToListAsync();
            ViewBag.ReceiverId = receiverId;
            var receiver = await _context.Users.FindAsync(receiverId);
            ViewBag.ReceiverUsername = receiver?.UserName;
            return View(messages);
        }

        [HttpPost]
        public async Task<IActionResult> SendGroupMessage(Guid courseId, string content)
        {
            try
            {
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

                var groupMessage = new GroupMessage
                {
                    CourseId = courseId,
                    SenderId = userId,
                    Content = content,
                    Timestamp = DateTime.Now
                };
                await _context.GroupMessages.AddAsync(groupMessage);
                await _context.SaveChangesAsync();
               // await _hubContext.Clients.Group($"Course_{courseId}").SendAsync("ReceiveGroupMessage", user.UserName, content, DateTime.Now);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                // تسجيل الخطأ لتتبعه
                Console.WriteLine($"Error in SendGroupMessage: {ex.Message }");
                return Json(new { success = false, message = $"Failed to send message: {ex.Message}" });
            }
        }


        [HttpPost]
        public async Task<IActionResult> SendPrivateMessage(string receiverId, string content)
        {
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

            return Json(new { success = true });
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
                //await _hubContext.Clients.User(guestId.ToString()).SendAsync("ReceivePrivateMessage", senderName, content, DateTime.Now);

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


    }
}