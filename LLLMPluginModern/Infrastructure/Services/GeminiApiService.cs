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
    public class GeminiApiService : ILlmApiService
    {
        private static readonly HttpClient _httpClient = new HttpClient();

        public async Task<LlmResponse> GenerateTextAsync(string prompt, string selectedText, LlmSettings settings)
        {
            var responseModel = new LlmResponse();
            try
            {
                string fullPrompt = string.IsNullOrWhiteSpace(selectedText) ? prompt : $"{selectedText}\n\n{prompt}";

             
                string endpoint = string.IsNullOrWhiteSpace(settings.Url) || !settings.Url.Contains("googleapis")
                    ? $"https://generativelanguage.googleapis.com/v1beta/models/{settings.Model}:generateContent?key={settings.ApiKey}"
                    : settings.Url;

                var requestData = new
                {
                    contents = new[] { new { parts = new[] { new { text = fullPrompt } } } }
                };

                string jsonString = JsonConvert.SerializeObject(requestData);
                var content = new StringContent(jsonString, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(endpoint, content);
                var responseBody = await response.Content.ReadAsStringAsync();

                response.EnsureSuccessStatusCode();

                var jsonResponse = JObject.Parse(responseBody);
                var resultText = jsonResponse["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString();

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