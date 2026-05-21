namespace TrainingCenterManagement_MVC.Models
{
    public class AISystemConfig
    {
        public int Id { get; set; }
        public AIProviderType Provider { get; set; } = AIProviderType.Anthropic;
        public string OllamaUrl   { get; set; } = "http://localhost:11434";
        public string OllamaModel { get; set; } = "llama3.2";
        public string OpenAIModel { get; set; } = "gpt-4o-mini";
        public string GroqModel   { get; set; } = "llama-3.3-70b-versatile";
        public int    MaxTokensPerResponse { get; set; } = 1024;
        public int    SystemDailyLimit     { get; set; } = 500;
        public bool   IsEnabled  { get; set; } = true;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string? UpdatedByUserId { get; set; }

        // API keys stored in DB (take priority over appsettings.json)
        public string? AnthropicApiKey { get; set; }
        public string? OpenAIApiKey    { get; set; }
        public string? GroqApiKey      { get; set; }
    }

    public enum AIProviderType { Anthropic = 1, Ollama = 2, OpenAI = 3, Groq = 4 }
}
