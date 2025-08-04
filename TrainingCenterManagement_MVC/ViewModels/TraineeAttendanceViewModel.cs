namespace TrainingCenterManagement_MVC.ViewModels
{
    public class TraineeAttendanceViewModel
    {
        public string CourseName { get; set; }
        public int TotalLectures { get; set; }
        public int AttendedLectures { get; set; }

        //public double AttendancePercentage =>
          //  TotalLectures > 0 ? (AttendedLectures * 100.0 / TotalLectures) : 0;
        public double AttendancePercentage { get; set; }

        public bool IsLowAttendance => AttendancePercentage < 75; // مثال: أقل من 75%

        public string FullName { get; set; }
        


    }
}
