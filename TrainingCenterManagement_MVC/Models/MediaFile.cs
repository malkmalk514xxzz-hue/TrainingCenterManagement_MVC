namespace TrainingCenterManagement_MVC.Models
{
    public class MediaFile
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string Url { get; set; } = string.Empty;
        public string FileType { get; set; } = string.Empty;        // image, video, audio
        public string ContentType { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        // معلومات الربط العامة (مرنة لأي كيان)
        public string EntityType { get; set; } = string.Empty;      // "Message", "Profile", "Course", "UserDocument", ...
        public string? EntityId { get; set; }                       // int أو Guid أو string حسب الكيان

        public string UserId { get; set; } = string.Empty;          // من قام برفع الملف
        public ApplicationUser User { get; set; } = null!;

        // ربط اختياري بالرسالة (للحفاظ على التوافق)
        public int? MessageId { get; set; }
        public Message? Message { get; set; }
    }
}
