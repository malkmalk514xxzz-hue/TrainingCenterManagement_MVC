using Messaging_Chat_Application_MahmoudHakim.Hubs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TrainingCenterManagement_MVC.Data;
using TrainingCenterManagement_MVC.Models;
using TrainingCenterManagement_MVC.ViewModels;

namespace TrainingCenterManagement_MVC.Controllers.Api
{
   // [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ApiController]
    [Route("api/[controller]")]
    public class ChatApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHubContext<ChatHub> _hubContext;

        public ChatApiController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IHubContext<ChatHub> hubContext)
        {
            _context = context;
            _userManager = userManager;
            _hubContext = hubContext;
        }

        [HttpGet("GetChatIndex/{userId:guid}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<ActionResult<ChatIndexViewModel>> GetChatIndex(string userId)
        {
           //var userId = await GetUserIdAsync();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null) return Unauthorized();

            // جلب الكورسات حسب الدور
            List<Course> myCourses = new List<Course>();

            if (user.Role == RoleType.Trainee)
            {
                var trainee = await _context.Trainees.FirstOrDefaultAsync(t => t.UserId == userId);
                if (trainee != null)
                {
                    myCourses = await _context.Courses
                        .Include(c => c.CourseTrainees)
                        .Where(c => c.CourseTrainees.Any(ct => ct.TraineeId == trainee.TraineeId))
                        .ToListAsync();
                }
            }
            else if (user.Role == RoleType.Trainer)
            {
                var trainer = await _context.Trainers.FirstOrDefaultAsync(t => t.UserId == userId);
                if (trainer != null)
                {
                    myCourses = await _context.Courses
                        .Include(c => c.CourseTrainers)
                        .Where(c => c.CourseTrainers.Any(ct => ct.TrainerId == trainer.TrainerId))
                        .ToListAsync();
                }
            }
            else if (user.Role == RoleType.Admin)
            {
                myCourses = await _context.Courses.ToListAsync();
            }

            // المحادثات الخاصة
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
                    LastMessageTime = g.OrderByDescending(m => m.Timestamp).First().Timestamp,
                    IsGroup = false
                })
                .ToListAsync();

            // المحادثات الجماعية
            var groupContacts = await _context.GroupMessages
                .Where(gm => myCourses.Select(c => c.CourseId).Contains(gm.CourseId))
                .Include(gm => gm.Course)
                .GroupBy(gm => gm.CourseId)
                .Select(g => new RecentContact
                {
                    UserId = g.Key.ToString(),
                    Username = g.First().Course.CourseName,
                    LastMessage = g.OrderByDescending(m => m.Timestamp).First().Content,
                    LastMessageTime = g.OrderByDescending(m => m.Timestamp).First().Timestamp,
                    IsGroup = true
                })
                .ToListAsync();

            var allContacts = privateContacts.Concat(groupContacts)
                .OrderByDescending(c => c.LastMessageTime)
                .ToList();

            var model = new ChatIndexViewModel
            {
                Courses = myCourses,
                RecentContacts = allContacts,
                AllUsers = await _context.Users.Where(u => u.Id != userId).ToListAsync()
            };

            return Ok(model);
        }

        [HttpGet("GroupChat/{courseId:guid}")]
        public async Task<ActionResult<IEnumerable<GroupMessage>>> GetGroupChat(Guid courseId)
        {
            var messages = await _context.GroupMessages
                .Where(m => m.CourseId == courseId)
                .Include(m => m.Sender)
                .OrderBy(m => m.Timestamp)
                .ToListAsync();

            return Ok(messages);
        }

        [HttpGet("PrivateChat/{receiverId}/{userId:guid}")]
        public async Task<ActionResult<IEnumerable<Message>>> GetPrivateChat(string receiverId,string userId)
        {
            //var userId = await GetUserIdAsync();

            var messages = await _context.Messages
                .Where(m => (m.SenderId == userId && m.ReceiverId == receiverId) ||
                            (m.SenderId == receiverId && m.ReceiverId == userId)
                            && !m.IsRead)
                .Include(m => m.Sender)
                .Include(m => m.Receiver)
                .OrderBy(m => m.Timestamp)
                .ToListAsync();

            return Ok(messages);
        }

        [HttpPost("SendGroupMessage/{userId:guid}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> SendGroupMessage(string userId,[FromBody] SendGroupMessageDto dto)
        {
            if (string.IsNullOrEmpty(dto.Content))
                return BadRequest(new { success = false, message = "Message cannot be empty" });

            //var userId = await GetUserIdAsync();
            var user = await _context.Users.FindAsync(userId);
            var course = await _context.Courses.FindAsync(dto.CourseId);

            if (user == null || course == null)
                return BadRequest(new { success = false, message = "Invalid user or course" });

            var groupMessage = new GroupMessage
            {
                CourseId = dto.CourseId,
                SenderId = userId,
                Content = dto.Content,
                Timestamp = DateTime.Now
            };

            _context.GroupMessages.Add(groupMessage);
            await _context.SaveChangesAsync();

            // إرسال عبر SignalR (مفعل لاحقاً)
            // await _hubContext.Clients.Group($"Course_{dto.CourseId}").SendAsync("ReceiveGroupMessage", user.UserName, dto.Content, DateTime.Now);

            return Ok(new { success = true });
        }

        [HttpPost("SendPrivateMessage/{userId:guid}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> SendPrivateMessage(string userId,[FromBody] SendPrivateMessageDto dto)
        {
            if (string.IsNullOrEmpty(dto.Content))
                return BadRequest(new { success = false, message = "Message cannot be empty" });

            //var userId = await GetUserIdAsync();

            var privateMessage = new Message
            {
                SenderId = userId,
                ReceiverId = dto.ReceiverId,
                Content = dto.Content,
                Timestamp = DateTime.Now
            };

            _context.Messages.Add(privateMessage);
            await _context.SaveChangesAsync();

            return Ok(new { success = true });
        }

       
    }

    // DTOs
    public class SendGroupMessageDto
    {
        public Guid CourseId { get; set; }
        public string Content { get; set; } = string.Empty;
    }

    public class SendPrivateMessageDto
    {
        public string ReceiverId { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }
}