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
            var payments = await _context.Payments
                .Include(p => p.Course)
                .Include(p => p.Trainee)
                    .ThenInclude(t => t.User)
                .ToListAsync();

            // تجميع الدفعات حسب السنة والشهر
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

            // استخدام FullName بدل UserId
            ViewData["TraineeId"] = new SelectList(
                _context.Trainees.Include(t => t.User).ToList(),
                "TraineeId",
                "User.FullName" // عرض FullName في القائمة
            );

            return View();
        }


        // POST: Payments/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Payment payment)
        {
            var course = await _context.Courses.FindAsync(payment.CourseId);
            var trianeePayments = await _context.Payments
                .Where(p => p.TraineeId == payment.TraineeId && p.CourseId == payment.CourseId)
                .ToListAsync();
         
            
             decimal totalAmount = trianeePayments.Count > 0? trianeePayments.Sum(p => p.TotalAmount): 0;
            // if AmountCourse bigger than TotalAmount
            if (totalAmount == course.Price)
            {
             
                TempData["ErrorMessage"] = "The total amount to Course is Complete";
                return View(payment);

            }
            var modifiedAmount = course.Price - totalAmount;
            // if Payment Amount bigger than Course Price
            if (modifiedAmount < payment.TotalAmount)
            {
               
                TempData["ErrorMessage"] = "The modifiedAmount less than Payment.";
                return View(payment);
            }
            if(payment.TotalAmount +totalAmount<=course.Price)
            {
                try
                {
                    _context.Add(payment);
                    await _context.SaveChangesAsync();
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
