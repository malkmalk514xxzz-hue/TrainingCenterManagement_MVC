using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using TrainingCenterManagement_MVC.Models;

namespace TrainingCenterManagement_MVC.Models
{
    public class Exam
    {
        [Key]
        public Guid ExamId { get; set; } = Guid.NewGuid();

        [Required]
        public string ExamName { get; set; }

        public DateTime ExamDate { get; set; } = DateTime.UtcNow;

        public bool IsDeleted { get; set; } = false;

        // Course (1:1)
        [Required]
        public Guid CourseId { get; set; }
        public Course Course { get; set; }

        public ICollection<Certificate> Certificates { get; set; } = new List<Certificate>();
    }
}
