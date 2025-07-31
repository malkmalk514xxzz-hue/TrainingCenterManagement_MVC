

using TrainingCenterManagement_MVC.Models;

namespace  TrainingCenterManagement_MVC.ViewModels
{
    public class ChatIndexViewModel
    {
        public List<Course> Courses { get; set; } = new List<Course>();
        public List<RecentContact> RecentContacts { get; set; } = new List<RecentContact>();
        public List<ApplicationUser> AllUsers { get; set; } = new List<ApplicationUser>();

    }
}
