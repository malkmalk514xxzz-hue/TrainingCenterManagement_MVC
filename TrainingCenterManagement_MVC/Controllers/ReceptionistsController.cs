using DocumentFormat.OpenXml.InkML;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using NuGet.Protocol.Plugins;
using Rotativa.AspNetCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TrainingCenterManagement_MVC.Data;
using TrainingCenterManagement_MVC.Models;
using TrainingCenterManagement_MVC.ViewModels;


namespace TrainingCenterManagement_MVC.Controllers
{
    public class ReceptionistsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ReceptionistsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Index()
        {
            var receptionists = await _context.Receptionists
                .Include(r => r.User)
                .ToListAsync();
            return View(receptionists);
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var receptionist = await _context.Receptionists
                .Include(r => r.User)
                .FirstOrDefaultAsync(m => m.ReceptionistId == id);
            if (receptionist == null)
            {
                return NotFound();
            }
            return View(receptionist);
        }

        [Authorize(Roles = "Admin")]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create(ReceptionistCreateViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = new ApplicationUser
                {
                    UserName = model.Email,
                    Email = model.Email,
                    FullName = model.FullName
                };
                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                var receptionist = new Receptionist
                {
                    ReceptionistId = Guid.NewGuid(),
                    UserId = user.Id
                };
                _context.Receptionists.Add(receptionist);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var receptionist = await _context.Receptionists
                .Include(r => r.User)
                .FirstOrDefaultAsync(m => m.ReceptionistId == id);
            if (receptionist == null)
            {
                return NotFound();
            }
            return View(receptionist);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var receptionist = await _context.Receptionists.FindAsync(id);
            if (receptionist != null)
            {
                _context.Receptionists.Remove(receptionist);
            }
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool ReceptionistExists(Guid id)
        {
            return _context.Receptionists.Any(e => e.ReceptionistId == id);
        }

        [Authorize(Roles = "Receptionist,Admin")]
        public async Task<IActionResult> RegisterTraineeToCourse()
        {
            ViewData["CourseId"] = new SelectList(_context.Courses, "CourseId", "CourseName");
            ViewData["TraineeId"] = new SelectList(_context.Trainees.Include(t => t.User), "TraineeId", "User.FullName");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Receptionist,Admin")]
        public async Task<IActionResult> RegisterTraineeToCourse(Guid courseId, Guid traineeId)
        {
            if (!_context.Courses.Any(c => c.CourseId == courseId) || !_context.Trainees.Any(t => t.TraineeId == traineeId))
            {
                TempData["ErrorMessage"] = "الدورة أو الطالب غير موجود.";
                return RedirectToAction(nameof(RegisterTraineeToCourse));
            }

            var existingRegistration = await _context.CourseTrainees
                .AnyAsync(ct => ct.CourseId == courseId && ct.TraineeId == traineeId);
            if (existingRegistration)
            {
                TempData["ErrorMessage"] = "الطالب مسجل بالفعل في هذه الدورة.";
                return RedirectToAction(nameof(RegisterTraineeToCourse));
            }

            _context.CourseTrainees.Add(new CourseTrainee
            {
                CourseId = courseId,
                TraineeId = traineeId
            });
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم تسجيل الطالب في الدورة بنجاح.";
            return RedirectToAction("ReceptionistDashboard", "Dashboard");
        }

        [Authorize(Roles = "Receptionist,Admin")]
        public IActionResult UnregisterTraineeFromCourse()
        {
            ViewData["CourseId"] = new SelectList(_context.Courses, "CourseId", "CourseName");
            ViewData["TraineeId"] = new SelectList(_context.Trainees.Include(t => t.User), "TraineeId", "User.FullName");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Receptionist,Admin")]
        public async Task<IActionResult> UnregisterTraineeFromCourse(Guid courseId, Guid traineeId)
        {
            var registration = await _context.CourseTrainees
                .FirstOrDefaultAsync(ct => ct.CourseId == courseId && ct.TraineeId == traineeId);
            if (registration == null)
            {
                TempData["ErrorMessage"] = "الطالب غير مسجل في هذه الدورة.";
                return RedirectToAction(nameof(UnregisterTraineeFromCourse));
            }

            _context.CourseTrainees.Remove(registration);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم إلغاء تسجيل الطالب من الدورة بنجاح.";
            return RedirectToAction("ReceptionistDashboard", "Dashboard");
        }

        [Authorize(Roles = "Receptionist,Admin")]
        public async Task<IActionResult> RefundPayment(Guid paymentId)
        {
            var payment = await _context.Payments.FindAsync(paymentId);
            if (payment == null)
            {
                TempData["ErrorMessage"] = "الدفعة غير موجودة.";
                return RedirectToAction("PaymentReports");
            }

            _context.Payments.Remove(payment);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم استرداد الدفعة بنجاح.";
            return RedirectToAction("ReceptionistDashboard", "Dashboard");
        }

        [Authorize(Roles = "Receptionist,Admin")]
        public async Task<IActionResult> CourseDetails(Guid courseId)
        {
            var course = await _context.Courses
                .Include(c => c.CourseTrainees).ThenInclude(ct => ct.Trainee).ThenInclude(t => t.User)
                .Include(c => c.CourseTrainers).ThenInclude(ct => ct.Trainer).ThenInclude(t => t.User)
                .FirstOrDefaultAsync(c => c.CourseId == courseId);
            if (course == null)
            {
                return NotFound();
            }
            return View(course);
        }

        [Authorize(Roles = "Receptionist,Admin")]
        public async Task<IActionResult> AttendanceReports()
        {
            var reports = await _context.Presences
                .Include(p => p.Trainee).ThenInclude(t => t.User)
                .Include(p => p.Lecture).ThenInclude(l => l.Course)
                .ToListAsync();
            return View(reports);
        }

        [Authorize(Roles = "Receptionist,Admin")]
        public async Task<IActionResult> PaymentReports()
        {
            var payments = await _context.Payments
                .Include(p => p.Course)
                .Include(p => p.Trainee).ThenInclude(t => t.User)
                .ToListAsync();
            return View(payments);
        }

        [Authorize(Roles = "Receptionist,Admin")]
        public async Task<IActionResult> ContactTrainees()
        {
            var trainees = await _context.Trainees.Include(t => t.User).ToListAsync();
            return View(trainees);
        }

        [Authorize(Roles = "Receptionist")]
        public IActionResult AttendanceReportPdf()
        {
            var data = _context.Presences
                .Include(p => p.Lecture).ThenInclude(l => l.Course)
                .Include(p => p.Trainee).ThenInclude(t => t.User)
                .ToList();
            return new ViewAsPdf("AttendanceReportPdf", data)
            {
                FileName = "Attendance_Report.pdf"
            };
        }

        [Authorize(Roles = "Receptionist")]
        public IActionResult PaymentReportPdf()
        {
            var data = _context.Payments
                .Include(p => p.Course)
                .Include(p => p.Trainee).ThenInclude(t => t.User)
                .ToList();
            return new ViewAsPdf("PaymentReportPdf", data)
            {
                FileName = "Payment_Report.pdf"
            };
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Receptionist,Admin")]
        public async Task<IActionResult> SendMessageToTrainee(string email, string message)
        {
            TempData["SuccessMessage"] = $"تم إرسال الرسالة إلى {email}";
            var Receiver = await _context.Users.FirstOrDefaultAsync(u => u.UserName == email);
            var sender = await _context.Receptionists.FirstOrDefaultAsync();
            _context.Messages.Add(new TrainingCenterManagement_MVC.Models.Message()
            {
                Content = message,
                ReceiverId = Receiver.Id,
                SenderId = sender.UserId,
                Timestamp = DateTime.Now,


            });
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(ContactTrainees));
        }
    }
}