using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using TrainingCenterManagement_MVC.Models.Enums;

namespace TrainingCenterManagement_MVC.DTOs
{
    // ─────────────────────────────────────────────────────────────
    //  INPUT
    // ─────────────────────────────────────────────────────────────

    public class CreateQuestionDto
    {
        [Required(ErrorMessage = "نص السؤال مطلوب")]
        [MaxLength(2000)]
        public string QuestionText { get; set; }

        public QuestionType QuestionType { get; set; } = QuestionType.MultipleChoice;

        public DifficultyLevel DifficultyLevel { get; set; } = DifficultyLevel.Medium;

        /// للـ MultipleChoice: قائمة الخيارات (2–6 خيارات)
        public List<string>? Options { get; set; }

        /// الإجابة الصحيحة
        [MaxLength(1000)]
        public string? CorrectAnswer { get; set; }

        [MaxLength(2000)]
        public string? Explanation { get; set; }

        [Range(0.25, 100)]
        public decimal DefaultPoints { get; set; } = 1;
    }

    public class UpdateQuestionDto
    {
        [Required]
        public Guid QuestionId { get; set; }

        [Required, MaxLength(2000)]
        public string QuestionText { get; set; }

        public QuestionType QuestionType { get; set; }
        public DifficultyLevel DifficultyLevel { get; set; }
        public List<string>? Options { get; set; }

        [MaxLength(1000)]
        public string? CorrectAnswer { get; set; }

        [MaxLength(2000)]
        public string? Explanation { get; set; }

        [Range(0.25, 100)]
        public decimal DefaultPoints { get; set; }
    }

    // ─────────────────────────────────────────────────────────────
    //  OUTPUT — للمدرب (يرى الإجابة الصحيحة)
    // ─────────────────────────────────────────────────────────────

    public class QuestionDto
    {
        public Guid QuestionId { get; set; }
        public string QuestionText { get; set; }
        public QuestionType QuestionType { get; set; }
        public DifficultyLevel DifficultyLevel { get; set; }
        public List<string> Options { get; set; } = new();
        public string? CorrectAnswer { get; set; }
        public string? Explanation { get; set; }
        public decimal DefaultPoints { get; set; }
        public int OrderIndex { get; set; }
        public decimal EffectivePoints { get; set; }
        public int UsedInExamsCount { get; set; }
    }

    // ─────────────────────────────────────────────────────────────
    //  OUTPUT — للطالب (بدون الإجابة الصحيحة)
    // ─────────────────────────────────────────────────────────────

    public class QuestionForStudentDto
    {
        public Guid QuestionId { get; set; }
        public string QuestionText { get; set; }
        public QuestionType QuestionType { get; set; }
        public List<string> Options { get; set; } = new();
        public int OrderIndex { get; set; }
        public decimal Points { get; set; }

        /// الإجابة المحفوظة مسبقاً (لدعم الاستئناف)
        public string? SavedAnswer { get; set; }
    }

    // ─────────────────────────────────────────────────────────────
    //  تصحيح يدوي (Essay) من المدرب
    // ─────────────────────────────────────────────────────────────

    public class ManualGradeDto
    {
        [Required]
        public Guid AnswerId { get; set; }

        [Range(0, 100)]
        public decimal PointsEarned { get; set; }

        [MaxLength(1000)]
        public string? Feedback { get; set; }
    }
}
