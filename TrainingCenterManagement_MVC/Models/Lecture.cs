using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using TrainingCenterManagement_MVC.Models;

namespace TrainingCenterManagement_MVC.Models
{
    public class Lecture
    {
        [Key]
        public Guid LectureId { get; set; } = Guid.NewGuid();

        [Required]
        public string Title { get; set; }

        public string Description { get; set; }

        public string? VideoUrl { get; set; }

        public string? ThumbnailUrl { get; set; }

        public DateTime LectureDate { get; set; } = DateTime.UtcNow;

        public bool IsDeleted { get; set; } = false;

        // Course
        [Required]
        public Guid CourseId { get; set; }
        public Course Course { get; set; }

        public ICollection<Presence> Presences { get; set; } = new List<Presence>();

        // Video Management System
        public ICollection<LectureVideo> Videos { get; set; } = new List<LectureVideo>();

        // Lecture Materials
        public ICollection<LectureMaterial> Materials { get; set; } = new List<LectureMaterial>();

        // Lecture Resources (new system)
        public ICollection<LectureResource> Resources { get; set; } = new List<LectureResource>();

        // Session tracking
        public ICollection<LectureSession> Sessions { get; set; } = new List<LectureSession>();
    }
}
