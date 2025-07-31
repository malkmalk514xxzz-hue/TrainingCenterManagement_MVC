using System;
using System.ComponentModel.DataAnnotations;
using TrainingCenterManagement_MVC.Models;

namespace TrainingCenterManagement_MVC.Models
{
    public class Payment
    {
        [Key]
        public Guid PaymentId { get; set; } = Guid.NewGuid();

        [Required]
        public decimal TotalAmount { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        public bool IsDeleted { get; set; } = false;

        [Required]
        public string TraineeId { get; set; }
        public Trainee Trainee { get; set; }

        [Required]
        public Guid CourseId { get; set; }
        public Course Course { get; set; }
    }
}
