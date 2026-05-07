using System;
using System.ComponentModel.DataAnnotations;

namespace TrainingCenterManagement_MVC.Models
{
    public class LectureMaterial
    {
        [Key]
        public Guid MaterialId { get; set; } = Guid.NewGuid();

        [Required]
        public Guid LectureId { get; set; }
        public Lecture Lecture { get; set; }

        [Required, MaxLength(500)]
        public string Title { get; set; }

        [Required, MaxLength(255)]
        public string FileName { get; set; }

        [Required]
        public string FilePath { get; set; }

        public string LocalFilePath { get; set; }

        [Required, MaxLength(100)]
        public string ContentType { get; set; }

        public long FileSizeInBytes { get; set; }

        public Guid UploadedByTrainerId { get; set; }
        public Trainer UploadedByTrainer { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
    }
}
