using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using TrainingCenterManagement_MVC.Models;

namespace TrainingCenterManagement_MVC.Models
{
    public class Trainer
    {
        [Key]
        public Guid TrainerId { get; set; } = Guid.NewGuid();

        [Required]
        public Guid UserId { get; set; }
        public ApplicationUser User { get; set; }

        [Required, MaxLength(100)]
        public string Specialty { get; set; }

        [Required]
        public int YearsOfExperience { get; set; }

        [Url]
        public string BusinessLink { get; set; }

        public ICollection<Course> Courses { get; set; } = new List<Course>();
        public ICollection<Certificate> Certificates { get; set; } = new List<Certificate>();
    }
}
