namespace LLLMPluginModern.Core.Models
{
    public class LlmSettings
    {
        public string AiServer { get; set; } = "Ollama"; 
        public string Url { get; set; } = "http://localhost:11434/api/generate";
        public string Model { get; set; } = "deepseek-r1:8b";
        public string ApiKey { get; set; } = ""; 
        public int TextScalePercent { get; set; } = 100;
    }
}
