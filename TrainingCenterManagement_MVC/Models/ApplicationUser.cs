using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.Reflection.Metadata.Ecma335;

namespace TrainingCenterManagement_MVC.Models
{
    public class ApplicationUser : IdentityUser
    {
        [Required, MaxLength(50)]
        public string FullName { get; set; }
        public DateTime BirthDate { get; set; }
        public RoleType Role { get; set; } = RoleType.Trainee;
        public string ProfilePictureUrl { get; set; }

        // Navigation properties (اختياري لربط الحساب بصاحب الدور)
        public Trainer Trainer { get; set; }
        public Trainee Trainee { get; set; }
        public Admin Admin { get; set; }
        public Receptionist Receptionist { get; set; }
        public ICollection<Message> Messages { get; set; } = new List<Message>();
        public ICollection<UserNotification> Notifications { get; set; } = new List<UserNotification>();
    }

    public enum RoleType
    {
        Admin = 1,
        Trainer = 2,
        Trainee = 3,
        Receptionist = 4
    }
}

