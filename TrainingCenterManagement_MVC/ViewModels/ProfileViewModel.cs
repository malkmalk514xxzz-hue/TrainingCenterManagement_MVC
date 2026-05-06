using System.ComponentModel.DataAnnotations;

namespace TrainingCenterManagement_MVC.ViewModels
{
    public class ProfileViewModel
    {
        [Required(ErrorMessage = "الاسم الكامل مطلوب")]
        [MaxLength(50, ErrorMessage = "الاسم لا يتجاوز 50 حرفاً")]
        [Display(Name = "الاسم الكامل")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "تاريخ الميلاد مطلوب")]
        [Display(Name = "تاريخ الميلاد")]
        public DateTime BirthDate { get; set; }

        [Phone(ErrorMessage = "رقم الهاتف غير صالح")]
        [Display(Name = "رقم الهاتف")]
        public string? PhoneNumber { get; set; }

        // Read-only display fields
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string? ProfilePictureUrl { get; set; }

        // Trainer-specific fields
        [MaxLength(100, ErrorMessage = "التخصص لا يتجاوز 100 حرف")]
        [Display(Name = "التخصص")]
        public string? Specialty { get; set; }

        [Range(0, 50, ErrorMessage = "سنوات الخبرة يجب أن تكون بين 0 و 50")]
        [Display(Name = "سنوات الخبرة")]
        public int? YearsOfExperience { get; set; }

        [Url(ErrorMessage = "رابط العمل غير صالح")]
        [Display(Name = "رابط العمل / LinkedIn")]
        public string? BusinessLink { get; set; }
    }
}
