using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using TrainingCenterManagement_MVC.Models.Enums;

namespace TrainingCenterManagement_MVC.Models
{
    /// سؤال في بنك الأسئلة — يمكن إعادة استخدامه في أكثر من امتحان
    public class Question
    {
        [Key]
        public Guid QuestionId { get; set; } = Guid.NewGuid();

        [Required, MaxLength(2000)]
        public string QuestionText { get; set; }

        public QuestionType QuestionType { get; set; } = QuestionType.MultipleChoice;

        public DifficultyLevel DifficultyLevel { get; set; } = DifficultyLevel.Medium;

        /// JSON: ["الخيار أ", "الخيار ب", "الخيار ج", "الخيار د"]
        /// للـ TrueFalse لا يلزم تعبئته (يُستخدم True / False)
        public string? OptionsJson { get; set; }

        /// للـ MC: نص الخيار الصحيح كما ورد في Options
        /// للـ TrueFalse: "True" أو "False"
        /// للـ ShortAnswer: النص المتوقع (مقارنة case-insensitive)
        /// للـ Essay: null (تصحيح يدوي)
        [MaxLength(1000)]
        public string? CorrectAnswer { get; set; }

        /// تظهر للطالب بعد التصحيح
        [MaxLength(2000)]
        public string? Explanation { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        [Range(0.25, 100)]
        public decimal DefaultPoints { get; set; } = 1;

        public bool IsDeleted { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // صاحب السؤال (المدرب الذي أنشأه — Question Bank per Trainer)
        [Required]
        public Guid TrainerId { get; set; }
        public Trainer Trainer { get; set; }

        public ICollection<ExamQuestion> ExamQuestions { get; set; } = new List<ExamQuestion>();
        public ICollection<StudentAnswer> StudentAnswers { get; set; } = new List<StudentAnswer>();

        // ====== Helpers ======

        [NotMapped]
        public List<string> Options
        {
            get => string.IsNullOrWhiteSpace(OptionsJson)
                ? new List<string>()
                : JsonSerializer.Deserialize<List<string>>(OptionsJson) ?? new List<string>();
            set => OptionsJson = JsonSerializer.Serialize(value);
        }

        /// هل هذا السؤال قابل للتصحيح التلقائي؟
        [NotMapped]
        public bool IsAutoGradable =>
            QuestionType == QuestionType.MultipleChoice ||
            QuestionType == QuestionType.TrueFalse ||
            QuestionType == QuestionType.ShortAnswer;
    }
}
