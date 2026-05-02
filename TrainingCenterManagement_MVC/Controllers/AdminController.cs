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
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public AdminController(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _context = context;
        }

        // ── Dashboard ─────────────────────────────────────────────────────────

        // FIX: Added [Authorize(Roles = "Admin")] via class-level attribute.
        // Previously this action had no authorization at all.
        public IActionResult Dashboard()
        {
            return RedirectToAction("AdminDashboard", "Dashboard");
        }

        // ── Admin Auth (separate login page for admin panel) ──────────────────

        [AllowAnonymous]
        [HttpGet]
        public IActionResult AdminAuth()
        {
            if (User.Identity.IsAuthenticated)
                return RedirectToAction("Dashboard", "Admin");

            return View();
        }

        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> AdminAuth(LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            if (User.Identity.IsAuthenticated)
                return RedirectToAction("Dashboard", "Admin");

            var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, false, false);

            if (result.Succeeded)
            {
                var user = await _userManager.FindByEmailAsync(model.Email);
                if (user != null && await _userManager.IsInRoleAsync(user, "Admin"))
                {
                    HttpContext.Session.SetString("UserId", user.Id);
                    return RedirectToAction("Dashboard", "Admin");
                }

                // User authenticated but not admin — sign out
                await _signInManager.SignOutAsync();
                ModelState.AddModelError(string.Empty, "Access denied. Admin role required.");
                return View(model);
            }

            ModelState.AddModelError(string.Empty, SharedResource.InvalidLoginCredentials);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            HttpContext.Session.Clear();
            await _signInManager.SignOutAsync();
            return RedirectToAction("Login", "Account");
        }

        // ── Users Management ──────────────────────────────────────────────────

        public async Task<IActionResult> GetUsers()
        {
            var users = await _context.Users.ToListAsync();
            return View(users);
        }

        public async Task<IActionResult> EditUser(string id)
        {
            if (string.IsNullOrEmpty(id))
                return NotFound();

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return NotFound();

            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUser(ApplicationUser model)
        {
            var user = await _userManager.FindByIdAsync(model.Id);
            if (user == null)
                return NotFound();

            user.FullName = model.FullName;
            user.PhoneNumber = model.PhoneNumber;
            user.BirthDate = model.BirthDate;

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
                return RedirectToAction("GetUsers");

            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(string id)
        {
            if (string.IsNullOrEmpty(id))
                return NotFound();

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return NotFound();

            await _userManager.DeleteAsync(user);
            return RedirectToAction("GetUsers");
        }

        // ── Course Trainees Management ────────────────────────────────────────

        public async Task<IActionResult> CourseTrainees(Guid courseId)
        {
            var course = await _context.Courses
                .Include(c => c.CourseTrainees)
                    .ThenInclude(ct => ct.Trainee)
                        .ThenInclude(t => t.User)
                .FirstOrDefaultAsync(c => c.CourseId == courseId);

            if (course == null)
                return NotFound();

            ViewBag.CourseId = courseId;
            ViewBag.CourseName = course.CourseName;
            return View(course.CourseTrainees.ToList());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveTraineeFromCourse(Guid courseId, Guid traineeId)
        {
            var entry = await _context.CourseTrainees
                .FirstOrDefaultAsync(ct => ct.CourseId == courseId && ct.TraineeId == traineeId);

            if (entry != null)
            {
                _context.CourseTrainees.Remove(entry);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("CourseTrainees", new { courseId });
        }

        // ── Certificates ──────────────────────────────────────────────────────

        public async Task<IActionResult> SearchCertificates(Guid? courseId, Guid? traineeId)
        {
            var courses = await _context.Courses.Where(c => !c.IsDeleted).ToListAsync();
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

        [Authorize(Roles = "Admin,Trainee")]
        public async Task<IActionResult> GenerateCertificatePdf(Guid certificateId)
        {
            var cert = await _context.Certificates
                .Include(c => c.Trainee).ThenInclude(t => t.User)
                .Include(c => c.Course)
                .Include(c => c.Trainer).ThenInclude(t => t.User)
                .FirstOrDefaultAsync(c => c.CertificateId == certificateId);

            if (cert == null)
                return NotFound();

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");
            var isOwner = cert.Trainee.UserId == currentUserId;

            if (!isAdmin && !isOwner)
                return Forbid();

            return new Rotativa.AspNetCore.ViewAsPdf("CertificateTemplate", cert)
            {
                FileName = $"{cert.Trainee.User.FullName}_Certificate.pdf",
                PageSize = Rotativa.AspNetCore.Options.Size.A4,
                PageOrientation = Rotativa.AspNetCore.Options.Orientation.Landscape
            };
        }

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

        // ── Featured Courses ──────────────────────────────────────────────────

        [HttpGet]
        public async Task<IActionResult> ManageFeaturedCourses()
        {
            var courses = await _context.Courses
                .Where(c => !c.IsDeleted)
                .Select(c => new CourseViewModel
                {
                    CourseId = c.CourseId,
                    CourseName = c.CourseName,
                    Description = c.Description,
                    IsFeatured = c.IsFeatured
                })
                .ToListAsync();

            var model = new FeaturedCoursesViewModel { Courses = courses };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ManageFeaturedCourses(FeaturedCoursesViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var allCourses = await _context.Courses.Where(c => !c.IsDeleted).ToListAsync();
            foreach (var course in allCourses)
            {
                var vm = model.Courses.FirstOrDefault(c => c.CourseId == course.CourseId);
                if (vm != null)
                    course.IsFeatured = vm.IsFeatured;
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = SharedResource.FeaturedCourses;
            return RedirectToAction("AdminDashboard", "Dashboard");
        }

        // FIX: Added missing UpdateFeaturedCourses action that AdminDashboard view calls.
        // The view form posts to Admin/UpdateFeaturedCourses, so this action is required.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateFeaturedCourses(List<Guid> selectedCourseIds)
        {
            var allCourses = await _context.Courses.Where(c => !c.IsDeleted).ToListAsync();
            foreach (var course in allCourses)
            {
                course.IsFeatured = selectedCourseIds != null && selectedCourseIds.Contains(course.CourseId);
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Featured courses updated successfully.";
            return RedirectToAction("AdminDashboard", "Dashboard");
        }

        // ── JSON Helpers (for AdminDashboard JS) ──────────────────────────────

        [HttpGet]
        public async Task<IActionResult> GetCoursesJson()
        {
            var courses = await _context.Courses
                .Where(c => !c.IsDeleted)
                .Select(c => new
                {
                    c.CourseId,
                    c.CourseName,
                    c.Description,
                    DurationWeeks = c.NumberOfLectures / 5,
                    c.IsFeatured
                })
                .ToListAsync();

            return Json(courses);
        }
    }
}
