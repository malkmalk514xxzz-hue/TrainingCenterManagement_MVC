namespace TrainingCenterManagement_MVC.ViewModels
{
    public class AttendanceViewModel
    {
        public Guid TraineeId { get; set; }
        public string FullName { get; set; }
        public bool IsPresent { get; set; }
    }
}
