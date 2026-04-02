namespace TrainingCenterManagement_MVC.Models
{
    public class Message
    {
        public int Id { get; set; }
        public string SenderId { get; set; }
        public ApplicationUser Sender { get; set; }
        public string ReceiverId { get; set; }
        public ApplicationUser Receiver { get; set; }
        public string Content { get; set; }
        public bool IsRead { get; set; }
        public DateTime Timestamp { get; set; }
        // الملفات المرفقة بالرسالة
        public ICollection<MediaFile> MediaFiles { get; set; } = new List<MediaFile>();

        // للتوافق مع الرسائل النصية فقط (اختياري)
        public string? MediaUrl { get; set; }   // إذا كان ملف واحد فقط
    }
}
