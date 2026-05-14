using System.ComponentModel.DataAnnotations;

namespace TrainingCenterManagement_MVC.Models
{
    public class AIAccessLog
    {
        [Key]
        public Guid LogId { get; set; } = Guid.NewGuid();

        [Required]
        public string UserId { get; set; } = string.Empty;
        public ApplicationUser User { get; set; } = null!;

        [Required, MaxLength(50)]
        public string AccessType { get; set; } = string.Empty;

        [Required, MaxLength(200)]
        public string ResourceAccessed { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? ResourceId { get; set; }

        public bool IsAuthorized { get; set; }

        [MaxLength(500)]
        public string? DenialReason { get; set; }

        public DateTime AccessedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(45)]
        public string? IpAddress { get; set; }

        [MaxLength(500)]
        public string? UserAgent { get; set; }

        [MaxLength(1000)]
        public string? Details { get; set; }
    }
}
