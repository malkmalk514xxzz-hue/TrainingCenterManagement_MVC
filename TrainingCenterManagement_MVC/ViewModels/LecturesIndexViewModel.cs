using TrainingCenterManagement_MVC.Models;

namespace TrainingCenterManagement_MVC.ViewModels
{
    public class LecturesIndexViewModel
    {
        public List<Course> Courses { get; set; } = new();
        public List<Lecture> Lectures { get; set; } = new();
    }
}
