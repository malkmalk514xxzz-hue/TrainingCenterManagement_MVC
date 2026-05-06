using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using TrainingCenterManagement_MVC.Models.Enums;

namespace TrainingCenterManagement_MVC.DTOs
{
    // ─────────────────────────────────────────────────────────────
    //  INPUT DTOs  (Trainer → Server)
    // ─────────────────────────────────────────────────────────────

    public class CreateExamDto
    {
        [Required(ErrorMessage = "اسم الامتحان مطلوب")]
        [MaxLength(200)]
        public string ExamName { get; set; }

        [MaxLength(2000)]
        public string? Instructions { get; set; }

        [Required(ErrorMessage = "وقت البدء مطلوب")]
        public DateTime StartDateTime { get; set; }

        [Required]
        [Range(1, 480, ErrorMessage = "المدة بين 1 و 480 دقيقة")]
        public int DurationMinutes { get; set; } = 60;

        [Range(0, 100)]
        public decimal PassingScore { get; set; } = 60;

        [Range(1, 10)]
        public int MaxAttempts { get; set; } = 1;

        public bool IsRandomized { get; set; } = false;
        public bool ShowResultsImmediately { get; set; } = true;

        [Required(ErrorMessage = "يجب اختيار الكورس")]
        public Guid CourseId { get; set; }

        /// معرّفات الأسئلة المُضافة عند الإنشاء (اختياري)
        public List<Guid>? QuestionIds { get; set; }
    }

    public class UpdateExamDto
    {
        [Required]
        public Guid ExamId { get; set; }

        [Required, MaxLength(200)]
        public string ExamName { get; set; }

        [MaxLength(2000)]
        public string? Instructions { get; set; }

        [Required]
        public DateTime StartDateTime { get; set; }

        [Required, Range(1, 480)]
        public int DurationMinutes { get; set; }

        [Range(0, 100)]
        public decimal PassingScore { get; set; }

        [Range(1, 10)]
        public int MaxAttempts { get; set; }

        public bool IsRandomized { get; set; }
        public bool ShowResultsImmediately { get; set; }
    }

    public class AddQuestionToExamDto
    {
        [Required]
        public Guid ExamId { get; set; }

        [Required]
        public Guid QuestionId { get; set; }

        public int OrderIndex { get; set; } = 0;

        [Range(0.25, 100)]
        public decimal? PointsOverride { get; set; }
    }

    // ─────────────────────────────────────────────────────────────
    //  OUTPUT DTOs  (Server → View / API)
    // ─────────────────────────────────────────────────────────────

    public class ExamDto
    {
        public Guid ExamId { get; set; }
        public string ExamName { get; set; }
        public string? Instructions { get; set; }
        public DateTime StartDateTime { get; set; }
        public int DurationMinutes { get; set; }
        public decimal PassingScore { get; set; }
        public int MaxAttempts { get; set; }
        public bool IsRandomized { get; set; }
        public bool ShowResultsImmediately { get; set; }
        public bool IsPublished { get; set; }
        public Guid CourseId { get; set; }
        public string CourseName { get; set; }
        public string TrainerName { get; set; }
        public int QuestionCount { get; set; }
        public decimal TotalPoints { get; set; }
        public bool IsActive { get; set; }
        public bool HasStarted { get; set; }
        public bool HasEnded { get; set; }
    }

    public class ExamSummaryDto
    {
        public Guid ExamId { get; set; }
        public string ExamName { get; set; }
        public DateTime StartDateTime { get; set; }
        public int DurationMinutes { get; set; }
        public bool IsPublished { get; set; }
        public bool IsActive { get; set; }
        public int AttemptCount { get; set; }

        /// للطالب: هل بدأ هذا الامتحان أم لا
        public bool? HasAttempted { get; set; }
        public AttemptStatus? LastAttemptStatus { get; set; }
        public Guid? LastAttemptId { get; set; }
    }
}
