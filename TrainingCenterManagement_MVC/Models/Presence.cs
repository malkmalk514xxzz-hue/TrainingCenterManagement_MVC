using System;
using System.ComponentModel.DataAnnotations;
using TrainingCenterManagement_MVC.Models;

namespace TrainingCenterManagement_MVC.Models
{
    public class Presence
    {
        [Key]
        public Guid PresenceId { get; set; } = Guid.NewGuid();

        [Required]
        public bool IsPresent { get; set; }

        public bool IsDeleted { get; set; } = false;

        [Required]
        public Guid LectureId { get; set; }
        public Lecture Lecture { get; set; }

        [Required]
        public string TraineeId { get; set; }
        public Trainee Trainee { get; set; }
    }
}
