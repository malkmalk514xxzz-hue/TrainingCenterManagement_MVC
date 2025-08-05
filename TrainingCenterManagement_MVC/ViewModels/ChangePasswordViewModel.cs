using System.ComponentModel.DataAnnotations;

namespace TrainingCenterManagement_MVC.ViewModels
{
    public class ChangePasswordViewModel
    {
        // Current password is required
        [Required]
        [Display(Name = "Current password")]
        public string OldPassword { get; set; }

        // New password is required
        [Required]
        [Display(Name = "New password")]
        public string NewPassword { get; set; }

        // Confirmation for the new password
        [Required]
        // Validates that the confirmation matches the new password
        [Compare("NewPassword", ErrorMessage = "The confirmation password does not match the new password.")]
        public string Confirm { get; set; }
    }
}
