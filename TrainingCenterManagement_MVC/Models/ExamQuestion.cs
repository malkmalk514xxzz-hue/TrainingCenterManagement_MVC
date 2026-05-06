using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TrainingCenterManagement_MVC.Models
{
    /// جدول وسيط بين Exam و Question
    /// يسمح بإضافة نفس السؤال لأكثر من امتحان مع إمكانية تخصيص الدرجة والترتيب
    public class ExamQuestion
    {
        [Key]
        public Guid ExamQuestionId { get; set; } = Guid.NewGuid();

        [Required]
        public Guid ExamId { get; set; }
        public Exam Exam { get; set; }

        [Required]
        public Guid QuestionId { get; set; }
        public Question Question { get; set; }

        /// ترتيب السؤال داخل الامتحان
        public int OrderIndex { get; set; } = 0;

        /// تجاوز الدرجة الافتراضية للسؤال — null = استخدم DefaultPoints من Question
        [Column(TypeName = "decimal(5,2)")]
        public decimal? PointsOverride { get; set; }

        // ====== Helper ======

        [NotMapped]
        public decimal EffectivePoints =>
            PointsOverride.HasValue ? PointsOverride.Value : (Question?.DefaultPoints ?? 1);
    }
}
