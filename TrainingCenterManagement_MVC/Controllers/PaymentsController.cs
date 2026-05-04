using Messaging_Chat_Application_MahmoudHakim.Hubs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Rotativa.AspNetCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TrainingCenterManagement_MVC.Data;
using TrainingCenterManagement_MVC.Models;

namespace TrainingCenterManagement_MVC.Controllers
{
    public class PaymentsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<ChatHub> _hubContext;

        public PaymentsController(ApplicationDbContext context, IHubContext<ChatHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        // GET: Payments
        public async Task<IActionResult> Index()
        {
            var payments = await _context.Payments
                .Include(p => p.Course)
                .Include(p => p.Trainee)
                    .ThenInclude(t => t.User)
                .ToListAsync();

            var groupedPayments = payments
                .GroupBy(p => new { p.CreatedDate.Year, p.CreatedDate.Month })
                .OrderByDescending(g => g.Key.Year)
                .ThenByDescending(g => g.Key.Month)
                .ToList();

            return View(groupedPayments);
        }

        // GET: Payments/Details/5
        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var payment = await _context.Payments
                .Include(p => p.Course)
                .Include(p => p.Trainee)
                .FirstOrDefaultAsync(m => m.PaymentId == id);
            if (payment == null)
            {
                return NotFound();
            }

            return View(payment);
        }

        // GET: Payments/Create
        public IActionResult Create()
        {
            ViewData["CourseId"] = new SelectList(_context.Courses, "CourseId", "CourseName");

            ViewData["TraineeId"] = new SelectList(
                _context.Trainees.Include(t => t.User).ToList(),
                "TraineeId",
                "User.FullName"
            );

            return View();
        }

        // POST: Payments/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Payment payment)
        {
            var course = await _context.Courses.FindAsync(payment.CourseId);
            var trianeePayments = await _context.Payments
                .Where(p => p.TraineeId == payment.TraineeId && p.CourseId == payment.CourseId)
                .ToListAsync();

            decimal totalAmount = trianeePayments.Count > 0 ? trianeePayments.Sum(p => p.TotalAmount) : 0;

            if (totalAmount == course.Price)
            {
                TempData["ErrorMessage"] = "The total amount to Course is Complete";
                return View(payment);
            }

            var modifiedAmount = course.Price - totalAmount;

            if (modifiedAmount < payment.TotalAmount)
            {
                TempData["ErrorMessage"] = "The modifiedAmount less than Payment.";
                return View(payment);
            }

            if (payment.TotalAmount + totalAmount <= course.Price)
            {
                try
                {
                    _context.Add(payment);
                    await _context.SaveChangesAsync();

                    await SendPaymentNotificationsAsync(payment, course);

                    TempData["SuccessMessage"] = "Payment created successfully.";
                    return RedirectToAction(nameof(Index));
                }
                catch
                {
                    TempData["ErrorMessage"] = "An error occurred while creating the payment.";
                    return View(payment);
                }
            }
            else
            {
                TempData["ErrorMessage"] = "The total amount Big than modified";
                return View(payment);
            }
        }

        private async Task SendPaymentNotificationsAsync(Payment payment, Course course)
        {
            var trainee = await _context.Trainees
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.TraineeId == payment.TraineeId);

            var traineeName = trainee?.User?.FullName ?? "متدرب";
            var notificationMessage = $"تم استلام دفعة بقيمة {payment.TotalAmount} ريال من {traineeName} لدورة {course.CourseName}";

            var adminUsers = await _context.Users
                .Where(u => u.Role == RoleType.Admin)
                .ToListAsync();

            var notifications = adminUsers.Select(admin => new UserNotification
            {
                NotificationId = Guid.NewGuid(),
                UserId = admin.Id,
                Title = "دفعة جديدة",
                Message = notificationMessage,
                Type = NotificationType.PaymentReceived,
                IsRead = false,
                CreatedAt = DateTime.UtcNow,
                RelatedId = payment.PaymentId.ToString()
            }).ToList();

            if (notifications.Any())
            {
                _context.Notifications.AddRange(notifications);
                await _context.SaveChangesAsync();

                foreach (var admin in adminUsers)
                {
                    var connections = await _context.UserConnections
                        .Where(c => c.UserId == admin.Id && c.IsConnected)
                        .Select(c => c.ConnectionId)
                        .ToListAsync();

                    foreach (var connId in connections)
                    {
                        await _hubContext.Clients.Client(connId).SendAsync(
                            "ReceiveSystemNotification",
                            "دفعة جديدة",
                            notificationMessage);
                    }
                }
            }
        }

        // GET: Payments/Delete/5
        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var payment = await _context.Payments
                .Include(p => p.Course)
                .Include(p => p.Trainee)
                .FirstOrDefaultAsync(m => m.PaymentId == id);
            if (payment == null)
            {
                return NotFound();
            }

            return View(payment);
        }

        // POST: Payments/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var payment = await _context.Payments.FindAsync(id);
            if (payment != null)
            {
                _context.Payments.Remove(payment);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool PaymentExists(Guid id)
        {
            return _context.Payments.Any(e => e.PaymentId == id);
        }

        public async Task<IActionResult> ViewCoursePayments(Guid courseId)
        {
            var course = await _context.Courses.FindAsync(courseId);
            if (course == null) return NotFound();

            ViewBag.CourseName = course.CourseName;
            ViewBag.CourseId = courseId;

            var payments = await _context.Payments
                .Include(p => p.Trainee).ThenInclude(t => t.User)
                .Where(p => p.CourseId == courseId)
                .ToListAsync();

            return View("CoursePayments", payments);
        }

        public async Task<IActionResult> ExportPaymentsPdf(Guid courseId)
        {
            var course = await _context.Courses.FirstOrDefaultAsync(c => c.CourseId == courseId);
            if (course == null) return NotFound();

            var payments = await _context.Payments
                .Where(p => p.CourseId == courseId)
                .Include(p => p.Trainee).ThenInclude(t => t.User)
                .ToListAsync();

            ViewBag.CourseName = course.CourseName;
            ViewBag.IsPdf = true;
            return new ViewAsPdf("ExportPaymentsPdf", payments)
            {
                PageSize = Rotativa.AspNetCore.Options.Size.A4,
                PageOrientation = Rotativa.AspNetCore.Options.Orientation.Portrait
            };
        }
    }
}
