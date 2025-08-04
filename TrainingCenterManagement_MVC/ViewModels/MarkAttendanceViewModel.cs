namespace TrainingCenterManagement_MVC.ViewModels
{
    public class MarkAttendanceViewModel
    {
        public Guid LectureId { get; set; }
        public string LectureTitle { get; set; }

        // ✅ أضفنا هذه الخاصية
        public double DurationHours { get; set; }

        public List<TraineeAttendanceInput> Trainees { get; set; }
    }

    public class TraineeAttendanceInput
    {
        public Guid TraineeId { get; set; }
        public string FullName { get; set; }
        public bool IsPresent { get; set; }
    }
}
