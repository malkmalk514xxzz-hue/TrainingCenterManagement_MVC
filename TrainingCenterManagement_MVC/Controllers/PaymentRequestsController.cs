using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TrainingCenterManagement_MVC.Data;
using TrainingCenterManagement_MVC.Helpers;
using TrainingCenterManagement_MVC.Models;
using static TrainingCenterManagement_MVC.Helpers.ReceiptExtractor;

namespace TrainingCenterManagement_MVC.Controllers
{
    public class PaymentRequestsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ISettingsService     _settings;
        private readonly IWebHostEnvironment  _env;

        public PaymentRequestsController(
            ApplicationDbContext context,
            ISettingsService settings,
            IWebHostEnvironment env)
        {
            _context  = context;
            _settings = settings;
            _env      = env;
        }

        // ── GET: PaymentRequests/Submit?courseId=...&method=ShamCash ─────────
        [Authorize(Roles = "Trainee")]
        [HttpGet]
        public async Task<IActionResult> Submit(Guid? courseId, string method = "ShamCash")
        {
            var userId  = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var trainee = await _context.Trainees.FirstOrDefaultAsync(t => t.UserId == userId);
            if (trainee == null) return NotFound();

            // Load all enrolled courses for the dropdown
            var enrolledCourses = await _context.CourseTrainees
                .Where(ct => ct.TraineeId == trainee.TraineeId)
                .Include(ct => ct.Course)
                .Where(ct => !ct.Course.IsDeleted)
                .Select(ct => ct.Course)
                .ToListAsync();

            // If no courseId given, pick the first enrolled course
            if (courseId == null || courseId == Guid.Empty)
            {
                if (!enrolledCourses.Any())
                    return RedirectToAction("MyCourses", "Trainees");
                courseId = enrolledCourses.First().CourseId;
            }

            var enrollment = await _context.CourseTrainees
                .FirstOrDefaultAsync(ct => ct.CourseId == courseId && ct.TraineeId == trainee.TraineeId);
            if (enrollment == null) return Forbid();

            var course = await _context.Courses.FindAsync(courseId);
            if (course == null) return NotFound();

            // Load gateway settings
            await LoadGatewayViewBagAsync();

            ViewBag.Course  = course;
            ViewBag.Trainee = trainee;
            ViewBag.Method  = method;

            var hasPending = await _context.PaymentRequests.AnyAsync(r =>
                r.CourseId == courseId && r.TraineeId == trainee.TraineeId &&
                r.Status == PaymentRequestStatus.Pending);
            ViewBag.HasPending = hasPending;
            ViewBag.CourseList = new SelectList(enrolledCourses, "CourseId", "CourseName", courseId);

            return View();
        }

        // ── GET: PaymentRequests/MyRequests (Trainee history) ────────────────
        [Authorize(Roles = "Trainee")]
        public async Task<IActionResult> MyRequests()
        {
            var userId  = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var trainee = await _context.Trainees.FirstOrDefaultAsync(t => t.UserId == userId);
            if (trainee == null) return NotFound();

            var requests = await _context.PaymentRequests
                .Include(r => r.Course)
                .Where(r => r.TraineeId == trainee.TraineeId)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            return View(requests);
        }

        // ── POST: PaymentRequests/Submit ─────────────────────────────────────
        [Authorize(Roles = "Trainee")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(
            Guid courseId,
            string method,
            decimal amount,
            PaymentCurrency currency,
            string? transactionReference,
            string? studentNotes,
            IFormFile? receiptFile)
        {
            var userId  = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var trainee = await _context.Trainees.FirstOrDefaultAsync(t => t.UserId == userId);
            if (trainee == null) return Forbid();

            var enrollment = await _context.CourseTrainees
                .FirstOrDefaultAsync(ct => ct.CourseId == courseId && ct.TraineeId == trainee.TraineeId);
            if (enrollment == null) return Forbid();

            if (amount <= 0)
            {
                ModelState.AddModelError("amount", "المبلغ يجب أن يكون أكبر من صفر.");
                return await RebuildSubmitView(courseId, method, trainee);
            }

            // Validate receipt file
            if (receiptFile == null || receiptFile.Length == 0)
            {
                ModelState.AddModelError("receiptFile", "يرجى رفع ملف الإيصال.");
                return await RebuildSubmitView(courseId, method, trainee);
            }

            var allowedExts = new[] { ".pdf", ".jpg", ".jpeg", ".png" };
            var ext = Path.GetExtension(receiptFile.FileName).ToLowerInvariant();
            if (!allowedExts.Contains(ext))
            {
                ModelState.AddModelError("receiptFile", "يُقبل فقط: PDF, JPG, PNG.");
                return await RebuildSubmitView(courseId, method, trainee);
            }

            // Save file
            var uploadsDir = Path.Combine(_env.WebRootPath, "uploads", "receipts");
            Directory.CreateDirectory(uploadsDir);
            var fileName = $"{Guid.NewGuid()}{ext}";
            var filePath = Path.Combine(uploadsDir, fileName);
            await using (var stream = new FileStream(filePath, FileMode.Create))
                await receiptFile.CopyToAsync(stream);
            var relPath = $"/uploads/receipts/{fileName}";

            // Extract receipt data from PDF automatically
            ExtractedReceiptData? extracted = null;
            if (ext == ".pdf")
                extracted = ReceiptExtractor.ExtractFromPdf(filePath);

            // Parse method enum
            var parsedMethod = method == "Binance" ? OnlinePaymentMethod.Binance : OnlinePaymentMethod.ShamCash;

            var request = new PaymentRequest
            {
                TraineeId            = trainee.TraineeId,
                CourseId             = courseId,
                Amount               = amount,
                Currency             = currency,
                Method               = parsedMethod,
                ReceiptFilePath      = relPath,
                TransactionReference = transactionReference?.Trim(),
                StudentNotes         = studentNotes?.Trim(),
                Status               = PaymentRequestStatus.Pending,
                CreatedAt            = DateTime.UtcNow,
                // Auto-extracted from PDF receipt
                RcptSenderName       = extracted?.SenderName,
                RcptRecipientName    = extracted?.RecipientName,
                RcptRecipientAccount = extracted?.RecipientAccount,
                RcptAmount           = extracted?.Amount,
                RcptPaymentDate      = extracted?.PaymentDate,
                RcptOperationNumber  = extracted?.OperationNumber
            };
            _context.PaymentRequests.Add(request);

            // Notify admins
            var adminUsers = await _context.Users
                .Where(u => u.Role == RoleType.Admin)
                .ToListAsync();
            var methodLabel = parsedMethod == OnlinePaymentMethod.Binance ? "Binance" : "شام كاش";
            var course = await _context.Courses.FindAsync(courseId);
            foreach (var admin in adminUsers)
            {
                _context.Notifications.Add(new UserNotification
                {
                    NotificationId = Guid.NewGuid(),
                    UserId         = admin.Id,
                    Title          = "طلب دفع جديد بانتظار المراجعة",
                    Message        = $"قدّم {trainee.User?.FullName ?? "متدرب"} طلب دفع بمبلغ {amount:N0} عبر {methodLabel} لدورة \"{course?.CourseName}\".",
                    Type           = NotificationType.PaymentReceived,
                    IsRead         = false,
                    CreatedAt      = DateTime.UtcNow,
                    RelatedId      = request.RequestId.ToString()
                });
            }

            await _context.SaveChangesAsync();

            TempData["PaymentRequestSuccess"] = "تم إرسال طلب الدفع بنجاح. سيتم مراجعته من قبل الإدارة وستصلك إشعار عند الموافقة.";
            return RedirectToAction("Details", "Courses", new { id = courseId });
        }

        // ── GET: PaymentRequests/Manage ──────────────────────────────────────
        [Authorize(Roles = "Admin,Receptionist")]
        public async Task<IActionResult> Manage(string? status)
        {
            var query = _context.PaymentRequests
                .Include(r => r.Trainee).ThenInclude(t => t.User)
                .Include(r => r.Course)
                .AsQueryable();

            if (status == "pending")
                query = query.Where(r => r.Status == PaymentRequestStatus.Pending);
            else if (status == "approved")
                query = query.Where(r => r.Status == PaymentRequestStatus.Approved);
            else if (status == "rejected")
                query = query.Where(r => r.Status == PaymentRequestStatus.Rejected);

            var requests = await query.OrderByDescending(r => r.CreatedAt).ToListAsync();
            ViewBag.StatusFilter = status ?? "all";
            ViewBag.PendingCount = await _context.PaymentRequests
                .CountAsync(r => r.Status == PaymentRequestStatus.Pending);
            return View(requests);
        }

        // ── GET: PaymentRequests/Review/id ───────────────────────────────────
        [Authorize(Roles = "Admin,Receptionist")]
        public async Task<IActionResult> Review(Guid id)
        {
            var request = await _context.PaymentRequests
                .Include(r => r.Trainee).ThenInclude(t => t.User)
                .Include(r => r.Course)
                .FirstOrDefaultAsync(r => r.RequestId == id);
            if (request == null) return NotFound();

            // Load gateway settings for comparison
            await LoadGatewayViewBagAsync();

            return View(request);
        }

        // ── POST: PaymentRequests/Approve ────────────────────────────────────
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(Guid requestId, string? adminNotes)
        {
            var request = await _context.PaymentRequests
                .Include(r => r.Trainee).ThenInclude(t => t.User)
                .Include(r => r.Course)
                .FirstOrDefaultAsync(r => r.RequestId == requestId);
            if (request == null) return NotFound();

            if (request.Status != PaymentRequestStatus.Pending)
            {
                TempData["Error"] = "هذا الطلب تمت معالجته مسبقاً.";
                return RedirectToAction(nameof(Review), new { id = requestId });
            }

            var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Mark request as approved
            request.Status           = PaymentRequestStatus.Approved;
            request.AdminNotes       = adminNotes?.Trim();
            request.ProcessedAt      = DateTime.UtcNow;
            request.ProcessedByAdminId = adminId;

            // Create actual Payment record
            var payment = new Payment
            {
                PaymentId   = Guid.NewGuid(),
                TraineeId   = request.TraineeId,
                CourseId    = request.CourseId,
                TotalAmount = request.Amount,
                Currency    = request.Currency,
                Notes       = $"[{(request.Method == OnlinePaymentMethod.Binance ? "Binance" : "شام كاش")}] {adminNotes}".Trim().TrimEnd(']').TrimEnd('['),
                CreatedDate = DateTime.UtcNow,
                IsDeleted   = false
            };
            _context.Payments.Add(payment);

            // Notify trainee
            _context.Notifications.Add(new UserNotification
            {
                NotificationId = Guid.NewGuid(),
                UserId         = request.Trainee.UserId,
                Title          = "تمت الموافقة على دفعتك ✓",
                Message        = $"تمت الموافقة على دفعتك بمبلغ {request.Amount:N0} لدورة \"{request.Course?.CourseName}\". تم إضافتها إلى حسابك.",
                Type           = NotificationType.PaymentReceived,
                IsRead         = false,
                CreatedAt      = DateTime.UtcNow,
                RelatedId      = request.CourseId.ToString()
            });

            await _context.SaveChangesAsync();

            TempData["Success"] = "تمت الموافقة على الطلب وتم تسجيل الدفعة بنجاح.";
            return RedirectToAction(nameof(Manage), new { status = "pending" });
        }

        // ── POST: PaymentRequests/Reject ─────────────────────────────────────
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(Guid requestId, string? rejectionReason)
        {
            var request = await _context.PaymentRequests
                .Include(r => r.Trainee).ThenInclude(t => t.User)
                .Include(r => r.Course)
                .FirstOrDefaultAsync(r => r.RequestId == requestId);
            if (request == null) return NotFound();

            if (request.Status != PaymentRequestStatus.Pending)
            {
                TempData["Error"] = "هذا الطلب تمت معالجته مسبقاً.";
                return RedirectToAction(nameof(Review), new { id = requestId });
            }

            var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            request.Status             = PaymentRequestStatus.Rejected;
            request.RejectionReason    = rejectionReason?.Trim();
            request.ProcessedAt        = DateTime.UtcNow;
            request.ProcessedByAdminId = adminId;

            // Notify trainee
            _context.Notifications.Add(new UserNotification
            {
                NotificationId = Guid.NewGuid(),
                UserId         = request.Trainee.UserId,
                Title          = "تم رفض طلب دفعتك",
                Message        = $"تم رفض طلب الدفع بمبلغ {request.Amount:N0} لدورة \"{request.Course?.CourseName}\". السبب: {(string.IsNullOrWhiteSpace(rejectionReason) ? "لم يُحدد سبب" : rejectionReason)}",
                Type           = NotificationType.General,
                IsRead         = false,
                CreatedAt      = DateTime.UtcNow,
                RelatedId      = request.CourseId.ToString()
            });

            await _context.SaveChangesAsync();

            TempData["Success"] = "تم رفض الطلب وتم إشعار الطالب.";
            return RedirectToAction(nameof(Manage), new { status = "pending" });
        }

        // ── Private Helpers ──────────────────────────────────────────────────
        private async Task LoadGatewayViewBagAsync()
        {
            ViewBag.ShamCashAccountName   = await _settings.GetAsync("ShamCash:AccountName")   ?? "";
            ViewBag.ShamCashAccountNumber = await _settings.GetAsync("ShamCash:AccountNumber") ?? "";
            ViewBag.ShamCashQrCodeUrl     = await _settings.GetAsync("ShamCash:QrCodeUrl")     ?? "";
            ViewBag.BinanceWalletAddress  = await _settings.GetAsync("Binance:WalletAddress")  ?? "";
            ViewBag.BinanceNetwork        = await _settings.GetAsync("Binance:Network")         ?? "TRC20";
            ViewBag.BinanceQrCodeUrl      = await _settings.GetAsync("Binance:QrCodeUrl")       ?? "";
        }

        private async Task<IActionResult> RebuildSubmitView(Guid courseId, string method, Trainee trainee)
        {
            await LoadGatewayViewBagAsync();
            var course = await _context.Courses.FindAsync(courseId);
            ViewBag.Course  = course;
            ViewBag.Trainee = trainee;
            ViewBag.Method  = method;

            var enrolledCourses = await _context.CourseTrainees
                .Where(ct => ct.TraineeId == trainee.TraineeId)
                .Include(ct => ct.Course)
                .Where(ct => !ct.Course.IsDeleted)
                .Select(ct => ct.Course)
                .ToListAsync();
            ViewBag.CourseList = new SelectList(enrolledCourses, "CourseId", "CourseName", courseId);

            return View();
        }
    }
}
