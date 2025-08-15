namespace TrainingCenterManagement_MVC.Models
{
    public class RecentContact
    {
        public string UserId { get; set; }
        public string Username { get; set; }
        public string LastMessage { get; set; }
        public DateTime LastMessageTime { get; set; }
        public bool IsGroup { get; set; } = false;
    }
}
