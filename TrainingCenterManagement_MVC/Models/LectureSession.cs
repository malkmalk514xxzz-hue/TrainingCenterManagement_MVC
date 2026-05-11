using System;
using System.ComponentModel.DataAnnotations;

namespace TrainingCenterManagement_MVC.Models
{
    public class LectureSession
    {
        [Key]
        public Guid SessionId { get; set; } = Guid.NewGuid();

        [Required]
        public Guid LectureId { get; set; }
        public Lecture Lecture { get; set; }

        [Required]
        public Guid TraineeId { get; set; }
        public Trainee Trainee { get; set; }

        public DateTime StartedAt { get; set; } = DateTime.UtcNow;

        public DateTime? EndedAt { get; set; }

        public int TotalDurationSeconds { get; set; } = 0;

        public bool IsCompleted { get; set; } = false;

        public double CompletionPercentage { get; set; } = 0;
    }
}
