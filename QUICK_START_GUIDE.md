# 🚀 دليل الاستخدام السريع للتعديلات المستردة

## ⚡ الخطوات الأساسية الفورية

### 1️⃣ تطبيق Database Migrations

```bash
cd "D:\Asp.Net Core API\Forth Year Final Project\TrainingCenterManagement_MVC"
dotnet ef database update
```

**ماذا سيحدث:**
- ✅ إضافة جدول `PaymentRequests` الجديد
- ✅ إضافة حقول استخراج الإيصال
- ✅ إضافة حقول تعطيل الطالب
- ✅ إضافة مدة الجلسة
- ✅ تحديث البيانات الأساسية

---

## 🎯 الميزات الجديدة المتاحة

### A. نظام طلب الدفع 💰

#### الوصول إليها:
```
📍 /PaymentRequests/Submit?courseId={courseId}&method=ShamCash
```

#### الميزات:
- 📤 رفع إيصال (PDF/صورة)
- 🔍 استخراج تلقائي للبيانات
- 📋 متابعة حالة الطلب
- ✅ موافقة/رفض من الإدارة

#### المتطلبات:
```
✓ يجب تسجيل الدخول (Trainee)
✓ يجب أن تكون مسجلاً بالدورة
✓ رفع إيصال دفع صحيح
```

#### مثال الاستخدام:
```csharp
// من صفحة الدورة:
<a href="/PaymentRequests/Submit?courseId=@course.CourseId&method=ShamCash">
	رفع إيصال الدفع
</a>
```

---

### B. تحليل ShamCash QR 🔍

#### الملف:
```
wwwroot/js/shamcash-analyzer.js
```

#### الوظائف المتاحة:
```javascript
// تحليل صورة QR
analyzeShamQr(input)
// يرجع:
// - اسم الحساب (SenderName)
// - الرقم التسلصلي (SerialNumber)
// - المبلغ (Amount)
// - الرقم المرجعي (ReferenceNumber)
// - التاريخ والوقت (DateTime)

// إضافة زر تحليل في الـ HTML
<input type="file" onchange="analyzeShamQr(this)" />
```

#### مثال الاستخدام:
```html
<form>
	<input type="file" 
		   id="receiptInput" 
		   accept="image/*" 
		   onchange="analyzeShamQr(this)">

	<!-- نتائج الاستخراج -->
	<div id="shamQrResult" style="display:none;">
		<p id="resultAccountName"></p>
		<p id="resultSerialNumber"></p>
		<p id="resultAmount"></p>
	</div>
</form>
```

---

### C. تعطيل الطلاب 🚫

#### الوصول إليها:
```
📍 /Courses/Details/{courseId}
(أو من لوحة التحكم)
```

#### الميزات:
- 🚫 تعطيل طالب عن دورة
- 📝 تسجيل السبب
- 📅 تتبع التاريخ والمسؤول
- 👁️ عرض حالة الطالب

#### المتطلبات:
```
✓ يجب أن تكون مدرباً أو مسؤولاً
✓ الطالب يجب أن يكون مسجلاً بالدورة
```

#### مثال الاستخدام:
```csharp
// في View:
@if (courseTrainee.IsSuspended)
{
	<div class="alert alert-warning">
		🚫 معطل - السبب: @courseTrainee.SuspensionReason
		<br />
		منذ: @courseTrainee.SuspendedAt?.ToLocalTime()
	</div>
}
```

---

### D. مدة الجلسات المباشرة 📺

#### الوصول إليها:
```
📍 /LiveSessions/Create
📍 /LiveSessions/Edit/{id}
```

#### الميزات:
- ⏱️ تحديد مدة الجلسة
- 🕐 وقت البداية والنهاية
- 📊 عرض المدة المتبقية

#### مثال الاستخدام:
```csharp
// في Model:
var session = new LiveSession
{
	StartTime = DateTime.Now,
	EndTime = DateTime.Now.AddMinutes(90),
	Duration = TimeSpan.FromMinutes(90)
};
```

---

## 📊 Database Schema الجديد

### جدول PaymentRequest

```sql
PaymentRequest
├── RequestId (GUID) - المفتاح
├── TraineeId (GUID) - الطالب
├── CourseId (GUID) - الدورة
├── Amount (decimal) - المبلغ
├── Currency (enum) - العملة
├── Method (enum) - الطريقة (ShamCash, Binance)
├── ReceiptFilePath (string) - مسار الإيصال
├── TransactionReference (string) - الرقم المرجعي
├── StudentNotes (string) - ملاحظات الطالب
├── Status (enum) - الحالة
├── RejectionReason (string) - سبب الرفض
├── AdminNotes (string) - ملاحظات الإدارة
├── CreatedAt (DateTime) - تاريخ الإنشاء
├── ProcessedAt (DateTime?) - تاريخ المعالجة
├── ProcessedByAdminId (string) - المسؤول
├── RcptSenderName (string) - اسم المرسل
├── RcptRecipientName (string) - اسم المستقبل
├── RcptRecipientAccount (string) - حساب المستقبل
├── RcptAmount (string) - المبلغ المستخرج
├── RcptPaymentDate (string) - تاريخ الدفع
└── RcptOperationNumber (string) - رقم العملية
```

### تحديثات CourseTrainee

```sql
CourseTrainee
├── IsSuspended (bool) - هل معطل
├── SuspensionReason (string) - سبب التعطيل
├── SuspendedAt (DateTime?) - متى تم التعطيل
└── SuspendedBy (string) - من قام بالتعطيل
```

### تحديثات LiveSession

```sql
LiveSession
└── Duration (TimeSpan) - مدة الجلسة
```

---

## 🎮 Controllers المتاحة

### PaymentRequestsController

#### Endpoints:

```csharp
// رفع إيصال جديد
[HttpGet("/PaymentRequests/Submit")]
public IActionResult Submit(Guid? courseId, string method)

// عرض طلباتي
[HttpGet("/PaymentRequests/MyRequests")]
public IActionResult MyRequests()

// تفاصيل الطلب
[HttpGet("/PaymentRequests/Details/{id}")]
public IActionResult Details(Guid id)

// الطلبات المعلقة (إدارة)
[HttpGet("/PaymentRequests/Pending")]
[Authorize(Roles = "Admin,Receptionist")]
public IActionResult Pending()

// موافقة على الطلب (إدارة)
[HttpPost("/PaymentRequests/Approve/{id}")]
[Authorize(Roles = "Admin")]
public IActionResult Approve(Guid id)

// رفض الطلب (إدارة)
[HttpPost("/PaymentRequests/Reject/{id}")]
[Authorize(Roles = "Admin")]
public IActionResult Reject(Guid id, string reason)
```

---

## 📝 الملفات الرئيسية

### Models:
```
✅ Models/PaymentRequest.cs
✅ Models/CourseTrainee.cs (تحديث)
✅ Models/LiveSession.cs (تحديث)
```

### Controllers:
```
✅ Controllers/PaymentRequestsController.cs
```

### Helpers:
```
✅ Helpers/ReceiptExtractor.cs
```

### JavaScript:
```
✅ wwwroot/js/shamcash-analyzer.js
```

### Views:
```
✅ Views/PaymentRequests/Submit.cshtml
✅ Views/PaymentRequests/MyRequests.cshtml
✅ Views/PaymentRequests/Review.cshtml
✅ Views/PaymentRequests/Manage.cshtml
✅ Views/Account/SignUp.cshtml (جديد)
✅ Views/Home/Error404.cshtml (جديد)
✅ Views/LiveSessions/Create.cshtml
✅ Views/LiveSessions/Edit.cshtml
```

### Migrations:
```
✅ 20260518122840_AddLiveSessionDuration
✅ 20260518124750_MakeRatingCommentNullable
✅ 20260519061513_AddCourseTraineeSuspension
✅ 20260519064505_AddPaymentRequests
✅ 20260519080908_AddReceiptExtractedFields
```

---

## 🧪 اختبار سريع

### اختبار نظام الدفع:
```
1. سجل دخول كطالب
2. انتقل إلى دورة مسجل بها
3. اختر "رفع إيصال الدفع"
4. حمّل صورة إيصال
5. تحقق من استخراج البيانات
6. أرسل الطلب
7. (كمسؤول) وافق أو ارفض الطلب
```

### اختبار تعطيل الطالب:
```
1. سجل دخول كمدرب/مسؤول
2. انتقل لتفاصيل الدورة
3. ابحث عن الطالب في القائمة
4. اختر "تعطيل" أو "suspend"
5. أدخل السبب
6. تحقق من التحديث
```

### اختبار الجلسات المباشرة:
```
1. انتقل إلى إنشاء جلسة مباشرة
2. حدد المدة (مثلاً 90 دقيقة)
3. احفظ الجلسة
4. تحقق من عرض المدة
```

---

## 🔧 استكشاف الأخطاء

### خطأ: "Tables don't exist"
```bash
# الحل:
dotnet ef database update
```

### خطأ: "File upload failed"
```bash
# تحقق من:
# 1. تصاريح المجلد wwwroot/uploads
# 2. حجم الملف
# 3. صيغة الملف (PDF/JPG/PNG)
```

### خطأ: "QR Analysis not working"
```bash
# تحقق من:
# 1. تضمين shamcash-analyzer.js
# 2. جودة الصورة
# 3. متصفح حديث (يدعم FileReader)
```

---

## 📚 مراجع إضافية

### ملفات التوثيق:
```
✅ RECOVERED_SHAMCASH_CHANGES_SUMMARY.md
✅ RECOVERY_STATUS_REPORT.md
✅ QUICK_START_GUIDE.md (هذا الملف)
```

### روابط مفيدة:
```
📍 Commit: 2001954 - add ShamCash adnd fix buges
📍 Branch: master
📍 Remote: origin/master
```

---

## ✅ تذكير مهم

```
⚠️ تأكد من:
  ✓ تشغيل dotnet ef database update
  ✓ إعادة تشغيل التطبيق بعد التحديث
  ✓ اختبار الميزات الجديدة
  ✓ حفظ التغييرات إذا لزم الأمر

🎯 الأولويات:
  1. تطبيق الـ migrations
  2. اختبار رفع الإيصالات
  3. اختبار التحليل الآلي
  4. اختبار تعطيل الطلاب
  5. اختبار الجلسات المباشرة
```

---

## 🎊 مبروك!

**تم استرجاع جميع التعديلات بنجاح! 🎉**

استمتع بالميزات الجديدة وأخبرني إذا واجهت أي مشاكل! 💪

