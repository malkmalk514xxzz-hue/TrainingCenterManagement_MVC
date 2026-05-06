using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrainingCenterManagement_MVC.Data;
using TrainingCenterManagement_MVC.Models;
using TrainingCenterManagement_MVC.ViewModels;

namespace TrainingCenterManagement_MVC.Controllers
{
    [Authorize]
    public class ProfileController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        private const long MaxPhotoBytes = 5 * 1024 * 1024; // 5 MB

        public ProfileController(UserManager<ApplicationUser> userManager,
                                 ApplicationDbContext context,
                                 IWebHostEnvironment env)
        {
            _userManager = userManager;
            _context    = context;
            _env        = env;
        }

        // GET /Profile
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Forbid();

            var vm = BuildViewModel(user);

            if (User.IsInRole("Trainer"))
            {
                var trainer = await _context.Trainers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.UserId == user.Id);

                if (trainer != null)
                {
                    vm.Specialty          = trainer.Specialty;
                    vm.YearsOfExperience  = trainer.YearsOfExperience;
                    vm.BusinessLink       = trainer.BusinessLink;
                }
            }

            return View(vm);
        }

        // POST /Profile/UpdateInfo
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateInfo(ProfileViewModel vm)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Forbid();

            // Trainer-only fields are irrelevant for other roles
            if (!User.IsInRole("Trainer"))
            {
                ModelState.Remove(nameof(vm.Specialty));
                ModelState.Remove(nameof(vm.YearsOfExperience));
                ModelState.Remove(nameof(vm.BusinessLink));
            }

            if (!ModelState.IsValid)
            {
                vm.Email             = user.Email!;
                vm.ProfilePictureUrl = user.ProfilePictureUrl;
                vm.Role              = GetRoleLabel();
                TempData["Error"]    = "يرجى تصحيح الأخطاء في النموذج.";
                return View("Index", vm);
            }

            user.FullName    = vm.FullName.Trim();
            user.BirthDate   = vm.BirthDate;
            user.PhoneNumber = vm.PhoneNumber?.Trim();

            var result = await _userManager.UpdateAsync(user);

            if (result.Succeeded && User.IsInRole("Trainer"))
            {
                var trainer = await _context.Trainers
                    .FirstOrDefaultAsync(t => t.UserId == user.Id);

                if (trainer != null)
                {
                    if (!string.IsNullOrWhiteSpace(vm.Specialty))
                        trainer.Specialty = vm.Specialty.Trim();

                    if (vm.YearsOfExperience.HasValue)
                        trainer.YearsOfExperience = vm.YearsOfExperience.Value;

                    trainer.BusinessLink = string.IsNullOrWhiteSpace(vm.BusinessLink)
                        ? null : vm.BusinessLink.Trim();

                    await _context.SaveChangesAsync();
                }
            }

            TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded
                ? "تم تحديث معلوماتك الشخصية بنجاح."
                : string.Join(" | ", result.Errors.Select(e => e.Description));

            return RedirectToAction(nameof(Index));
        }

        // POST /Profile/UpdatePhoto
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdatePhoto(IFormFile photo)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Forbid();

            if (photo == null || photo.Length == 0)
            {
                TempData["Error"] = "يرجى اختيار صورة أولاً.";
                return RedirectToAction(nameof(Index));
            }

            var ext = Path.GetExtension(photo.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(ext))
            {
                TempData["Error"] = "نوع الملف غير مسموح. الأنواع المقبولة: jpg، png، gif، webp.";
                return RedirectToAction(nameof(Index));
            }

            if (photo.Length > MaxPhotoBytes)
            {
                TempData["Error"] = "حجم الصورة يتجاوز الحد المسموح (5 ميغابايت).";
                return RedirectToAction(nameof(Index));
            }

            var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "profiles");
            Directory.CreateDirectory(uploadsFolder);

            // Remove old photo file
            if (!string.IsNullOrEmpty(user.ProfilePictureUrl))
            {
                var oldRelative = user.ProfilePictureUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                var oldAbsolute = Path.Combine(_env.WebRootPath, oldRelative);
                if (System.IO.File.Exists(oldAbsolute))
                    System.IO.File.Delete(oldAbsolute);
            }

            var fileName = user.Id + ext;
            var filePath = Path.Combine(uploadsFolder, fileName);

            await using (var stream = new FileStream(filePath, FileMode.Create))
                await photo.CopyToAsync(stream);

            user.ProfilePictureUrl = "/uploads/profiles/" + fileName;
            await _userManager.UpdateAsync(user);

            TempData["Success"] = "تم تحديث صورة الملف الشخصي بنجاح.";
            return RedirectToAction(nameof(Index));
        }

        // ── helpers ──────────────────────────────────────────────────

        private ProfileViewModel BuildViewModel(ApplicationUser user) => new()
        {
            FullName          = user.FullName,
            BirthDate         = user.BirthDate == default ? DateTime.Today.AddYears(-20) : user.BirthDate,
            PhoneNumber       = user.PhoneNumber,
            ProfilePictureUrl = user.ProfilePictureUrl,
            Email             = user.Email!,
            Role              = GetRoleLabel()
        };

        private string GetRoleLabel() =>
            User.IsInRole("Admin")        ? "مدير النظام" :
            User.IsInRole("Trainer")      ? "مدرّب"       :
            User.IsInRole("Trainee")      ? "متدرّب"      :
            User.IsInRole("Receptionist") ? "موظف استقبال" : "مستخدم";
    }
}
