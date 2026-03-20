using System;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using LLLMPluginModern.Core.Interfaces;
using LLLMPluginModern.Core.Models;

namespace LLLMPluginModern.Infrastructure.Services
{
    public class OllamaApiService : ILlmApiService
    {

        private static readonly HttpClient _httpClient = new HttpClient();

        public async Task<LlmResponse> GenerateTextAsync(string prompt, string selectedText, LlmSettings settings)
        {
            var responseModel = new LlmResponse();
            try
            {
              
                string fullPrompt = string.IsNullOrWhiteSpace(selectedText)
                    ? prompt
                    : $"{selectedText}\n\n{prompt}";

                var requestData = new
                {
                    model = settings.Model,
                    prompt = fullPrompt
                };

                string jsonString = JsonConvert.SerializeObject(requestData);
                var content = new StringContent(jsonString, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(settings.Url, content);
                response.EnsureSuccessStatusCode();

                var responseBody = await response.Content.ReadAsStringAsync();

             
                var responseParts = responseBody.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                var fullResponseText = new StringBuilder();

                foreach (var part in responseParts)
                {
                    var jsonResponse = JObject.Parse(part);
                    var responseContent = jsonResponse["response"]?.ToString();
                    if (!string.IsNullOrEmpty(responseContent))
                    {
                        fullResponseText.Append(responseContent);
                    }
                }

            
                string resultText = fullResponseText.ToString().Replace("\\u003c", "<").Replace("\\u003e", ">").Trim();
                string pattern = @"<think>.*?</think>";
                resultText = Regex.Replace(resultText, pattern, "", RegexOptions.Singleline);

                responseModel.Text = resultText.Trim();
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