using System.ComponentModel.DataAnnotations;

namespace TrainingCenterManagement_MVC.ViewModels
{
    public class ForgotPasswordViewModel
    {
        [Required(ErrorMessage = "البريد الإلكتروني مطلوب.")]
        [EmailAddress(ErrorMessage = "صيغة البريد الإلكتروني غير صحيحة.")]
        [Display(Name = "البريد الإلكتروني")]
        public string Email { get; set; }
    }
}
