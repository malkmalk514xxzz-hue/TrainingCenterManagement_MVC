using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TrainingCenterManagement_MVC.Models
{
    /// إجابة طالب على سؤال واحد في محاولة معينة
    public class StudentAnswer
    {
        [Key]
        public Guid AnswerId { get; set; } = Guid.NewGuid();

        [Required]
        public Guid AttemptId { get; set; }
        public ExamAttempt Attempt { get; set; }

        [Required]
        public Guid QuestionId { get; set; }
        public Question Question { get; set; }

        /// نص الإجابة:
        /// MC / TrueFalse: نص الخيار المختار
        /// ShortAnswer / Essay: نص مكتوب
        [MaxLength(5000)]
        public string? AnswerText { get; set; }

        /// null = لم يُصحَّح بعد (Essay)، true/false = نتيجة التصحيح
        public bool? IsCorrect { get; set; }

        /// الدرجة المكتسبة على هذا السؤال
        [Column(TypeName = "decimal(5,2)")]
        public decimal PointsEarned { get; set; } = 0;

        /// ملاحظة المدرب على إجابة الـ Essay
        [MaxLength(1000)]
        public string? TrainerFeedback { get; set; }

        public DateTime AnsweredAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastModifiedAt { get; set; }

        /// هل صُحِّح هذا السؤال يدوياً (Essay) أم تلقائياً؟
        public bool IsManuallyGraded { get; set; } = false;
    }
}
