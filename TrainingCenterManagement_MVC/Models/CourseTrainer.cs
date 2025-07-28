using System;

namespace TrainingCenterManagement_MVC.Models
{
    public class CourseTrainer
    {
        public Guid CourseId { get; set; }
        public Course Course { get; set; }

        public Guid TrainerId { get; set; }
        public Trainer Trainer { get; set; }
    }
}
