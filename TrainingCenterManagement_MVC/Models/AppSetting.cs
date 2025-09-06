using System.ComponentModel.DataAnnotations;

namespace TrainingCenterManagement_MVC.Models
{
    public class AppSetting
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Key { get; set; }

        public string Value { get; set; }
    }
}
