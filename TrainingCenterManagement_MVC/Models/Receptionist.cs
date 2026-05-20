using System;
using System.ComponentModel.DataAnnotations;
using TrainingCenterManagement_MVC.Models;

namespace TrainingCenterManagement_MVC.Models
{
    public class Receptionist
    {
        [Key]
        public Guid ReceptionistId { get; set; } = Guid.NewGuid();

        [Required]
        public string UserId { get; set; }
        public ApplicationUser User { get; set; }

        [Required, StringLength(32, MinimumLength = 32)]
        [RegularExpression("^[a-fA-F0-9]{32}$")]
        public string ShamCashAccountCode { get; set; } = string.Empty;
    }
}
