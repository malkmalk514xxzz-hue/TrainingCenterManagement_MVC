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
    }

}
