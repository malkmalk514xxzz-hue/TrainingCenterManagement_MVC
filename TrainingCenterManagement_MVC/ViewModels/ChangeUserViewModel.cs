using System.ComponentModel.DataAnnotations;

namespace TrainingCenterManagement_MVC.ViewModels
{
    public class ChangeUserViewModel
    {
        // FirstName is required
        [Required(ErrorMessage = "FullName is required.")]
        [Display(Name = "FullName")]
        public string FullName { get; set; }

        

        // Address with a max length of 100 characters
        [MaxLength(100, ErrorMessage = "The field {0} can only contain {1} characters.")]
        public string Address { get; set; }

        // PhoneNumber with a max length of 20 characters and allows only digits and valid phone characters
        [MaxLength(20, ErrorMessage = "The field {0} can only contain {1} characters.")]
        [RegularExpression(@"^\+?[0-9\s\-()]*$", ErrorMessage = "The field {0} must contain only numbers and valid phone characters.")]
        public string PhoneNumber { get; set; }
    }
}
