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
            var applicationDbContext = _context.Courses.Include(c => c.Admin);
            return View(await applicationDbContext.ToListAsync());
        }

        // GET: Courses/Details/5
        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var course = await _context.Courses
                .Include(c => c.Admin)
                .Include(c => c.CourseTrainers).ThenInclude(ct => ct.Trainer).ThenInclude(t => t.User)
                .Include(c => c.CourseTrainees).ThenInclude(ct => ct.Trainee).ThenInclude(t => t.User)
                .FirstOrDefaultAsync(m => m.CourseId == id);

            if (course == null)
            {
                return NotFound();
            }

            return View(course);
        }


        // GET: Courses/Create
        public IActionResult Create()
        {
            ViewData["AdminId"] = new SelectList(_context.Admins, "AdminId", "UserId");
            return View();
        }

        // POST: Courses/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("CourseId,CourseName,BatchNumber,NumberOfLectures,Price,Description,VideoUrl,ThumbnailUrl,CreatedDate,ReleaseDate,IsDeleted,AdminId")] Course course)
        {
            if (ModelState.IsValid)
            {
                course.CourseId = Guid.NewGuid();
                _context.Add(course);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["AdminId"] = new SelectList(_context.Admins, "AdminId", "UserId", course.AdminId);
            return View(course);
        }

        // GET: Courses/Edit/5
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var course = await _context.Courses.FindAsync(id);
            if (course == null)
            {
                return NotFound();
            }
            ViewData["AdminId"] = new SelectList(_context.Admins, "AdminId", "UserId", course.AdminId);
            return View(course);
        }

        // POST: Courses/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, [Bind("CourseId,CourseName,BatchNumber,NumberOfLectures,Price,Description,VideoUrl,ThumbnailUrl,CreatedDate,ReleaseDate,IsDeleted,AdminId")] Course course)
        {
            if (id != course.CourseId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(course);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!CourseExists(course.CourseId))
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
            ViewData["AdminId"] = new SelectList(_context.Admins, "AdminId", "UserId", course.AdminId);
            return View(course);
        }

        // GET: Courses/Delete/5
        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var course = await _context.Courses
                .Include(c => c.Admin)
                .FirstOrDefaultAsync(m => m.CourseId == id);
            if (course == null)
            {
                return NotFound();
            }

            return View(course);
        }

        // POST: Courses/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var course = await _context.Courses.FindAsync(id);
            if (course != null)
            {
                _context.Courses.Remove(course);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool CourseExists(Guid id)
        {
            return _context.Courses.Any(e => e.CourseId == id);
        }
        // GET: Courses/AssignTrainer/5
        public async Task<IActionResult> AssignTrainer(Guid? id)
        {
            if (id == null) return NotFound();

            var course = await _context.Courses.FindAsync(id);
            if (course == null) return NotFound();

            var trainers = await _context.Trainers
                   .Include(t => t.User)
                   .ToListAsync();
            ViewBag.Trainers = new SelectList(_context.Trainers.Include(t => t.User), "TrainerId", "User.FullName");
            ViewBag.CourseId = course.CourseId;
            return View(course.CourseId);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignTrainer(Guid courseId, Guid trainerId)
        {
            // تحقق أولاً إن كان المعلم مُسند مسبقاً لنفس الكورس
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
        public async Task<IActionResult> AssignTrainees(Guid? id)
        {
            if (id == null) return NotFound();

            var course = await _context.Courses.FindAsync(id);
            if (course == null) return NotFound();

            var trainees = await _context.Trainees
                .Include(t => t.User)
                .ToListAsync();

            ViewBag.Trainees = new MultiSelectList(trainees, "TraineeId", "User.FullName");
            ViewBag.CourseId = course.CourseId;

            return View(course.CourseId);
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignTrainees(Guid courseId, List<Guid> traineeIds)
        {
            foreach (var traineeId in traineeIds)
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
                }
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { id = courseId });
        }


        [Authorize(Roles = "Trainer")]
        public async Task<IActionResult> CourseAttendance(Guid courseId, double? min, double? max)
        {
            var course = await _context.Courses
                .Include(c => c.Lectures)
                    .ThenInclude(l => l.Presences)
                .Include(c => c.CourseTrainees)
                    .ThenInclude(ct => ct.Trainee)
                        .ThenInclude(t => t.User)
                .FirstOrDefaultAsync(c => c.CourseId == courseId);

            if (course == null) return NotFound();

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
                    AttendancePercentage = percent
                };
            });

            if (min.HasValue)
                attendanceList = attendanceList.Where(x => x.AttendancePercentage >= min.Value);
            if (max.HasValue)
                attendanceList = attendanceList.Where(x => x.AttendancePercentage <= max.Value);

            ViewBag.CourseId = course.CourseId; // Pass the course ID to the view
            ViewBag.CourseName = course.CourseName;
            return View(attendanceList.ToList());
        }
        [Authorize(Roles = "Trainer")]
        public async Task<IActionResult> ExportExcel(Guid courseId)
        {
            var course = await _context.Courses
                .Include(c => c.Lectures)
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
                    Percentage = totalLectures > 0 ? Math.Round((attended * 100.0 / totalLectures), 2) : 0
                };
            }).ToList();

            var stream = new MemoryStream();

            using (var workbook = new ClosedXML.Excel.XLWorkbook())
            {
                var ws = workbook.Worksheets.Add("Attendance");

                // رأس الجدول
                ws.Cell(1, 1).Value = "Student";
                ws.Cell(1, 2).Value = "Attended";
                ws.Cell(1, 3).Value = "Total";
                ws.Cell(1, 4).Value = "Percentage";

                // بيانات الطلاب
                for (int i = 0; i < data.Count; i++)
                {
                    ws.Cell(i + 2, 1).Value = data[i].Name;
                    ws.Cell(i + 2, 2).Value = data[i].Attended;
                    ws.Cell(i + 2, 3).Value = data[i].Total;
                    ws.Cell(i + 2, 4).Value = data[i].Percentage;
                }

                workbook.SaveAs(stream);
            }

            stream.Position = 0; // مهم جدًا قبل الإرجاع

            return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "CourseAttendance.xlsx");
        }








        [Authorize(Roles = "Trainer")]
        public async Task<IActionResult> ExportPdf(Guid courseId)
        {
            var result = await CourseAttendance(courseId, null, null);
            var viewResult = result as ViewResult;

            if (viewResult == null)
            {
                // هنا يمكن أن ترجع رسالة واضحة أو PDF فارغ أو Redirect
                return NotFound($"Course with ID {courseId} was not found.");
            }

            return new Rotativa.AspNetCore.ViewAsPdf("CourseAttendance", viewResult.Model)
            {
                FileName = "CourseAttendance.pdf",
                PageSize = Rotativa.AspNetCore.Options.Size.A4
            };
        }



    }
}
