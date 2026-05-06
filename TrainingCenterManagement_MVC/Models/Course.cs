using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TrainingCenterManagement_MVC.Models;

namespace TrainingCenterManagement_MVC.Models
{
    public class Course
    {
        [Key]
        public Guid CourseId { get; set; } = Guid.NewGuid();

        [Required, MaxLength(100)]
        public string CourseName { get; set; }

        [Required]
        public int BatchNumber { get; set; }

        [Required]
        public int NumberOfLectures { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        public string Description { get; set; }

        [Url]
        public string VideoUrl { get; set; }

        [Url]
        public string ThumbnailUrl { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        public DateTime ReleaseDate { get; set; }

        public bool IsDeleted { get; set; } = false;

        // Admin who created the course
        public Guid AdminId { get; set; }
        public Admin Admin { get; set; }
        public bool IsFeatured { get; set; } = false;
        public ICollection<CourseTrainee> CourseTrainees { get; set; } = new List<CourseTrainee>();
        public ICollection<CourseTrainer> CourseTrainers { get; set; } = new List<CourseTrainer>();
        public ICollection<Lecture> Lectures { get; set; } = new List<Lecture>();
        public ICollection<Payment> Payments { get; set; } = new List<Payment>();
        public ICollection<Certificate> Certificates { get; set; } = new List<Certificate>();
        public ICollection<GroupMessage> GroupMessages { get; set; } = new List<GroupMessage>();
        // كورس واحد → امتحانات متعددة (midterm, final, quiz...)
        public ICollection<Exam> Exams { get; set; } = new List<Exam>();
    }
}
