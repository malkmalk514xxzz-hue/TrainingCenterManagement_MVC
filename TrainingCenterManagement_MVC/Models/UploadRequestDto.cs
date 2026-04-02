using System.ComponentModel.DataAnnotations;

namespace TrainingCenterManagement_MVC.Models
{
    public class UploadRequestDto
    {
        public IFormFile File { get; set; } = null!;

        public string EntityType { get; set; } = string.Empty;   // "Profile", "Message", "Course", ...
        public string? EntityId { get; set; }
        public string FileType { get; set; } = string.Empty;     // "image", "video", "audio"
    }
}
