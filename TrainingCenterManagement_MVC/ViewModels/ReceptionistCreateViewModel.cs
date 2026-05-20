using System.ComponentModel.DataAnnotations;

namespace TrainingCenterManagement_MVC.ViewModels
{
    public class ReceptionistCreateViewModel
    {
        // بيانات Receptionist
        public Guid ReceptionistId { get; set; } = Guid.NewGuid();

        // بيانات ApplicationUser
        [Required]
        [Display(Name = "Full Name")]
        public string FullName { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required(ErrorMessage = "كود حساب الشام كاش مطلوب")]
        [StringLength(32, MinimumLength = 32, ErrorMessage = "كود حساب الشام كاش يجب أن يكون 32 محرف")]
        [RegularExpression("^[a-fA-F0-9]{32}$", ErrorMessage = "كود حساب الشام كاش يجب أن يحتوي على أرقام وأحرف hex فقط")]
        [Display(Name = "كود حساب الشام كاش")]
        public string ShamCashAccountCode { get; set; }
    }

}
