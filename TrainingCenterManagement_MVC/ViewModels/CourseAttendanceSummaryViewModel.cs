namespace TrainingCenterManagement_MVC.ViewModels
{
    public class CourseAttendanceSummaryViewModel
    {
        public string CourseName { get; set; }
        public List<TraineeAttendanceViewModel> TraineeAttendances { get; set; }
    }
}
