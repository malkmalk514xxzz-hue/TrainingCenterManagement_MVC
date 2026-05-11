# 📚 نظام إدارة المحاضرات الشامل (Advanced Lecture Management System)
## الوقت المتوقع: 3-4 أيام
**الأولوية**: عالية جداً | **الصعوبة**: متوسط إلى عالي

---

## 🎯 الرؤية الشاملة

تطوير نظام متكامل لإدارة المحاضرات يمكّن المدربين من:
1. **إنشاء محاضرات غنية بالمحتوى** (نص + فيديو + ملفات)
2. **إدارة الفيديوهات** (تحميل مباشر أو استيراد YouTube)
3. **إضافة الملفات الداعمة** (PDF, PPT, Word, Excel, وغيرها)
4. **تنظيم الموارد** بشكل منطقي وسهل الوصول
5. **تتبع استخدام الموارد** من قبل الطلاب

وتمكين الطلاب من:
1. **عرض محاضرة متكاملة** مع جميع الموارد
2. **تحميل الملفات** للاحتفاظ بنسختهم
3. **تتبع تقدمهم** (مشاهدة الفيديو + تحميل الملفات)
4. **البحث والفلترة** حسب نوع المحتوى

---

## 📊 المتطلبات الوظيفية

### أ. إدارة الفيديوهات (بالفعل موجود - تحسينات)
- ✅ تحميل فيديوهات مباشرة
- ✅ استيراد من YouTube
- ⚠️ **تحسين مطلوب**: عرض قائمة الفيديوهات بشكل أفضل

### ب. نظام الملفات الداعمة (NEW - ميزة جديدة)
- 📄 رفع ملفات (PDF, DOCX, PPTX, XLSX, ZIP, RAR)
- 🏷️ تصنيف الملفات (محاضرة، ملخص، واجب، حل، مشروع، إضافي)
- 📊 عرض معلومات الملف (الحجم، تاريخ الرفع، عدد التحميلات)
- ⬇️ تحميل الملفات من قبل الطلاب
- 🔐 التحكم بالوصول (يمكن إخفاء ملف معين)

### ج. تحسينات العرض (ENHANCEMENT)
- 📱 واجهة مستخدم حديثة (Grid + List view)
- 🔍 بحث وتصفية متقدمة
- 📌 ترتيب الموارد بالسحب والإفلات
- 📊 شريط معلومات (عدد الفيديوهات، الملفات، آخر تحديث)

### د. نظام التتبع (TRACKING)
- ▶️ تتبع مشاهدة الفيديو (من الأساس موجود)
- 📥 تتبع تحميل الملفات
- ⏱️ وقت قضاؤه على الدرس
- ✅ إكمال المحاضرة (عند مشاهدة جميع الفيديوهات)

---

## 🗄️ قاعدة البيانات - Models الجديدة

### 1. LectureResource Model (ملف الدعم)

```csharp
namespace TrainingCenterManagement_MVC.Models
{
	public class LectureResource
	{
		[Key]
		public Guid ResourceId { get; set; } = Guid.NewGuid();

		[Required]
		public Guid LectureId { get; set; }
		public Lecture Lecture { get; set; }

		/// <summary>
		/// اسم الملف الأصلي
		/// </summary>
		[Required, MaxLength(500)]
		public string FileName { get; set; }

		/// <summary>
		/// مسار الملف على الخادم
		/// </summary>
		[Required]
		public string FilePath { get; set; }

		/// <summary>
		/// نوع الملف (pdf, docx, pptx, xlsx, zip, rar, إلخ)
		/// </summary>
		[Required, MaxLength(50)]
		public string FileExtension { get; set; }

		/// <summary>
		/// حجم الملف بـ Bytes
		/// </summary>
		public long FileSizeInBytes { get; set; }

		/// <summary>
		/// تصنيف الملف
		/// </summary>
		[Required]
		public ResourceType ResourceType { get; set; }

		/// <summary>
		/// وصف الملف
		/// </summary>
		[MaxLength(1000)]
		public string Description { get; set; }

		/// <summary>
		/// ترتيب الملف في القائمة
		/// </summary>
		public int DisplayOrder { get; set; } = 1;

		/// <summary>
		/// عدد مرات التحميل
		/// </summary>
		public int DownloadCount { get; set; } = 0;

		/// <summary>
		/// هل الملف مرئي للطلاب
		/// </summary>
		public bool IsVisible { get; set; } = true;

		/// <summary>
		/// هل الملف مطلوب قبل الامتحان
		/// </summary>
		public bool IsRequired { get; set; } = false;

		/// <summary>
		/// من قام برفع الملف
		/// </summary>
		[Required]
		public Guid UploadedByTrainerId { get; set; }
		public Trainer UploadedByTrainer { get; set; }

		/// <summary>
		/// تاريخ الرفع
		/// </summary>
		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

		/// <summary>
		/// آخر تحديث
		/// </summary>
		public DateTime? UpdatedAt { get; set; }

		/// <summary>
		/// ملاحظات إدارية
		/// </summary>
		[MaxLength(500)]
		public string AdminNotes { get; set; }

		/// <summary>
		/// soft delete
		/// </summary>
		public bool IsDeleted { get; set; } = false;
		public DateTime? DeletedAt { get; set; }

		// Tracking
		public ICollection<ResourceDownload> Downloads { get; set; } = new List<ResourceDownload>();
	}

	/// <summary>
	/// تصنيفات الملفات الداعمة
	/// </summary>
	public enum ResourceType
	{
		/// <summary>
		/// شرائح المحاضرة (PowerPoint)
		/// </summary>
		[Display(Name = "شرائح المحاضرة")]
		LectureSlides = 0,

		/// <summary>
		/// ملفات ملخص أو ملاحظات
		/// </summary>
		[Display(Name = "ملاحظات")]
		Notes = 1,

		/// <summary>
		/// واجب منزلي
		/// </summary>
		[Display(Name = "واجب")]
		Assignment = 2,

		/// <summary>
		/// حل الواجب أو الامتحان السابق
		/// </summary>
		[Display(Name = "الحل")]
		Solution = 3,

		/// <summary>
		/// ملفات المشروع
		/// </summary>
		[Display(Name = "ملفات المشروع")]
		ProjectFiles = 4,

		/// <summary>
		/// مراجع إضافية
		/// </summary>
		[Display(Name = "مراجع إضافية")]
		Reference = 5,

		/// <summary>
		/// أكواد برمجية
		/// </summary>
		[Display(Name = "أكواد")]
		Code = 6,

		/// <summary>
		/// ملفات أخرى
		/// </summary>
		[Display(Name = "ملفات أخرى")]
		Other = 7
	}
}
```

### 2. ResourceDownload Model (تتبع التحميلات)

```csharp
namespace TrainingCenterManagement_MVC.Models
{
	public class ResourceDownload
	{
		[Key]
		public Guid DownloadId { get; set; } = Guid.NewGuid();

		[Required]
		public Guid ResourceId { get; set; }
		public LectureResource Resource { get; set; }

		[Required]
		public Guid TraineeId { get; set; }
		public Trainee Trainee { get; set; }

		/// <summary>
		/// وقت التحميل
		/// </summary>
		public DateTime DownloadedAt { get; set; } = DateTime.UtcNow;

		/// <summary>
		/// عنوان IP المُحمل من
		/// </summary>
		public string IpAddress { get; set; }

		/// <summary>
		/// معلومات جهاز المتصفح
		/// </summary>
		public string UserAgent { get; set; }
	}
}
```

### 3. LectureSession Model (تتبع جلسات الطالب)

```csharp
namespace TrainingCenterManagement_MVC.Models
{
	public class LectureSession
	{
		[Key]
		public Guid SessionId { get; set; } = Guid.NewGuid();

		[Required]
		public Guid LectureId { get; set; }
		public Lecture Lecture { get; set; }

		[Required]
		public Guid TraineeId { get; set; }
		public Trainee Trainee { get; set; }

		/// <summary>
		/// وقت بداية الزيارة
		/// </summary>
		public DateTime StartedAt { get; set; } = DateTime.UtcNow;

		/// <summary>
		/// وقت نهاية الزيارة
		/// </summary>
		public DateTime? EndedAt { get; set; }

		/// <summary>
		/// الوقت الإجمالي بالثواني
		/// </summary>
		public int TotalDurationSeconds { get; set; } = 0;

		/// <summary>
		/// هل اكتمل الدرس (شاهد كل الفيديوهات)
		/// </summary>
		public bool IsCompleted { get; set; } = false;

		/// <summary>
		/// نسبة الإكمال
		/// </summary>
		public double CompletionPercentage { get; set; } = 0;
	}
}
```

---

## 🔧 تحديثات ApplicationDbContext

```csharp
// في DbSet declarations:
public DbSet<LectureResource> LectureResources { get; set; }
public DbSet<ResourceDownload> ResourceDownloads { get; set; }
public DbSet<LectureSession> LectureSessions { get; set; }

// في OnModelCreating:
// ──────────────────────────────────────────────────────
// LECTURE RESOURCES
// ──────────────────────────────────────────────────────

builder.Entity<LectureResource>()
	.HasOne(lr => lr.Lecture)
	.WithMany(l => l.Resources)
	.HasForeignKey(lr => lr.LectureId)
	.OnDelete(DeleteBehavior.Cascade);

builder.Entity<LectureResource>()
	.HasOne(lr => lr.UploadedByTrainer)
	.WithMany(t => t.LectureResources)
	.HasForeignKey(lr => lr.UploadedByTrainerId)
	.OnDelete(DeleteBehavior.Restrict);

builder.Entity<LectureResource>()
	.HasQueryFilter(lr => !lr.IsDeleted);

builder.Entity<LectureResource>()
	.HasIndex(lr => lr.LectureId)
	.HasDatabaseName("IX_LectureResources_LectureId");

builder.Entity<LectureResource>()
	.HasIndex(lr => new { lr.LectureId, lr.ResourceType })
	.HasDatabaseName("IX_LectureResources_LectureId_Type");

// ──────────────────────────────────────────────────────
// RESOURCE DOWNLOADS
// ──────────────────────────────────────────────────────

builder.Entity<ResourceDownload>()
	.HasOne(rd => rd.Resource)
	.WithMany(r => r.Downloads)
	.HasForeignKey(rd => rd.ResourceId)
	.OnDelete(DeleteBehavior.Cascade);

builder.Entity<ResourceDownload>()
	.HasOne(rd => rd.Trainee)
	.WithMany(t => t.ResourceDownloads)
	.HasForeignKey(rd => rd.TraineeId)
	.OnDelete(DeleteBehavior.Restrict);

builder.Entity<ResourceDownload>()
	.HasIndex(rd => new { rd.ResourceId, rd.TraineeId })
	.HasDatabaseName("IX_ResourceDownload_ResourceId_TraineeId");

// ──────────────────────────────────────────────────────
// LECTURE SESSIONS
// ──────────────────────────────────────────────────────

builder.Entity<LectureSession>()
	.HasOne(ls => ls.Lecture)
	.WithMany(l => l.Sessions)
	.HasForeignKey(ls => ls.LectureId)
	.OnDelete(DeleteBehavior.Cascade);

builder.Entity<LectureSession>()
	.HasOne(ls => ls.Trainee)
	.WithMany(t => t.LectureSessions)
	.HasForeignKey(ls => ls.TraineeId)
	.OnDelete(DeleteBehavior.Restrict);

builder.Entity<LectureSession>()
	.HasIndex(ls => new { ls.LectureId, ls.TraineeId })
	.HasDatabaseName("IX_LectureSession_LectureId_TraineeId");
```

---

## 🔄 تحديثات Models الموجودة

### Lecture Model
```csharp
// أضف هذه Collections:
public ICollection<LectureResource> Resources { get; set; } = new List<LectureResource>();
public ICollection<LectureSession> Sessions { get; set; } = new List<LectureSession>();
```

### Trainer Model
```csharp
// أضف:
public ICollection<LectureResource> LectureResources { get; set; } = new List<LectureResource>();
```

### Trainee Model
```csharp
// أضف:
public ICollection<ResourceDownload> ResourceDownloads { get; set; } = new List<ResourceDownload>();
public ICollection<LectureSession> LectureSessions { get; set; } = new List<LectureSession>();
```

---

## 🎮 Services المطلوبة

### 1. ILectureResourceService & LectureResourceService

```csharp
public interface ILectureResourceService
{
	// Upload
	Task<LectureResource> UploadResourceAsync(
		IFormFile file, 
		Guid lectureId, 
		Guid trainerId, 
		ResourceType resourceType,
		string description = null,
		bool isRequired = false);

	// Get Resources
	Task<List<LectureResource>> GetLectureResourcesAsync(Guid lectureId);
	Task<LectureResource> GetResourceAsync(Guid resourceId);

	// Delete
	Task<bool> DeleteResourceAsync(Guid resourceId, Guid trainerId);

	// Update
	Task<bool> UpdateResourceAsync(
		Guid resourceId, 
		ResourceType? newType,
		string newDescription,
		bool? isRequired,
		bool? isVisible);

	// Download
	Task<(byte[] fileBytes, string fileName)> DownloadResourceAsync(
		Guid resourceId, 
		Guid traineeId);

	// Statistics
	Task<Dictionary<ResourceType, int>> GetResourceStatisticsAsync(Guid lectureId);
}

public class LectureResourceService : ILectureResourceService
{
	private readonly ApplicationDbContext _context;
	private readonly IWebHostEnvironment _environment;
	private readonly ILogger<LectureResourceService> _logger;
	private const long MAX_FILE_SIZE = 500 * 1024 * 1024; // 500 MB
	private readonly string[] _allowedExtensions = 
	{ 
		".pdf", ".docx", ".doc", ".pptx", ".ppt", ".xlsx", ".xls", 
		".txt", ".zip", ".rar", ".jpg", ".png", ".gif", ".mp4", ".avi" 
	};

	public LectureResourceService(
		ApplicationDbContext context, 
		IWebHostEnvironment environment,
		ILogger<LectureResourceService> logger)
	{
		_context = context;
		_environment = environment;
		_logger = logger;
	}

	public async Task<LectureResource> UploadResourceAsync(
		IFormFile file, 
		Guid lectureId, 
		Guid trainerId, 
		ResourceType resourceType,
		string description = null,
		bool isRequired = false)
	{
		// Validate lecture exists
		var lecture = await _context.Lectures.FindAsync(lectureId);
		if (lecture == null)
			throw new InvalidOperationException("المحاضرة غير موجودة");

		// Validate file
		if (file == null || file.Length == 0)
			throw new InvalidOperationException("الملف مطلوب");

		var fileExtension = Path.GetExtension(file.FileName).ToLower();
		if (!_allowedExtensions.Contains(fileExtension))
			throw new InvalidOperationException($"نوع الملف {fileExtension} غير مسموح");

		if (file.Length > MAX_FILE_SIZE)
			throw new InvalidOperationException("حجم الملف أكبر من 500 MB");

		try
		{
			// Create upload directory
			var uploadsPath = Path.Combine(
				_environment.WebRootPath, 
				"uploads", 
				"resources", 
				lectureId.ToString());
			Directory.CreateDirectory(uploadsPath);

			// Save file with unique name
			var fileName = $"{Guid.NewGuid()}{fileExtension}";
			var filePath = Path.Combine(uploadsPath, fileName);

			using (var stream = new FileStream(filePath, FileMode.Create))
			{
				await file.CopyToAsync(stream);
			}

			// Get next display order
			var nextOrder = await _context.LectureResources
				.Where(r => r.LectureId == lectureId && !r.IsDeleted)
				.MaxAsync(r => (int?)r.DisplayOrder) ?? 0;

			// Create resource record
			var resource = new LectureResource
			{
				LectureId = lectureId,
				FileName = file.FileName,
				FilePath = filePath,
				FileExtension = fileExtension,
				FileSizeInBytes = file.Length,
				ResourceType = resourceType,
				Description = description,
				IsRequired = isRequired,
				DisplayOrder = nextOrder + 1,
				UploadedByTrainerId = trainerId
			};

			_context.LectureResources.Add(resource);
			await _context.SaveChangesAsync();

			_logger.LogInformation($"تم رفع الملف: {resource.ResourceId}");
			return resource;
		}
		catch (Exception ex)
		{
			_logger.LogError($"خطأ في رفع الملف: {ex.Message}");
			throw;
		}
	}

	public async Task<List<LectureResource>> GetLectureResourcesAsync(Guid lectureId)
	{
		return await _context.LectureResources
			.Where(r => r.LectureId == lectureId && r.IsVisible && !r.IsDeleted)
			.OrderBy(r => r.DisplayOrder)
			.ToListAsync();
	}

	public async Task<LectureResource> GetResourceAsync(Guid resourceId)
	{
		return await _context.LectureResources
			.FirstOrDefaultAsync(r => r.ResourceId == resourceId && !r.IsDeleted);
	}

	public async Task<bool> DeleteResourceAsync(Guid resourceId, Guid trainerId)
	{
		var resource = await _context.LectureResources.FindAsync(resourceId);
		if (resource == null)
			return false;

		if (resource.UploadedByTrainerId != trainerId)
			throw new UnauthorizedAccessException("لا تملك صلاحية حذف هذا الملف");

		try
		{
			resource.IsDeleted = true;
			resource.DeletedAt = DateTime.UtcNow;

			// Delete file from disk
			if (File.Exists(resource.FilePath))
				File.Delete(resource.FilePath);

			await _context.SaveChangesAsync();
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError($"خطأ في حذف الملف: {ex.Message}");
			throw;
		}
	}

	public async Task<bool> UpdateResourceAsync(
		Guid resourceId, 
		ResourceType? newType,
		string newDescription,
		bool? isRequired,
		bool? isVisible)
	{
		var resource = await _context.LectureResources.FindAsync(resourceId);
		if (resource == null)
			return false;

		if (newType.HasValue)
			resource.ResourceType = newType.Value;

		if (!string.IsNullOrWhiteSpace(newDescription))
			resource.Description = newDescription;

		if (isRequired.HasValue)
			resource.IsRequired = isRequired.Value;

		if (isVisible.HasValue)
			resource.IsVisible = isVisible.Value;

		resource.UpdatedAt = DateTime.UtcNow;
		await _context.SaveChangesAsync();
		return true;
	}

	public async Task<(byte[] fileBytes, string fileName)> DownloadResourceAsync(
		Guid resourceId, 
		Guid traineeId)
	{
		var resource = await _context.LectureResources.FindAsync(resourceId);
		if (resource == null)
			throw new InvalidOperationException("الملف غير موجود");

		if (!resource.IsVisible)
			throw new UnauthorizedAccessException("هذا الملف غير متاح");

		try
		{
			// Read file
			var fileBytes = await File.ReadAllBytesAsync(resource.FilePath);

			// Record download
			var download = new ResourceDownload
			{
				ResourceId = resourceId,
				TraineeId = traineeId,
				IpAddress = "0.0.0.0", // يتم تعيينه من Controller
				UserAgent = "" // يتم تعيينه من Controller
			};

			_context.ResourceDownloads.Add(download);
			resource.DownloadCount++;
			await _context.SaveChangesAsync();

			return (fileBytes, resource.FileName);
		}
		catch (Exception ex)
		{
			_logger.LogError($"خطأ في تحميل الملف: {ex.Message}");
			throw;
		}
	}

	public async Task<Dictionary<ResourceType, int>> GetResourceStatisticsAsync(Guid lectureId)
	{
		var stats = await _context.LectureResources
			.Where(r => r.LectureId == lectureId && !r.IsDeleted)
			.GroupBy(r => r.ResourceType)
			.Select(g => new { Type = g.Key, Count = g.Count() })
			.ToListAsync();

		return stats.ToDictionary(x => x.Type, x => x.Count);
	}
}
```

---

## 🎮 Controllers

### LectureResourcesController.cs

```csharp
[Authorize(Roles = "Trainer,Admin")]
[Route("api/[controller]")]
[ApiController]
public class LectureResourcesController : ControllerBase
{
	private readonly ILectureResourceService _resourceService;
	private readonly ApplicationDbContext _context;
	private readonly UserManager<ApplicationUser> _userManager;
	private readonly ILogger<LectureResourcesController> _logger;

	public LectureResourcesController(
		ILectureResourceService resourceService,
		ApplicationDbContext context,
		UserManager<ApplicationUser> userManager,
		ILogger<LectureResourcesController> logger)
	{
		_resourceService = resourceService;
		_context = context;
		_userManager = userManager;
		_logger = logger;
	}

	[AllowAnonymous]
	[HttpGet("lecture/{lectureId}")]
	public async Task<IActionResult> GetLectureResources(Guid lectureId)
	{
		var resources = await _resourceService.GetLectureResourcesAsync(lectureId);
		var result = resources.Select(r => new
		{
			r.ResourceId,
			r.FileName,
			r.FileExtension,
			FileSizeInMB = (r.FileSizeInBytes / (1024.0 * 1024.0)).ToString("F2"),
			r.ResourceType,
			r.Description,
			r.DownloadCount,
			r.CreatedAt,
			r.IsRequired
		});

		return Ok(result);
	}

	[HttpPost("upload")]
	public async Task<IActionResult> UploadResource(
		[FromQuery] Guid lectureId,
		[FromForm] int resourceType,
		[FromForm] string description,
		[FromForm] bool isRequired,
		[FromForm] IFormFile file)
	{
		try
		{
			var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
			var trainer = await _context.Trainers.FirstOrDefaultAsync(t => t.UserId == userId);

			if (trainer == null)
				return Unauthorized("المستخدم ليس مدرباً");

			var type = (ResourceType)resourceType;
			var resource = await _resourceService.UploadResourceAsync(
				file, lectureId, trainer.TrainerId, type, description, isRequired);

			return Ok(new
			{
				success = true,
				message = "تم رفع الملف بنجاح",
				resourceId = resource.ResourceId
			});
		}
		catch (Exception ex)
		{
			_logger.LogError($"خطأ: {ex.Message}");
			return BadRequest(new { success = false, message = ex.Message });
		}
	}

	[HttpDelete("{resourceId}")]
	public async Task<IActionResult> DeleteResource(Guid resourceId)
	{
		try
		{
			var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
			var trainer = await _context.Trainers.FirstOrDefaultAsync(t => t.UserId == userId);

			if (trainer == null)
				return Unauthorized("المستخدم ليس مدرباً");

			var success = await _resourceService.DeleteResourceAsync(resourceId, trainer.TrainerId);
			return success 
				? Ok(new { success = true, message = "تم حذف الملف" })
				: NotFound();
		}
		catch (Exception ex)
		{
			return BadRequest(new { success = false, message = ex.Message });
		}
	}

	[Authorize(Roles = "Trainee")]
	[HttpGet("download/{resourceId}")]
	public async Task<IActionResult> DownloadResource(Guid resourceId)
	{
		try
		{
			var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
			var trainee = await _context.Trainees.FirstOrDefaultAsync(t => t.UserId == userId);

			if (trainee == null)
				return Unauthorized();

			var (fileBytes, fileName) = await _resourceService.DownloadResourceAsync(resourceId, trainee.TraineeId);

			var contentType = GetMimeType(Path.GetExtension(fileName));
			return File(fileBytes, contentType, fileName);
		}
		catch (Exception ex)
		{
			return BadRequest(new { message = ex.Message });
		}
	}

	private string GetMimeType(string fileExtension)
	{
		return fileExtension.ToLower() switch
		{
			".pdf" => "application/pdf",
			".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
			".doc" => "application/msword",
			".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
			".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
			".zip" => "application/zip",
			".rar" => "application/x-rar-compressed",
			".txt" => "text/plain",
			_ => "application/octet-stream"
		};
	}
}
```

---

## 📋 Views المطلوبة

### Views/Lectures/Edit.cshtml (تحسينات)
- إضافة صفحة إدارة الملفات
- قسم لرفع ملفات جديدة
- قائمة بالملفات الموجودة مع أزرار الحذف والتعديل

### Views/Lectures/Details.cshtml (تحسينات)
- عرض جميع الملفات حسب نوعها
- أزرار تحميل بسيطة
- عرض عدد مرات التحميل
- شريط معلومات (عدد الفيديوهات، الملفات، إلخ)

### Views/LectureResources/Index.cshtml (NEW)
```html
<div class="lecture-resources-container">
	<!-- Statistics Bar -->
	<div class="resources-stats">
		<span>📹 الفيديوهات: {{ videosCount }}</span>
		<span>📄 الملفات: {{ filesCount }}</span>
		<span>📊 آخر تحديث: {{ lastUpdate }}</span>
	</div>

	<!-- Filter/Search -->
	<div class="resources-filters">
		<input type="search" placeholder="ابحث عن ملف...">
		<select id="typeFilter">
			<option value="">جميع الأنواع</option>
			<option value="0">شرائح المحاضرة</option>
			<option value="1">ملاحظات</option>
			<option value="2">واجب</option>
			<!-- ... -->
		</select>
	</div>

	<!-- Videos Section -->
	<div class="videos-section">
		<h3>📹 الفيديوهات</h3>
		<div class="videos-grid">
			<!-- فيديو 1 -->
			<!-- فيديو 2 -->
		</div>
	</div>

	<!-- Resources Section by Type -->
	<div class="resources-section">
		<h3>📄 الملفات الداعمة</h3>

		<!-- Lecture Slides -->
		<div class="resource-category">
			<h4>شرائح المحاضرة</h4>
			<div class="resource-list">
				<!-- ملف 1 -->
				<!-- ملف 2 -->
			</div>
		</div>

		<!-- Notes -->
		<div class="resource-category">
			<h4>الملاحظات</h4>
			<!-- ... -->
		</div>

		<!-- Assignments -->
		<!-- ... -->
	</div>
</div>
```

---

## 🗄️ Database Migration

```bash
dotnet ef migrations add AddLectureResourceManagementSystem
dotnet ef database update
```

---

## ⚙️ Program.cs Registration

```csharp
builder.Services.AddScoped<ILectureResourceService, LectureResourceService>();
```

---

## 📋 قائمة الاقتراحات الإضافية

### 1. **عرض متقدم للمحاضرة**
- ✅ Tab view (معلومات + فيديوهات + ملفات + تعليقات)
- ✅ Side panel يظهر الموارد المتاحة
- ✅ Progress bar لإكمال المحاضرة

### 2. **نظام التقييمات والتعليقات**
- إضافة قسم تعليقات على المحاضرة
- تقييم المحاضرة من قبل الطلاب
- رد المدرب على التعليقات

### 3. **إحصائيات متقدمة**
- عدد الطلاب الذين شاهدوا كل فيديو
- عدد الطلاب الذين حملوا كل ملف
- متوسط وقت المشاهدة
- نسبة إكمال المحاضرة

### 4. **Search & Filters المتقدمة**
- بحث في عناوين الملفات
- تصفية حسب النوع والتاريخ والحجم
- ترتيب حسب الاسم أو التاريخ أو عدد التحميلات

### 5. **نظام الإشعارات**
- إخطار الطلاب بملفات جديدة
- إخطار المدرب بتحميل الطالب للملفات
- تنبيهات الامتحانات

### 6. **Drag & Drop لترتيب الموارد**
- سحب وإفلات الملفات لتغيير الترتيب
- سحب وإفلات الفيديوهات لإعادة ترتيبها

### 7. **Preview للملفات**
- معاينة الصور
- معاينة النصوص
- معاينة PDF في المتصفح

### 8. **نظام الأصول (Assets) المتقدم**
- Compression تلقائي للصور والفيديوهات
- Streaming للفيديوهات الكبيرة
- CDN integration للملفات الثقيلة

### 9. **Access Control متقدم**
- تحديد تاريخ انتهاء الوصول للملف
- تحديد أيام معينة لرؤية الملف
- تحديد مجموعات معينة من الطلاب

### 10. **Integration مع الامتحان**
- ربط الملفات بالامتحانات
- عدم السماح بالامتحان إلا بعد مشاهدة الفيديو
- إجبارية تحميل الملفات قبل البدء

---

## 📊 جدول التنفيذ الموصى به

| المرحلة | المهام | الوقت |
|--------|--------|-------|
| **1** | إنشاء Models + Migration | 1 يوم |
| **2** | Service + Controller للملفات | 1 يوم |
| **3** | Views وواجهة المستخدم | 1 يوم |
| **4** | نظام التتبع والإحصائيات | 1 يوم |
| **5** | التحسينات والاختبار | 1 يوم |

---

## ✅ معايير القبول

- ✅ يمكن للمدرب رفع ملفات متعددة الأنواع
- ✅ يمكن للطالب تحميل الملفات
- ✅ تتبع التحميلات والمشاهدات
- ✅ واجهة سهلة وجذابة
- ✅ لا توجد أخطاء في عملية الرفع أو التحميل
- ✅ الملفات الكبيرة تُرفع بشكل آمن
- ✅ البحث والتصفية يعمل بشكل صحيح

---

## 🚀 الفوائد المتوقعة

1. **تحسين تجربة المدرب**: إدارة أسهل للمحاضرات والموارد
2. **تحسين تجربة الطالب**: الوصول السهل لجميع الموارد المطلوبة
3. **تتبع أفضل**: معرفة من قام بتحميل الملفات ومتى
4. **تنظيم أفضل**: تصنيف الملفات حسب النوع
5. **زيادة الكفاءة**: وقت أقل في البحث عن الملفات
6. **مرونة أكثر**: تحكم كامل على الوصول للملفات

---

**هل تريد البدء بتطبيق هذا النظام؟** 🚀

