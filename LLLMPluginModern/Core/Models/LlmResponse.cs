namespace LLLMPluginModern.Core.Models
{
    public class LlmResponse
    {
        public string Text { get; set; }
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; }
    }
}