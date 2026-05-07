using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TrainingCenterManagement_MVC.Models
{
    /// <summary>
    /// نموذج لتخزين معلومات الفيديوهات المرتبطة بالدروس
    /// يدعم تحميل الفيديوهات المباشرة واستيراد من YouTube
    /// </summary>
    public class LectureVideo
    {
        [Key]
        public Guid VideoId { get; set; } = Guid.NewGuid();

        [Required]
        public Guid LectureId { get; set; }
        public Lecture Lecture { get; set; }

        /// <summary>
        /// عنوان الفيديو
        /// </summary>
        [Required, MaxLength(500)]
        public string VideoTitle { get; set; }

        /// <summary>
        /// وصف الفيديو
        /// </summary>
        [MaxLength(2000)]
        public string Description { get; set; }

        /// <summary>
        /// نوع الفيديو: Uploaded أو YouTube
        /// </summary>
        [Required]
        public VideoSourceType VideoSourceType { get; set; }

        /// <summary>
        /// مسار الفيديو المحمل (محلي) أو null إذا كان من YouTube
        /// </summary>
        public string LocalFilePath { get; set; }

        /// <summary>
        /// معرف الفيديو على YouTube
        /// يتم استخراجه من الرابط
        /// </summary>
        public string YouTubeVideoId { get; set; }

        /// <summary>
        /// الرابط الكامل للفيديو (YouTube أو محلي)
        /// </summary>
        [Required]
        public string VideoUrl { get; set; }

        /// <summary>
        /// مدة الفيديو بالدقائق
        /// </summary>
        public int? DurationMinutes { get; set; }

        /// <summary>
        /// حجم الملف بـ Bytes (محلي فقط)
        /// </summary>
        public long? FileSizeInBytes { get; set; }

        /// <summary>
        /// صورة مصغرة للفيديو
        /// </summary>
        public string ThumbnailUrl { get; set; }

        /// <summary>
        /// ترتيب الفيديو في الدرس (إذا كان هناك أكثر من فيديو واحد)
        /// </summary>
        public int DisplayOrder { get; set; } = 1;

        /// <summary>
        /// عدد مرات المشاهدة
        /// </summary>
        public int ViewCount { get; set; } = 0;

        /// <summary>
        /// هل الفيديو نشط أو محجوب
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// هل الفيديو مطلوب قبل الامتحان
        /// </summary>
        public bool IsRequired { get; set; } = true;

        /// <summary>
        /// تاريخ الإنشاء
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// آخر تحديث
        /// </summary>
        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// من قام برفع الفيديو (معرف المدرب)
        /// </summary>
        public Guid UploadedByTrainerId { get; set; }
        public Trainer UploadedByTrainer { get; set; }

        /// <summary>
        /// ملاحظات إدارية
        /// </summary>
        [MaxLength(500)]
        public string AdminNotes { get; set; }

        /// <summary>
        /// هل الفيديو محذوف (soft delete)
        /// </summary>
        public bool IsDeleted { get; set; } = false;

        public DateTime? DeletedAt { get; set; }

        public ICollection<VideoView> Views { get; set; } = new List<VideoView>();
    }

    /// <summary>
    /// تعداد لأنواع مصادر الفيديو
    /// </summary>
    public enum VideoSourceType
    {
        /// <summary>
        /// فيديو محمل مباشرة على الموقع
        /// </summary>
        Uploaded,

        /// <summary>
        /// فيديو مستورد من YouTube
        /// </summary>
        YouTube
    }

    /// <summary>
    /// نموذج لتتبع مشاهدات الفيديو
    /// </summary>
    public class VideoView
    {
        [Key]
        public Guid ViewId { get; set; } = Guid.NewGuid();

        [Required]
        public Guid VideoId { get; set; }
        public LectureVideo Video { get; set; }

        [Required]
        public Guid TraineeId { get; set; }
        public Trainee Trainee { get; set; }

        /// <summary>
        /// وقت البدء بمشاهدة الفيديو
        /// </summary>
        public DateTime ViewedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// الوقت المستغرق بالثوانية
        /// </summary>
        public int WatchedSeconds { get; set; } = 0;

        /// <summary>
        /// نسبة المشاهدة (0-100%)
        /// </summary>
        public double WatchPercentage { get; set; } = 0;

        /// <summary>
        /// هل تمت مشاهدة الفيديو بالكامل
        /// </summary>
        public bool IsCompleted { get; set; } = false;
    }
}
