using System.ComponentModel.DataAnnotations;

namespace TrainingCenterManagement_MVC.Models
{
    public class LiveSession
    {
        [Key]
        public Guid LiveSessionId { get; set; } = Guid.NewGuid();

        [Required]
        public Guid CourseId { get; set; }
        public Course Course { get; set; } = null!;

        [Required, MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Description { get; set; }

        public DateTime ScheduledAt { get; set; }

        // unique Jitsi room identifier
        public string JitsiRoomName { get; set; } = string.Empty;

        public string CreatedByUserId { get; set; } = string.Empty;
        public ApplicationUser CreatedBy { get; set; } = null!;

        public bool IsCancelled { get; set; } = false;

        // Session duration in minutes (null = unlimited)
        public int? DurationMinutes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
