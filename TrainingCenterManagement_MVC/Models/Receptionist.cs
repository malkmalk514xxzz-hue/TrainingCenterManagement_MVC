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
    }
}
