using System;
using System.ComponentModel.DataAnnotations;

namespace TrainingCenterManagement_MVC.Models
{
    public class ResourceDownload
    {
        [Key]
        public Guid DownloadId { get; set; } = Guid.NewGuid();

        [Required]
        public Guid ResourceId { get; set; }
        public LectureResource Resource { get; set; }

        [Required]
        public Guid TraineeId { get; set; }
        public Trainee Trainee { get; set; }

        public DateTime DownloadedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(50)]
        public string? IpAddress { get; set; }

        [MaxLength(500)]
        public string? UserAgent { get; set; }
    }
}
