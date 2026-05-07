# 🚀 دليل تطبيق ميزات جديدة للمشروع
## TrainingCenterManagement_MVC - مرحلة التطوير

---

## 📋 ملخص سريع
هذا الدليل يحتوي على **5 ميزات جديدة** يمكن تطبيقها بسهولة في أسبوع:
1. **نظام التقييمات (Course Ratings)** ⭐
2. **مكتبة الموارد (Resources Library)** 📚
3. **نظام الإنجازات (Achievements & Badges)** 🏆
4. **التعليقات على الدروس (Lecture Comments)** 💬
5. **تقارير الأداء المتقدمة (Performance Reports)** 📊

---

---

# ✨ الميزة 1: نظام التقييمات (Course Ratings)
## الوقت المتوقع: 1 يوم

### 🎯 الوصف
- الطلاب يمكنهم تقييم الدورة من 1 إلى 5 نجوم
- يمكنهم إضافة تعليق مكتوب على التقييم
- عرض متوسط التقييمات على بطاقة الدورة
- المدربون يرون التقييمات على dashboard الخاص بهم

### 📊 البيانات المطلوبة

**1. إنشاء Model جديد:**
```csharp
namespace TrainingCenterManagement_MVC.Models
{
	public class CourseRating
	{
		[Key]
		public Guid RatingId { get; set; } = Guid.NewGuid();

		[Required]
		public Guid CourseId { get; set; }
		public Course Course { get; set; }

		[Required]
		public Guid TraineeId { get; set; }
		public Trainee Trainee { get; set; }

		[Range(1, 5)]
		public int Rating { get; set; } // 1 to 5 stars

		[MaxLength(500)]
		public string Comment { get; set; }

		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
		public DateTime? UpdatedAt { get; set; }

		public bool IsDeleted { get; set; } = false;
	}
}
```

**2. تحديث Course Model:**
```csharp
public ICollection<CourseRating> Ratings { get; set; } = new List<CourseRating>();
```

**3. تحديث Trainee Model:**
```csharp
public ICollection<CourseRating> Ratings { get; set; } = new List<CourseRating>();
```

### 🗄️ Database Migration
```bash
dotnet ef migrations add AddCourseRatingsTable
dotnet ef database update
```

### 🎮 Controller - CoursesController.cs
```csharp
[HttpPost]
[Authorize(Roles = "Trainee")]
public async Task<IActionResult> AddRating(Guid courseId, int rating, string comment)
{
	if (rating < 1 || rating > 5)
		return BadRequest("التقييم يجب أن يكون من 1 إلى 5");

	var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
	var trainee = await _context.Trainees.FirstOrDefaultAsync(t => t.UserId == userId);

	if (trainee == null)
		return Unauthorized();

	var existingRating = await _context.CourseRatings
		.FirstOrDefaultAsync(r => r.CourseId == courseId && r.TraineeId == trainee.TraineeId);

	if (existingRating != null)
	{
		// تحديث التقييم القديم
		existingRating.Rating = rating;
		existingRating.Comment = comment;
		existingRating.UpdatedAt = DateTime.UtcNow;
	}
	else
	{
		// إضافة تقييم جديد
		var newRating = new CourseRating
		{
			CourseId = courseId,
			TraineeId = trainee.TraineeId,
			Rating = rating,
			Comment = comment
		};
		_context.CourseRatings.Add(newRating);
	}

	await _context.SaveChangesAsync();
	return Ok(new { message = "تم حفظ التقييم بنجاح" });
}

[HttpGet]
public async Task<IActionResult> GetCourseRatings(Guid courseId)
{
	var ratings = await _context.CourseRatings
		.Where(r => r.CourseId == courseId && !r.IsDeleted)
		.Include(r => r.Trainee.User)
		.OrderByDescending(r => r.CreatedAt)
		.Select(r => new
		{
			r.Rating,
			r.Comment,
			r.CreatedAt,
			TraineeName = r.Trainee.User.FullName
		})
		.ToListAsync();

	var averageRating = ratings.Any() 
		? ratings.Average(r => r.Rating) 
		: 0;

	return Ok(new { ratings, averageRating });
}
```

### 🎨 View - صفحة الدورة (Courses/Details.cshtml)
```html
<!-- عرض التقييمات -->
<div class="ratings-section mt-4">
	<h4>التقييمات ⭐</h4>
	<div class="average-rating mb-3">
		<strong>متوسط التقييم: <span id="avgRating">0</span>/5</strong>
		<div class="star-display">
			<span id="starDisplay"></span>
		</div>
	</div>

	<!-- نموذج إضافة تقييم (للطلاب المسجلين فقط) -->
	@if (User.IsInRole("Trainee"))
	{
		<form id="ratingForm" class="mb-4">
			<div class="form-group">
				<label>تقييمك:</label>
				<div class="star-rating">
					@for (int i = 1; i <= 5; i++)
					{
						<input type="radio" name="rating" value="@i" id="star@i" />
						<label for="star@i">★</label>
					}
				</div>
			</div>

			<div class="form-group">
				<label>تعليقك (اختياري):</label>
				<textarea id="comment" class="form-control" rows="3" maxlength="500"></textarea>
			</div>

			<button type="submit" class="btn btn-primary">إضافة التقييم</button>
		</form>
	}

	<!-- عرض التقييمات السابقة -->
	<div id="ratingsList" class="ratings-list">
		<!-- سيتم ملأها من JavaScript -->
	</div>
</div>

<style>
.star-rating {
	display: flex;
	flex-direction: row-reverse;
	font-size: 2rem;
	gap: 10px;
}

.star-rating input {
	display: none;
}

.star-rating label {
	cursor: pointer;
	color: #ddd;
	transition: color 0.2s;
}

.star-rating input:checked ~ label,
.star-rating label:hover,
.star-rating label:hover ~ label {
	color: #ffc107;
}

.rating-card {
	border: 1px solid #e0e0e0;
	padding: 15px;
	margin-bottom: 10px;
	border-radius: 5px;
}
</style>

<script>
document.getElementById('ratingForm').addEventListener('submit', async (e) => {
	e.preventDefault();

	const rating = document.querySelector('input[name="rating"]:checked')?.value;
	const comment = document.getElementById('comment').value;

	if (!rating) {
		alert('من فضلك اختر تقييم');
		return;
	}

	const courseId = '@Model.CourseId';

	try {
		const response = await fetch(`/Courses/AddRating?courseId=${courseId}&rating=${rating}&comment=${comment}`, {
			method: 'POST'
		});

		if (response.ok) {
			alert('شكراً على التقييم!');
			document.getElementById('ratingForm').reset();
			loadRatings();
		}
	} catch (error) {
		console.error('خطأ:', error);
	}
});

async function loadRatings() {
	const courseId = '@Model.CourseId';
	const response = await fetch(`/Courses/GetCourseRatings?courseId=${courseId}`);
	const data = await response.json();

	document.getElementById('avgRating').textContent = data.averageRating.toFixed(1);

	// عرض النجوم
	const starDisplay = document.getElementById('starDisplay');
	starDisplay.innerHTML = '★'.repeat(Math.round(data.averageRating)) + 
						   '☆'.repeat(5 - Math.round(data.averageRating));

	// عرض التقييمات
	const ratingsList = document.getElementById('ratingsList');
	ratingsList.innerHTML = data.ratings.map(r => `
		<div class="rating-card">
			<strong>${r.traineeName}</strong> - 
			<span class="stars">${'★'.repeat(r.rating)}${'☆'.repeat(5-r.rating)}</span>
			<p>${r.comment || 'بدون تعليق'}</p>
			<small class="text-muted">${new Date(r.createdAt).toLocaleDateString('ar-EG')}</small>
		</div>
	`).join('');
}

// تحميل التقييمات عند فتح الصفحة
loadRatings();
</script>
```

---

---

# 📚 الميزة 2: مكتبة الموارد (Resources Library)
## الوقت المتوقع: 1.5 يوم

### 🎯 الوصف
- المدربون يرفعون ملفات موارد تعليمية (PDF, DOC, ZIP)
- الملفات منظمة حسب الدرس أو الموضوع
- الطلاب يحملون الملفات مع تتبع عدد التحميلات
- إحصائيات عن استخدام كل ملف

### 📊 البيانات المطلوبة

**1. إنشاء Model جديد:**
```csharp
namespace TrainingCenterManagement_MVC.Models
{
	public class CourseResource
	{
		[Key]
		public Guid ResourceId { get; set; } = Guid.NewGuid();

		[Required]
		public Guid CourseId { get; set; }
		public Course Course { get; set; }

		public Guid? LectureId { get; set; }
		public Lecture Lecture { get; set; }

		[Required, MaxLength(200)]
		public string ResourceName { get; set; }

		[MaxLength(500)]
		public string Description { get; set; }

		[Required]
		public string FilePath { get; set; }

		[Required]
		public string FileType { get; set; } // PDF, DOC, ZIP, etc

		public long FileSizeInBytes { get; set; }

		public Guid UploadedByTrainerId { get; set; }
		public Trainer UploadedByTrainer { get; set; }

		public int DownloadCount { get; set; } = 0;

		public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

		public bool IsDeleted { get; set; } = false;

		public ICollection<ResourceDownload> Downloads { get; set; } = new List<ResourceDownload>();
	}

	public class ResourceDownload
	{
		[Key]
		public Guid DownloadId { get; set; } = Guid.NewGuid();

		[Required]
		public Guid ResourceId { get; set; }
		public CourseResource Resource { get; set; }

		[Required]
		public Guid TraineeId { get; set; }
		public Trainee Trainee { get; set; }

		public DateTime DownloadedAt { get; set; } = DateTime.UtcNow;
	}
}
```

**2. تحديث Course و Lecture Models:**
```csharp
// في Course.cs
public ICollection<CourseResource> Resources { get; set; } = new List<CourseResource>();

// في Lecture.cs
public ICollection<CourseResource> Resources { get; set; } = new List<CourseResource>();
```

### 🗄️ Database Migration
```bash
dotnet ef migrations add AddResourcesLibrary
dotnet ef database update
```

### 🎮 Controller - جديد: ResourcesController.cs
```csharp
[Authorize(Roles = "Trainer")]
public class ResourcesController : Controller
{
	private readonly ApplicationDbContext _context;
	private readonly IWebHostEnvironment _environment;
	private readonly UserManager<ApplicationUser> _userManager;

	public ResourcesController(ApplicationDbContext context, 
		IWebHostEnvironment environment,
		UserManager<ApplicationUser> userManager)
	{
		_context = context;
		_environment = environment;
		_userManager = userManager;
	}

	[HttpPost]
	[ValidateAntiForgeryToken]
	public async Task<IActionResult> UploadResource(Guid courseId, Guid? lectureId, 
		string resourceName, string description, IFormFile file)
	{
		if (file == null || file.Length == 0)
			return BadRequest("الملف مطلوب");

		// التحقق من صلاحيات المدرب
		var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
		var trainer = await _context.Trainers.FirstOrDefaultAsync(t => t.UserId == userId);

		var courseTrainer = await _context.CourseTrainers
			.FirstOrDefaultAsync(ct => ct.CourseId == courseId && ct.TrainerId == trainer.TrainerId);

		if (courseTrainer == null)
			return Unauthorized("أنت لست مدرب هذه الدورة");

		// التحقق من نوع الملف المسموح
		var allowedExtensions = new[] { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".zip", ".txt", ".ppt", ".pptx" };
		var fileExtension = Path.GetExtension(file.FileName).ToLower();

		if (!allowedExtensions.Contains(fileExtension))
			return BadRequest("نوع الملف غير مسموح");

		// التحقق من حجم الملف (max 50 MB)
		if (file.Length > 50 * 1024 * 1024)
			return BadRequest("حجم الملف يجب أن يكون أقل من 50 MB");

		try
		{
			// إنشاء مجلد للموارد إذا لم يكن موجوداً
			var resourcesPath = Path.Combine(_environment.WebRootPath, "uploads", "resources", courseId.ToString());
			Directory.CreateDirectory(resourcesPath);

			// حفظ الملف مع اسم فريد
			var fileName = $"{Guid.NewGuid()}{fileExtension}";
			var filePath = Path.Combine(resourcesPath, fileName);

			using (var stream = new FileStream(filePath, FileMode.Create))
			{
				await file.CopyToAsync(stream);
			}

			// حفظ البيانات في قاعدة البيانات
			var resource = new CourseResource
			{
				CourseId = courseId,
				LectureId = lectureId,
				ResourceName = resourceName,
				Description = description,
				FilePath = $"/uploads/resources/{courseId}/{fileName}",
				FileType = fileExtension.TrimStart('.').ToUpper(),
				FileSizeInBytes = file.Length,
				UploadedByTrainerId = trainer.TrainerId
			};

			_context.CourseResources.Add(resource);
			await _context.SaveChangesAsync();

			return Ok(new { 
				message = "تم رفع الملف بنجاح",
				resourceId = resource.ResourceId 
			});
		}
		catch (Exception ex)
		{
			return StatusCode(500, $"خطأ في رفع الملف: {ex.Message}");
		}
	}

	[AllowAnonymous]
	[HttpGet]
	public async Task<IActionResult> GetCourseResources(Guid courseId, Guid? lectureId = null)
	{
		var query = _context.CourseResources
			.Where(r => r.CourseId == courseId && !r.IsDeleted)
			.Include(r => r.UploadedByTrainer.User);

		if (lectureId.HasValue)
			query = query.Where(r => r.LectureId == lectureId);

		var resources = await query
			.OrderByDescending(r => r.UploadedAt)
			.Select(r => new
			{
				r.ResourceId,
				r.ResourceName,
				r.Description,
				r.FileType,
				FileSizeInMB = (double)r.FileSizeInBytes / (1024 * 1024),
				r.DownloadCount,
				r.UploadedAt,
				UploadedByTrainer = r.UploadedByTrainer.User.FullName
			})
			.ToListAsync();

		return Ok(resources);
	}

	[Authorize(Roles = "Trainee")]
	[HttpGet]
	public async Task<IActionResult> DownloadResource(Guid resourceId)
	{
		var resource = await _context.CourseResources
			.FirstOrDefaultAsync(r => r.ResourceId == resourceId && !r.IsDeleted);

		if (resource == null)
			return NotFound("الملف غير موجود");

		// التحقق من أن الطالب مشترك في الدورة
		var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
		var trainee = await _context.Trainees.FirstOrDefaultAsync(t => t.UserId == userId);

		var isEnrolled = await _context.CourseTrainees
			.AnyAsync(ct => ct.CourseId == resource.CourseId && ct.TraineeId == trainee.TraineeId);

		if (!isEnrolled)
			return Unauthorized("أنت غير مشترك في هذه الدورة");

		// تسجيل التحميل
		var download = new ResourceDownload
		{
			ResourceId = resourceId,
			TraineeId = trainee.TraineeId
		};

		resource.DownloadCount++;
		_context.ResourceDownloads.Add(download);
		await _context.SaveChangesAsync();

		// إرجاع الملف
		var filePath = Path.Combine(_environment.WebRootPath, resource.FilePath.TrimStart('/'));

		if (!System.IO.File.Exists(filePath))
			return NotFound("الملف غير موجود على الخادم");

		var fileBytes = System.IO.File.ReadAllBytes(filePath);
		return File(fileBytes, "application/octet-stream", resource.ResourceName);
	}

	[Authorize(Roles = "Trainer")]
	[HttpDelete]
	public async Task<IActionResult> DeleteResource(Guid resourceId)
	{
		var resource = await _context.CourseResources.FirstOrDefaultAsync(r => r.ResourceId == resourceId);

		if (resource == null)
			return NotFound("الملف غير موجود");

		// التحقق من أن المدرب هو من رفع الملف
		var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
		var trainer = await _context.Trainers.FirstOrDefaultAsync(t => t.UserId == userId);

		if (resource.UploadedByTrainerId != trainer.TrainerId)
			return Unauthorized("أنت لم ترفع هذا الملف");

		resource.IsDeleted = true;
		await _context.SaveChangesAsync();

		return Ok(new { message = "تم حذف الملف بنجاح" });
	}
}
```

### 🎨 View - صفحة الموارد (Courses/Resources.cshtml)
```html
@model Course

<div class="container mt-4">
	<h2>مكتبة الموارد - @Model.CourseName</h2>

	@if (User.IsInRole("Trainer") && isTrainerOfCourse)
	{
		<div class="card mb-4">
			<div class="card-header">
				<h4>رفع ملف جديد</h4>
			</div>
			<div class="card-body">
				<form id="uploadForm" enctype="multipart/form-data">
					<div class="form-group">
						<label for="resourceName">اسم الملف:</label>
						<input type="text" class="form-control" id="resourceName" required>
					</div>

					<div class="form-group">
						<label for="description">الوصف (اختياري):</label>
						<textarea class="form-control" id="description" rows="3"></textarea>
					</div>

					<div class="form-group">
						<label for="lectureSelect">الدرس (اختياري):</label>
						<select class="form-control" id="lectureSelect">
							<option value="">-- اختر درس --</option>
							@foreach (var lecture in Model.Lectures.Where(l => !l.IsDeleted))
							{
								<option value="@lecture.LectureId">@lecture.Title</option>
							}
						</select>
					</div>

					<div class="form-group">
						<label for="fileInput">الملف (PDF, DOC, ZIP):</label>
						<input type="file" class="form-control-file" id="fileInput" required>
						<small class="text-muted">الحد الأقصى: 50 MB</small>
					</div>

					<button type="submit" class="btn btn-primary">رفع الملف</button>
				</form>
			</div>
		</div>
	}

	<div class="resources-list">
		<h4>الملفات المتاحة</h4>
		<div id="resourcesList" class="list-group">
			<!-- سيتم ملأها من JavaScript -->
		</div>
	</div>
</div>

<script>
const courseId = '@Model.CourseId';

// رفع الملف
document.getElementById('uploadForm').addEventListener('submit', async (e) => {
	e.preventDefault();

	const formData = new FormData();
	formData.append('courseId', courseId);
	formData.append('resourceName', document.getElementById('resourceName').value);
	formData.append('description', document.getElementById('description').value);
	formData.append('lectureId', document.getElementById('lectureSelect').value);
	formData.append('file', document.getElementById('fileInput').files[0]);

	try {
		const response = await fetch('/Resources/UploadResource', {
			method: 'POST',
			body: formData
		});

		if (response.ok) {
			alert('تم رفع الملف بنجاح!');
			document.getElementById('uploadForm').reset();
			loadResources();
		} else {
			const error = await response.json();
			alert('خطأ: ' + error);
		}
	} catch (error) {
		console.error('خطأ:', error);
	}
});

// تحميل قائمة الملفات
async function loadResources() {
	try {
		const response = await fetch(`/Resources/GetCourseResources?courseId=${courseId}`);
		const resources = await response.json();

		const list = document.getElementById('resourcesList');
		list.innerHTML = resources.map(r => `
			<div class="list-group-item">
				<div class="d-flex justify-content-between align-items-center">
					<div>
						<h6>📄 ${r.resourceName}</h6>
						<small>${r.description}</small>
						<p class="text-muted">
							${r.fileType} • ${r.fileSizeInMB.toFixed(2)} MB • 
							تحميلات: ${r.downloadCount}
						</p>
					</div>
					<button onclick="downloadResource('${r.resourceId}')" class="btn btn-sm btn-primary">
						تحميل
					</button>
				</div>
			</div>
		`).join('');
	} catch (error) {
		console.error('خطأ في تحميل الملفات:', error);
	}
}

function downloadResource(resourceId) {
	window.location.href = `/Resources/DownloadResource?resourceId=${resourceId}`;
}

// تحميل الملفات عند فتح الصفحة
loadResources();
</script>
```

---

---

# 🏆 الميزة 3: نظام الإنجازات والشارات (Achievements & Badges)
## الوقت المتوقع: 1.5 يوم

### 🎯 الوصف
- شارات مختلفة بناءً على إنجازات الطالب:
  - ✅ إكمال دورة
  - 🎯 الحصول على 90% فأعلى في الامتحانات
  - 🔥 حضور كامل (100%)
  - 💬 المشاركة النشطة في الدردشة
- عرض الشارات على ملف الطالب الشخصي

### 📊 البيانات المطلوبة

**1. إنشاء Models جديدة:**
```csharp
namespace TrainingCenterManagement_MVC.Models
{
	public class Badge
	{
		[Key]
		public Guid BadgeId { get; set; } = Guid.NewGuid();

		[Required, MaxLength(100)]
		public string BadgeName { get; set; }

		[MaxLength(500)]
		public string Description { get; set; }

		public string BadgeIcon { get; set; } // emoji or image path

		[Required]
		public BadgeType BadgeType { get; set; }

		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

		public ICollection<TraineeBadge> TraineeBadges { get; set; } = new List<TraineeBadge>();
	}

	public enum BadgeType
	{
		CourseCompletion,      // إكمال دورة
		HighScore,            // درجات عالية
		PerfectAttendance,    // حضور مثالي
		ActiveParticipant,    // مشارك نشط
		QuickLearner          // متعلم سريع
	}

	public class TraineeBadge
	{
		[Key]
		public Guid TraineeBadgeId { get; set; } = Guid.NewGuid();

		[Required]
		public Guid TraineeId { get; set; }
		public Trainee Trainee { get; set; }

		[Required]
		public Guid BadgeId { get; set; }
		public Badge Badge { get; set; }

		public DateTime EarnedAt { get; set; } = DateTime.UtcNow;

		[MaxLength(500)]
		public string Reason { get; set; }
	}
}
```

**2. تحديث Trainee Model:**
```csharp
public ICollection<TraineeBadge> Badges { get; set; } = new List<TraineeBadge>();
```

### 🗄️ Database Migration و Seeding
```bash
dotnet ef migrations add AddBadgesSystem
dotnet ef database update
```

**ملف Seeding:**
```csharp
// في SeedDataInitializer.cs
public static async Task SeedBadges(ApplicationDbContext context)
{
	if (context.Badges.Any())
		return;

	var badges = new[]
	{
		new Badge 
		{ 
			BadgeName = "خريج", 
			Description = "أكملت دورة بنجاح",
			BadgeIcon = "🎓",
			BadgeType = BadgeType.CourseCompletion 
		},
		new Badge 
		{ 
			BadgeName = "متفوق", 
			Description = "حصلت على 90% أو أكثر في الامتحانات",
			BadgeIcon = "⭐",
			BadgeType = BadgeType.HighScore 
		},
		new Badge 
		{ 
			BadgeName = "حاضر دائم", 
			Description = "حضور مثالي 100% في الدورة",
			BadgeIcon = "✅",
			BadgeType = BadgeType.PerfectAttendance 
		},
		new Badge 
		{ 
			BadgeName = "نشط", 
			Description = "مشاركة نشطة في الدردشة والمناقشات",
			BadgeIcon = "💬",
			BadgeType = BadgeType.ActiveParticipant 
		},
		new Badge 
		{ 
			BadgeName = "سريع التعلم", 
			Description = "أكملت الدورة في وقت قياسي",
			BadgeIcon = "⚡",
			BadgeType = BadgeType.QuickLearner 
		}
	};

	context.Badges.AddRange(badges);
	await context.SaveChangesAsync();
}
```

### 🎮 Service - جديد: BadgeService.cs
```csharp
public interface IBadgeService
{
	Task CheckAndAwardBadgesAsync(Guid traineeId, Guid courseId);
}

public class BadgeService : IBadgeService
{
	private readonly ApplicationDbContext _context;
	private readonly ILogger<BadgeService> _logger;

	public BadgeService(ApplicationDbContext context, ILogger<BadgeService> logger)
	{
		_context = context;
		_logger = logger;
	}

	public async Task CheckAndAwardBadgesAsync(Guid traineeId, Guid courseId)
	{
		try
		{
			// 1. التحقق من إكمال الدورة
			await CheckCourseCompletionBadge(traineeId, courseId);

			// 2. التحقق من الدرجات العالية
			await CheckHighScoreBadge(traineeId, courseId);

			// 3. التحقق من الحضور المثالي
			await CheckPerfectAttendanceBadge(traineeId, courseId);

			// 4. التحقق من المشاركة النشطة
			await CheckActiveParticipantBadge(traineeId, courseId);
		}
		catch (Exception ex)
		{
			_logger.LogError($"خطأ في منح الشارات: {ex.Message}");
		}
	}

	private async Task CheckCourseCompletionBadge(Guid traineeId, Guid courseId)
	{
		// التحقق من أن الطالب أكمل جميع الدروس
		var course = await _context.Courses
			.Include(c => c.Lectures)
			.FirstOrDefaultAsync(c => c.CourseId == courseId);

		var completedLectures = await _context.Presences
			.Where(p => p.Lecture.CourseId == courseId && 
					   p.TraineeId == traineeId && 
					   p.IsPresent)
			.CountAsync();

		if (completedLectures >= course.Lectures.Count)
		{
			await AwardBadgeAsync(traineeId, BadgeType.CourseCompletion, 
				$"أكملت دورة {course.CourseName}");
		}
	}

	private async Task CheckHighScoreBadge(Guid traineeId, Guid courseId)
	{
		// التحقق من أن الطالب حصل على 90% أو أكثر
		var averageScore = await _context.ExamAttempts
			.Where(ea => ea.Exam.CourseId == courseId && 
						ea.TraineeId == traineeId &&
						ea.Status == AttemptStatus.Completed)
			.AverageAsync(ea => ea.Score);

		if (averageScore >= 90)
		{
			await AwardBadgeAsync(traineeId, BadgeType.HighScore, 
				$"حصلت على متوسط {averageScore:F1}% في الدورة");
		}
	}

	private async Task CheckPerfectAttendanceBadge(Guid traineeId, Guid courseId)
	{
		// التحقق من حضور 100%
		var courseLectures = await _context.Lectures
			.Where(l => l.CourseId == courseId && !l.IsDeleted)
			.CountAsync();

		var attendedLectures = await _context.Presences
			.Where(p => p.Lecture.CourseId == courseId && 
					   p.TraineeId == traineeId && 
					   p.IsPresent)
			.CountAsync();

		if (courseLectures > 0 && attendedLectures == courseLectures)
		{
			await AwardBadgeAsync(traineeId, BadgeType.PerfectAttendance, 
				"حضور مثالي 100%");
		}
	}

	private async Task CheckActiveParticipantBadge(Guid traineeId, Guid courseId)
	{
		// التحقق من عدد الرسائل النشطة
		var messageCount = await _context.GroupMessages
			.Where(m => m.Course.CourseId == courseId && 
					   m.SenderId == traineeId)
			.CountAsync();

		if (messageCount >= 10) // 10 رسائل أو أكثر
		{
			await AwardBadgeAsync(traineeId, BadgeType.ActiveParticipant, 
				$"مشاركة نشطة مع {messageCount} رسالة");
		}
	}

	private async Task AwardBadgeAsync(Guid traineeId, BadgeType badgeType, string reason)
	{
		var badge = await _context.Badges
			.FirstOrDefaultAsync(b => b.BadgeType == badgeType);

		if (badge == null)
			return;

		// تحقق من أن الطالب لم يحصل على هذه الشارة من قبل
		var existingBadge = await _context.TraineeBadges
			.AnyAsync(tb => tb.TraineeId == traineeId && tb.BadgeId == badge.BadgeId);

		if (!existingBadge)
		{
			var traineeBadge = new TraineeBadge
			{
				TraineeId = traineeId,
				BadgeId = badge.BadgeId,
				Reason = reason
			};

			_context.TraineeBadges.Add(traineeBadge);
			await _context.SaveChangesAsync();

			_logger.LogInformation($"تم منح شارة {badge.BadgeName} للطالب {traineeId}");
		}
	}
}
```

### 🎨 View - الملف الشخصي (Profile/Index.cshtml)
```html
<!-- قسم الشارات -->
<div class="badges-section mt-4">
	<h3>🏆 الشارات المكتسبة</h3>

	<div class="badges-grid">
		@foreach (var badge in Model.Badges.OrderByDescending(b => b.EarnedAt))
		{
			<div class="badge-card">
				<div class="badge-icon">@Html.Raw(badge.Badge.BadgeIcon)</div>
				<h5>@badge.Badge.BadgeName</h5>
				<p class="badge-description">@badge.Badge.Description</p>
				<small class="text-muted">
					حصلت عليها: @badge.EarnedAt.ToShortDateString()
				</small>
				@if (!string.IsNullOrEmpty(badge.Reason))
				{
					<p class="badge-reason">@badge.Reason</p>
				}
			</div>
		}
	</div>
</div>

<style>
.badges-grid {
	display: grid;
	grid-template-columns: repeat(auto-fill, minmax(150px, 1fr));
	gap: 20px;
	margin-top: 20px;
}

.badge-card {
	background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
	border-radius: 10px;
	padding: 20px;
	text-align: center;
	color: white;
	box-shadow: 0 4px 6px rgba(0, 0, 0, 0.1);
	transition: transform 0.3s;
}

.badge-card:hover {
	transform: scale(1.05);
}

.badge-icon {
	font-size: 3rem;
	margin-bottom: 10px;
}

.badge-card h5 {
	margin: 10px 0 5px 0;
}

.badge-description {
	font-size: 0.85rem;
	opacity: 0.9;
}
</style>
```

---

---

# 💬 الميزة 4: التعليقات على الدروس (Lecture Comments)
## الوقت المتوقع: 1 يوم

### 🎯 الوصف
- الطلاب يمكنهم التعليق على كل درس
- المدرب يرد على التعليقات
- نظام الإعجاب (Like) على التعليقات
- إظهار التعليقات المرتبطة (Threads)

### 📊 البيانات المطلوبة

**1. إنشاء Model جديد:**
```csharp
namespace TrainingCenterManagement_MVC.Models
{
	public class LectureComment
	{
		[Key]
		public Guid CommentId { get; set; } = Guid.NewGuid();

		[Required]
		public Guid LectureId { get; set; }
		public Lecture Lecture { get; set; }

		[Required]
		public string UserId { get; set; }
		public ApplicationUser User { get; set; }

		[Required, MaxLength(1000)]
		public string Content { get; set; }

		public Guid? ParentCommentId { get; set; }
		public LectureComment ParentComment { get; set; }

		public ICollection<LectureComment> Replies { get; set; } = new List<LectureComment>();

		public int LikeCount { get; set; } = 0;

		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
		public DateTime? UpdatedAt { get; set; }

		public bool IsDeleted { get; set; } = false;

		public ICollection<LectureCommentLike> Likes { get; set; } = new List<LectureCommentLike>();
	}

	public class LectureCommentLike
	{
		[Key]
		public Guid LikeId { get; set; } = Guid.NewGuid();

		[Required]
		public Guid CommentId { get; set; }
		public LectureComment Comment { get; set; }

		[Required]
		public string UserId { get; set; }
		public ApplicationUser User { get; set; }

		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	}
}
```

**2. تحديث Lecture و ApplicationUser Models:**
```csharp
// في Lecture.cs
public ICollection<LectureComment> Comments { get; set; } = new List<LectureComment>();

// في ApplicationUser.cs
public ICollection<LectureComment> LectureComments { get; set; } = new List<LectureComment>();
public ICollection<LectureCommentLike> CommentLikes { get; set; } = new List<LectureCommentLike>();
```

### 🗄️ Database Migration
```bash
dotnet ef migrations add AddLectureCommentsSystem
dotnet ef database update
```

### 🎮 Controller - LectureCommentsController.cs
```csharp
[Authorize]
[Route("api/[controller]")]
[ApiController]
public class LectureCommentsController : ControllerBase
{
	private readonly ApplicationDbContext _context;
	private readonly UserManager<ApplicationUser> _userManager;

	public LectureCommentsController(ApplicationDbContext context, 
		UserManager<ApplicationUser> userManager)
	{
		_context = context;
		_userManager = userManager;
	}

	[HttpPost("add")]
	public async Task<IActionResult> AddComment(Guid lectureId, string content, Guid? parentCommentId = null)
	{
		if (string.IsNullOrWhiteSpace(content) || content.Length > 1000)
			return BadRequest("التعليق يجب أن يكون بين 1 و 1000 حرف");

		var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
		var lecture = await _context.Lectures.FindAsync(lectureId);

		if (lecture == null)
			return NotFound("الدرس غير موجود");

		var comment = new LectureComment
		{
			LectureId = lectureId,
			UserId = userId,
			Content = content,
			ParentCommentId = parentCommentId
		};

		_context.LectureComments.Add(comment);
		await _context.SaveChangesAsync();

		return Ok(new { 
			message = "تم إضافة التعليق بنجاح",
			commentId = comment.CommentId
		});
	}

	[HttpGet("lecture/{lectureId}")]
	[AllowAnonymous]
	public async Task<IActionResult> GetLectureComments(Guid lectureId)
	{
		var comments = await _context.LectureComments
			.Where(c => c.LectureId == lectureId && !c.IsDeleted && c.ParentCommentId == null)
			.Include(c => c.User)
			.Include(c => c.Replies.Where(r => !r.IsDeleted))
			.ThenInclude(r => r.User)
			.Include(c => c.Likes)
			.OrderByDescending(c => c.CreatedAt)
			.Select(c => new
			{
				c.CommentId,
				c.Content,
				c.LikeCount,
				c.CreatedAt,
				UserName = c.User.FullName,
				UserRole = _context.UserRoles
					.Where(ur => ur.UserId == c.UserId)
					.Select(ur => ur.RoleId)
					.FirstOrDefault(),
				Replies = c.Replies.Select(r => new
				{
					r.CommentId,
					r.Content,
					r.LikeCount,
					r.CreatedAt,
					UserName = r.User.FullName
				})
			})
			.ToListAsync();

		return Ok(comments);
	}

	[HttpPost("like/{commentId}")]
	public async Task<IActionResult> LikeComment(Guid commentId)
	{
		var comment = await _context.LectureComments.FindAsync(commentId);

		if (comment == null)
			return NotFound("التعليق غير موجود");

		var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

		var existingLike = await _context.LectureCommentLikes
			.FirstOrDefaultAsync(l => l.CommentId == commentId && l.UserId == userId);

		if (existingLike != null)
		{
			// حذف الإعجاب
			_context.LectureCommentLikes.Remove(existingLike);
			comment.LikeCount--;
		}
		else
		{
			// إضافة إعجاب
			var like = new LectureCommentLike
			{
				CommentId = commentId,
				UserId = userId
			};
			_context.LectureCommentLikes.Add(like);
			comment.LikeCount++;
		}

		await _context.SaveChangesAsync();

		return Ok(new { 
			liked = existingLike == null,
			likeCount = comment.LikeCount
		});
	}

	[HttpDelete("{commentId}")]
	public async Task<IActionResult> DeleteComment(Guid commentId)
	{
		var comment = await _context.LectureComments.FindAsync(commentId);

		if (comment == null)
			return NotFound("التعليق غير موجود");

		var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

		// تحقق من أن المستخدم هو صاحب التعليق أو مدرب
		var isTrainer = User.IsInRole("Trainer");

		if (comment.UserId != userId && !isTrainer)
			return Unauthorized("لا يمكنك حذف هذا التعليق");

		comment.IsDeleted = true;
		await _context.SaveChangesAsync();

		return Ok(new { message = "تم حذف التعليق بنجاح" });
	}
}
```

### 🎨 View - صفحة الدرس (Lectures/ViewLecture.cshtml)
```html
<!-- قسم التعليقات -->
<div class="comments-section mt-5">
	<h4>💬 التعليقات</h4>

	@if (User.Identity.IsAuthenticated)
	{
		<div class="card mb-4">
			<div class="card-body">
				<form id="commentForm">
					<textarea id="commentContent" class="form-control mb-2" 
						placeholder="أضف تعليقك..." rows="3" required></textarea>
					<button type="submit" class="btn btn-primary">إضافة التعليق</button>
				</form>
			</div>
		</div>
	}

	<div id="commentsList" class="comments-list">
		<!-- سيتم ملأها من JavaScript -->
	</div>
</div>

<style>
.comment-item {
	border-left: 3px solid #007bff;
	padding: 15px;
	margin-bottom: 15px;
	background-color: #f8f9fa;
	border-radius: 5px;
}

.comment-header {
	display: flex;
	justify-content: space-between;
	align-items: center;
	margin-bottom: 10px;
}

.comment-author {
	font-weight: bold;
	color: #007bff;
}

.comment-time {
	color: #999;
	font-size: 0.9rem;
}

.comment-actions {
	display: flex;
	gap: 10px;
	margin-top: 10px;
}

.reply {
	margin-left: 30px;
	margin-top: 10px;
}

.like-button {
	background: none;
	border: none;
	color: #999;
	cursor: pointer;
	font-size: 0.9rem;
}

.like-button.liked {
	color: #ff4444;
}
</style>

<script>
const lectureId = '@Model.LectureId';

document.getElementById('commentForm').addEventListener('submit', async (e) => {
	e.preventDefault();

	const content = document.getElementById('commentContent').value;

	try {
		const response = await fetch('/api/lecturecomments/add', {
			method: 'POST',
			headers: { 'Content-Type': 'application/json' },
			body: JSON.stringify({ lectureId, content })
		});

		if (response.ok) {
			document.getElementById('commentContent').value = '';
			loadComments();
		}
	} catch (error) {
		console.error('خطأ:', error);
	}
});

async function loadComments() {
	try {
		const response = await fetch(`/api/lecturecomments/lecture/${lectureId}`);
		const comments = await response.json();

		const list = document.getElementById('commentsList');
		list.innerHTML = comments.map(c => `
			<div class="comment-item">
				<div class="comment-header">
					<span class="comment-author">${c.userName}</span>
					<span class="comment-time">${new Date(c.createdAt).toLocaleDateString('ar-EG')}</span>
				</div>
				<p>${c.content}</p>
				<div class="comment-actions">
					<button class="like-button" onclick="likeComment('${c.commentId}')">
						❤️ ${c.likeCount}
					</button>
				</div>
				${c.replies.length > 0 ? `
					<div class="reply">
						${c.replies.map(r => `
							<div class="comment-item">
								<strong>${r.userName}</strong>: ${r.content}
							</div>
						`).join('')}
					</div>
				` : ''}
			</div>
		`).join('');
	} catch (error) {
		console.error('خطأ:', error);
	}
}

async function likeComment(commentId) {
	try {
		const response = await fetch(`/api/lecturecomments/like/${commentId}`, {
			method: 'POST'
		});

		if (response.ok) {
			loadComments();
		}
	} catch (error) {
		console.error('خطأ:', error);
	}
}

// تحميل التعليقات عند فتح الصفحة
loadComments();
</script>
```

---

---

# 📊 الميزة 5: تقارير الأداء المتقدمة (Advanced Performance Reports)
## الوقت المتوقع: 2 يوم

### 🎯 الوصف
- رسم بياني لأداء الطالب عبر الدورات المختلفة
- تقرير مفصل عن الضعف في مواضيع معينة
- تنبيهات تحذيرية عند انخفاض الأداء
- مقارنة أداء الطالب بمتوسط الفصل

### 📊 البيانات المطلوبة

**1. إنشاء Models جديدة:**
```csharp
namespace TrainingCenterManagement_MVC.Models
{
	public class StudentPerformanceReport
	{
		[Key]
		public Guid ReportId { get; set; } = Guid.NewGuid();

		[Required]
		public Guid TraineeId { get; set; }
		public Trainee Trainee { get; set; }

		[Required]
		public Guid CourseId { get; set; }
		public Course Course { get; set; }

		// الدرجات
		public decimal AverageExamScore { get; set; }
		public decimal AttendancePercentage { get; set; }
		public decimal OverallGPA { get; set; }

		// الإحصائيات
		public int TotalLectures { get; set; }
		public int AttendedLectures { get; set; }
		public int ExamsTaken { get; set; }

		// تحليل الأداء
		public string PerformanceLevel { get; set; } // Excellent, Good, Fair, Poor
		public string[] WeakTopics { get; set; } // الموضوعات الضعيفة

		public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
	}

	public class PerformanceAlert
	{
		[Key]
		public Guid AlertId { get; set; } = Guid.NewGuid();

		[Required]
		public Guid TraineeId { get; set; }
		public Trainee Trainee { get; set; }

		[Required]
		public Guid CourseId { get; set; }
		public Course Course { get; set; }

		[Required]
		public AlertType AlertType { get; set; }

		[Required, MaxLength(500)]
		public string AlertMessage { get; set; }

		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
		public bool IsResolved { get; set; } = false;
	}

	public enum AlertType
	{
		LowExamScore,          // درجات منخفضة في الامتحان
		MissingLectures,       // تغيب متكرر
		FailingCourse,         // احتمالية الرسوب
		PoorParticipation      // عدم المشاركة
	}
}
```

### 🎮 Service - PerformanceReportService.cs
```csharp
public interface IPerformanceReportService
{
	Task<StudentPerformanceReport> GenerateReportAsync(Guid traineeId, Guid courseId);
	Task CheckPerformanceAlertsAsync(Guid traineeId, Guid courseId);
}

public class PerformanceReportService : IPerformanceReportService
{
	private readonly ApplicationDbContext _context;
	private readonly ILogger<PerformanceReportService> _logger;

	public PerformanceReportService(ApplicationDbContext context, 
		ILogger<PerformanceReportService> logger)
	{
		_context = context;
		_logger = logger;
	}

	public async Task<StudentPerformanceReport> GenerateReportAsync(Guid traineeId, Guid courseId)
	{
		// حساب متوسط درجات الامتحانات
		var examScores = await _context.ExamAttempts
			.Where(ea => ea.Exam.CourseId == courseId && 
						ea.TraineeId == traineeId &&
						ea.Status == AttemptStatus.Completed)
			.Select(ea => ea.Score)
			.ToListAsync();

		var averageExamScore = examScores.Any() ? examScores.Average() : 0;

		// حساب نسبة الحضور
		var lectures = await _context.Lectures
			.Where(l => l.CourseId == courseId && !l.IsDeleted)
			.CountAsync();

		var attendedLectures = await _context.Presences
			.Where(p => p.Lecture.CourseId == courseId && 
					   p.TraineeId == traineeId && 
					   p.IsPresent)
			.CountAsync();

		var attendancePercentage = lectures > 0 ? (attendedLectures * 100M) / lectures : 0;

		// حساب GPA
		var gpa = (averageExamScore * 0.7M + attendancePercentage * 0.3M) / 100;

		// تحديد المستوى
		string performanceLevel = gpa >= 80 ? "متفوق" :
								  gpa >= 70 ? "جيد" :
								  gpa >= 60 ? "مقبول" : "ضعيف";

		// تحديد الموضوعات الضعيفة
		var weakTopics = await GetWeakTopicsAsync(traineeId, courseId);

		var report = new StudentPerformanceReport
		{
			TraineeId = traineeId,
			CourseId = courseId,
			AverageExamScore = (decimal)averageExamScore,
			AttendancePercentage = attendancePercentage,
			OverallGPA = gpa,
			TotalLectures = lectures,
			AttendedLectures = attendedLectures,
			ExamsTaken = examScores.Count,
			PerformanceLevel = performanceLevel,
			WeakTopics = weakTopics.ToArray()
		};

		return report;
	}

	public async Task CheckPerformanceAlertsAsync(Guid traineeId, Guid courseId)
	{
		var report = await GenerateReportAsync(traineeId, courseId);

		// حذف التنبيهات القديمة (بدون حل)
		var oldAlerts = await _context.PerformanceAlerts
			.Where(a => a.TraineeId == traineeId && 
					   a.CourseId == courseId && 
					   !a.IsResolved)
			.ToListAsync();

		_context.PerformanceAlerts.RemoveRange(oldAlerts);

		// إضافة تنبيهات جديدة بناءً على الأداء
		var alerts = new List<PerformanceAlert>();

		// تنبيه 1: درجات منخفضة
		if (report.AverageExamScore < 60)
		{
			alerts.Add(new PerformanceAlert
			{
				TraineeId = traineeId,
				CourseId = courseId,
				AlertType = AlertType.LowExamScore,
				AlertMessage = $"متوسط درجاتك منخفض جداً ({report.AverageExamScore:F1}%). برجاء مراجعة المواد التعليمية."
			});
		}

		// تنبيه 2: تغيب متكرر
		if (report.AttendancePercentage < 75)
		{
			alerts.Add(new PerformanceAlert
			{
				TraineeId = traineeId,
				CourseId = courseId,
				AlertType = AlertType.MissingLectures,
				AlertMessage = $"نسبة حضورك منخفضة ({report.AttendancePercentage:F1}%). حاول زيادة حضورك للدروس."
			});
		}

		// تنبيه 3: احتمالية الرسوب
		if (report.OverallGPA < 60)
		{
			alerts.Add(new PerformanceAlert
			{
				TraineeId = traineeId,
				CourseId = courseId,
				AlertType = AlertType.FailingCourse,
				AlertMessage = "أنت معرض لخطر الرسوب. برجاء التواصل مع مدربك للحصول على مساعدة."
			});
		}

		_context.PerformanceAlerts.AddRange(alerts);
		await _context.SaveChangesAsync();
	}

	private async Task<List<string>> GetWeakTopicsAsync(Guid traineeId, Guid courseId)
	{
		// حساب أقل الموضوعات أداءً
		var weakTopics = await _context.ExamAttempts
			.Where(ea => ea.Exam.CourseId == courseId && ea.TraineeId == traineeId)
			.Include(ea => ea.Exam)
			.Include(ea => ea.StudentAnswers)
			.ThenInclude(sa => sa.Question)
			.SelectMany(ea => ea.StudentAnswers)
			.Where(sa => !sa.IsCorrect)
			.Select(sa => sa.Question.Topic)
			.GroupBy(t => t)
			.OrderByDescending(g => g.Count())
			.Select(g => g.Key)
			.Take(3)
			.ToListAsync();

		return weakTopics;
	}
}
```

### 🎮 Controller - ReportsController.cs
```csharp
[Authorize(Roles = "Trainee")]
public class ReportsController : Controller
{
	private readonly IPerformanceReportService _reportService;
	private readonly ApplicationDbContext _context;
	private readonly UserManager<ApplicationUser> _userManager;

	public ReportsController(IPerformanceReportService reportService, 
		ApplicationDbContext context,
		UserManager<ApplicationUser> userManager)
	{
		_reportService = reportService;
		_context = context;
		_userManager = userManager;
	}

	public async Task<IActionResult> MyPerformance()
	{
		var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
		var trainee = await _context.Trainees.FirstOrDefaultAsync(t => t.UserId == userId);

		if (trainee == null)
			return NotFound();

		// الحصول على جميع الدورات المشترك فيها الطالب
		var courses = await _context.CourseTrainees
			.Where(ct => ct.TraineeId == trainee.TraineeId)
			.Include(ct => ct.Course)
			.Select(ct => ct.Course)
			.ToListAsync();

		var reports = new List<StudentPerformanceReport>();

		foreach (var course in courses)
		{
			var report = await _reportService.GenerateReportAsync(trainee.TraineeId, course.CourseId);
			reports.Add(report);

			// فحص التنبيهات
			await _reportService.CheckPerformanceAlertsAsync(trainee.TraineeId, course.CourseId);
		}

		return View(reports);
	}

	public async Task<IActionResult> CourseReport(Guid courseId)
	{
		var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
		var trainee = await _context.Trainees.FirstOrDefaultAsync(t => t.UserId == userId);

		if (trainee == null)
			return NotFound();

		var report = await _reportService.GenerateReportAsync(trainee.TraineeId, courseId);
		await _reportService.CheckPerformanceAlertsAsync(trainee.TraineeId, courseId);

		// الحصول على التنبيهات
		var alerts = await _context.PerformanceAlerts
			.Where(a => a.TraineeId == trainee.TraineeId && a.CourseId == courseId && !a.IsResolved)
			.ToListAsync();

		ViewBag.Alerts = alerts;
		return View(report);
	}

	[HttpGet("api/performance/{courseId}")]
	[AllowAnonymous]
	public async Task<IActionResult> GetPerformanceData(Guid courseId)
	{
		var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
		var trainee = await _context.Trainees.FirstOrDefaultAsync(t => t.UserId == userId);

		if (trainee == null)
			return Unauthorized();

		var report = await _reportService.GenerateReportAsync(trainee.TraineeId, courseId);

		return Ok(new
		{
			report.PerformanceLevel,
			report.AverageExamScore,
			report.AttendancePercentage,
			report.OverallGPA,
			report.ExamsTaken,
			report.AttendedLectures,
			report.TotalLectures,
			WeakTopics = report.WeakTopics
		});
	}
}
```

### 🎨 View - تقرير الأداء (Reports/MyPerformance.cshtml)
```html
<div class="container mt-4">
	<h2>📊 تقارير الأداء</h2>

	@if (ViewBag.Alerts?.Count > 0)
	{
		<div class="alert-section mb-4">
			<h4>⚠️ تنبيهات مهمة</h4>
			@foreach (var alert in ViewBag.Alerts)
			{
				<div class="alert alert-warning alert-dismissible fade show">
					<strong>@alert.AlertType.ToString()</strong>
					<p>@alert.AlertMessage</p>
					<small>@alert.CreatedAt.ToShortDateString()</small>
				</div>
			}
		</div>
	}

	<div class="reports-grid">
		@foreach (var report in Model)
		{
			<div class="card mb-4">
				<div class="card-header">
					<h5>@report.Course.CourseName</h5>
					<span class="badge badge-@GetBadgeClass(report.PerformanceLevel)">
						@report.PerformanceLevel
					</span>
				</div>
				<div class="card-body">
					<div class="performance-metrics">
						<div class="metric">
							<label>متوسط درجات الامتحانات:</label>
							<div class="progress">
								<div class="progress-bar" style="width: @report.AverageExamScore%">
									@report.AverageExamScore:F1%
								</div>
							</div>
						</div>

						<div class="metric">
							<label>نسبة الحضور:</label>
							<div class="progress">
								<div class="progress-bar" style="width: @report.AttendancePercentage%">
									@report.AttendancePercentage:F1%
								</div>
							</div>
						</div>

						<div class="metric">
							<label>المعدل الكلي (GPA):</label>
							<div class="progress">
								<div class="progress-bar" style="width: @report.OverallGPA%">
									@report.OverallGPA:F1/100
								</div>
							</div>
						</div>
					</div>

					<div class="statistics mt-3">
						<p>📚 الدروس المحضورة: @report.AttendedLectures / @report.TotalLectures</p>
						<p>📝 الامتحانات المجتازة: @report.ExamsTaken</p>
					</div>

					@if (report.WeakTopics.Any())
					{
						<div class="weak-topics mt-3">
							<h6>🔴 الموضوعات الضعيفة:</h6>
							<ul>
								@foreach (var topic in report.WeakTopics)
								{
									<li>@topic</li>
								}
							</ul>
						</div>
					}

					<a href="/Reports/CourseReport/@report.CourseId" class="btn btn-primary btn-sm mt-3">
						عرض التفاصيل الكاملة
					</a>
				</div>
			</div>
		}
	</div>
</div>

<style>
.reports-grid {
	display: grid;
	grid-template-columns: repeat(auto-fill, minmax(400px, 1fr));
	gap: 20px;
}

.performance-metrics {
	margin: 20px 0;
}

.metric {
	margin-bottom: 15px;
}

.metric label {
	display: block;
	margin-bottom: 5px;
	font-weight: bold;
}

.progress {
	background-color: #e9ecef;
	border-radius: 5px;
	overflow: hidden;
}

.progress-bar {
	height: 30px;
	background: linear-gradient(90deg, #667eea 0%, #764ba2 100%);
	display: flex;
	align-items: center;
	justify-content: center;
	color: white;
	font-weight: bold;
	transition: width 0.3s ease;
}

.weak-topics {
	background-color: #fff3cd;
	padding: 15px;
	border-radius: 5px;
	border-left: 4px solid #ffc107;
}

.weak-topics ul {
	list-style: none;
	padding: 0;
}

.weak-topics li {
	padding: 5px 0;
}

.badge-متفوق { background-color: #28a745; color: white; }
.badge-جيد { background-color: #17a2b8; color: white; }
.badge-مقبول { background-color: #ffc107; color: black; }
.badge-ضعيف { background-color: #dc3545; color: white; }
</style>

@functions {
	private string GetBadgeClass(string level)
	{
		return level switch
		{
			"متفوق" => "متفوق",
			"جيد" => "جيد",
			"مقبول" => "مقبول",
			_ => "ضعيف"
		};
	}
}
```

---

---

## 🚀 خطوات التطبيق الإجمالية:

### **الأسبوع الأول:**
- [ ] الميزة 1: نظام التقييمات (1 يوم)
- [ ] الميزة 2: مكتبة الموارد (1.5 يوم)
- [ ] الميزة 3: الشارات والإنجازات (1.5 يوم)

### **الأسبوع الثاني:**
- [ ] الميزة 4: التعليقات على الدروس (1 يوم)
- [ ] الميزة 5: تقارير الأداء (2 يوم)
- [ ] Testing والتحسينات (2 يوم)

---

## 📝 ملاحظات مهمة:

1. **Database Migrations**: لا تنسى تشغيل migrations بعد إضافة كل model
2. **Authorization**: تأكد من التحقق من الصلاحيات بشكل صحيح
3. **Validation**: أضف server-side validation على جميع inputs
4. **Error Handling**: أضف try-catch مناسبة
5. **Testing**: اختبر كل ميزة قبل الانتقال للـ feature التالية

---

## 💾 للبدء:
1. انسخ الـ Models والـ Services
2. أضفها للـ ApplicationDbContext
3. قم بإنشاء migrations
4. أضف الـ Controllers الجديدة
5. أنشئ الـ Views
6. سجل الخدمات في Program.cs

---

**هل تريد مساعدة في تطبيق أي ميزة؟** 🚀
