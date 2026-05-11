using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TrainingCenterManagement_MVC.Models
{
    public enum ResourceType
    {
        [Display(Name = "شرائح المحاضرة")]
        LectureSlides = 0,

        [Display(Name = "ملاحظات")]
        Notes = 1,

        [Display(Name = "واجب")]
        Assignment = 2,

        [Display(Name = "الحل")]
        Solution = 3,

        [Display(Name = "ملفات المشروع")]
        ProjectFiles = 4,

        [Display(Name = "مراجع إضافية")]
        Reference = 5,

        [Display(Name = "أكواد")]
        Code = 6,

        [Display(Name = "ملفات أخرى")]
        Other = 7
    }

    public class LectureResource
    {
        [Key]
        public Guid ResourceId { get; set; } = Guid.NewGuid();

        [Required]
        public Guid LectureId { get; set; }
        public Lecture Lecture { get; set; }

        [Required, MaxLength(500)]
        public string FileName { get; set; }

        [Required]
        public string FilePath { get; set; }

        [Required, MaxLength(50)]
        public string FileExtension { get; set; }

        public long FileSizeInBytes { get; set; }

        [Required]
        public ResourceType ResourceType { get; set; }

        [MaxLength(1000)]
        public string? Description { get; set; }

        public int DisplayOrder { get; set; } = 1;

        public int DownloadCount { get; set; } = 0;

        public bool IsVisible { get; set; } = true;

        public bool IsRequired { get; set; } = false;

        // Nullable to allow Admin uploads (not tied to a trainer)
        public Guid? UploadedByTrainerId { get; set; }
        public Trainer? UploadedByTrainer { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        [MaxLength(500)]
        public string? AdminNotes { get; set; }

        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }

        public ICollection<ResourceDownload> Downloads { get; set; } = new List<ResourceDownload>();
    }
}
