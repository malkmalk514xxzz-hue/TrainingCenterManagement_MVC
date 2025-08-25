using System.ComponentModel.DataAnnotations;

namespace TrainingCenterManagement_MVC.ViewModels
{
    public class TraineeCreateViewModel
    {
        public Guid TraineeId { get; set; } = Guid.NewGuid();

        [Required]
        [Display(Name = "Full Name")]
        public string FullName { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }
    }

}
