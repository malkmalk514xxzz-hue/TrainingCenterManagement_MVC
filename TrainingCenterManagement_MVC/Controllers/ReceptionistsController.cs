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
            var applicationDbContext = _context.Receptionists.Include(r => r.User);
            return View(await applicationDbContext.ToListAsync());
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
            ViewData["UserId"] = new SelectList(_context.Users, "Id", "Id");
            return View();
        }

        // POST: Receptionists/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]

        public async Task<IActionResult> Create([Bind("ReceptionistId,UserId")] Receptionist receptionist)
        {
            if (ModelState.IsValid)
            {
                receptionist.ReceptionistId = Guid.NewGuid();
                _context.Add(receptionist);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["UserId"] = new SelectList(_context.Users, "Id", "Id", receptionist.UserId);
            return View(receptionist);
        }

        // GET: Receptionists/Edit/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var receptionist = await _context.Receptionists.FindAsync(id);
            if (receptionist == null)
            {
                return NotFound();
            }
            ViewData["UserId"] = new SelectList(_context.Users, "Id", "Id", receptionist.UserId);
            return View(receptionist);
        }

        // POST: Receptionists/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(Guid id, [Bind("ReceptionistId,UserId")] Receptionist receptionist)
        {
            if (id != receptionist.ReceptionistId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(receptionist);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ReceptionistExists(receptionist.ReceptionistId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["UserId"] = new SelectList(_context.Users, "Id", "Id", receptionist.UserId);
            return View(receptionist);
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
