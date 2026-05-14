using System.ComponentModel.DataAnnotations;

namespace TrainingCenterManagement_MVC.Models
{
    public class AIChatMessage
    {
        [Key]
        public Guid MessageId { get; set; } = Guid.NewGuid();

        [Required]
        public string UserId { get; set; } = string.Empty;
        public ApplicationUser User { get; set; } = null!;

        [Required, MaxLength(2000)]
        public string UserMessage { get; set; } = string.Empty;

        [MaxLength(5000)]
        public string? AIResponse { get; set; }

        public AIQuestionType QuestionType { get; set; }

        public bool IsAnswered { get; set; } = false;

        public bool RequiresManualReview { get; set; } = false;

        [MaxLength(500)]
        public string? ReviewReason { get; set; }

        [MaxLength(50)]
        public string? UserRole { get; set; }

        [MaxLength(1000)]
        public string? DataAccessLog { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? AnsweredAt { get; set; }

        public int? Rating { get; set; }

        [MaxLength(500)]
        public string? UserFeedback { get; set; }

        public bool? IsHelpful { get; set; }

        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
    }

    public enum AIQuestionType
    {
        General             = 0,
        Personal            = 1,
        Navigation          = 2,
        DataRequest         = 3,
        FeatureExplanation  = 4,
        Recommendation      = 5,
        TechnicalSupport    = 6,
        Other               = 7
    }
}
