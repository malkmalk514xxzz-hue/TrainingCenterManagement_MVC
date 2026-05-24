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
using TrainingCenterManagement_MVC.Helpers;
using ClosedXML.Excel;
using System.Security.Claims;

namespace TrainingCenterManagement_MVC.Controllers
{
    public class CoursesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly TrainingCenterManagement_MVC.Services.ExchangeRateApiService _rateService;

        public CoursesController(ApplicationDbContext context,
            TrainingCenterManagement_MVC.Services.ExchangeRateApiService rateService)
        {
            _context = context;
            _rateService = rateService;
        }

        // GET: Courses
        public async Task<IActionResult> Index(
            int page = 1,
            int pageSize = 10,
            string search = "",
            string filter = "all")   // all | active | deleted | featured
        {
            pageSize = Math.Clamp(pageSize, 3, 50);

            // ── Base query ────────────────────────────────────────────────
            var query = _context.Courses
                .Include(c => c.Admin).ThenInclude(a => a.User)
                .Include(c => c.Ratings)
                .Include(c => c.CourseTrainees)
                .AsQueryable();

            // Non-admins/trainers never see deleted courses
            bool canManage = User.IsInRole("Admin") || User.IsInRole("Trainer");
            if (!canManage)
                query = query.Where(c => !c.IsDeleted);

            // ── Filter tab ────────────────────────────────────────────────
            query = filter switch
            {
                "active" => query.Where(c => !c.IsDeleted),
                "deleted" => query.Where(c => c.IsDeleted),
                "featured" => query.Where(c => c.IsFeatured && !c.IsDeleted),
                _ => query   // "all"
            };

            // ── Search ────────────────────────────────────────────────────
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(c =>
                    c.CourseName.ToLower().Contains(term) ||
                    (c.Description != null && c.Description.ToLower().Contains(term)));
            }

            // ── Hero stats (always from full non-deleted set) ─────────────
            var allActive = await _context.Courses
                .Where(c => !c.IsDeleted)
                .Include(c => c.CourseTrainees)
                .Include(c => c.CourseTrainers)
                .ToListAsync();

            ViewBag.TotalCourses = allActive.Count;
            ViewBag.TotalTrainees = allActive.Sum(c => c.CourseTrainees?.Count ?? 0);
            ViewBag.TotalTrainers = allActive
                .SelectMany(c => c.CourseTrainers ?? Enumerable.Empty<CourseTrainer>())
                .Select(ct => ct.TrainerId).Distinct().Count();
            ViewBag.TotalLectures = allActive.Sum(c => c.NumberOfLectures);

            // ── Count before paging ───────────────────────────────────────
            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            page = Math.Max(1, Math.Min(page, Math.Max(1, totalPages)));

            // ── Page ──────────────────────────────────────────────────────
            var courses = await query
                .OrderByDescending(c => c.IsFeatured)
                .ThenByDescending(c => c.CreatedDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // ── Trainee-specific data ─────────────────────────────────────
            if (User.IsInRole("Trainee"))
            {
                decimal sypPerUsd = await _rateService.GetSypPerUsdAsync();
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var trainee = await _context.Trainees.FirstOrDefaultAsync(t => t.UserId == userId);
                if (trainee != null)
                {
                    var enrolled = await _context.CourseTrainees
                        .Where(ct => ct.TraineeId == trainee.TraineeId)
                        .Select(ct => ct.CourseId)
                        .ToListAsync();

                    ViewBag.EnrolledCourseIds = enrolled;
                    ViewBag.TraineeBalanceSYP = trainee.BalanceSYP + (trainee.BalanceUSD * sypPerUsd);
                }
            }

            // ── ViewBag for paging / state ────────────────────────────────
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalCount = totalCount;
            ViewBag.PageSize = pageSize;
            ViewBag.Search = search;
            ViewBag.Filter = filter;

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return PartialView("_CoursesGridPartial", courses);

            return View(courses);
        }



        // GET: Courses/Purchase/5
        [Authorize(Roles = "Trainee")]
        public async Task<IActionResult> Purchase(Guid id)
        {
            var course = await _context.Courses
                .Include(c => c.CourseTrainers).ThenInclude(ct => ct.Trainer).ThenInclude(t => t.User)
                .FirstOrDefaultAsync(c => c.CourseId == id && !c.IsDeleted);

            if (course == null) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var trainee = await _context.Trainees.FirstOrDefaultAsync(t => t.UserId == userId);
            if (trainee == null) return Forbid();

            bool alreadyEnrolled = await _context.CourseTrainees
                .AnyAsync(ct => ct.CourseId == id && ct.TraineeId == trainee.TraineeId);
            if (alreadyEnrolled)
            {
                TempData["Info"] = "أنت مسجل بالفعل في هذه الدورة.";
                return RedirectToAction("TraineeDashboard", "Dashboard");
            }

            decimal sypPerUsd = await _rateService.GetSypPerUsdAsync();
            decimal priceInSyp = course.CourseCurrency == PaymentCurrency.SYP
                ? course.Price
                : course.Price * sypPerUsd;

            decimal totalSyp = trainee.BalanceSYP + (trainee.BalanceUSD * sypPerUsd);

            ViewBag.PriceInSYP = priceInSyp;
            ViewBag.PriceInUSD = priceInSyp / sypPerUsd;
            ViewBag.TraineeTotalSYP = totalSyp;
            ViewBag.TraineeBalanceUSD = trainee.BalanceUSD;
            ViewBag.TraineeBalanceSYP = trainee.BalanceSYP;
            ViewBag.InsufficientBalance = TempData["InsufficientBalance"];
            ViewBag.NeededSYP = TempData["NeededSYP"];
            return View(course);
        }

        // POST: Courses/ConfirmPurchase
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Trainee")]
        public async Task<IActionResult> ConfirmPurchase(Guid courseId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var trainee = await _context.Trainees.FirstOrDefaultAsync(t => t.UserId == userId);
            if (trainee == null) return Forbid();

            var course = await _context.Courses.FindAsync(courseId);
            if (course == null) return NotFound();

            bool alreadyEnrolled = await _context.CourseTrainees
                .AnyAsync(ct => ct.CourseId == courseId && ct.TraineeId == trainee.TraineeId);
            if (alreadyEnrolled)
            {
                TempData["Info"] = "أنت مسجل بالفعل في هذه الدورة.";
                return RedirectToAction("TraineeDashboard", "Dashboard");
            }

            decimal sypPerUsd = await _rateService.GetSypPerUsdAsync();
            var (hasBalance, errorMsg) = CheckAndDeductBalance(trainee, course, sypPerUsd);
            if (!hasBalance)
            {
                decimal priceInSyp = course.CourseCurrency == PaymentCurrency.SYP
                    ? course.Price
                    : course.Price * sypPerUsd;
                decimal totalSyp = trainee.BalanceSYP + (trainee.BalanceUSD * sypPerUsd);
                TempData["InsufficientBalance"] = true;
                TempData["NeededSYP"] = Math.Ceiling(priceInSyp - totalSyp);
                return RedirectToAction(nameof(Purchase), new { id = courseId });
            }

            _context.CourseTrainees.Add(new CourseTrainee
            {
                CourseId = courseId,
                TraineeId = trainee.TraineeId
            });
            await _context.SaveChangesAsync();

            TempData["Success"] = $"تم التسجيل في دورة \"{course.CourseName}\" بنجاح!";
            return RedirectToAction("TraineeDashboard", "Dashboard");
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
                .Include(c => c.Ratings).ThenInclude(r => r.Trainee).ThenInclude(t => t.User)
                .FirstOrDefaultAsync(m => m.CourseId == id && !m.IsDeleted);

            if (course == null)
                return NotFound();

            // ── Currency conversion ─────────────────────────────────────
            var dbRates = await _context.ExchangeRates.ToListAsync();
            var rates = new Dictionary<PaymentCurrency, decimal>
            {
                [PaymentCurrency.SYP] = 1m,
                [PaymentCurrency.USD] = dbRates.FirstOrDefault(r => r.Currency == PaymentCurrency.USD)?.RateToSYP
                                        ?? CurrencyHelper.DefaultRates[PaymentCurrency.USD],
                [PaymentCurrency.EUR] = dbRates.FirstOrDefault(r => r.Currency == PaymentCurrency.EUR)?.RateToSYP
                                        ?? CurrencyHelper.DefaultRates[PaymentCurrency.EUR],
            };
            var priceInSyp = CurrencyHelper.ToSYP(course.Price, course.CourseCurrency, rates);
            ViewBag.PriceInSYP          = Math.Round(priceInSyp, 0);
            ViewBag.PriceInUSD          = rates[PaymentCurrency.USD] > 0 ? Math.Round(priceInSyp / rates[PaymentCurrency.USD], 2) : 0;
            ViewBag.PriceInEUR          = rates[PaymentCurrency.EUR] > 0 ? Math.Round(priceInSyp / rates[PaymentCurrency.EUR], 2) : 0;
            ViewBag.CourseCurrencySymbol = CurrencyHelper.GetSymbol(course.CourseCurrency);
            ViewBag.CourseCurrencyCode   = CurrencyHelper.GetCode(course.CourseCurrency);
            ViewBag.CoursePrice          = course.Price;

            // ── Trainee-specific data ───────────────────────────────────
            if (User.IsInRole("Trainee"))
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var trainee = await _context.Trainees.FirstOrDefaultAsync(t => t.UserId == userId);
                if (trainee != null)
                {
                    var enrollment = course.CourseTrainees.FirstOrDefault(ct => ct.TraineeId == trainee.TraineeId);
                    ViewBag.IsEnrolled       = enrollment != null;
                    ViewBag.IsSuspended      = enrollment?.IsSuspended ?? false;
                    ViewBag.SuspensionReason = enrollment?.SuspensionReason;
                    ViewBag.MyRating         = course.Ratings.FirstOrDefault(r => r.TraineeId == trainee.TraineeId);
                }
            }

            return View(course);
        }

        // POST: Courses/SuspendTrainee
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SuspendTrainee(Guid courseId, Guid traineeId, string? reason, string? returnUrl)
        {
            var ct = await _context.CourseTrainees
                .FirstOrDefaultAsync(x => x.CourseId == courseId && x.TraineeId == traineeId);
            if (ct == null) return NotFound();

            ct.IsSuspended      = true;
            ct.SuspensionReason = reason?.Trim();
            ct.SuspendedAt      = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            var trainee = await _context.Trainees.Include(t => t.User)
                .FirstOrDefaultAsync(t => t.TraineeId == traineeId);
            if (trainee != null)
            {
                var course = await _context.Courses.FindAsync(courseId);
                _context.Notifications.Add(new UserNotification
                {
                    NotificationId = Guid.NewGuid(),
                    UserId    = trainee.UserId,
                    Title     = "تم إيقاف وصولك مؤقتاً",
                    Message   = $"تم إيقاف وصولك إلى دورة \"{course?.CourseName}\" بسبب: {(string.IsNullOrWhiteSpace(reason) ? "متأخر في الدفع" : reason)}. يرجى التواصل مع الإدارة.",
                    Type      = NotificationType.General,
                    IsRead    = false,
                    CreatedAt = DateTime.UtcNow,
                    RelatedId = courseId.ToString()
                });
                await _context.SaveChangesAsync();
            }

            TempData["Success"] = "تم إيقاف الطالب بنجاح.";
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);
            return RedirectToAction(nameof(Details), new { id = courseId });
        }

        // POST: Courses/UnsuspendTrainee
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UnsuspendTrainee(Guid courseId, Guid traineeId, string? returnUrl)
        {
            var ct = await _context.CourseTrainees
                .FirstOrDefaultAsync(x => x.CourseId == courseId && x.TraineeId == traineeId);
            if (ct == null) return NotFound();

            ct.IsSuspended      = false;
            ct.SuspensionReason = null;
            ct.SuspendedAt      = null;
            await _context.SaveChangesAsync();

            var trainee = await _context.Trainees.Include(t => t.User)
                .FirstOrDefaultAsync(t => t.TraineeId == traineeId);
            if (trainee != null)
            {
                var course = await _context.Courses.FindAsync(courseId);
                _context.Notifications.Add(new UserNotification
                {
                    NotificationId = Guid.NewGuid(),
                    UserId    = trainee.UserId,
                    Title     = "تم تفعيل وصولك",
                    Message   = $"تم إعادة تفعيل وصولك إلى دورة \"{course?.CourseName}\". يمكنك متابعة الدراسة الآن.",
                    Type      = NotificationType.General,
                    IsRead    = false,
                    CreatedAt = DateTime.UtcNow,
                    RelatedId = courseId.ToString()
                });
                await _context.SaveChangesAsync();
            }

            TempData["Success"] = "تم تفعيل الطالب بنجاح.";
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);
            return RedirectToAction(nameof(Details), new { id = courseId });
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
        public async Task<IActionResult> Create([Bind("CourseId,CourseName,BatchNumber,NumberOfLectures,Price,CourseCurrency,Description,VideoUrl,ThumbnailUrl,CreatedDate,ReleaseDate,IsDeleted,IsFeatured,AdminId")] Course course)
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
        public async Task<IActionResult> Edit(Guid id, [Bind("CourseId,CourseName,BatchNumber,NumberOfLectures,Price,CourseCurrency,Description,VideoUrl,ThumbnailUrl,CreatedDate,ReleaseDate,IsDeleted,IsFeatured,AdminId")] Course course)
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
            return View(id.Value);
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
            return View(id.Value);
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

            var trainee = await _context.Trainees.FirstOrDefaultAsync(t => t.UserId == userId);
            if (trainee == null)
                return Unauthorized();

            var course = await _context.Courses.FindAsync(courseId);
            if (course == null)
                return NotFound();

            bool alreadyAssigned = await _context.CourseTrainees
                .AnyAsync(ct => ct.CourseId == courseId && ct.TraineeId == trainee.TraineeId);

            if (!alreadyAssigned)
            {
                decimal sypPerUsd = await _rateService.GetSypPerUsdAsync();
                var (hasBalance, errorMsg) = CheckAndDeductBalance(trainee, course, sypPerUsd);
                if (!hasBalance)
                {
                    TempData["ErrorMessage"] = errorMsg;
                    return RedirectToAction(nameof(Details), new { id = courseId });
                }

                _context.CourseTrainees.Add(new CourseTrainee
                {
                    CourseId = courseId,
                    TraineeId = trainee.TraineeId
                });
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

            decimal sypPerUsd = await _rateService.GetSypPerUsdAsync();
            var (hasBalance, errorMsg) = CheckAndDeductBalance(trainee, course, sypPerUsd);
            if (!hasBalance)
            {
                TempData["ErrorMessage"] = errorMsg;
                return RedirectToAction(nameof(Details), new { id });
            }

            _context.CourseTrainees.Add(new CourseTrainee
            {
                CourseId = id,
                TraineeId = trainee.TraineeId
            });
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

        private (bool success, string error) CheckAndDeductBalance(Trainee trainee, Course course, decimal sypPerUsd)
        {
            decimal priceUsd = course.CourseCurrency == PaymentCurrency.USD
                ? course.Price
                : course.Price / sypPerUsd;

            decimal totalUsd = trainee.BalanceUSD + (trainee.BalanceSYP / sypPerUsd);

            if (totalUsd < priceUsd)
            {
                string currencyLabel = course.CourseCurrency == PaymentCurrency.USD ? "USD" : "SYP";
                return (false,
                    $"رصيدك غير كافٍ للتسجيل في هذه الدورة. " +
                    $"سعر الدورة: {course.Price} {currencyLabel} " +
                    $"| رصيدك: {trainee.BalanceUSD:0.##} USD + {trainee.BalanceSYP:0.##} SYP " +
                    $"(ما يعادل {totalUsd:0.##} USD).");
            }

            decimal remaining = priceUsd;
            if (trainee.BalanceUSD >= remaining)
            {
                trainee.BalanceUSD -= remaining;
            }
            else
            {
                remaining -= trainee.BalanceUSD;
                trainee.BalanceUSD = 0;
                trainee.BalanceSYP -= remaining * sypPerUsd;
                if (trainee.BalanceSYP < 0) trainee.BalanceSYP = 0;
            }

            return (true, string.Empty);
        }
    }
}
