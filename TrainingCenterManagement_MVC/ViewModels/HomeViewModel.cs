using TrainingCenterManagement_MVC.Models;

namespace TrainingCenterManagement_MVC.ViewModels
{
    public class HomeViewModel
    {
        public List<Course> Courses { get; set; } = new();
        public List<Course> FeaturedCourses { get; set; } = new();
        public List<Trainer> FeaturedTrainers { get; set; } = new();
        public int TotalCourses { get; set; }
        public int TotalTrainees { get; set; }
        public int TotalTrainers { get; set; }
        public int TotalLectures { get; set; }
        public int TotalCertificates { get; set; }
    }
}
