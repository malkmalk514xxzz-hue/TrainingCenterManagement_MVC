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

        // ── Index ─────────────────────────────────────────────────────────────

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Index()
        {
            var receptionists = await _context.Receptionists
                .Include(r => r.User)
                .ToListAsync();
            return View(receptionists);
        }

        // ── Details ───────────────────────────────────────────────────────────

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null) return NotFound();

            var receptionist = await _context.Receptionists
                .Include(r => r.User)
                .FirstOrDefaultAsync(m => m.ReceptionistId == id);

            if (receptionist == null) return NotFound();
            return View(receptionist);
        }

        // ── Create ────────────────────────────────────────────────────────────

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
                    UserId = user.Id,
                    ShamCashAccountCode = model.ShamCashAccountCode.Trim()
                };
                _context.Receptionists.Add(receptionist);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        // ── Delete ────────────────────────────────────────────────────────────

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null) return NotFound();

            var receptionist = await _context.Receptionists
                .Include(r => r.User)
                .FirstOrDefaultAsync(m => m.ReceptionistId == id);

            if (receptionist == null) return NotFound();
            return View(receptionist);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var receptionist = await _context.Receptionists.FindAsync(id);
            if (receptionist != null)
                _context.Receptionists.Remove(receptionist);

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool ReceptionistExists(Guid id)
            => _context.Receptionists.Any(e => e.ReceptionistId == id);

        // ── Register Trainee To Course ────────────────────────────────────────

        [Authorize(Roles = "Receptionist,Admin")]
        public async Task<IActionResult> RegisterTraineeToCourse()
        {
            ViewData["CourseId"] = new SelectList(_context.Courses, "CourseId", "CourseName");
            ViewData["TraineeId"] = new SelectList(
                _context.Trainees.Include(t => t.User), "TraineeId", "User.FullName");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Receptionist,Admin")]
        public async Task<IActionResult> RegisterTraineeToCourse(Guid courseId, Guid traineeId)
        {
            if (!_context.Courses.Any(c => c.CourseId == courseId) ||
                !_context.Trainees.Any(t => t.TraineeId == traineeId))
            {
                TempData["ErrorMessage"] = "الدورة أو الطالب غير موجود.";
                return RedirectToAction(nameof(RegisterTraineeToCourse));
            }

            var alreadyRegistered = await _context.CourseTrainees
                .AnyAsync(ct => ct.CourseId == courseId && ct.TraineeId == traineeId);

            if (alreadyRegistered)
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

        // ── Unregister Trainee From Course ────────────────────────────────────

        [Authorize(Roles = "Receptionist,Admin")]
        public IActionResult UnregisterTraineeFromCourse()
        {
            ViewData["CourseId"] = new SelectList(_context.Courses, "CourseId", "CourseName");
            ViewData["TraineeId"] = new SelectList(
                _context.Trainees.Include(t => t.User), "TraineeId", "User.FullName");
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

        // ── Refund Payment ────────────────────────────────────────────────────

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

        // ── Course Details ────────────────────────────────────────────────────

        [Authorize(Roles = "Receptionist,Admin")]
        public async Task<IActionResult> CourseDetails(Guid courseId)
        {
            var course = await _context.Courses
                .Include(c => c.CourseTrainees).ThenInclude(ct => ct.Trainee).ThenInclude(t => t.User)
                .Include(c => c.CourseTrainers).ThenInclude(ct => ct.Trainer).ThenInclude(t => t.User)
                .FirstOrDefaultAsync(c => c.CourseId == courseId);

            if (course == null) return NotFound();
            return View(course);
        }

        // ── Contact Trainees ──────────────────────────────────────────────────

        [Authorize(Roles = "Receptionist,Admin")]
        public async Task<IActionResult> ContactTrainees(
     int page = 1,
     int pageSize = 8,
     string search = "")
        {
            pageSize = Math.Clamp(pageSize, 5, 50);

            var query = _context.Trainees
                .Include(t => t.User)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(t =>
                    (t.User.FullName != null && t.User.FullName.ToLower().Contains(term)) ||
                    (t.User.Email != null && t.User.Email.ToLower().Contains(term)) ||
                    (t.User.PhoneNumber != null && t.User.PhoneNumber.ToLower().Contains(term)));
            }

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            page = Math.Max(1, Math.Min(page, Math.Max(1, totalPages)));

            var trainees = await query
                .OrderBy(t => t.User.FullName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalCount = totalCount;
            ViewBag.Search = search;
            ViewBag.PageSize = pageSize;

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return PartialView("_ContactTraineesTablePartial", trainees);

            return View(trainees);
        }


        // ── PDF Exports ───────────────────────────────────────────────────────

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

        // ── PaymentReports — Pagination + Ajax + Search + Sort ────────────────
        // MODIFIED: Added pagination, Ajax support, search, and sorting.
        // The view now returns a partial when called via Ajax (X-Requested-With header).

        [Authorize(Roles = "Receptionist,Admin")]
        public async Task<IActionResult> PaymentReports(
            int page = 1,
            int pageSize = 10,
            string search = "",
            string sortBy = "date",
            string sortDir = "desc")
        {
            pageSize = Math.Clamp(pageSize, 5, 50);

            var query = _context.Payments
                .Include(p => p.Course)
                .Include(p => p.Trainee).ThenInclude(t => t.User)
                .AsQueryable();

            // Search by trainee name or course name
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(p =>
                    (p.Trainee.User.FullName != null && p.Trainee.User.FullName.ToLower().Contains(term)) ||
                    (p.Course.CourseName != null && p.Course.CourseName.ToLower().Contains(term)));
            }

            var totalCount = await query.CountAsync();
            var totalAmount = await query.SumAsync(p => (decimal?)p.TotalAmount) ?? 0m;
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            page = Math.Max(1, Math.Min(page, Math.Max(1, totalPages)));

            // Sort
            query = (sortBy, sortDir) switch
            {
                ("name", "asc") => query.OrderBy(p => p.Trainee.User.FullName),
                ("name", "desc") => query.OrderByDescending(p => p.Trainee.User.FullName),
                ("amount", "asc") => query.OrderBy(p => p.TotalAmount),
                ("amount", "desc") => query.OrderByDescending(p => p.TotalAmount),
                ("date", "asc") => query.OrderBy(p => p.CreatedDate),
                _ => query.OrderByDescending(p => p.CreatedDate)
            };

            var payments = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalCount = totalCount;
            ViewBag.TotalAmount = totalAmount;
            ViewBag.Search = search;
            ViewBag.SortBy = sortBy;
            ViewBag.SortDir = sortDir;
            ViewBag.PageSize = pageSize;

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return PartialView("_PaymentReportsTablePartial", payments);

            return View(payments);
        }

        // ── AttendanceReports — Pagination + Ajax + Search + Sort + Filter ─────
        // MODIFIED: Added pagination, Ajax support, search, sorting, and status filter.

        [Authorize(Roles = "Receptionist,Admin")]
        public async Task<IActionResult> AttendanceReports(
            int page = 1,
            int pageSize = 10,
            string search = "",
            string sortBy = "date",
            string sortDir = "desc",
            string status = "all")
        {
            pageSize = Math.Clamp(pageSize, 5, 50);

            var query = _context.Presences
                .Include(p => p.Trainee).ThenInclude(t => t.User)
                .Include(p => p.Lecture).ThenInclude(l => l.Course)
                .AsQueryable();

            // Filter by attendance status
            if (status == "present") query = query.Where(p => p.IsPresent);
            if (status == "absent") query = query.Where(p => !p.IsPresent);

            // Search
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(p =>
                    (p.Trainee.User.FullName != null && p.Trainee.User.FullName.ToLower().Contains(term)) ||
                    (p.Lecture.Title != null && p.Lecture.Title.ToLower().Contains(term)) ||
                    (p.Lecture.Course.CourseName != null && p.Lecture.Course.CourseName.ToLower().Contains(term)));
            }

            var totalCount = await query.CountAsync();
            var totalPresent = await query.CountAsync(p => p.IsPresent);
            var totalAbsent = totalCount - totalPresent;
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            page = Math.Max(1, Math.Min(page, Math.Max(1, totalPages)));

            // Sort
            query = (sortBy, sortDir) switch
            {
                ("name", "asc") => query.OrderBy(p => p.Trainee.User.FullName),
                ("name", "desc") => query.OrderByDescending(p => p.Trainee.User.FullName),
                ("lecture", "asc") => query.OrderBy(p => p.Lecture.Title),
                ("lecture", "desc") => query.OrderByDescending(p => p.Lecture.Title),
                _ => query.OrderByDescending(p => p.Lecture.LectureDate)
            };

            var presences = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalCount = totalCount;
            ViewBag.TotalPresent = totalPresent;
            ViewBag.TotalAbsent = totalAbsent;
            ViewBag.Search = search;
            ViewBag.SortBy = sortBy;
            ViewBag.SortDir = sortDir;
            ViewBag.PageSize = pageSize;
            ViewBag.Status = status;

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return PartialView("_AttendanceReportsTablePartial", presences);

            return View(presences);
        }
    }
}
