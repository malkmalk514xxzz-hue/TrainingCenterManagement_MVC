using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TrainingCenterManagement_MVC.Models.Enums;

namespace TrainingCenterManagement_MVC.Models
{
    /// محاولة طالب في امتحان معين
    public class ExamAttempt
    {
        [Key]
        public Guid AttemptId { get; set; } = Guid.NewGuid();

        [Required]
        public Guid ExamId { get; set; }
        public Exam Exam { get; set; }

        [Required]
        public Guid TraineeId { get; set; }
        public Trainee Trainee { get; set; }

        public AttemptStatus Status { get; set; } = AttemptStatus.InProgress;

        /// لحظة ضغط الطالب على "ابدأ الامتحان"
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;

        /// لحظة الإرسال أو انتهاء الوقت
        public DateTime? SubmittedAt { get; set; }

        // ====== النتيجة ======

        /// مجموع الدرجات المكتسبة
        [Column(TypeName = "decimal(7,2)")]
        public decimal? TotalScore { get; set; }

        /// إجمالي الدرجات الكاملة للامتحان (لحساب النسبة)
        [Column(TypeName = "decimal(7,2)")]
        public decimal? MaxScore { get; set; }

        /// النسبة المئوية (0–100)
        [Column(TypeName = "decimal(5,2)")]
        public decimal? ScorePercentage { get; set; }

        public bool? IsPassed { get; set; }

        // ====== Anti-Cheating ======

        [MaxLength(45)]
        public string? IpAddress { get; set; }

        [MaxLength(500)]
        public string? UserAgent { get; set; }

        /// عدد مرات مغادرة تبويب الامتحان
        public int TabSwitchCount { get; set; } = 0;

        /// آخر وقت سُجّل فيه نشاط (لاكتشاف الانقطاع)
        public DateTime? LastActivityAt { get; set; }

        // ====== Penalty ======

        /// هل طُبِّقت عقوبة على هذه المحاولة
        public bool PenaltyApplied { get; set; } = false;

        /// سبب العقوبة المُدخَل من المدرب
        [MaxLength(500)]
        public string? PenaltyReason { get; set; }

        /// الدرجة الأصلية قبل تطبيق العقوبة
        [Column(TypeName = "decimal(7,2)")]
        public decimal? OriginalTotalScore { get; set; }

        /// النسبة المئوية الأصلية قبل تطبيق العقوبة
        [Column(TypeName = "decimal(5,2)")]
        public decimal? OriginalScorePercentage { get; set; }

        // ====== Reconnection Support ======
        /// رقم المحاولة (1 للمحاولة الأولى، 2 للثانية...)
        public int AttemptNumber { get; set; } = 1;

        public ICollection<StudentAnswer> StudentAnswers { get; set; } = new List<StudentAnswer>();

        // ====== Computed ======

        [NotMapped]
        public DateTime? Deadline =>
            Exam != null ? StartedAt.AddMinutes(Exam.DurationMinutes) : null;

        [NotMapped]
        public int SecondsRemaining
        {
            get
            {
                if (Exam == null || Status != AttemptStatus.InProgress) return 0;
                var remaining = StartedAt.AddMinutes(Exam.DurationMinutes) - DateTime.UtcNow;
                return Math.Max(0, (int)remaining.TotalSeconds);
            }
        }

        [NotMapped]
        public bool IsExpired =>
            Exam != null && DateTime.UtcNow > StartedAt.AddMinutes(Exam.DurationMinutes);
    }
}
