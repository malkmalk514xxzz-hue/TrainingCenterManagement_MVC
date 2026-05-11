# 🚀 خطة التطبيق السريعة - نظام الملفات الداعمة

**الوقت المتوقع**: 3-4 أيام عمل | **الصعوبة**: متوسط

---

## 📋 جدول المهام

### ✅ المرحلة 1: قاعدة البيانات (يوم 1)

**[ ] الخطوة 1: إنشاء LectureResource Model**
- الملف: `Models/LectureResource.cs`
- الحجم المتوقع: 150 سطر
- الوقت: 30 دقيقة

```csharp
public class LectureResource
{
	[Key] public Guid ResourceId { get; set; } = Guid.NewGuid();
	[Required] public Guid LectureId { get; set; }
	public Lecture Lecture { get; set; }

	[Required, MaxLength(500)] public string FileName { get; set; }
	[Required] public string FilePath { get; set; }
	[Required, MaxLength(50)] public string FileExtension { get; set; }
	public long FileSizeInBytes { get; set; }

	[Required] public ResourceType ResourceType { get; set; }
	[MaxLength(1000)] public string Description { get; set; }

	public int DisplayOrder { get; set; } = 1;
	public int DownloadCount { get; set; } = 0;
	public bool IsVisible { get; set; } = true;
	public bool IsRequired { get; set; } = false;

	[Required] public Guid UploadedByTrainerId { get; set; }
	public Trainer UploadedByTrainer { get; set; }

	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	public DateTime? UpdatedAt { get; set; }
	[MaxLength(500)] public string AdminNotes { get; set; }

	public bool IsDeleted { get; set; } = false;
	public DateTime? DeletedAt { get; set; }

	public ICollection<ResourceDownload> Downloads { get; set; } = new List<ResourceDownload>();
}

public enum ResourceType
{
	[Display(Name = "شرائح المحاضرة")] LectureSlides = 0,
	[Display(Name = "ملاحظات")] Notes = 1,
	[Display(Name = "واجب")] Assignment = 2,
	[Display(Name = "حل")] Solution = 3,
	[Display(Name = "ملفات المشروع")] ProjectFiles = 4,
	[Display(Name = "مرجع")] Reference = 5,
	[Display(Name = "كود")] Code = 6,
	[Display(Name = "أخرى")] Other = 7
}
```

**[ ] الخطوة 2: إنشاء ResourceDownload Model**
- الملف: `Models/ResourceDownload.cs`
- الحجم المتوقع: 50 سطر
- الوقت: 15 دقيقة

```csharp
public class ResourceDownload
{
	[Key] public Guid DownloadId { get; set; } = Guid.NewGuid();
	[Required] public Guid ResourceId { get; set; }
	public LectureResource Resource { get; set; }
	[Required] public Guid TraineeId { get; set; }
	public Trainee Trainee { get; set; }

	public DateTime DownloadedAt { get; set; } = DateTime.UtcNow;
	public string IpAddress { get; set; }
	public string UserAgent { get; set; }
}
```

**[ ] الخطوة 3: تحديث ApplicationDbContext**
- الملف: `Data/ApplicationDbContext.cs`
- الإضافات:
  - DbSet<LectureResource>
  - DbSet<ResourceDownload>
  - Fluent API configuration
- الوقت: 30 دقيقة

**[ ] الخطوة 4: تحديث Models الموجودة**
- الملفات:
  - `Models/Lecture.cs` → إضافة `public ICollection<LectureResource> Resources`
  - `Models/Trainer.cs` → إضافة `public ICollection<LectureResource> LectureResources`
  - `Models/Trainee.cs` → إضافة `public ICollection<ResourceDownload> ResourceDownloads`
- الوقت: 15 دقيقة

**[ ] الخطوة 5: إنشاء Database Migration**
```bash
dotnet ef migrations add AddLectureResourceManagementSystem
dotnet ef database update
```
- الوقت: 5 دقيقة

---

### ✅ المرحلة 2: Services (يوم 2)

**[ ] الخطوة 1: إنشاء Interface**
- الملف: `Services/ILectureResourceService.cs`
- الوقت: 15 دقيقة

```csharp
public interface ILectureResourceService
{
	Task<LectureResource> UploadResourceAsync(IFormFile file, Guid lectureId, Guid trainerId, ResourceType type, string description = null);
	Task<List<LectureResource>> GetLectureResourcesAsync(Guid lectureId);
	Task<LectureResource> GetResourceAsync(Guid resourceId);
	Task<bool> DeleteResourceAsync(Guid resourceId, Guid trainerId);
	Task<bool> UpdateResourceAsync(Guid resourceId, ResourceType? newType, string description, bool? isRequired, bool? isVisible);
	Task<(byte[] fileBytes, string fileName)> DownloadResourceAsync(Guid resourceId, Guid traineeId);
	Task<Dictionary<ResourceType, int>> GetResourceStatisticsAsync(Guid lectureId);
}
```

**[ ] الخطوة 2: إنشاء Service Implementation**
- الملف: `Services/LectureResourceService.cs`
- الحجم المتوقع: 300 سطر
- الوقت: 1.5 ساعة

**الميزات الرئيسية:**
- ✅ Upload: تحقق من النوع والحجم والملف
- ✅ Get: استرجاع الملفات من قاعدة البيانات
- ✅ Delete: حذف آمن من القرص ومن DB
- ✅ Download: حفظ البيانات وتسجيل التحميل
- ✅ Statistics: إحصائيات الملفات

**[ ] الخطوة 3: تسجيل Service في Program.cs**
```csharp
builder.Services.AddScoped<ILectureResourceService, LectureResourceService>();
```
- الوقت: 5 دقائق

---

### ✅ المرحلة 3: Controllers (يوم 3 - الصباح)

**[ ] الخطوة 1: إنشاء LectureResourcesController**
- الملف: `Controllers/LectureResourcesController.cs`
- الحجم المتوقع: 200 سطر
- الوقت: 1 ساعة

**الـ Endpoints:**
```
GET    /api/lectureresources/lecture/{lectureId}    → جلب الملفات
POST   /api/lectureresources/upload                   → رفع ملف
DELETE /api/lectureresources/{resourceId}             → حذف ملف
GET    /api/lectureresources/download/{resourceId}    → تحميل ملف
PATCH  /api/lectureresources/{resourceId}             → تحديث معلومات الملف
```

**[ ] الخطوة 2: اختبار الـ Controller (Postman)
- ✅ اختبار Upload
- ✅ اختبار Get
- ✅ اختبار Delete
- ✅ اختبار Download
- الوقت: 30 دقيقة

---

### ✅ المرحلة 4: Views (يوم 3 - بعد الظهر)

**[ ] الخطوة 1: تحديث Lectures/Edit.cshtml**
- إضافة قسم إدارة الملفات
- نموذج لرفع ملف جديد
- قائمة الملفات مع أزرار الحذف
- الوقت: 30 دقيقة

**[ ] الخطوة 2: تحديث Lectures/Details.cshtml**
- إضافة قسم الملفات الداعمة
- عرض الملفات حسب النوع
- أزرار التحميل
- الوقت: 45 دقيقة

**[ ] الخطوة 3: JavaScript لعمليات AJAX**
- رفع الملفات بدون تحديث الصفحة
- حذف الملفات بتأكيد
- عرض الرسائل والأخطاء
- الوقت: 30 دقيقة

---

### ✅ المرحلة 5: تحسينات (يوم 4)

**[ ] الخطوة 1: البحث والتصفية**
- بحث حسب الاسم
- تصفية حسب النوع
- ترتيب حسب التاريخ/الحجم
- الوقت: 1 ساعة

**[ ] الخطوة 2: الإحصائيات**
- عدد الملفات
- إجمالي الحجم
- عدد التحميلات
- الملفات الأكثر تحميلاً
- الوقت: 45 دقيقة

**[ ] الخطوة 3: الاختبار الشامل**
- ✅ اختبار رفع أنواع ملفات مختلفة
- ✅ اختبار الحجم الكبير (500 MB)
- ✅ اختبار الحذف والتحميل
- ✅ اختبار الأمان والصلاحيات
- الوقت: 1.5 ساعة

---

## 🛠️ المتطلبات التقنية

### NuGet Packages (إذا لم تكن موجودة):
```xml
<!-- عادة موجودة بالفعل في ASP.NET Core 8 -->
<PackageReference Include="Microsoft.EntityFrameworkCore" Version="8.0.0" />
<PackageReference Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" Version="8.0.0" />
```

### File Extensions المدعومة:
```csharp
.pdf, .docx, .doc, .pptx, .ppt, .xlsx, .xls, .txt, .zip, .rar, .jpg, .png, .gif, .mp4, .avi
```

### حدود الملفات:
- **الحد الأقصى للملف**: 500 MB
- **المجلد**: `wwwroot/uploads/resources/{lectureId}/`
- **البيانات**: آمنة في قاعدة البيانات

---

## 📋 قائمة التحقق (Checklist)

### قبل الشروع:
- [ ] تحديث الكود من Git
- [ ] فتح الـ Solution في Visual Studio
- [ ] التأكد من توفر SQL Server

### المرحلة 1:
- [ ] ✅ إنشاء Models
- [ ] ✅ تحديث DbContext
- [ ] ✅ إنشاء Migration
- [ ] ✅ تطبيق Migration على DB

### المرحلة 2:
- [ ] ✅ إنشاء Interface
- [ ] ✅ إنشاء Service
- [ ] ✅ تسجيل Service في Program.cs
- [ ] ✅ البناء بدون أخطاء

### المرحلة 3:
- [ ] ✅ إنشاء Controller
- [ ] ✅ اختبار الـ Endpoints مع Postman
- [ ] ✅ التحقق من الأخطاء والرسائل

### المرحلة 4:
- [ ] ✅ تحديث Edit View
- [ ] ✅ تحديث Details View
- [ ] ✅ إضافة JavaScript
- [ ] ✅ اختبار من المتصفح

### المرحلة 5:
- [ ] ✅ إضافة البحث والتصفية
- [ ] ✅ إضافة الإحصائيات
- [ ] ✅ الاختبار الشامل
- [ ] ✅ Commit إلى Git

---

## 🐛 الأخطاء الشائعة وحلولها

### خطأ 1: "ملف الملف غير موجود"
**السبب**: مسار الملف غير صحيح
**الحل**: تحقق من `wwwroot/uploads/resources/` موجود ومكتوب صح

### خطأ 2: "خطأ في الوصول للملف"
**السبب**: صلاحيات المجلد غير كافية
**الحل**: اعطِ الـ IIS صلاحيات Full Control على مجلد uploads

### خطأ 3: "Migration Failed"
**السبب**: الاتصال بـ Database مقطوع
**الحل**: تحقق من connection string في `appsettings.json`

### خطأ 4: "الملف كبير جداً"
**السبب**: الملف أكثر من 500 MB
**الحل**: عدّل `MAX_FILE_SIZE` في Service أو اطلب من المستخدم ملف أصغر

### خطأ 5: "Upload folder not found"
**السبب**: المجلد ما تم إنشاؤه
**الحل**: تأكد من وجود كود `Directory.CreateDirectory()` في Service

---

## 📝 أمثلة الكود

### كيف يرفع المدرب ملف (من JavaScript):
```javascript
const form = new FormData();
form.append('file', fileInput.files[0]);
form.append('lectureId', lectureId);
form.append('resourceType', resourceType); // 0-7
form.append('description', description);
form.append('isRequired', isRequired);

fetch('/api/lectureresources/upload', {
	method: 'POST',
	body: form,
	headers: {
		'Authorization': 'Bearer ' + token
	}
})
.then(r => r.json())
.then(data => {
	if (data.success) {
		alert('تم الرفع بنجاح!');
		location.reload();
	} else {
		alert('خطأ: ' + data.message);
	}
});
```

### كيف يحمل الطالب ملف (من HTML):
```html
<a href="/api/lectureresources/download/@resource.ResourceId" 
   class="btn btn-sm btn-primary">
	<i class="fas fa-download"></i> تحميل
</a>
```

---

## 🎯 معايير النجاح

بعد الانتهاء، يجب أن:

✅ **يمكن للمدرب:**
- [ ] رفع ملفات بدون أخطاء
- [ ] حذف الملفات
- [ ] تعديل معلومات الملف
- [ ] رؤية عدد التحميلات

✅ **يمكن للطالب:**
- [ ] عرض الملفات
- [ ] تحميل الملفات بسهولة
- [ ] رؤية نوع الملف والحجم

✅ **البناء:**
- [ ] لا توجد أخطاء Compilation
- [ ] لا توجد أخطاء Runtime
- [ ] الـ Migrations تطبقت بنجاح

✅ **الأداء:**
- [ ] رفع الملفات سريع (< 10 ثواني)
- [ ] التحميل سريع (< 5 ثواني)
- [ ] الصفحة تحمل بسرعة (< 2 ثانية)

---

## 📞 المساعدة والدعم

إذا واجهت مشكلة:
1. تحقق من messages الخطأ بدقة
2. ابحث عن الخطأ في قسم "الأخطاء الشائعة"
3. تحقق من logs في Output window
4. جرب اعادة البناء (Clean + Rebuild)
5. اعادة تشغيل Visual Studio

---

## 🎓 المراجع المفيدة

- [ASP.NET Core File Upload](https://learn.microsoft.com/en-us/aspnet/core/mvc/models/file-uploads)
- [Entity Framework Core Navigation Properties](https://learn.microsoft.com/en-us/ef/core/modeling/relationships)
- [Async/Await Best Practices](https://learn.microsoft.com/en-us/archive/msdn-magazine/2013/march/async-await-best-practices-in-asynchronous-programming)

---

**الآن أنت جاهز للبدء!** 🚀

**الخطوة التالية:** ابدأ بإنشاء Models في ملف جديد.

