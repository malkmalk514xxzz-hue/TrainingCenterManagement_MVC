using System.ComponentModel.DataAnnotations;
namespace TrainingCenterManagement_MVC.Models
{
    public enum NotificationType
    {
        LectureAdded = 1,
        PaymentReceived = 2,
        MessageReceived = 3
    }
    public class UserNotification
    {
        [Key]
        public Guid NotificationId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public ApplicationUser? User { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public NotificationType Type { get; set; }
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? RelatedId { get; set; }
    }
}
