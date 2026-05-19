using System;
using System.ComponentModel.DataAnnotations;

namespace TrainingCenterManagement_MVC.Models
{
    public class CourseTrainee
    {
        public Guid CourseId { get; set; }
        public Course Course { get; set; }

        public Guid TraineeId { get; set; }
        public Trainee Trainee { get; set; }

        public DateTime EnrolledAt { get; set; } = DateTime.UtcNow;

        // ── Suspension (payment hold) ──────────────────────────
        public bool IsSuspended { get; set; } = false;

        [MaxLength(500)]
        public string? SuspensionReason { get; set; }

        public DateTime? SuspendedAt { get; set; }
    }
}
