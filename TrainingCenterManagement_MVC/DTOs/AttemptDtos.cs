using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using TrainingCenterManagement_MVC.Models.Enums;

namespace TrainingCenterManagement_MVC.DTOs
{
    // ─────────────────────────────────────────────────────────────
    //  INPUT — من الطالب
    // ─────────────────────────────────────────────────────────────

    /// حفظ إجابة سؤال واحد (auto-save أثناء الامتحان)
    public class SaveAnswerDto
    {
        [Required]
        public Guid AttemptId { get; set; }

        [Required]
        public Guid QuestionId { get; set; }

        /// يمكن أن يكون null لو الطالب مسح إجابته
        [MaxLength(5000)]
        public string? AnswerText { get; set; }
    }

    /// إرسال الامتحان نهائياً
    public class SubmitExamDto
    {
        [Required]
        public Guid AttemptId { get; set; }

        /// الإجابات النهائية — يمكن إرسالها كلها دفعة واحدة
        public List<SaveAnswerDto> Answers { get; set; } = new();
    }

    /// تسجيل تغيير التبويب (anti-cheat)
    public class TabSwitchDto
    {
        [Required]
        public Guid AttemptId { get; set; }
    }

    /// تطبيق عقوبة على محاولة طالب
    public class ApplyPenaltyDto
    {
        [Required]
        public Guid AttemptId { get; set; }

        [Required]
        public PenaltyType PenaltyType { get; set; }

        /// قيمة الخصم — نقاط أو نسبة مئوية (لا تُستخدم عند ZeroGrade)
        [Range(0.01, 1000)]
        public decimal? DeductionValue { get; set; }

        [Required, MaxLength(500)]
        public string Reason { get; set; } = string.Empty;
    }

    // ─────────────────────────────────────────────────────────────
    //  OUTPUT — للطالب أثناء الامتحان
    // ─────────────────────────────────────────────────────────────

    /// بيانات الامتحان بعد الضغط على "ابدأ"
    public class ExamAttemptDto
    {
        public Guid AttemptId { get; set; }
        public Guid ExamId { get; set; }
        public string ExamName { get; set; }
        public string? Instructions { get; set; }
        public int DurationMinutes { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime Deadline { get; set; }
        public int SecondsRemaining { get; set; }
        public AttemptStatus Status { get; set; }
        public int AttemptNumber { get; set; }
        public List<QuestionForStudentDto> Questions { get; set; } = new();
    }

    // ─────────────────────────────────────────────────────────────
    //  OUTPUT — النتائج
    // ─────────────────────────────────────────────────────────────

    /// نتيجة سؤال واحد في صفحة النتائج
    public class AnswerResultDto
    {
        public Guid AnswerId { get; set; }
        public Guid QuestionId { get; set; }
        public string QuestionText { get; set; }
        public QuestionType QuestionType { get; set; }
        public List<string> Options { get; set; } = new();
        public string? StudentAnswer { get; set; }
        public string? CorrectAnswer { get; set; }
        public bool? IsCorrect { get; set; }
        public decimal PointsEarned { get; set; }
        public decimal MaxPoints { get; set; }
        public string? Explanation { get; set; }
        public string? TrainerFeedback { get; set; }
    }

    /// النتيجة الكاملة لطالب بعد إنهاء الامتحان
    public class ExamResultDto
    {
        public Guid AttemptId { get; set; }
        public Guid ExamId { get; set; }
        public string ExamName { get; set; }
        public string TraineeName { get; set; }
        public decimal TotalScore { get; set; }
        public decimal MaxScore { get; set; }
        public decimal ScorePercentage { get; set; }
        public decimal PassingScore { get; set; }
        public bool IsPassed { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime SubmittedAt { get; set; }
        public int TimeTakenMinutes { get; set; }
        public bool HasPendingEssays { get; set; }

        // بيانات المخالفات (للمدرب فقط)
        public int TabSwitchCount { get; set; }
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }

        // بيانات العقوبة
        public bool PenaltyApplied { get; set; }
        public string? PenaltyReason { get; set; }
        public decimal? OriginalTotalScore { get; set; }
        public decimal? OriginalScorePercentage { get; set; }

        /// متاح فقط إذا كان ShowResultsImmediately = true
        public List<AnswerResultDto>? AnswerResults { get; set; }
    }

    // ─────────────────────────────────────────────────────────────
    //  OUTPUT — للمدرب (نظرة شاملة على كل الطلاب)
    // ─────────────────────────────────────────────────────────────

    public class AttemptSummaryDto
    {
        public Guid AttemptId { get; set; }
        public Guid TraineeId { get; set; }
        public string TraineeName { get; set; }
        public string TraineeEmail { get; set; }
        public decimal? ScorePercentage { get; set; }
        public bool? IsPassed { get; set; }
        public AttemptStatus Status { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public int TabSwitchCount { get; set; }
        public string? IpAddress { get; set; }
        public bool HasPendingEssays { get; set; }
        public bool PenaltyApplied { get; set; }
        public string? PenaltyReason { get; set; }
    }

    public class TrainerExamResultsDto
    {
        public Guid ExamId { get; set; }
        public string ExamName { get; set; }
        public int TotalAttempts { get; set; }
        public int SubmittedCount { get; set; }
        public int PassedCount { get; set; }
        public int FailedCount { get; set; }
        public decimal? AverageScore { get; set; }
        public decimal? HighestScore { get; set; }
        public decimal? LowestScore { get; set; }
        public List<AttemptSummaryDto> Attempts { get; set; } = new();
    }
}
