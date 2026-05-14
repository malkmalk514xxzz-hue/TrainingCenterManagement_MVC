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

    public interface IAIAssistantService
    {
        Task<AIChatMessage> AskQuestionAsync(string userId, string question,
            string? ipAddress, string? userAgent);

        Task<List<AIChatMessage>> GetChatHistoryAsync(string userId,
            int pageNumber = 1, int pageSize = 20);

        Task<bool> RateResponseAsync(Guid messageId, string userId, int rating, string? feedback);

        Task<AIStatistics> GetStatisticsAsync(string userId);

        Task<bool> DeleteMessageAsync(Guid messageId, string userId);
    }
}
