using System;
using System.ComponentModel.DataAnnotations;
using TrainingCenterManagement_MVC.Models;

namespace TrainingCenterManagement_MVC.Models
{
    public class Certificate
    {
        [Key]
        public Guid CertificateId { get; set; } = Guid.NewGuid();

        [Required]
        public float Average { get; set; }

        [Url]
        public string Url { get; set; }

        public bool IsDeleted { get; set; } = false;

        [Required]
        public Guid TraineeId { get; set; }
        public Trainee Trainee { get; set; }

        [Required]
        public Guid TrainerId { get; set; }
        public Trainer Trainer { get; set; }

        [Required]
        public Guid CourseId { get; set; }
        public Course Course { get; set; }

        [Required]
        public Guid ExamId { get; set; }
        public Exam Exam { get; set; }
    }
}
