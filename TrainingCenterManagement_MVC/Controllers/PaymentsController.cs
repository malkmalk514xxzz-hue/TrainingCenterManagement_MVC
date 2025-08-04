using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Rotativa.AspNetCore;
using TrainingCenterManagement_MVC.Data;
using TrainingCenterManagement_MVC.Models;

namespace TrainingCenterManagement_MVC.Controllers
{
    public class PaymentsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PaymentsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Payments
        public async Task<IActionResult> Index()
        {
            var applicationDbContext = _context.Payments.Include(p => p.Course).Include(p => p.Trainee);
            return View(await applicationDbContext.ToListAsync());
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
            ViewData["TraineeId"] = new SelectList(_context.Trainees, "TraineeId", "UserId");
            return View();
        }

        // POST: Payments/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("PaymentId,TotalAmount,CreatedDate,IsDeleted,TraineeId,CourseId")] Payment payment)
        {
            if (ModelState.IsValid)
            {
                // احسب مجموع الدفعات السابقة لنفس الطالب ونفس الدورة
                var totalPaid = await _context.Payments
                    .Where(p => p.TraineeId == payment.TraineeId && p.CourseId == payment.CourseId)
                    .SumAsync(p => (decimal?)p.TotalAmount) ?? 0;

                var course = await _context.Courses.FindAsync(payment.CourseId);
                if (course == null)
                {
                    TempData["ErrorMessage"] = "الدورة غير موجودة.";
                    return RedirectToAction(nameof(Index));
                }

                if (totalPaid + payment.TotalAmount > (decimal) course.Price)
                {
                    TempData["ErrorMessage"] = "لا يمكن أن يتجاوز مجموع المدفوعات سعر الدورة.";
                    return RedirectToAction(nameof(Index));
                }

                payment.PaymentId = Guid.NewGuid();
                payment.CreatedDate = DateTime.Now;

                _context.Add(payment);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "تم الدفع بنجاح.";
                return RedirectToAction(nameof(Index));
            }

            ViewData["CourseId"] = new SelectList(_context.Courses, "CourseId", "CourseName", payment.CourseId);
            ViewData["TraineeId"] = new SelectList(_context.Trainees, "TraineeId", "UserId", payment.TraineeId);
            return View(payment);
        }


        // GET: Payments/Edit/5
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var payment = await _context.Payments.FindAsync(id);
            if (payment == null)
            {
                return NotFound();
            }
            ViewData["CourseId"] = new SelectList(_context.Courses, "CourseId", "CourseName", payment.CourseId);
            ViewData["TraineeId"] = new SelectList(_context.Trainees, "TraineeId", "UserId", payment.TraineeId);
            return View(payment);
        }

        // POST: Payments/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, [Bind("PaymentId,TotalAmount,CreatedDate,IsDeleted,TraineeId,CourseId")] Payment payment)
        {
            if (id != payment.PaymentId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(payment);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!PaymentExists(payment.PaymentId))
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
            ViewData["CourseId"] = new SelectList(_context.Courses, "CourseId", "CourseName", payment.CourseId);
            ViewData["TraineeId"] = new SelectList(_context.Trainees, "TraineeId", "UserId", payment.TraineeId);
            return View(payment);
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

            return new ViewAsPdf("ExportPaymentsPdf", payments)
            {
                PageSize = Rotativa.AspNetCore.Options.Size.A4,
                PageOrientation = Rotativa.AspNetCore.Options.Orientation.Portrait
            };
        }
    }
}
