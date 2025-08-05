using Messaging_Chat_Application_MahmoudHakim.Hubs;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TrainingCenterManagement_MVC.Data;
using TrainingCenterManagement_MVC.Models;
using TrainingCenterManagement_MVC.ViewModels;

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
                    LastMessageTime = g.OrderByDescending(m => m.Timestamp).First().Timestamp
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
                    LastMessageTime = g.OrderByDescending(m => m.Timestamp).First().Timestamp
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
            return user?.Id ?? "";
        }
    }
}