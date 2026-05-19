using System.ComponentModel.DataAnnotations;
using TrainingCenterManagement_MVC.Models;

namespace TrainingCenterManagement_MVC.ViewModels
{
    public class SignUpViewModel
    {
        [Required(ErrorMessage = "الاسم الكامل مطلوب")]
        [MaxLength(50)]
        public string FullName { get; set; }

        [Required(ErrorMessage = "البريد الإلكتروني مطلوب")]
        [DataType(DataType.EmailAddress, ErrorMessage = "بريد إلكتروني غير صحيح")]
        public string Email { get; set; }

        [MaxLength(20)]
        [RegularExpression(@"^\+?[0-9\s\-()]*$", ErrorMessage = "رقم الهاتف غير صحيح")]
        public string? PhoneNumber { get; set; }

        [Required(ErrorMessage = "تاريخ الميلاد مطلوب")]
        public DateTime BirthDate { get; set; }

        public Gender Gender { get; set; } = Gender.Male;

        [Required(ErrorMessage = "كلمة المرور مطلوبة")]
        [MinLength(6, ErrorMessage = "كلمة المرور يجب أن تكون 6 أحرف على الأقل")]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [Required(ErrorMessage = "تأكيد كلمة المرور مطلوب")]
        [Compare("Password", ErrorMessage = "كلمتا المرور غير متطابقتين")]
        [DataType(DataType.Password)]
        public string ConfirmPassword { get; set; }
    }
}
