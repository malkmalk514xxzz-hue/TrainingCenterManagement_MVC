namespace TrainingCenterManagement_MVC.Models
{
    public class AIKnowledgeEntry
    {
        public int    Id        { get; set; }
        public string Title     { get; set; } = string.Empty;
        public string Content   { get; set; } = string.Empty;
        public string Category  { get; set; } = "عام";
        public bool   IsActive  { get; set; } = true;
        public int    SortOrder { get; set; }
        public DateTime  CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public string? CreatedByUserId { get; set; }
    }
}
