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

        [Url]
        public string VideoUrl { get; set; }

        [Url]
        public string ThumbnailUrl { get; set; }

        public DateTime LectureDate { get; set; } = DateTime.UtcNow;

        public bool IsDeleted { get; set; } = false;

        // Course
        [Required]
        public Guid CourseId { get; set; }
        public Course Course { get; set; }

        public ICollection<Presence> Presences { get; set; } = new List<Presence>();
    }
}
