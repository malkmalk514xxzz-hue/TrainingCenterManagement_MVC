using TrainingCenterManagement_MVC.Models;

namespace TrainingCenterManagement_MVC.ViewModels
{
    public class HomeViewModel
    {
        public List<Course> Courses { get; set; } = new List<Course>();
    }
}