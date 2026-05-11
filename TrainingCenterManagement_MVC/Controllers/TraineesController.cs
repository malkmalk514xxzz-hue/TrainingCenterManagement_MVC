using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Rotativa.AspNetCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using TrainingCenterManagement_MVC.Data;
using TrainingCenterManagement_MVC.Models;
using TrainingCenterManagement_MVC.ViewModels;

namespace TrainingCenterManagement_MVC.Controllers
{
    public class TraineesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public TraineesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Trainees
        [Authorize(Roles = "Admin")]

        public async Task<IActionResult> Index()
        {
            var applicationDbContext = _context.Trainees.Include(t => t.User);
            return View(await applicationDbContext.ToListAsync());
        }



        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var trainee = await _context.Trainees
                .Include(t => t.User)
                .FirstOrDefaultAsync(m => m.TraineeId == id);
            if (trainee == null)
            {
                return NotFound();
            }

            return View(trainee);
        }

        // GET: Trainees/Create
        [Authorize(Roles = "Admin")]

        // GET
        public IActionResult Create()
        {
            return View();
        }

        // POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(TraineeCreateViewModel model)
        {

            // إنشاء مستخدم جديد
            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FullName = model.FullName
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // إنشاء Trainee وربطه بالمستخدم
            var trainee = new Trainee
            {
                TraineeId = model.TraineeId,
                UserId = user.Id
            };

            _context.Trainees.Add(trainee);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));

        }




        // GET: Trainees/Delete/5
        [Authorize(Roles = "Admin")]

        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var trainee = await _context.Trainees
                .Include(t => t.User)
                .FirstOrDefaultAsync(m => m.TraineeId == id);
            if (trainee == null)
            {
                return NotFound();
            }

            return View(trainee);
        }

        // POST: Trainees/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]

        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var trainee = await _context.Trainees.FindAsync(id);
            if (trainee != null)
            {
                _context.Trainees.Remove(trainee);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool TraineeExists(Guid id)
        {
            return _context.Trainees.Any(e => e.TraineeId == id);
        }

        [Authorize(Roles = "Trainee")]
        public async Task<IActionResult> MyCertificates()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var trainee = await _context.Trainees.FirstOrDefaultAsync(t => t.UserId == userId);
            if (trainee == null) return NotFound();

            var certificates = await _context.Certificates
                .Where(c => c.TraineeId == trainee.TraineeId)
                .Include(c => c.Course)
                .Include(c => c.Trainer).ThenInclude(t => t.User)
                .ToListAsync();

            return View(certificates);
        }
        [Authorize(Roles = "Trainee")]
        public async Task<IActionResult> MyCourses()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var trainee = await _context.Trainees.FirstOrDefaultAsync(t => t.UserId == userId);
            if (trainee == null) return NotFound();

            var courses = await _context.CourseTrainees
                .Where(ct => ct.TraineeId == trainee.TraineeId)
                .Include(ct => ct.Course)
                .Select(ct => ct.Course)
                .ToListAsync();

            return View(courses);
        }

        [Authorize(Roles = "Trainee")]
        public async Task<IActionResult> Profile()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var trainee = await _context.Trainees
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.UserId == userId);

            if (trainee == null) return NotFound();
            return View(trainee);
        }

        [Authorize(Roles = "Trainee")]
        public async Task<IActionResult> ViewLectures(Guid courseId)
        {
            var lectures = await _context.Lectures
                .Include(l => l.Videos)
                .Include(l => l.Resources)
                .Where(l => l.CourseId == courseId && !l.IsDeleted)
                .OrderBy(l => l.LectureDate)
                .AsSplitQuery()
                .ToListAsync();

            // Deduplicate by primary key in case EF relationship fixup adds extras
            lectures = lectures.DistinctBy(l => l.LectureId).ToList();

            ViewBag.Course = await _context.Courses.FindAsync(courseId);
            return View(lectures);
        }
        [HttpGet("TrackAttendance")]
        [Authorize(Roles = "Trainee")]
        public async Task<IActionResult> TrackAttendance()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var trainee = await _context.Trainees.FirstOrDefaultAsync(t => t.UserId == userId);
            if (trainee == null) return NotFound();

            var attendanceData = await _context.CourseTrainees
                .Where(ct => ct.TraineeId == trainee.TraineeId)
                .Select(ct => new TraineeAttendanceViewModel
                {
                    CourseName = ct.Course.CourseName,
                    TotalLectures = ct.Course.NumberOfLectures,
                    AttendedLectures = ct.Course.Lectures
                        .Count(l => l.Presences.Any(p => p.TraineeId == trainee.TraineeId && p.IsPresent))
                })
                .ToListAsync();

            return View(attendanceData);
        }
        [HttpGet("TrackAttendance/{courseId}")]
        [Authorize(Roles = "Trainee")]
        public async Task<IActionResult> TrackAttendance(Guid courseId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var trainee = await _context.Trainees.FirstOrDefaultAsync(t => t.UserId == userId);
            if (trainee == null) return NotFound();

            var course = await _context.Courses
                .Include(c => c.Lectures)
                .ThenInclude(l => l.Presences)
                .FirstOrDefaultAsync(c => c.CourseId == courseId);

            if (course == null) return NotFound();

            var viewModel = new TraineeAttendanceViewModel
            {
                CourseName = course.CourseName,
                TotalLectures = course.NumberOfLectures,
                AttendedLectures = course.Lectures.Count(l => l.Presences.Any(p => p.TraineeId == trainee.TraineeId && p.IsPresent))
            };

            return View("TrackAttendanceSingle", viewModel); // View مخصصة لدورة واحدة
        }
        [Authorize(Roles = "Trainee")]
        public async Task<IActionResult> TakeExam(Guid courseId)
        {
            var exam = await _context.Exams
                .Where(e => e.CourseId == courseId)
                .OrderByDescending(e => e.ExamDate)
                .FirstOrDefaultAsync();

            if (exam == null)
                return NotFound("لا يوجد امتحان متاح لهذه الدورة.");

            // من هنا يمكن إعادة توجيه المتدرّب لصفحة الامتحان أو عرض محتوى
            return RedirectToAction("StartExam", "Exams", new { examId = exam.ExamId });
        }
        [Authorize(Roles = "Trainee")]
        public async Task<IActionResult> ViewPayments(Guid courseId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var trainee = await _context.Trainees.FirstOrDefaultAsync(t => t.UserId == userId);
            if (trainee == null) return NotFound();

            var payments = await _context.Payments
                .Where(p => p.TraineeId == trainee.TraineeId && p.CourseId == courseId)
                .ToListAsync();

            ViewBag.Course = await _context.Courses.FindAsync(courseId);
            return View(payments);
        }

   
    }
}
