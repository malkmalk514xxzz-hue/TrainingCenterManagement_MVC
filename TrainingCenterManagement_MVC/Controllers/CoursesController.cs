using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TrainingCenterManagement_MVC.Data;
using TrainingCenterManagement_MVC.Models;
using TrainingCenterManagement_MVC.ViewModels;
using ClosedXML.Excel;
using System.Security.Claims;

namespace TrainingCenterManagement_MVC.Controllers
{
    public class CoursesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CoursesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Courses
        public async Task<IActionResult> Index()
        {
            // FIX: Filter out soft-deleted courses
            var courses = await _context.Courses
                .Where(c => !c.IsDeleted)
                .Include(c => c.Admin)
                    .ThenInclude(a => a.User)
                .ToListAsync();

            return View(courses);
        }

        // GET: Courses/Details/5
        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null)
                return NotFound();

            var course = await _context.Courses
                .Include(c => c.Admin)
                .Include(c => c.CourseTrainers).ThenInclude(ct => ct.Trainer).ThenInclude(t => t.User)
                .Include(c => c.CourseTrainees).ThenInclude(ct => ct.Trainee).ThenInclude(t => t.User)
                .FirstOrDefaultAsync(m => m.CourseId == id && !m.IsDeleted);

            if (course == null)
                return NotFound();

            return View(course);
        }

        // GET: Courses/Create
        [Authorize(Roles = "Admin")]
        public IActionResult Create()
        {
            ViewData["AdminId"] = new SelectList(
                _context.Admins.Include(a => a.User),
                "AdminId",
                "User.FullName"
            );
            return View();
        }

        // POST: Courses/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([Bind("CourseId,CourseName,BatchNumber,NumberOfLectures,Price,Description,VideoUrl,ThumbnailUrl,CreatedDate,ReleaseDate,IsDeleted,AdminId")] Course course)
        {
            var courseExists = await _context.Courses
                .AnyAsync(c => c.CourseName == course.CourseName && c.BatchNumber == course.BatchNumber);

            if (courseExists)
            {
                ModelState.AddModelError("", "A course with the same name and batch number already exists.");
                ViewData["AdminId"] = new SelectList(_context.Admins.Include(a => a.User), "AdminId", "User.FullName", course.AdminId);
                return View(course);
            }

            course.CourseId = Guid.NewGuid();
            course.CreatedDate = DateTime.UtcNow;
            _context.Add(course);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // GET: Courses/Edit/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null)
                return NotFound();

            var course = await _context.Courses.FindAsync(id);
            if (course == null)
                return NotFound();

            ViewData["AdminId"] = new SelectList(
                _context.Admins.Include(a => a.User),
                "AdminId",
                "User.FullName",
                course.AdminId
            );
            return View(course);
        }

        // POST: Courses/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(Guid id, [Bind("CourseId,CourseName,BatchNumber,NumberOfLectures,Price,Description,VideoUrl,ThumbnailUrl,CreatedDate,ReleaseDate,IsDeleted,AdminId")] Course course)
        {
            if (id != course.CourseId)
                return NotFound();

            try
            {
                _context.Update(course);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!CourseExists(course.CourseId))
                    return NotFound();
                else
                    throw;
            }

            ViewData["AdminId"] = new SelectList(_context.Admins.Include(a => a.User), "AdminId", "User.FullName", course.AdminId);
            return RedirectToAction(nameof(Index));
        }

        // GET: Courses/Delete/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null)
                return NotFound();

            var course = await _context.Courses
                .Include(c => c.Admin)
                    .ThenInclude(a => a.User)
                .FirstOrDefaultAsync(m => m.CourseId == id);

            if (course == null)
                return NotFound();

            return View(course);
        }

        // POST: Courses/Delete/5 — Soft Delete
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var course = await _context.Courses.FindAsync(id);
            if (course != null)
            {
                // Soft delete instead of hard delete to preserve related data
                course.IsDeleted = true;
                _context.Courses.Update(course);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // GET: Courses/AssignTrainer/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AssignTrainer(Guid? id)
        {
            if (id == null)
                return NotFound();

            var course = await _context.Courses.FindAsync(id);
            if (course == null)
                return NotFound();

            ViewData["TrainerId"] = new SelectList(
                _context.Trainers.Include(t => t.User),
                "TrainerId",
                "User.FullName"
            );
            ViewBag.CourseId = id;
            return View(course);
        }

        // POST: Assign Trainer to Course
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AssignTrainer(Guid courseId, Guid trainerId)
        {
            bool alreadyAssigned = await _context.CourseTrainers
                .AnyAsync(ct => ct.CourseId == courseId && ct.TrainerId == trainerId);

            if (!alreadyAssigned)
            {
                var courseTrainer = new CourseTrainer
                {
                    CourseId = courseId,
                    TrainerId = trainerId
                };
                _context.CourseTrainers.Add(courseTrainer);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Details), new { id = courseId });
        }

        // GET: Courses/AssignTrainees/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AssignTrainees(Guid? id)
        {
            if (id == null)
                return NotFound();

            var course = await _context.Courses.FindAsync(id);
            if (course == null)
                return NotFound();

            ViewData["TraineeId"] = new SelectList(
                _context.Trainees.Include(t => t.User),
                "TraineeId",
                "User.FullName"
            );
            ViewBag.CourseId = id;
            return View(course);
        }

        // POST: Assign Trainee to Course (Admin)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AssignTrainees(Guid courseId, Guid traineeId)
        {
            bool alreadyAssigned = await _context.CourseTrainees
                .AnyAsync(ct => ct.CourseId == courseId && ct.TraineeId == traineeId);

            if (!alreadyAssigned)
            {
                var courseTrainee = new CourseTrainee
                {
                    CourseId = courseId,
                    TraineeId = traineeId
                };
                _context.CourseTrainees.Add(courseTrainee);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Details), new { id = courseId });
        }

        // POST: Trainee self-enrolls in a course
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Trainee")]
        public async Task<IActionResult> AssignTraineeToCourse(Guid courseId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            // FIX: null check before accessing TraineeId
            var trainee = await _context.Trainees.FirstOrDefaultAsync(t => t.UserId == userId);
            if (trainee == null)
                return Unauthorized();

            bool alreadyAssigned = await _context.CourseTrainees
                .AnyAsync(ct => ct.CourseId == courseId && ct.TraineeId == trainee.TraineeId);

            if (!alreadyAssigned)
            {
                // FIX: Was incorrectly adding to CourseTrainers — now correctly adds to CourseTrainees
                var courseTrainee = new CourseTrainee
                {
                    CourseId = courseId,
                    TraineeId = trainee.TraineeId
                };
                _context.CourseTrainees.Add(courseTrainee);
                await _context.SaveChangesAsync();
            }

            TempData["SuccessMessage"] = "تم التسجيل في الدورة بنجاح.";
            return RedirectToAction("TraineeDashboard", "Dashboard");
        }

        // GET: Enroll a trainee in a course (from course list)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Trainee")]
        public async Task<IActionResult> EnrollInCourse(Guid id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var trainee = await _context.Trainees.FirstOrDefaultAsync(t => t.UserId == userId);
            if (trainee == null)
                return Unauthorized();

            var course = await _context.Courses.FindAsync(id);
            if (course == null)
                return NotFound("الدورة غير موجودة.");

            var alreadyEnrolled = await _context.CourseTrainees
                .AnyAsync(ct => ct.CourseId == id && ct.TraineeId == trainee.TraineeId);

            if (alreadyEnrolled)
            {
                TempData["ErrorMessage"] = "أنت مسجل بالفعل في هذه الدورة.";
                return RedirectToAction("Index");
            }

            var courseTrainee = new CourseTrainee
            {
                CourseId = id,
                TraineeId = trainee.TraineeId,
            };

            _context.CourseTrainees.Add(courseTrainee);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم التسجيل في الدورة بنجاح.";
            return RedirectToAction("TraineeDashboard", "Dashboard");
        }

        // GET: Course Preview (public)
        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> Preview(Guid id)
        {
            var course = await _context.Courses
                .Include(c => c.Lectures)
                .FirstOrDefaultAsync(c => c.CourseId == id && !c.IsDeleted);

            if (course == null)
                return NotFound("الدورة غير موجودة.");

            return View("Preview", course);
        }

        // GET: Course Attendance Report
        [Authorize(Roles = "Admin,Trainer")]
        public async Task<IActionResult> CourseAttendance(Guid courseId, double? min, double? max)
        {
            var course = await _context.Courses
                .Include(c => c.Lectures.Where(l => !l.IsDeleted))
                    .ThenInclude(l => l.Presences)
                .Include(c => c.CourseTrainees)
                    .ThenInclude(ct => ct.Trainee)
                        .ThenInclude(t => t.User)
                .FirstOrDefaultAsync(c => c.CourseId == courseId && !c.IsDeleted);

            if (course == null)
                return NotFound();

            var totalLectures = course.Lectures.Count;

            var attendanceList = course.CourseTrainees.Select(ct =>
            {
                var attended = course.Lectures.Count(l =>
                    l.Presences.Any(p => p.TraineeId == ct.TraineeId && p.IsPresent));
                var percent = totalLectures > 0 ? (attended * 100.0 / totalLectures) : 0;

                return new TraineeAttendanceViewModel
                {
                    CourseName = course.CourseName,
                    TotalLectures = totalLectures,
                    AttendedLectures = attended,
                    FullName = ct.Trainee.User.FullName,
                    AttendancePercentage = Math.Round(percent, 2),
                    
                };
            }).AsQueryable();

            if (min.HasValue)
                attendanceList = attendanceList.Where(x => x.AttendancePercentage >= min.Value);
            if (max.HasValue)
                attendanceList = attendanceList.Where(x => x.AttendancePercentage <= max.Value);

            ViewBag.CourseId = course.CourseId;
            ViewBag.CourseName = course.CourseName;
            return View(attendanceList.ToList());
        }

        // GET: Export Attendance to Excel
        [Authorize(Roles = "Admin,Trainer")]
        public async Task<IActionResult> ExportExcel(Guid courseId)
        {
            var course = await _context.Courses
                .Include(c => c.Lectures.Where(l => !l.IsDeleted))
                    .ThenInclude(l => l.Presences)
                .Include(c => c.CourseTrainees)
                    .ThenInclude(ct => ct.Trainee)
                        .ThenInclude(t => t.User)
                .FirstOrDefaultAsync(c => c.CourseId == courseId);

            if (course == null)
                return NotFound();

            var totalLectures = course.Lectures.Count;
            var data = course.CourseTrainees.Select(ct =>
            {
                var attended = course.Lectures.Count(l =>
                    l.Presences.Any(p => p.TraineeId == ct.TraineeId && p.IsPresent));

                return new
                {
                    Name = ct.Trainee.User.FullName,
                    Attended = attended,
                    Total = totalLectures,
                    Percentage = totalLectures > 0 ? Math.Round(attended * 100.0 / totalLectures, 2) : 0
                };
            }).ToList();

            var stream = new MemoryStream();
            using (var workbook = new XLWorkbook())
            {
                var ws = workbook.Worksheets.Add("Attendance");
                ws.Cell(1, 1).Value = "Student";
                ws.Cell(1, 2).Value = "Attended";
                ws.Cell(1, 3).Value = "Total";
                ws.Cell(1, 4).Value = "Percentage";

                for (int i = 0; i < data.Count; i++)
                {
                    ws.Cell(i + 2, 1).Value = data[i].Name;
                    ws.Cell(i + 2, 2).Value = data[i].Attended;
                    ws.Cell(i + 2, 3).Value = data[i].Total;
                    ws.Cell(i + 2, 4).Value = data[i].Percentage;
                }
                workbook.SaveAs(stream);
            }

            stream.Position = 0;
            return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{course.CourseName}_Attendance.xlsx");
        }

        // GET: Export Attendance to PDF
        [Authorize(Roles = "Admin,Trainer")]
        public async Task<IActionResult> ExportPdf(Guid courseId)
        {
            var result = await CourseAttendance(courseId, null, null);
            var viewResult = result as ViewResult;

            if (viewResult == null)
                return NotFound($"Course with ID {courseId} was not found.");

            return new Rotativa.AspNetCore.ViewAsPdf("CourseAttendance", viewResult.Model)
            {
                FileName = "CourseAttendance.pdf",
                PageSize = Rotativa.AspNetCore.Options.Size.A4
            };
        }

        private bool CourseExists(Guid id)
        {
            return _context.Courses.Any(e => e.CourseId == id);
        }
    }
}
