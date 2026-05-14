using System.ComponentModel.DataAnnotations;

namespace TrainingCenterManagement_MVC.Models
{
    public class AIPermissionRole
    {
        [Key]
        public Guid PermissionId { get; set; } = Guid.NewGuid();

        [Required, MaxLength(50)]
        public string RoleName { get; set; } = string.Empty;

        public bool CanReadPersonalData { get; set; } = true;
        public bool CanReadOtherUsersData { get; set; } = false;
        public bool CanReadAdminData { get; set; } = false;
        public bool CanModifyPersonalData { get; set; } = false;
        public bool CanModifyOtherUsersData { get; set; } = false;

        public int DailyQueryLimit { get; set; } = 100;

        [MaxLength(1000)]
        public string? AllowedDataCategories { get; set; }

        [MaxLength(1000)]
        public string? BlockedDataCategories { get; set; }

        public bool CanAccessAdvancedFeatures { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        [MaxLength(100)]
        public string? CreatedBy { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }
    }
}
