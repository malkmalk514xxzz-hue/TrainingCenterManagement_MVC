
namespace TrainingCenterManagement_MVC.ViewModels
{
    public class FeaturedCoursesViewModel
    {
        public List<CourseViewModel> Courses { get; set; } = new List<CourseViewModel>();
    }

    public class CourseViewModel
    {
        public Guid CourseId { get; set; }
        public string? CourseName { get; set; }
        public string? Description { get; set; }
        //public int Duration { get; set; }
        public bool IsFeatured { get; set; }
    }
}
