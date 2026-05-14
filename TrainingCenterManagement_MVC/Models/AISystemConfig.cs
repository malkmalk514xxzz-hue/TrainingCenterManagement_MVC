namespace TrainingCenterManagement_MVC.Models
{
    public class AISystemConfig
    {
        public int Id { get; set; }
        public AIProviderType Provider { get; set; } = AIProviderType.Anthropic;
        public string OllamaUrl   { get; set; } = "http://localhost:11434";
        public string OllamaModel { get; set; } = "llama3.2";
        public int    MaxTokensPerResponse { get; set; } = 1024;
        public int    SystemDailyLimit     { get; set; } = 500;
        public bool   IsEnabled  { get; set; } = true;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string? UpdatedByUserId { get; set; }
    }

    public enum AIProviderType { Anthropic = 1, Ollama = 2 }
}
