using TrainingCenterManagement_MVC.Models;

namespace TrainingCenterManagement_MVC.Services
{
    public class AIStatistics
    {
        public int TotalQuestions      { get; set; }
        public int AnsweredQuestions   { get; set; }
        public double AverageRating    { get; set; }
        public string MostAskedCategory { get; set; } = "—";
        public DateTime? LastQuestion  { get; set; }
    }

    /// <summary>نتيجة تحليل محاضرة بالذكاء الاصطناعي</summary>
    public class LectureAIAnalysis
    {
        public bool    Success        { get; set; }
        public string? Content        { get; set; }
        public string? Error          { get; set; }
        public string? LectureTitle   { get; set; }
        public string  Provider       { get; set; } = "";
        public string  AnalysisType   { get; set; } = ""; // summary | mindmap | qna
    }

    public interface IAIAssistantService
    {
        Task<AIChatMessage> AskQuestionAsync(string userId, string question,
            string? ipAddress, string? userAgent);

        Task<List<AIChatMessage>> GetChatHistoryAsync(string userId,
            int pageNumber = 1, int pageSize = 20);

        Task<bool> RateResponseAsync(Guid messageId, string userId, int rating, string? feedback);

        Task<AIStatistics> GetStatisticsAsync(string userId);

        Task<bool> DeleteMessageAsync(Guid messageId, string userId);

        // ── Lecture AI Tools ─────────────────────────────────────────────────
        /// <summary>تلخيص محتوى محاضرة بالذكاء الاصطناعي</summary>
        Task<LectureAIAnalysis> SummarizeLectureAsync(string userId, Guid lectureId);

        /// <summary>توليد خريطة ذهنية للمحاضرة بتنسيق Markdown</summary>
        Task<LectureAIAnalysis> GenerateMindMapAsync(string userId, Guid lectureId);

        /// <summary>توليد أسئلة وأجوبة عن محتوى المحاضرة</summary>
        Task<LectureAIAnalysis> GenerateQnAAsync(string userId, Guid lectureId);
    }
}
