using System;
using System.ComponentModel.DataAnnotations;

namespace TrainingCenterManagement_MVC.Models
{
    public class CourseRating
    {
        [Key]
        public Guid RatingId { get; set; } = Guid.NewGuid();

        [Required]
        public Guid CourseId { get; set; }
        public Course Course { get; set; }

        [Required]
        public Guid TraineeId { get; set; }
        public Trainee Trainee { get; set; }

        [Required, Range(1, 5)]
        public int Stars { get; set; }

        [MaxLength(1000)]
        public string Comment { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
