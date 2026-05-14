# 🤖 نظام المساعد الذكي (AI Assistant System)
## الوقت المتوقع: 5-7 أيام
**الأولوية**: عالية جداً | **الصعوبة**: عالي

---

## 🎯 الرؤية الشاملة

إضافة **مساعد ذكي (AI Assistant)** إلى النظام يمكّنه من:

### للمستخدم (واجهة المساعد):
1. **الإجابة على الأسئلة العامة** عن النظام
2. **الإجابة على أسئلة شخصية** متعلقة به فقط
3. **المساعدة في الملاحة** والبحث
4. **شرح الميزات** والوظائف
5. **إعطاء توصيات شخصية** بناء على بيانات المستخدم

### للنظام (الأمان والخصوصية):
1. **التحكم بالصلاحيات** حسب دور المستخدم
2. **عدم الكشف عن بيانات المستخدمين الآخرين**
3. **تسجيل جميع الاستفسارات** للتدقيق
4. **تقييد الوصول** حسب الأدوار
5. **منع التطفل** على خصوصية الآخرين

---

## 🏗️ المعمارية الشاملة

```
┌─────────────────────────────────────────────────────────────┐
│                        USER INTERFACE                        │
│                                                              │
│  ┌────────────────────────────────────────────────────┐    │
│  │          AI Chat Widget                            │    │
│  │  ┌──────────────────────────────────────────┐     │    │
│  │  │  Messages / Conversation                │     │    │
│  │  ├──────────────────────────────────────────┤     │    │
│  │  │  Input Field + Send Button              │     │    │
│  │  └──────────────────────────────────────────┘     │    │
│  │  Typing indicators, Loading states              │    │
│  └────────────────────────────────────────────────────┘    │
│                                                              │
└─────────────────────────────────────────────────────────────┘
							↓
┌─────────────────────────────────────────────────────────────┐
│                  API LAYER (SignalR/REST)                    │
│                                                              │
│  POST /api/ai/ask                                           │
│  POST /api/ai/chat                                          │
│  GET  /api/ai/history/{userId}                             │
│  POST /api/ai/feedback                                      │
│                                                              │
└─────────────────────────────────────────────────────────────┘
							↓
┌─────────────────────────────────────────────────────────────┐
│                  AUTH & VALIDATION LAYER                     │
│                                                              │
│  Authorization (IsUserAuthorized)                           │
│  RoleBasedFilter (بناء على دور المستخدم)                   │
│  DataAccessFilter (تصفية البيانات المسموح بها)              │
│                                                              │
└─────────────────────────────────────────────────────────────┘
							↓
┌─────────────────────────────────────────────────────────────┐
│                  AI PROCESSING LAYER                         │
│                                                              │
│  ┌─────────────────────────────────────────────────┐       │
│  │ Question Classifier (تصنيف السؤال)              │       │
│  │ - General Question (عام)                        │       │
│  │ - Personal Question (شخصي)                      │       │
│  │ - System Navigation (ملاحة)                     │       │
│  │ - Data Request (طلب بيانات)                     │       │
│  └─────────────────────────────────────────────────┘       │
│                            ↓                                 │
│  ┌─────────────────────────────────────────────────┐       │
│  │ Query Builder (بناء استعلام)                    │       │
│  │ - Natural Language Processing                   │       │
│  │ - Entity Recognition                            │       │
│  │ - Parameter Extraction                          │       │
│  └─────────────────────────────────────────────────┘       │
│                            ↓                                 │
│  ┌─────────────────────────────────────────────────┐       │
│  │ Permission Checker (التحقق من الصلاحيات)        │       │
│  │ - Role-Based Access                             │       │
│  │ - Data Ownership                                │       │
│  │ - Audit Logging                                 │       │
│  └─────────────────────────────────────────────────┘       │
│                                                              │
└─────────────────────────────────────────────────────────────┘
							↓
┌─────────────────────────────────────────────────────────────┐
│                  DATA ACCESS LAYER                           │
│                                                              │
│  Filtered Queries                                           │
│  - Only user's own data                                     │
│  - Based on permissions                                     │
│  - Respecting data privacy                                  │
│                                                              │
└─────────────────────────────────────────────────────────────┘
							↓
┌─────────────────────────────────────────────────────────────┐
│                  DATABASE (SQL Server)                       │
│                                                              │
│  - ApplicationUser data (آمن)                               │
│  - User's own courses (آمن)                                 │
│  - User's progress (آمن)                                    │
│  - Non-sensitive public data (معلومات عامة)                 │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

---

## 📊 Models الجديدة المطلوبة

### 1. AIChatMessage Model

```csharp
namespace TrainingCenterManagement_MVC.Models
{
	public class AIChatMessage
	{
		[Key]
		public Guid MessageId { get; set; } = Guid.NewGuid();

		/// <summary>
		/// من يسأل؟
		/// </summary>
		[Required]
		public Guid UserId { get; set; }
		public ApplicationUser User { get; set; }

		/// <summary>
		/// السؤال من المستخدم
		/// </summary>
		[Required, MaxLength(2000)]
		public string UserMessage { get; set; }

		/// <summary>
		/// الإجابة من الـ AI
		/// </summary>
		[MaxLength(5000)]
		public string AIResponse { get; set; }

		/// <summary>
		/// تصنيف السؤال
		/// </summary>
		public QuestionType QuestionType { get; set; }

		/// <summary>
		/// هل تم الإجابة على السؤال
		/// </summary>
		public bool IsAnswered { get; set; } = false;

		/// <summary>
		/// هل الإجابة تحتاج مراجعة يدوية
		/// </summary>
		public bool RequiresManualReview { get; set; } = false;

		/// <summary>
		/// سبب الحاجة للمراجعة (إذا كانت مطلوبة)
		/// </summary>
		[MaxLength(500)]
		public string ReviewReason { get; set; }

		/// <summary>
		/// دور المستخدم عند الاستفسار
		/// </summary>
		[MaxLength(50)]
		public string UserRole { get; set; }

		/// <summary>
		/// ملخص البيانات المسترجعة (للتدقيق)
		/// </summary>
		[MaxLength(1000)]
		public string DataAccessLog { get; set; }

		/// <summary>
		/// وقت الاستفسار
		/// </summary>
		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

		/// <summary>
		/// وقت الإجابة
		/// </summary>
		public DateTime? AnsweredAt { get; set; }

		/// <summary>
		/// تقييم المستخدم للإجابة
		/// </summary>
		public int? Rating { get; set; } // 1-5 stars

		/// <summary>
		/// تعليق من المستخدم
		/// </summary>
		[MaxLength(500)]
		public string UserFeedback { get; set; }

		/// <summary>
		/// هل الإجابة مفيدة
		/// </summary>
		public bool? IsHelpful { get; set; }

		/// <summary>
		/// soft delete
		/// </summary>
		public bool IsDeleted { get; set; } = false;
		public DateTime? DeletedAt { get; set; }
	}

	/// <summary>
	/// تصنيفات الأسئلة
	/// </summary>
	public enum QuestionType
	{
		/// <summary>
		/// أسئلة عامة عن النظام
		/// </summary>
		[Display(Name = "سؤال عام")]
		General = 0,

		/// <summary>
		/// أسئلة شخصية عن المستخدم
		/// </summary>
		[Display(Name = "سؤال شخصي")]
		Personal = 1,

		/// <summary>
		/// أسئلة الملاحة والبحث
		/// </summary>
		[Display(Name = "ملاحة")]
		Navigation = 2,

		/// <summary>
		/// طلبات بيانات
		/// </summary>
		[Display(Name = "طلب بيانات")]
		DataRequest = 3,

		/// <summary>
		/// أسئلة شرح الميزات
		/// </summary>
		[Display(Name = "شرح ميزة")]
		FeatureExplanation = 4,

		/// <summary>
		/// توصيات شخصية
		/// </summary>
		[Display(Name = "توصيات")]
		Recommendation = 5,

		/// <summary>
		/// مساعدة تقنية
		/// </summary>
		[Display(Name = "مساعدة تقنية")]
		TechnicalSupport = 6,

		/// <summary>
		/// أسئلة أخرى
		/// </summary>
		[Display(Name = "أخرى")]
		Other = 7
	}
}
```

### 2. AIAccessLog Model

```csharp
namespace TrainingCenterManagement_MVC.Models
{
	public class AIAccessLog
	{
		[Key]
		public Guid LogId { get; set; } = Guid.NewGuid();

		[Required]
		public Guid UserId { get; set; }
		public ApplicationUser User { get; set; }

		/// <summary>
		/// نوع الوصول (GET, SET, UPDATE)
		/// </summary>
		[Required, MaxLength(50)]
		public string AccessType { get; set; }

		/// <summary>
		/// اسم الجدول أو العملية
		/// </summary>
		[Required, MaxLength(200)]
		public string ResourceAccessed { get; set; }

		/// <summary>
		/// معرّف البيانات المسترجعة (إذا كانت)
		/// </summary>
		[MaxLength(100)]
		public string ResourceId { get; set; }

		/// <summary>
		/// هل تم السماح بالوصول
		/// </summary>
		public bool IsAuthorized { get; set; }

		/// <summary>
		/// سبب الرفض (إذا تم الرفض)
		/// </summary>
		[MaxLength(500)]
		public string DenialReason { get; set; }

		/// <summary>
		/// وقت الوصول
		/// </summary>
		public DateTime AccessedAt { get; set; } = DateTime.UtcNow;

		/// <summary>
		/// عنوان IP
		/// </summary>
		[MaxLength(45)]
		public string IpAddress { get; set; }

		/// <summary>
		/// معلومات المتصفح
		/// </summary>
		[MaxLength(500)]
		public string UserAgent { get; set; }

		/// <summary>
		/// تفاصيل إضافية
		/// </summary>
		[MaxLength(1000)]
		public string Details { get; set; }
	}
}
```

### 3. AIPermissionRole Model

```csharp
namespace TrainingCenterManagement_MVC.Models
{
	public class AIPermissionRole
	{
		[Key]
		public Guid PermissionId { get; set; } = Guid.NewGuid();

		/// <summary>
		/// دور المستخدم
		/// </summary>
		[Required, MaxLength(50)]
		public string RoleName { get; set; }

		/// <summary>
		/// يمكنه قراءة بيانات شخصية
		/// </summary>
		public bool CanReadPersonalData { get; set; } = true;

		/// <summary>
		/// يمكنه قراءة بيانات الآخرين
		/// </summary>
		public bool CanReadOtherUsersData { get; set; } = false;

		/// <summary>
		/// يمكنه قراءة البيانات الإدارية
		/// </summary>
		public bool CanReadAdminData { get; set; } = false;

		/// <summary>
		/// يمكنه تعديل بيانات شخصية
		/// </summary>
		public bool CanModifyPersonalData { get; set; } = false;

		/// <summary>
		/// يمكنه تعديل بيانات الآخرين
		/// </summary>
		public bool CanModifyOtherUsersData { get; set; } = false;

		/// <summary>
		/// عدد الأسئلة المسموحة يومياً
		/// </summary>
		public int DailyQueryLimit { get; set; } = 100;

		/// <summary>
		/// المجالات المسموحة (مفصولة بفاصلة)
		/// </summary>
		[MaxLength(1000)]
		public string AllowedDataCategories { get; set; }

		/// <summary>
		/// المجالات المحظورة (مفصولة بفاصلة)
		/// </summary>
		[MaxLength(1000)]
		public string BlockedDataCategories { get; set; }

		/// <summary>
		/// هل يمكنه الوصول إلى الدوال المتقدمة
		/// </summary>
		public bool CanAccessAdvancedFeatures { get; set; } = false;

		/// <summary>
		/// تاريخ الإنشاء
		/// </summary>
		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

		/// <summary>
		/// آخر تحديث
		/// </summary>
		public DateTime? UpdatedAt { get; set; }

		/// <summary>
		/// من قام بالإنشاء
		/// </summary>
		[MaxLength(100)]
		public string CreatedBy { get; set; }

		/// <summary>
		/// ملاحظات
		/// </summary>
		[MaxLength(500)]
		public string Notes { get; set; }
	}
}
```

---

## 🔄 تحديثات ApplicationDbContext

```csharp
// في DbSet declarations:
public DbSet<AIChatMessage> AIChatMessages { get; set; }
public DbSet<AIAccessLog> AIAccessLogs { get; set; }
public DbSet<AIPermissionRole> AIPermissionRoles { get; set; }

// في OnModelCreating:
// ──────────────────────────────────────────────────────
// AI CHAT MESSAGES
// ──────────────────────────────────────────────────────

builder.Entity<AIChatMessage>()
	.HasOne(acm => acm.User)
	.WithMany(u => u.AIChatMessages)
	.HasForeignKey(acm => acm.UserId)
	.OnDelete(DeleteBehavior.Restrict);

builder.Entity<AIChatMessage>()
	.HasQueryFilter(acm => !acm.IsDeleted);

builder.Entity<AIChatMessage>()
	.HasIndex(acm => acm.UserId)
	.HasDatabaseName("IX_AIChatMessages_UserId");

builder.Entity<AIChatMessage>()
	.HasIndex(acm => acm.CreatedAt)
	.HasDatabaseName("IX_AIChatMessages_CreatedAt");

builder.Entity<AIChatMessage>()
	.HasIndex(acm => new { acm.UserId, acm.CreatedAt })
	.HasDatabaseName("IX_AIChatMessages_UserId_CreatedAt");

// ──────────────────────────────────────────────────────
// AI ACCESS LOGS
// ──────────────────────────────────────────────────────

builder.Entity<AIAccessLog>()
	.HasOne(aal => aal.User)
	.WithMany(u => u.AIAccessLogs)
	.HasForeignKey(aal => aal.UserId)
	.OnDelete(DeleteBehavior.Restrict);

builder.Entity<AIAccessLog>()
	.HasIndex(aal => aal.UserId)
	.HasDatabaseName("IX_AIAccessLogs_UserId");

builder.Entity<AIAccessLog>()
	.HasIndex(aal => aal.AccessedAt)
	.HasDatabaseName("IX_AIAccessLogs_AccessedAt");

builder.Entity<AIAccessLog>()
	.HasIndex(aal => new { aal.UserId, aal.IsAuthorized })
	.HasDatabaseName("IX_AIAccessLogs_UserId_IsAuthorized");

// ──────────────────────────────────────────────────────
// AI PERMISSION ROLES
// ──────────────────────────────────────────────────────

builder.Entity<AIPermissionRole>()
	.HasIndex(apr => apr.RoleName)
	.IsUnique()
	.HasDatabaseName("IX_AIPermissionRoles_RoleName");

// Seed Default Data
builder.Entity<AIPermissionRole>().HasData(
	new AIPermissionRole
	{
		PermissionId = Guid.NewGuid(),
		RoleName = "Trainee",
		CanReadPersonalData = true,
		CanReadOtherUsersData = false,
		CanReadAdminData = false,
		CanModifyPersonalData = false,
		CanModifyOtherUsersData = false,
		DailyQueryLimit = 50,
		AllowedDataCategories = "Courses,Lectures,Progress,Grades",
		BlockedDataCategories = "AdminSettings,UserPasswords,PaymentDetails",
		CanAccessAdvancedFeatures = false,
		CreatedBy = "System"
	},
	new AIPermissionRole
	{
		PermissionId = Guid.NewGuid(),
		RoleName = "Trainer",
		CanReadPersonalData = true,
		CanReadOtherUsersData = false,
		CanReadAdminData = false,
		CanModifyPersonalData = false,
		CanModifyOtherUsersData = false,
		DailyQueryLimit = 100,
		AllowedDataCategories = "Courses,Lectures,Students,Grades,Analytics",
		BlockedDataCategories = "AdminSettings,UserPasswords,PaymentDetails",
		CanAccessAdvancedFeatures = true,
		CreatedBy = "System"
	},
	new AIPermissionRole
	{
		PermissionId = Guid.NewGuid(),
		RoleName = "Admin",
		CanReadPersonalData = true,
		CanReadOtherUsersData = true,
		CanReadAdminData = true,
		CanModifyPersonalData = false,
		CanModifyOtherUsersData = false,
		DailyQueryLimit = -1, // Unlimited
		AllowedDataCategories = "All",
		BlockedDataCategories = "",
		CanAccessAdvancedFeatures = true,
		CreatedBy = "System"
	}
);
```

---

## 🔄 تحديثات Models الموجودة

### ApplicationUser.cs

```csharp
// أضف:
public ICollection<AIChatMessage> AIChatMessages { get; set; } = new List<AIChatMessage>();
public ICollection<AIAccessLog> AIAccessLogs { get; set; } = new List<AIAccessLog>();
```

---

## 🎮 Services المطلوبة

### 1. IAIPermissionService

```csharp
public interface IAIPermissionService
{
	/// <summary>
	/// التحقق من صلاحية المستخدم
	/// </summary>
	Task<bool> IsUserAuthorizedAsync(Guid userId, string resourceType, string accessType);

	/// <summary>
	/// الحصول على إذن الدور
	/// </summary>
	Task<AIPermissionRole> GetRolePermissionsAsync(string roleName);

	/// <summary>
	/// التحقق من حد الاستفسارات اليومية
	/// </summary>
	Task<bool> IsWithinDailyLimitAsync(Guid userId);

	/// <summary>
	/// تسجيل الوصول (Audit Log)
	/// </summary>
	Task LogAccessAsync(Guid userId, string accessType, string resource, string resourceId, bool isAuthorized, string reason = null);

	/// <summary>
	/// التحقق من وصول شخص لبيانات شخص آخر
	/// </summary>
	Task<bool> CanAccessUserDataAsync(Guid requesterId, Guid targetUserId);

	/// <summary>
	/// الحصول على الفئات المسموحة
	/// </summary>
	Task<List<string>> GetAllowedCategoriesAsync(Guid userId);
}
```

### 2. IAIQueryBuilderService

```csharp
public interface IAIQueryBuilderService
{
	/// <summary>
	/// تصنيف السؤال
	/// </summary>
	Task<QuestionType> ClassifyQuestionAsync(string question, Guid userId);

	/// <summary>
	/// استخراج الكلمات المفتاحية
	/// </summary>
	List<string> ExtractKeywords(string question);

	/// <summary>
	/// بناء الاستعلام الآمن
	/// </summary>
	Task<(IQueryable<dynamic> Query, List<string> DataAccessed)> BuildSecureQueryAsync(
		string question, 
		Guid userId, 
		QuestionType questionType);

	/// <summary>
	/// تحويل النتائج إلى إجابة طبيعية
	/// </summary>
	string FormatResponseAsNaturalLanguage(object data, string question, QuestionType questionType);
}
```

### 3. IAIAssistantService

```csharp
public interface IAIAssistantService
{
	/// <summary>
	/// الإجابة على سؤال
	/// </summary>
	Task<AIChatMessage> AskQuestionAsync(Guid userId, string question, string ipAddress, string userAgent);

	/// <summary>
	/// الحصول على سجل المحادثات
	/// </summary>
	Task<List<AIChatMessage>> GetChatHistoryAsync(Guid userId, int pageNumber = 1, int pageSize = 20);

	/// <summary>
	/// إعطاء تقييم للإجابة
	/// </summary>
	Task<bool> RateResponseAsync(Guid messageId, int rating, string feedback);

	/// <summary>
	/// الحصول على الإحصائيات
	/// </summary>
	Task<AIStatistics> GetStatisticsAsync(Guid userId);

	/// <summary>
	/// حذف محادثة
	/// </summary>
	Task<bool> DeleteMessageAsync(Guid messageId, Guid userId);
}
```

### 4. Implementation مثال

```csharp
public class AIAssistantService : IAIAssistantService
{
	private readonly ApplicationDbContext _context;
	private readonly IAIPermissionService _permissionService;
	private readonly IAIQueryBuilderService _queryBuilder;
	private readonly UserManager<ApplicationUser> _userManager;
	private readonly ILogger<AIAssistantService> _logger;

	public AIAssistantService(
		ApplicationDbContext context,
		IAIPermissionService permissionService,
		IAIQueryBuilderService queryBuilder,
		UserManager<ApplicationUser> userManager,
		ILogger<AIAssistantService> logger)
	{
		_context = context;
		_permissionService = permissionService;
		_queryBuilder = queryBuilder;
		_userManager = userManager;
		_logger = logger;
	}

	public async Task<AIChatMessage> AskQuestionAsync(Guid userId, string question, string ipAddress, string userAgent)
	{
		try
		{
			// 1. التحقق من المستخدم
			var user = await _userManager.FindByIdAsync(userId.ToString());
			if (user == null)
				throw new InvalidOperationException("المستخدم غير موجود");

			// 2. التحقق من حد الاستفسارات
			if (!await _permissionService.IsWithinDailyLimitAsync(userId))
				throw new InvalidOperationException("تجاوزت حد الأسئلة اليومي");

			// 3. تصنيف السؤال
			var questionType = await _queryBuilder.ClassifyQuestionAsync(question, userId);

			// 4. بناء الاستعلام الآمن
			var (query, dataAccessed) = await _queryBuilder.BuildSecureQueryAsync(question, userId, questionType);

			// 5. تنفيذ الاستعلام
			var results = await query.ToListAsync();

			// 6. صيغة الإجابة
			var response = _queryBuilder.FormatResponseAsNaturalLanguage(results, question, questionType);

			// 7. حفظ الرسالة
			var message = new AIChatMessage
			{
				UserId = userId,
				UserMessage = question,
				AIResponse = response,
				QuestionType = questionType,
				IsAnswered = true,
				UserRole = (await _userManager.GetRolesAsync(user)).FirstOrDefault(),
				DataAccessLog = string.Join(", ", dataAccessed),
				AnsweredAt = DateTime.UtcNow
			};

			_context.AIChatMessages.Add(message);
			await _context.SaveChangesAsync();

			// 8. تسجيل الوصول
			await _permissionService.LogAccessAsync(
				userId,
				"READ",
				string.Join(", ", dataAccessed),
				null,
				true);

			_logger.LogInformation($"تم الإجابة على سؤال: {message.MessageId}");
			return message;
		}
		catch (Exception ex)
		{
			_logger.LogError($"خطأ في الإجابة: {ex.Message}");

			// حفظ الرسالة حتى لو فشلت الإجابة
			var failedMessage = new AIChatMessage
			{
				UserId = userId,
				UserMessage = question,
				IsAnswered = false,
				RequiresManualReview = true,
				ReviewReason = ex.Message,
				UserRole = (await _userManager.GetRolesAsync(user)).FirstOrDefault()
			};

			_context.AIChatMessages.Add(failedMessage);
			await _context.SaveChangesAsync();

			throw;
		}
	}

	public async Task<List<AIChatMessage>> GetChatHistoryAsync(Guid userId, int pageNumber = 1, int pageSize = 20)
	{
		return await _context.AIChatMessages
			.Where(m => m.UserId == userId)
			.OrderByDescending(m => m.CreatedAt)
			.Skip((pageNumber - 1) * pageSize)
			.Take(pageSize)
			.ToListAsync();
	}

	public async Task<bool> RateResponseAsync(Guid messageId, int rating, string feedback)
	{
		if (rating < 1 || rating > 5)
			return false;

		var message = await _context.AIChatMessages.FindAsync(messageId);
		if (message == null)
			return false;

		message.Rating = rating;
		message.UserFeedback = feedback;
		message.IsHelpful = rating >= 4;

		await _context.SaveChangesAsync();
		return true;
	}

	public async Task<AIStatistics> GetStatisticsAsync(Guid userId)
	{
		var messages = await _context.AIChatMessages
			.Where(m => m.UserId == userId)
			.ToListAsync();

		return new AIStatistics
		{
			TotalQuestions = messages.Count,
			AnsweredQuestions = messages.Count(m => m.IsAnswered),
			AverageRating = messages.Where(m => m.Rating.HasValue).Average(m => m.Rating) ?? 0,
			MostAskedCategory = messages
				.GroupBy(m => m.QuestionType)
				.OrderByDescending(g => g.Count())
				.FirstOrDefault()?.Key.ToString() ?? "None",
			LastQuestion = messages.OrderByDescending(m => m.CreatedAt).FirstOrDefault()?.CreatedAt
		};
	}

	public async Task<bool> DeleteMessageAsync(Guid messageId, Guid userId)
	{
		var message = await _context.AIChatMessages.FindAsync(messageId);
		if (message == null || message.UserId != userId)
			return false;

		message.IsDeleted = true;
		message.DeletedAt = DateTime.UtcNow;

		await _context.SaveChangesAsync();
		return true;
	}
}

public class AIStatistics
{
	public int TotalQuestions { get; set; }
	public int AnsweredQuestions { get; set; }
	public double AverageRating { get; set; }
	public string MostAskedCategory { get; set; }
	public DateTime? LastQuestion { get; set; }
}
```

---

## 🎮 Controllers

### AIAssistantController.cs

```csharp
[Authorize]
[Route("api/[controller]")]
[ApiController]
public class AIAssistantController : ControllerBase
{
	private readonly IAIAssistantService _aiService;
	private readonly UserManager<ApplicationUser> _userManager;
	private readonly ILogger<AIAssistantController> _logger;

	public AIAssistantController(
		IAIAssistantService aiService,
		UserManager<ApplicationUser> userManager,
		ILogger<AIAssistantController> logger)
	{
		_aiService = aiService;
		_userManager = userManager;
		_logger = logger;
	}

	[HttpPost("ask")]
	public async Task<IActionResult> AskQuestion([FromBody] AskQuestionRequest request)
	{
		try
		{
			var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
			var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
			var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();

			var message = await _aiService.AskQuestionAsync(
				Guid.Parse(userId),
				request.Question,
				ipAddress,
				userAgent);

			return Ok(new
			{
				success = true,
				messageId = message.MessageId,
				response = message.AIResponse,
				type = message.QuestionType.ToString(),
				timestamp = message.CreatedAt
			});
		}
		catch (Exception ex)
		{
			_logger.LogError($"خطأ: {ex.Message}");
			return BadRequest(new { success = false, message = ex.Message });
		}
	}

	[HttpGet("history")]
	public async Task<IActionResult> GetChatHistory(int pageNumber = 1, int pageSize = 20)
	{
		try
		{
			var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
			var messages = await _aiService.GetChatHistoryAsync(
				Guid.Parse(userId), 
				pageNumber, 
				pageSize);

			return Ok(new
			{
				success = true,
				count = messages.Count,
				messages = messages.Select(m => new
				{
					m.MessageId,
					m.UserMessage,
					m.AIResponse,
					m.QuestionType,
					m.Rating,
					m.CreatedAt
				})
			});
		}
		catch (Exception ex)
		{
			return BadRequest(new { success = false, message = ex.Message });
		}
	}

	[HttpPost("rate/{messageId}")]
	public async Task<IActionResult> RateResponse(Guid messageId, [FromBody] RateRequest request)
	{
		try
		{
			var success = await _aiService.RateResponseAsync(messageId, request.Rating, request.Feedback);
			return success
				? Ok(new { success = true, message = "تم تسجيل التقييم" })
				: BadRequest(new { success = false, message = "فشل التقييم" });
		}
		catch (Exception ex)
		{
			return BadRequest(new { success = false, message = ex.Message });
		}
	}

	[HttpGet("statistics")]
	public async Task<IActionResult> GetStatistics()
	{
		try
		{
			var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
			var stats = await _aiService.GetStatisticsAsync(Guid.Parse(userId));

			return Ok(new
			{
				success = true,
				statistics = stats
			});
		}
		catch (Exception ex)
		{
			return BadRequest(new { success = false, message = ex.Message });
		}
	}
}

public class AskQuestionRequest
{
	[Required, MaxLength(2000)]
	public string Question { get; set; }
}

public class RateRequest
{
	[Range(1, 5)]
	public int Rating { get; set; }

	[MaxLength(500)]
	public string Feedback { get; set; }
}
```

---

## 🌐 Razor Pages / Views

### Components/AIChat.razor (أو View)

```html
<div id="ai-chat-widget" class="ai-chat-container">
	<!-- Header -->
	<div class="ai-chat-header">
		<h4>🤖 المساعد الذكي</h4>
		<button class="close-btn" onclick="closeAIChat()">×</button>
	</div>

	<!-- Messages Container -->
	<div id="chat-messages" class="chat-messages">
		<div class="ai-welcome-message">
			<p>مرحباً! 👋</p>
			<p>أنا هنا لمساعدتك. اسأل عن أي شيء!</p>
			<div class="suggested-questions">
				<p>أمثلة الأسئلة:</p>
				<button onclick="askQuestion('ما هي درجاتي الحالية؟')">
					درجاتي
				</button>
				<button onclick="askQuestion('ما المحاضرات المتاحة؟')">
					المحاضرات
				</button>
				<button onclick="askQuestion('كيف أحمل ملف؟')">
					تحميل ملف
				</button>
			</div>
		</div>
	</div>

	<!-- Input Area -->
	<div class="ai-chat-input">
		<input 
			type="text" 
			id="question-input" 
			placeholder="اكتب سؤالك هنا..."
			onkeypress="handleKeyPress(event)">
		<button onclick="sendQuestion()" class="send-btn">
			<i class="fas fa-paper-plane"></i> إرسال
		</button>
	</div>

	<!-- Loading Indicator -->
	<div id="loading-indicator" style="display:none;" class="loading">
		<span>جاري المعالجة...</span>
	</div>
</div>

<style>
.ai-chat-container {
	position: fixed;
	bottom: 20px;
	right: 20px;
	width: 350px;
	height: 500px;
	border: 1px solid #ddd;
	border-radius: 12px;
	box-shadow: 0 4px 12px rgba(0,0,0,0.15);
	display: flex;
	flex-direction: column;
	background: white;
	z-index: 10000;
	font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
}

.ai-chat-header {
	background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
	color: white;
	padding: 15px;
	border-radius: 12px 12px 0 0;
	display: flex;
	justify-content: space-between;
	align-items: center;
}

.chat-messages {
	flex: 1;
	overflow-y: auto;
	padding: 15px;
	background: #f8f9fa;
}

.ai-welcome-message {
	text-align: center;
	color: #666;
}

.suggested-questions {
	margin-top: 15px;
}

.suggested-questions button {
	display: block;
	width: 100%;
	padding: 8px;
	margin: 5px 0;
	background: white;
	border: 1px solid #ddd;
	border-radius: 6px;
	cursor: pointer;
	transition: all 0.3s;
}

.suggested-questions button:hover {
	background: #e7f3ff;
	border-color: #667eea;
}

.message {
	margin: 10px 0;
	padding: 10px;
	border-radius: 8px;
	animation: slideIn 0.3s ease-in;
}

.user-message {
	background: #667eea;
	color: white;
	margin-right: 20px;
	text-align: right;
}

.ai-message {
	background: white;
	border: 1px solid #e0e0e0;
	margin-left: 20px;
	color: #333;
}

@keyframes slideIn {
	from {
		opacity: 0;
		transform: translateY(10px);
	}
	to {
		opacity: 1;
		transform: translateY(0);
	}
}

.ai-chat-input {
	display: flex;
	padding: 10px;
	border-top: 1px solid #ddd;
	gap: 10px;
}

.ai-chat-input input {
	flex: 1;
	padding: 10px;
	border: 1px solid #ddd;
	border-radius: 6px;
	font-size: 14px;
}

.send-btn {
	padding: 10px 15px;
	background: #667eea;
	color: white;
	border: none;
	border-radius: 6px;
	cursor: pointer;
	transition: background 0.3s;
}

.send-btn:hover {
	background: #5568d3;
}

.loading {
	text-align: center;
	padding: 10px;
	color: #667eea;
}
</style>

<script>
async function sendQuestion() {
	const input = document.getElementById('question-input');
	const question = input.value.trim();

	if (!question) return;

	// Add user message to chat
	addMessageToChat(question, 'user');
	input.value = '';

	// Show loading
	document.getElementById('loading-indicator').style.display = 'block';

	try {
		const response = await fetch('/api/aiassistant/ask', {
			method: 'POST',
			headers: {
				'Content-Type': 'application/json',
			},
			body: JSON.stringify({ question: question })
		});

		const data = await response.json();

		if (data.success) {
			addMessageToChat(data.response, 'ai');
			addRatingButtons(data.messageId);
		} else {
			addMessageToChat('عذراً، حدث خطأ: ' + data.message, 'ai');
		}
	} catch (error) {
		addMessageToChat('عذراً، حدث خطأ في الاتصال', 'ai');
	} finally {
		document.getElementById('loading-indicator').style.display = 'none';
	}
}

function addMessageToChat(text, sender) {
	const messagesContainer = document.getElementById('chat-messages');
	const messageDiv = document.createElement('div');
	messageDiv.className = `message ${sender}-message`;
	messageDiv.textContent = text;
	messagesContainer.appendChild(messageDiv);
	messagesContainer.scrollTop = messagesContainer.scrollHeight;
}

function askQuestion(question) {
	document.getElementById('question-input').value = question;
	sendQuestion();
}

function handleKeyPress(event) {
	if (event.key === 'Enter') {
		sendQuestion();
	}
}

function closeAIChat() {
	document.getElementById('ai-chat-widget').style.display = 'none';
}
</script>
```

---

## ⚙️ Program.cs Registration

```csharp
// Add Services
builder.Services.AddScoped<IAIPermissionService, AIPermissionService>();
builder.Services.AddScoped<IAIQueryBuilderService, AIQueryBuilderService>();
builder.Services.AddScoped<IAIAssistantService, AIAssistantService>();

// Or use decorator pattern
builder.Services.Decorate<IAIAssistantService, AIChatService>();
```

---

## 🗄️ Database Migration

```bash
dotnet ef migrations add AddAIAssistantSystem
dotnet ef database update
```

---

## 🔐 القواعد الأمنية الصارمة

### ✅ قواعد البيانات:

```csharp
// 1. لا يمكن لأي مستخدم عرض بيانات شخصية لمستخدم آخر
if (targetUserId != requesterId && !isAdmin)
	throw new UnauthorizedAccessException();

// 2. لا يمكن تعديل بيانات شخصية عبر الـ AI
if (accessType == "UPDATE" && !isAdmin)
	throw new UnauthorizedAccessException();

// 3. تسجيل جميع محاولات الوصول (حتى المرفوضة)
await LogAccessAsync(userId, accessType, resource, isAuthorized: false, reason: "Unauthorized");

// 4. تحديد حد يومي للأسئلة
if (dailyQuestionCount >= dailyLimit)
	throw new InvalidOperationException("تجاوزت الحد اليومي");

// 5. تصفية النتائج حسب الصلاحيات
var results = query.Where(x => x.UserId == userId || isAdmin);
```

---

## 📊 أمثلة السيناريوهات

### السيناريو 1: طالب يسأل عن درجاته
```
المستخدم: "ما درجاتي في الرياضيات؟"
↓
AI Classification: Personal Question
↓
AI Permission Check: يمكنه الوصول لبيانات نفسه فقط ✓
↓
AI Query Builder: SELECT * FROM Grades WHERE UserId = @UserId AND CourseName = 'رياضيات'
↓
Response: "درجتك في الرياضيات: 92/100"
↓
Log: "Authorized - User accessed own grades"
```

### السيناريو 2: طالب يحاول الوصول لدرجات طالب آخر
```
المستخدم: "ما درجات أحمد؟"
↓
AI Classification: Personal Question
↓
AI Permission Check: لا يمكنه الوصول لبيانات آخرين ✗
↓
Response: "عذراً، لا يمكنك الوصول لبيانات المستخدمين الآخرين"
↓
Log: "DENIED - Unauthorized access attempt to other user's data"
```

### السيناريو 3: مدرب يسأل عن طلابه
```
المستخدم: "كم طالب درجته أقل من 60؟"
↓
AI Classification: Data Request
↓
AI Permission Check: يمكنه رؤية بيانات طلابه فقط ✓
↓
AI Query Builder: SELECT * FROM Grades 
				  WHERE CourseId IN (SELECT CourseId FROM TrainerCourses WHERE TrainerId = @TrainerId)
				  AND Grade < 60
↓
Response: "هناك 5 طلاب درجاتهم أقل من 60"
↓
Log: "Authorized - Trainer accessed students in his courses"
```

### السيناريو 4: مسؤول يطلب تقرير شامل
```
المستخدم: "كم عدد الطلاب الذين استكملوا الدورة؟"
↓
AI Classification: Data Request (Admin)
↓
AI Permission Check: لديه صلاحية عرض جميع البيانات ✓
↓
AI Query Builder: SELECT COUNT(*) FROM Users WHERE Role = 'Trainee' AND HasCompletedCourse = true
↓
Response: "عدد الطلاب الذين استكملوا الدورة: 256"
↓
Log: "Authorized - Admin accessed system-wide data"
```

---

## 🎯 معايير الأمان

```
✓ Authentication: تسجيل دخول إلزامي
✓ Authorization: تحقق من الصلاحيات قبل أي وصول
✓ Encryption: تشفير البيانات الحساسة
✓ Audit Logging: تسجيل جميع الأنشطة
✓ Rate Limiting: حد يومي للأسئلة
✓ Data Masking: إخفاء البيانات الحساسة
✓ Input Validation: التحقق من الإدخال
✓ SQL Injection Prevention: استخدام Parameterized Queries
```

---

## 📊 جدول التنفيذ

| المرحلة | المهام | الوقت |
|--------|--------|-------|
| **1** | Models + DbContext + Migration | 1 يوم |
| **2** | Permission Service | 1 يوم |
| **3** | Query Builder Service | 1.5 يوم |
| **4** | AI Assistant Service | 1.5 يوم |
| **5** | Controllers + APIs | 1 يوم |
| **6** | UI/Views + JavaScript | 1 يوم |
| **7** | Testing & Security Review | 1 يوم |

**المجموع: 5-7 أيام**

---

## ✅ معايير القبول

```
✓ المستخدم يمكنه طرح أسئلة
✓ AI يجيب بناء على صلاحيات المستخدم
✓ لا يمكن الوصول لبيانات الآخرين
✓ جميع الأنشطة مسجلة
✓ الإجابات دقيقة وآمنة
✓ الواجهة سهلة الاستخدام
✓ الأداء جيد (< 2 ثانية)
✓ لا توجد ثغرات أمنية
```

---

