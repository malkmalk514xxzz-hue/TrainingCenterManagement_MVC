using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using TrainingCenterManagement_MVC.Models;

namespace TrainingCenterManagement_MVC.Models
{
    public class Trainer
    {
        [Key]
        public string TrainerId { get; set; } = string.Empty;

        [Required]
        public string UserId { get; set; }
        public ApplicationUser User { get; set; }

        [Required, MaxLength(100)]
        public string Specialty { get; set; }

        [Required]
        public int YearsOfExperience { get; set; }

        [Url]
        public string BusinessLink { get; set; }

        public ICollection<CourseTrainer> CourseTrainers { get; set; } = new List<CourseTrainer>();
        public ICollection<Certificate> Certificates { get; set; } = new List<Certificate>();
    }
}
