using System;
using System.ComponentModel.DataAnnotations;
using TrainingCenterManagement_MVC.Models;

namespace TrainingCenterManagement_MVC.Models
{
    public class Admin
    {
        [Key]
        public Guid AdminId { get; set; } = Guid.NewGuid();

        [Required]
        public string UserId { get; set; }
        public ApplicationUser User { get; set; }
    }
}
