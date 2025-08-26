using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TrainingCenterManagement_MVC.Data;
using TrainingCenterManagement_MVC.Models;
using TrainingCenterManagement_MVC.Resources;
using TrainingCenterManagement_MVC.ViewModels;


namespace TrainingCenterManagement_MVC.Controllers
{
    public class AdminController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public AdminController(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager
            , ApplicationDbContext context
            )
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _context = context;
        }
        public IActionResult Dashboard()
        {
            return View();
        }
        [HttpGet]
        public IActionResult AdminAuth()
        {
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Dashboard", "Admin");
            }
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> AdminAuth(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Dashboard", "Admin");
            }

            var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, false, false);

            if (result.Succeeded)
            {
                var user = await _userManager.FindByEmailAsync(model.Email);
                if (user != null)
                {
                    HttpContext.Session.SetString("UserId", user.Id);
                    return RedirectToAction("Dashboard", "Admin");
                }
            }

            ModelState.AddModelError(string.Empty, SharedResource.InvalidLoginCredentials);
            return View(model);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            HttpContext.Session.Clear();
            await _signInManager.SignOutAsync();
            return RedirectToAction("Login", "Account");
        }

        public IActionResult AccessDenied()
        {
            return View();
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> IssueCertificate(Guid traineeId, Guid courseId)
        {
            var trainee = await _context.Trainees
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.TraineeId == traineeId);
            var course = await _context.Courses
                .Include(c => c.Exam)
                .Include(c => c.CourseTrainers)
                .FirstOrDefaultAsync(c => c.CourseId == courseId);

            if (trainee == null || course == null) return NotFound();

            var trainerId = course.CourseTrainers.FirstOrDefault()?.TrainerId;

            var certificate = new Certificate
            {
                CertificateId = Guid.NewGuid(),
                Average = 100, // أو احسب من نتيجة الامتحان
                Url = "", // لاحقاً رابط الشهادة PDF
                CourseId = courseId,
                TraineeId = traineeId,
                TrainerId = (Guid)trainerId,
                ExamId = course.Exam.ExamId
            };

            _context.Certificates.Add(certificate);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Certificate has been issued successfully.";
            return RedirectToAction("CourseTrainees", new { courseId });
        }
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> SearchCertificates(Guid? courseId, Guid? traineeId)
        {
            var courses = await _context.Courses.ToListAsync();
            var trainees = await _context.Trainees.Include(t => t.User).ToListAsync();

            var query = _context.Certificates
                .Include(c => c.Course)
                .Include(c => c.Trainee).ThenInclude(t => t.User)
                .Include(c => c.Trainer).ThenInclude(t => t.User)
                .AsQueryable();

            if (courseId.HasValue)
                query = query.Where(c => c.CourseId == courseId.Value);

            if (traineeId.HasValue)
                query = query.Where(c => c.TraineeId == traineeId.Value);

            ViewBag.Courses = new SelectList(courses, "CourseId", "CourseName");
            ViewBag.Trainees = new SelectList(trainees, "TraineeId", "User.FullName");

            return View(await query.ToListAsync());
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ExportCertificatesToPdf()
        {
            var certificates = await _context.Certificates
                .Include(c => c.Course)
                .Include(c => c.Trainee).ThenInclude(t => t.User)
                .Include(c => c.Trainer).ThenInclude(t => t.User)
                .ToListAsync();

            return new Rotativa.AspNetCore.ViewAsPdf("CertificatesPdf", certificates)
            {
                FileName = "All_Certificates.pdf",
                PageSize = Rotativa.AspNetCore.Options.Size.A4,
                PageOrientation = Rotativa.AspNetCore.Options.Orientation.Landscape
            };
        }
        [Authorize(Roles = "Admin, Trainee")]
        public async Task<IActionResult> GenerateCertificatePdf(Guid certificateId)
        {
            var cert = await _context.Certificates
                .Include(c => c.Trainee)
                    .ThenInclude(t => t.User)
                .Include(c => c.Course)
                .Include(c => c.Trainer)
                    .ThenInclude(t => t.User)
                .FirstOrDefaultAsync(c => c.CertificateId == certificateId);

            if (cert == null)
                return NotFound();

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");
            var isOwner = cert.Trainee.UserId == currentUserId;

            if (!isAdmin && !isOwner)
                return Forbid(); // منع الوصول في حال عدم التوافق مع الدور أو الملكية

            return new Rotativa.AspNetCore.ViewAsPdf("CertificateTemplate", cert)
            {
                FileName = $"{cert.Trainee.User.FullName}_Certificate.pdf",
                PageSize = Rotativa.AspNetCore.Options.Size.A4,
                PageOrientation = Rotativa.AspNetCore.Options.Orientation.Landscape
            };
        }


        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DownloadAllCertificatesPdf(Guid courseId)
        {
            var certificates = await _context.Certificates
                .Where(c => c.CourseId == courseId)
                .Include(c => c.Course)
                .Include(c => c.Trainee).ThenInclude(t => t.User)
                .Include(c => c.Trainer).ThenInclude(t => t.User)
                .ToListAsync();

            return new Rotativa.AspNetCore.ViewAsPdf("CertificatesBatchTemplate", certificates)
            {
                FileName = $"Course_{courseId}_Certificates.pdf",
                PageSize = Rotativa.AspNetCore.Options.Size.A4,
                PageOrientation = Rotativa.AspNetCore.Options.Orientation.Portrait
            };
        }
        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> ManageFeaturedCourses()
        {
            var courses = await _context.Courses
                .Select(c => new CourseViewModel
                {
                    CourseId = c.CourseId,
                    CourseName = c.CourseName,
                    Description = c.Description,
                   
                    IsFeatured = c.IsFeatured
                })
                .ToListAsync();

            var model = new FeaturedCoursesViewModel
            {
                Courses = courses
            };

            return View(model);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ManageFeaturedCourses(FeaturedCoursesViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var allCourses = await _context.Courses.ToListAsync();
            foreach (var course in allCourses)
            {

                course.IsFeatured = model.Courses.Any(c => c.CourseId == course.CourseId && c.IsFeatured);
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = SharedResource.FeaturedCourses;
            return RedirectToAction("Dashboard");
        }


    }
}
