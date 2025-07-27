using System;
using System.ComponentModel.DataAnnotations;
using TrainingCenterManagement_MVC.Models;

namespace TrainingCenterManagement.Domain
{
    public class Receptionist
    {
        [Key]
        public Guid ReceptionistId { get; set; } = Guid.NewGuid();

        [Required]
        public Guid UserId { get; set; }
        public ApplicationUser User { get; set; }
    }
}
