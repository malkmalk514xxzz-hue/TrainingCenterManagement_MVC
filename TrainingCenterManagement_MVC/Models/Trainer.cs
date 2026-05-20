using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using TrainingCenterManagement_MVC.Models;

namespace TrainingCenterManagement_MVC.Models
{
    public class Trainer
    {
        [Key]
        public Guid TrainerId { get; set; } = Guid.NewGuid();

        [Required]
        public string UserId { get; set; }
        public ApplicationUser User { get; set; }

        [Required, MaxLength(100)]
        public string Specialty { get; set; } = string.Empty;

        [Required]
        public int YearsOfExperience { get; set; }

        [Url]
        public string? BusinessLink { get; set; }

        [Required, StringLength(32, MinimumLength = 32)]
        [RegularExpression("^[a-fA-F0-9]{32}$")]
        public string ShamCashAccountCode { get; set; } = string.Empty;

        public ICollection<CourseTrainer> CourseTrainers { get; set; } = new List<CourseTrainer>();
        public ICollection<Certificate> Certificates { get; set; } = new List<Certificate>();
        public ICollection<Question> Questions { get; set; } = new List<Question>();
        public ICollection<Exam> Exams { get; set; } = new List<Exam>();

        // Video Management System
        public ICollection<LectureVideo> LectureVideos { get; set; } = new List<LectureVideo>();
        public ICollection<LectureMaterial> LectureMaterials { get; set; } = new List<LectureMaterial>();

        // Lecture Resources (new system)
        public ICollection<LectureResource> LectureResources { get; set; } = new List<LectureResource>();
    }
}
