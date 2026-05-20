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
using TrainingCenterManagement_MVC.Helpers;
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
            if (id == null) return NotFound();

            var trainee = await _context.Trainees
                .Include(t => t.User)
                .Include(t => t.CourseTrainees).ThenInclude(ct => ct.Course)
                .Include(t => t.Certificates).ThenInclude(c => c.Course)
                .Include(t => t.ExamAttempts)
                .Include(t => t.Presences)
                .FirstOrDefaultAsync(m => m.TraineeId == id);

            if (trainee == null) return NotFound();

            var totalPresences = trainee.Presences.Count;
            var attended       = trainee.Presences.Count(p => p.IsPresent);
            ViewBag.AttendanceRate = totalPresences > 0
                ? Math.Round(attended * 100.0 / totalPresences, 1)
                : 0.0;

            var scoredAttempts = trainee.ExamAttempts.Where(a => a.ScorePercentage.HasValue).ToList();
            ViewBag.BestExamScore = scoredAttempts.Any()
                ? (double?)Math.Round((double)scoredAttempts.Max(a => a.ScorePercentage!.Value), 1)
                : null;

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
                UserId = user.Id,
                TransferCode = await GenerateUniqueTransferCodeAsync()
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

        private async Task<string> GenerateUniqueTransferCodeAsync()
        {
            string code;
            do
            {
                code = TransferCodeGenerator.Generate();
            }
            while (await _context.Trainees.AnyAsync(t => t.TransferCode == code));

            return code;
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
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var trainee = await _context.Trainees.FirstOrDefaultAsync(t => t.UserId == userId);
            if (trainee != null)
            {
                var enrollment = await _context.CourseTrainees
                    .FirstOrDefaultAsync(ct => ct.CourseId == courseId && ct.TraineeId == trainee.TraineeId);
                if (enrollment?.IsSuspended == true)
                {
                    TempData["IsSuspendedRedirect"] = true;
                    TempData["SuspendedMessage"] = enrollment.SuspensionReason ?? "";
                    return RedirectToAction("Details", "Courses", new { id = courseId });
                }
            }

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

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Trainee")]
        public async Task<IActionResult> SelfEnroll(Guid courseId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var trainee = await _context.Trainees.FirstOrDefaultAsync(t => t.UserId == userId);
            if (trainee == null) return Forbid();

            var already = await _context.CourseTrainees
                .AnyAsync(ct => ct.TraineeId == trainee.TraineeId && ct.CourseId == courseId);

            if (!already)
            {
                _context.CourseTrainees.Add(new CourseTrainee { TraineeId = trainee.TraineeId, CourseId = courseId });
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم تسجيلك في الدورة بنجاح!";
            }
            else
            {
                TempData["Info"] = "أنت مسجل بالفعل في هذه الدورة.";
            }

            return RedirectToAction("TraineeDashboard", "Dashboard");
        }

        [Authorize(Roles = "Trainee")]
        public async Task<IActionResult> Deposit()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var trainee = await _context.Trainees.FirstOrDefaultAsync(t => t.UserId == userId);
            if (trainee == null) return Forbid();
            ViewBag.TransferCode = trainee.TransferCode;
            return View();
        }

        // GET: Trainees/Refund
        [Authorize(Roles = "Trainee")]
        public async Task<IActionResult> Refund()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var trainee = await _context.Trainees.FirstOrDefaultAsync(t => t.UserId == userId);
            if (trainee == null) return Forbid();
            ViewBag.BalanceUSD = trainee.BalanceUSD;
            ViewBag.BalanceSYP = trainee.BalanceSYP;
            return View();
        }

        // POST: Trainees/SubmitRefund
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Trainee")]
        public async Task<IActionResult> SubmitRefund(decimal amountUSD, decimal amountSYP, IFormFile barcodeImage)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var trainee = await _context.Trainees.Include(t => t.User)
                .FirstOrDefaultAsync(t => t.UserId == userId);
            if (trainee == null) return Forbid();

            if (amountUSD < 0) amountUSD = 0;
            if (amountSYP < 0) amountSYP = 0;

            if (amountUSD > trainee.BalanceUSD)
            {
                TempData["RefundError"] = "المبلغ المطلوب بالدولار يتجاوز رصيدك الحالي.";
                return RedirectToAction(nameof(Refund));
            }
            if (amountSYP > trainee.BalanceSYP)
            {
                TempData["RefundError"] = "المبلغ المطلوب بالليرة السورية يتجاوز رصيدك الحالي.";
                return RedirectToAction(nameof(Refund));
            }
            if (amountUSD == 0 && amountSYP == 0)
            {
                TempData["RefundError"] = "يجب إدخال مبلغ للاسترداد.";
                return RedirectToAction(nameof(Refund));
            }
            if (barcodeImage == null || barcodeImage.Length == 0)
            {
                TempData["RefundError"] = "يجب رفع صورة الباركود الخاص بحسابك.";
                return RedirectToAction(nameof(Refund));
            }

            // Save barcode image
            string barcodeDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "barcodes");
            Directory.CreateDirectory(barcodeDir);
            string ext = Path.GetExtension(barcodeImage.FileName);
            string fileName = $"{Guid.NewGuid()}{ext}";
            string filePath = Path.Combine(barcodeDir, fileName);
            using (var fs = new FileStream(filePath, FileMode.Create))
                await barcodeImage.CopyToAsync(fs);

            // Deduct balance immediately
            trainee.BalanceUSD -= amountUSD;
            trainee.BalanceSYP -= amountSYP;

            var request = new WithdrawRequest
            {
                Id = Guid.NewGuid(),
                TraineeId = trainee.TraineeId,
                AmountUSD = amountUSD,
                AmountSYP = amountSYP,
                BarCodeImagePath = $"/uploads/barcodes/{fileName}",
                Status = WithdrawStatus.PendingReview,
                CreatedAt = DateTime.UtcNow
            };
            _context.WithdrawRequests.Add(request);
            await _context.SaveChangesAsync();

            // Notify admins and receptionists
            var adminIds = await _context.Admins.Select(a => a.UserId).ToListAsync();
            var recepIds = await _context.Receptionists.Select(r => r.UserId).ToListAsync();
            var targets = adminIds.Union(recepIds).Distinct().ToList();

            string currency = amountUSD > 0 && amountSYP > 0
                ? $"{amountUSD:N2} USD + {amountSYP:N0} ل.س"
                : amountUSD > 0 ? $"{amountUSD:N2} USD" : $"{amountSYP:N0} ل.س";

            foreach (var uid in targets)
            {
                _context.Notifications.Add(new UserNotification
                {
                    NotificationId = Guid.NewGuid(),
                    UserId = uid,
                    Title = "طلب استرداد أموال جديد",
                    Message = $"قام المتدرب {trainee.User.FullName} بطلب استرداد {currency}.\nيرجى مراجعة الطلب وإجراء التحويل.",
                    Type = NotificationType.General,
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow,
                    RelatedId = request.Id.ToString()
                });
            }
            await _context.SaveChangesAsync();

            TempData["Success"] = "تم تقديم طلب الاسترداد بنجاح. سيتم مراجعته قريباً.";
            return RedirectToAction("TraineeDashboard", "Dashboard");
        }

        // GET: Trainees/WithdrawRequests — Admin & Receptionist only
        [Authorize(Roles = "Admin,Receptionist")]
        public async Task<IActionResult> WithdrawRequests()
        {
            var requests = await _context.WithdrawRequests
                .Include(w => w.Trainee).ThenInclude(t => t.User)
                .OrderByDescending(w => w.CreatedAt)
                .ToListAsync();
            return View(requests);
        }

        // GET: Trainees/WithdrawRequestDetail/{id} — Admin & Receptionist only
        [Authorize(Roles = "Admin,Receptionist")]
        public async Task<IActionResult> WithdrawRequestDetail(Guid id)
        {
            var request = await _context.WithdrawRequests
                .Include(w => w.Trainee).ThenInclude(t => t.User)
                .FirstOrDefaultAsync(w => w.Id == id);
            if (request == null) return NotFound();
            return View(request);
        }
    }
}
