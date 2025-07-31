using System;

namespace TrainingCenterManagement_MVC.Models
{
    public class CourseTrainee
    {
        public Guid CourseId { get; set; }
        public Course Course { get; set; }

        public Guid TraineeId { get; set; }
        public Trainee Trainee { get; set; }
    }
}
