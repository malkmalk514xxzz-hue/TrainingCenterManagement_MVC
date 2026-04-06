using System.ComponentModel.DataAnnotations;
using TrainingCenterManagement_MVC.Models;

namespace TrainingCenterManagement_MVC.ViewModels
{
    public class LectureViewModel
    {
        [Required]
        public string Title { get; set; }

        public string Description { get; set; }

        [Url]
        public string VideoUrl { get; set; }

        [Url]
        public string ThumbnailUrl { get; set; }

        public DateTime LectureDate { get; set; } = DateTime.UtcNow;

       

        // Course
        [Required]
        public string CourseId { get; set; }
    }
}
