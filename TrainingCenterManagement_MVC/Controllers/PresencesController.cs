using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Rotativa.AspNetCore;
using TrainingCenterManagement_MVC.Data;
using TrainingCenterManagement_MVC.Models;
using TrainingCenterManagement_MVC.ViewModels;

namespace TrainingCenterManagement_MVC.Controllers
{
    [Authorize(Roles = "Trainer")]
    public class PresencesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PresencesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Presences
        public async Task<IActionResult> Index()
        {
            var applicationDbContext = _context.Presences.Include(p => p.Lecture).Include(p => p.Trainee);
            return View(await applicationDbContext.ToListAsync());
        }

        // GET: Presences/Details/5
        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var presence = await _context.Presences
                .Include(p => p.Lecture)
                .Include(p => p.Trainee)
                .FirstOrDefaultAsync(m => m.PresenceId == id);
            if (presence == null)
            {
                return NotFound();
            }

            return View(presence);
        }

        // GET: Presences/Create
        public IActionResult Create()
        {
            ViewData["LectureId"] = new SelectList(_context.Lectures, "LectureId", "Title");
            ViewData["TraineeId"] = new SelectList(_context.Trainees, "TraineeId", "UserId");
            return View();
        }

        // POST: Presences/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("PresenceId,IsPresent,IsDeleted,LectureId,TraineeId")] Presence presence)
        {
           
                presence.PresenceId = Guid.NewGuid();
                _context.Add(presence);
                await _context.SaveChangesAsync();
          
            ViewData["LectureId"] = new SelectList(_context.Lectures, "LectureId", "Title", presence.LectureId);
            ViewData["TraineeId"] = new SelectList(_context.Trainees, "TraineeId", "UserId", presence.TraineeId);
            return RedirectToAction(nameof(Index));
        }

        // GET: Presences/Edit/5
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var presence = await _context.Presences.FindAsync(id);
            if (presence == null)
            {
                return NotFound();
            }
            ViewData["LectureId"] = new SelectList(_context.Lectures, "LectureId", "Title", presence.LectureId);
            ViewData["TraineeId"] = new SelectList(_context.Trainees, "TraineeId", "UserId", presence.TraineeId);
            return View(presence);
        }

        // POST: Presences/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, [Bind("PresenceId,IsPresent,IsDeleted,LectureId,TraineeId")] Presence presence)
        {
            if (id != presence.PresenceId)
            {
                return NotFound();
            }

           
                try
                {
                    _context.Update(presence);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!PresenceExists(presence.PresenceId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
         
            ViewData["LectureId"] = new SelectList(_context.Lectures, "LectureId", "Title", presence.LectureId);
            ViewData["TraineeId"] = new SelectList(_context.Trainees, "TraineeId", "UserId", presence.TraineeId);
            return RedirectToAction(nameof(Index));
        }

        // GET: Presences/Delete/5
        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var presence = await _context.Presences
                .Include(p => p.Lecture)
                .Include(p => p.Trainee)
                .FirstOrDefaultAsync(m => m.PresenceId == id);
            if (presence == null)
            {
                return NotFound();
            }

            return View(presence);
        }

        // POST: Presences/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var presence = await _context.Presences.FindAsync(id);
            if (presence != null)
            {
                _context.Presences.Remove(presence);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool PresenceExists(Guid id)
        {
            return _context.Presences.Any(e => e.PresenceId == id);
        }
        public async Task<IActionResult> MarkAttendance(Guid lectureId)
        {
            var lecture = await _context.Lectures
                .Include(l => l.Course)
                    .ThenInclude(c => c.CourseTrainees)
                        .ThenInclude(ct => ct.Trainee)
                        .ThenInclude(t => t.User)
                .FirstOrDefaultAsync(l => l.LectureId == lectureId);

            if (lecture == null)
                return NotFound();
            // مؤقتًا نستخدم قيمة ثابتة
              var duration = 1.5;
            var viewModel = new MarkAttendanceViewModel
            {
                LectureId = lectureId,
                LectureTitle = lecture.Title,
                DurationHours = duration, // ✅ مهم تمريرها هنا
                Trainees = lecture.Course.CourseTrainees.Select(ct => new TraineeAttendanceInput
                {
                    TraineeId = ct.TraineeId,
                    FullName = ct.Trainee.User.FullName,
                    IsPresent = false
                }).ToList()
            };

            return View(viewModel);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAttendance(MarkAttendanceViewModel model)
        {
            var lecture = await _context.Lectures.FindAsync(model.LectureId);
            if (lecture == null || lecture.LectureDate < DateTime.Now.AddHours(-model.DurationHours))
            {
                TempData["Error"] = "Cannot mark attendance after lecture time has passed.";
                return RedirectToAction("Index", "Lectures");
            }

            foreach (var trainee in model.Trainees)
            {
                var existing = await _context.Presences
                    .FirstOrDefaultAsync(p => p.LectureId == model.LectureId && p.TraineeId == trainee.TraineeId);

                if (existing == null)
                {
                    _context.Presences.Add(new Presence
                    {
                        PresenceId = Guid.NewGuid(),
                        LectureId = model.LectureId,
                        TraineeId = trainee.TraineeId,
                        IsPresent = trainee.IsPresent,
                        IsDeleted = false
                    });
                }
                else
                {
                    existing.IsPresent = trainee.IsPresent;
                    _context.Presences.Update(existing);
                }
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Attendance saved successfully.";
            return RedirectToAction("Details", "Lectures", new { id = model.LectureId });
        }

        public async Task<IActionResult> LectureAttendance(Guid lectureId)
        {
            var lecture = await _context.Lectures
                .Include(l => l.Course)
                .Include(l => l.Presences)
                    .ThenInclude(p => p.Trainee)
                        .ThenInclude(t => t.User)
                .FirstOrDefaultAsync(l => l.LectureId == lectureId);

            if (lecture == null) return NotFound();

            return View(lecture);
        }
        public async Task<IActionResult> TraineeAttendanceInCourse(Guid traineeId, Guid courseId)
        {
            var course = await _context.Courses
                .Include(c => c.Lectures)
                    .ThenInclude(l => l.Presences)
                .FirstOrDefaultAsync(c => c.CourseId == courseId);

            if (course == null) return NotFound();

            var totalLectures = course.Lectures.Count;
            var attended = course.Lectures.Count(l =>
                l.Presences.Any(p => p.TraineeId == traineeId && p.IsPresent));

            ViewBag.Trainee = await _context.Trainees
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.TraineeId == traineeId);

            ViewBag.Course = course;
            ViewBag.Attended = attended;
            ViewBag.TotalLectures = totalLectures;

            return View();
        }
        public async Task<IActionResult> ExportLectureAttendancePdf(Guid lectureId)
        {
            var lecture = await _context.Lectures
                .Include(l => l.Course)
                .Include(l => l.Presences)
                    .ThenInclude(p => p.Trainee)
                        .ThenInclude(t => t.User)
                .FirstOrDefaultAsync(l => l.LectureId == lectureId);

            if (lecture == null)
                return NotFound();

            var viewModel = lecture.Presences.Select(p => new LectureAttendanceViewModel
            {
                LectureTitle = lecture.Title,
                LectureDate = lecture.LectureDate,
                TraineeName = p.Trainee.User.FullName,
                IsPresent = p.IsPresent
            }).ToList();

            return new ViewAsPdf("LectureAttendancePdf", viewModel)
            {
                FileName = $"Lecture_Attendance_{lecture.Title}.pdf",
                PageSize = Rotativa.AspNetCore.Options.Size.A4,
                PageOrientation = Rotativa.AspNetCore.Options.Orientation.Portrait
            };
        }

        public ActionResult LectureAttendanceReport(Guid lectureId)
        {
            var lecture = _context.Lectures
                .Include(l => l.Presences)
                .ThenInclude(p => p.Trainee)
                .FirstOrDefault(l => l.LectureId == lectureId);

            if (lecture == null)
            {
                return NotFound();
            }

            var viewModel = lecture.Presences.Select(p => new LectureAttendanceViewModel
            {
                LectureTitle = lecture.Title,
                LectureDate = lecture.LectureDate,
                TraineeName = p.Trainee.User.FullName,
                IsPresent = p.IsPresent
            }).ToList();

            return View(viewModel);
        }

    }
}
