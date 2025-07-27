using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
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
        public float Price { get; set; }

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

        public ICollection<Trainee> Trainees { get; set; } = new List<Trainee>();
        public ICollection<Trainer> Trainers { get; set; } = new List<Trainer>();
        public ICollection<Lecture> Lectures { get; set; } = new List<Lecture>();
        public ICollection<Payment> Payments { get; set; } = new List<Payment>();
        public ICollection<Certificate> Certificates { get; set; } = new List<Certificate>();
        public Exam Exam { get; set; }
    }
}
