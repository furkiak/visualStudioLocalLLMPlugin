using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using LLLMPluginModern.Core.Interfaces;
using LLLMPluginModern.Core.Models;

namespace LLLMPluginModern.Infrastructure.Services
{
    public class AnthropicApiService : ILlmApiService
    {
        private static readonly HttpClient _httpClient = new HttpClient();

        public async Task<LlmResponse> GenerateTextAsync(string prompt, string selectedText, LlmSettings settings)
        {
            var responseModel = new LlmResponse();
            try
            {
                string fullPrompt = string.IsNullOrWhiteSpace(selectedText) ? prompt : $"{selectedText}\n\n{prompt}";

                string endpoint = string.IsNullOrWhiteSpace(settings.Url) || settings.Url.Contains("localhost")
                    ? "https://api.anthropic.com/v1/messages"
                    : settings.Url;

                var requestData = new
                {
                    model = settings.Model, 
                    max_tokens = 4096,      
                    messages = new[] { new { role = "user", content = fullPrompt } }
                };

                var requestMessage = new HttpRequestMessage(HttpMethod.Post, endpoint);
                requestMessage.Headers.Add("x-api-key", settings.ApiKey);
                requestMessage.Headers.Add("anthropic-version", "2023-06-01");
                requestMessage.Content = new StringContent(JsonConvert.SerializeObject(requestData), Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(requestMessage);
                var responseBody = await response.Content.ReadAsStringAsync();

                response.EnsureSuccessStatusCode();

                var jsonResponse = JObject.Parse(responseBody);
                var resultText = jsonResponse["content"]?[0]?["text"]?.ToString();

                responseModel.Text = resultText?.Trim();
                responseModel.IsSuccess = true;
            }
            catch (Exception ex)
            {
                responseModel.IsSuccess = false;
                responseModel.ErrorMessage = ex.Message;
            }

            return responseModel;
        }
    }
}