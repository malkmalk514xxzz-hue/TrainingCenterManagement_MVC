using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
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

        // GET: Receptionists
        [Authorize(Roles = "Admin")]

        public async Task<IActionResult> Index()
        {
            var receptionists = await _context.Receptionists
                .Include(r => r.User) // لجلب بيانات المستخدم المرتبط
                .ToListAsync();

            return View(receptionists);
        }


        // GET: Receptionists/Details/5
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

        // GET: Receptionists/Create
        [Authorize(Roles = "Admin")]

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ReceptionistCreateViewModel model)
        {
            
                // إنشاء مستخدم جديد
                var user = new ApplicationUser
                {
                    UserName = model.Email,
                    Email = model.Email,
                    FullName = model.FullName
                };

                // حفظ المستخدم في قاعدة البيانات
                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                // إنشاء Receptionist وربطه بالمستخدم
                var receptionist = new Receptionist
                {
                    ReceptionistId = model.ReceptionistId,
                    UserId = user.Id
                };

                _context.Receptionists.Add(receptionist);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            

        }



        // GET: Receptionists/Delete/5
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

        // POST: Receptionists/Delete/5
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
            // يمكنك هنا استخدام خدمة إرسال بريد إلكتروني أو حفظ الرسالة في جدول
            TempData["SuccessMessage"] = $"Message sent to {email}";
            return RedirectToAction(nameof(ContactTrainees));
        }

    }
}
