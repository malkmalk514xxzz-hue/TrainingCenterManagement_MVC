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
    public class CertificatesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CertificatesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Certificates
        public async Task<IActionResult> Index()
        {
            var applicationDbContext = _context.Certificates
                    .Include(c => c.Course)
                    .Include(c => c.Exam)
                    .Include(c => c.Trainee)
                        .ThenInclude(t => t.User) // لجلب بيانات المستخدم
                    .Include(c => c.Trainer); return View(await applicationDbContext.ToListAsync());
        }

        // GET: Certificates/Details/5
        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var certificate = await _context.Certificates
                .Include(c => c.Course)
                .Include(c => c.Exam)
                .Include(c => c.Trainee)
                    .ThenInclude(t => t.User) // لجلب بيانات المستخدم
                .Include(c => c.Trainer)
                .FirstOrDefaultAsync(m => m.CertificateId == id);

            if (certificate == null)
            {
                return NotFound();
            }

            return View(certificate);
        }


        // GET: Certificates/Create
        [Authorize(Roles = "Admin")]

        public IActionResult Create()
        {
            ViewData["CourseId"] = new SelectList(_context.Courses, "CourseId", "CourseName");
            ViewData["ExamId"] = new SelectList(_context.Exams, "ExamId", "ExamName");
            //ViewData["TraineeId"] = new SelectList(_context.Trainees, "TraineeId", "UserId");
            ViewData["TraineeId"] = new SelectList(
                                            _context.Trainees
                                                .Include(t => t.User)
                                                .Select(t => new
                                                {
                                                    t.TraineeId,
                                                    Email = t.User.Email // أو t.User.UserName إذا أردت عرض اسم الحساب
                                                }),
                                            "TraineeId",
                                            "Email"
                                        );
            // ViewData["TrainerId"] = new SelectList(_context.Trainers, "TrainerId", "Specialty");
            // Trainers (show email instead of Specialty)
            ViewData["TrainerId"] = new SelectList(
                                            _context.Trainers
                                                .Include(tr => tr.User)
                                                .Select(tr => new
                                                {
                                                    tr.TrainerId,
                                                    Email = tr.User.Email
                                                }),
                                            "TrainerId",
                                            "Email"
                                        );
            return View();
        }

        // POST: Certificates/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("CertificateId,Average,Url,IsDeleted,TraineeId,TrainerId,CourseId,ExamId")] Certificate certificate)
        {
            
                certificate.CertificateId = Guid.NewGuid();
                _context.Add(certificate);
                await _context.SaveChangesAsync();  
            ViewData["CourseId"] = new SelectList(_context.Courses, "CourseId", "CourseName", certificate.CourseId);
            ViewData["ExamId"] = new SelectList(_context.Exams, "ExamId", "ExamName", certificate.ExamId);
            ViewData["TraineeId"] = new SelectList(_context.Trainees, "TraineeId", "UserId", certificate.TraineeId);
            ViewData["TrainerId"] = new SelectList(_context.Trainers, "TrainerId", "Specialty", certificate.TrainerId);
            return RedirectToAction(nameof(Index));
        }

        // GET: Certificates/Edit/5
        [Authorize(Roles = "Admin")]

        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var certificate = await _context.Certificates
                .Include(c => c.Trainee)
                    .ThenInclude(t => t.User) // لجلب بيانات المستخدم
                .FirstOrDefaultAsync(c => c.CertificateId == id);

            if (certificate == null)
            {
                return NotFound();
            }

            ViewData["CourseId"] = new SelectList(_context.Courses, "CourseId", "CourseName", certificate.CourseId);
            ViewData["ExamId"] = new SelectList(_context.Exams, "ExamId", "ExamName", certificate.ExamId);

            // هنا نعرض Email بدل UserId
            ViewData["TraineeId"] = new SelectList(
                _context.Trainees.Include(t => t.User),
                "TraineeId",
                "User.Email",
                certificate.TraineeId
            );

            ViewData["TrainerId"] = new SelectList(_context.Trainers, "TrainerId", "Specialty", certificate.TrainerId);

            return View(certificate);
        }


        // POST: Certificates/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, [Bind("CertificateId,Average,Url,IsDeleted,TraineeId,TrainerId,CourseId,ExamId")] Certificate certificate)
        {
            if (id != certificate.CertificateId)
            {
                return NotFound();
            }

           
                try
                {
                    _context.Update(certificate);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!CertificateExists(certificate.CertificateId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
           
            ViewData["CourseId"] = new SelectList(_context.Courses, "CourseId", "CourseName", certificate.CourseId);
            ViewData["ExamId"] = new SelectList(_context.Exams, "ExamId", "ExamName", certificate.ExamId);
            ViewData["TraineeId"] = new SelectList(_context.Trainees, "TraineeId", "UserId", certificate.TraineeId);
            ViewData["TrainerId"] = new SelectList(_context.Trainers, "TrainerId", "Specialty", certificate.TrainerId);
            return RedirectToAction(nameof(Index));
        }

        // GET: Certificates/Delete/5
        [Authorize(Roles = "Admin")]

        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var certificate = await _context.Certificates
                .Include(c => c.Course)
                .Include(c => c.Exam)
                .Include(c => c.Trainee)
                    .ThenInclude(t => t.User) // لجلب بيانات المستخدم
                .Include(c => c.Trainer)
                .FirstOrDefaultAsync(m => m.CertificateId == id);

            if (certificate == null)
            {
                return NotFound();
            }

            return View(certificate);
        }


        // POST: Certificates/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var certificate = await _context.Certificates.FindAsync(id);
            if (certificate != null)
            {
                _context.Certificates.Remove(certificate);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool CertificateExists(Guid id)
        {
            return _context.Certificates.Any(e => e.CertificateId == id);
        }

        [AllowAnonymous]
        public async Task<IActionResult> Verify(Guid id)
        {
            var cert = await _context.Certificates
                .Include(c => c.Course)
                .Include(c => c.Trainee).ThenInclude(t => t.User)
                .Include(c => c.Trainer).ThenInclude(t => t.User)
                .FirstOrDefaultAsync(c => c.CertificateId == id);

            if (cert == null) return NotFound();

            return View("VerifyCertificate", cert);
        }

        [AllowAnonymous]
        public async Task<IActionResult> Search(string traineeName, string courseName)
        {
            var results = await _context.Certificates
                .Include(c => c.Course)
                .Include(c => c.Trainee).ThenInclude(t => t.User)
                .Include(c => c.Trainer).ThenInclude(t => t.User)
                .Where(c =>
                    (string.IsNullOrEmpty(traineeName) || c.Trainee.User.FullName.Contains(traineeName)) &&
                    (string.IsNullOrEmpty(courseName) || c.Course.CourseName.Contains(courseName))
                ).ToListAsync();

            return View("SearchResults", results);
        }

        [Authorize(Roles = "Admin, Trainer,Trainee")]
        public async Task<IActionResult> CertificatesByCourse(Guid courseId)
        {
            var certificates = await _context.Certificates
                .Include(c => c.Course)
                .Include(c => c.Trainee).ThenInclude(t => t.User)
                .Include(c => c.Trainer).ThenInclude(t => t.User)
                .Where(c => c.CourseId == courseId)
                .ToListAsync();

            ViewBag.CourseId = courseId;
            return View(certificates);
        }

        [HttpGet]
       // [Authorize(Roles = "Admin, Trainer, Trainee")]
        public async Task<IActionResult> CertificatePdf(Guid certificateId)
        {
            var certificate = await _context.Certificates
                .Include(c => c.Course)
                .Include(c => c.Trainee).ThenInclude(t => t.User)
                .Include(c => c.Trainer).ThenInclude(t => t.User)
                .FirstOrDefaultAsync(c => c.CertificateId == certificateId);

            if (certificate == null)
                return NotFound();

            return new ViewAsPdf("CertificatePdf", certificate)
            {
                FileName = $"Certificate_{certificate.Course.CourseName}_{certificate.Trainee.User.FullName}.pdf",
                PageOrientation = Rotativa.AspNetCore.Options.Orientation.Landscape,
                PageSize = Rotativa.AspNetCore.Options.Size.A4
            };

        }

    }
}
