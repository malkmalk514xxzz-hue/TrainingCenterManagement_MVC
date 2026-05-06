using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TrainingCenterManagement_MVC.Models
{
    public class Exam
    {
        [Key]
        public Guid ExamId { get; set; } = Guid.NewGuid();

        [Required, MaxLength(200)]
        public string ExamName { get; set; }

        [MaxLength(2000)]
        public string? Instructions { get; set; }

        /// وقت بدء الامتحان الرسمي لكل الطلاب
        [Required]
        public DateTime StartDateTime { get; set; }

        /// المدة بالدقائق - نفسها لكل الطلاب
        [Required, Range(1, 480)]
        public int DurationMinutes { get; set; } = 60;

        /// درجة النجاح كنسبة مئوية
        [Column(TypeName = "decimal(5,2)")]
        [Range(0, 100)]
        public decimal PassingScore { get; set; } = 60;

        /// عدد المحاولات المسموحة
        [Range(1, 10)]
        public int MaxAttempts { get; set; } = 1;

        /// ترتيب الأسئلة عشوائياً لكل طالب
        public bool IsRandomized { get; set; } = false;

        /// إظهار النتيجة فور إنهاء الامتحان
        public bool ShowResultsImmediately { get; set; } = true;

        /// هل الامتحان منشور (مرئي للطلاب)
        public bool IsPublished { get; set; } = false;

        public bool IsDeleted { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// للتوافق مع الكود القديم — يشير إلى StartDateTime
        [NotMapped]
        public DateTime ExamDate
        {
            get => StartDateTime;
            set => StartDateTime = value;
        }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // FK → Course (M:1 — كورس واحد يمكن أن يحتوي على أكثر من امتحان)
        [Required]
        public Guid CourseId { get; set; }
        public Course Course { get; set; }

        // FK → Trainer (من أنشأ الامتحان)
        [Required]
        public Guid TrainerId { get; set; }
        public Trainer Trainer { get; set; }

        public ICollection<Certificate> Certificates { get; set; } = new List<Certificate>();
        public ICollection<ExamQuestion> ExamQuestions { get; set; } = new List<ExamQuestion>();
        public ICollection<ExamAttempt> ExamAttempts { get; set; } = new List<ExamAttempt>();

        // ====== Computed helpers (NotMapped) ======

        [NotMapped]
        public DateTime EndDateTime => StartDateTime.AddMinutes(DurationMinutes);

        [NotMapped]
        public bool IsActive => DateTime.UtcNow >= StartDateTime && DateTime.UtcNow <= EndDateTime && IsPublished;

        [NotMapped]
        public bool HasStarted => DateTime.UtcNow >= StartDateTime;

        [NotMapped]
        public bool HasEnded => DateTime.UtcNow > EndDateTime;
    }
}
