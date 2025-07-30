using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

using TrainingCenterManagement_MVC.Models;

namespace TrainingCenterManagement_MVC.Models
{
    public class ApplicationUser : IdentityUser
    {
        [Required, MaxLength(50)]
        public string FullName { get; set; }

        public RoleType Role { get; set; }

        // Navigation properties (اختياري لربط الحساب بصاحب الدور)
        public Trainer Trainer { get; set; }
        public Trainee Trainee { get; set; }
        public Admin Admin { get; set; }
        public Receptionist Receptionist { get; set; }
    }

    public enum RoleType
    {
        Admin = 1,
        Trainer = 2,
        Trainee = 3,
        Receptionist = 4
    }
}
