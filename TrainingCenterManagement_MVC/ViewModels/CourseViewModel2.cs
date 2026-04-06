using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TrainingCenterManagement_MVC.Models;

namespace TrainingCenterManagement_MVC.ViewModels
{
    public class CourseViewModel2
    {
        [Required, MaxLength(100)]
        public string CourseName { get; set; }

        [Required]
        public int BatchNumber { get; set; }

        [Required]
        public int NumberOfLectures { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        public string Description { get; set; }

        [Url]
        public string VideoUrl { get; set; }

        [Url]
        public string ThumbnailUrl { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        public DateTime ReleaseDate { get; set; }
    }
}

