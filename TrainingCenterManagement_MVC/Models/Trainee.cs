using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using TrainingCenterManagement_MVC.Models;

namespace TrainingCenterManagement_MVC.Models
{
    public class Trainee
    {
        [Key]
        public Guid TraineeId { get; set; } = Guid.NewGuid();

        [Required]
        public string UserId { get; set; }
        public ApplicationUser User { get; set; }

        public ICollection<CourseTrainee> CourseTrainees { get; set; } = new List<CourseTrainee>();
        public ICollection<Payment> Payments { get; set; } = new List<Payment>();
        public ICollection<Presence> Presences { get; set; } = new List<Presence>();
        public ICollection<Certificate> Certificates { get; set; } = new List<Certificate>();
        public ICollection<ExamAttempt> ExamAttempts { get; set; } = new List<ExamAttempt>();
        public ICollection<VideoView> VideoViews { get; set; } = new List<VideoView>();
        public ICollection<CourseRating> CourseRatings { get; set; } = new List<CourseRating>();
        public ICollection<ResourceDownload> ResourceDownloads { get; set; } = new List<ResourceDownload>();
        public ICollection<LectureSession> LectureSessions { get; set; } = new List<LectureSession>();
    }
}
