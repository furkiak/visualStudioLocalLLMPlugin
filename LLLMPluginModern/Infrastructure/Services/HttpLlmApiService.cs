using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LLLMPluginModern.Core.Interfaces;
using LLLMPluginModern.Core.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LLLMPluginModern.Infrastructure.Services
{
    public abstract class HttpLlmApiService : ILlmApiService
    {
        protected const int MaxResponseCharacters = 2 * 1024 * 1024;
        private static readonly HttpClient SharedClient = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
            { Timeout = Timeout.InfiniteTimeSpan };
        private readonly HttpClient client;
        private readonly TimeSpan timeout;
        protected HttpLlmApiService(HttpClient client = null, TimeSpan? timeout = null)
        {
            this.client = client ?? SharedClient;
            this.timeout = timeout ?? TimeSpan.FromMinutes(2);
        }

        public static Uri Endpoint(LlmSettings settings)
        {
            string url = settings.Url;
            switch (settings.AiServer)
            {
                case "Gemini":
                    url = "https://generativelanguage.googleapis.com/v1beta/models/" + Uri.EscapeDataString(settings.Model ?? "") + ":generateContent";
                    break;
                case "OpenAI": if (string.IsNullOrWhiteSpace(url)) url = "https://api.openai.com/v1/chat/completions"; break;
                case "Anthropic": if (string.IsNullOrWhiteSpace(url)) url = "https://api.anthropic.com/v1/messages"; break;
                case "Ollama": break;
                default: throw new ArgumentException("Unknown provider. Choose a provider in Settings.");
            }
            Uri endpoint;
            if (!Uri.TryCreate(url, UriKind.Absolute, out endpoint) ||
                (endpoint.Scheme != "https" && !(endpoint.Scheme == "http" && endpoint.IsLoopback)) ||
                !string.IsNullOrEmpty(endpoint.UserInfo) || !string.IsNullOrEmpty(endpoint.Query) || !string.IsNullOrEmpty(endpoint.Fragment))
                throw new ArgumentException("Use an HTTPS endpoint, or HTTP on loopback only. Put credentials in API Key, not the URL.");
            return endpoint;
        }

        public async Task<LlmResponse> GenerateTextAsync(string prompt, string selectedText, LlmSettings settings,
            CancellationToken cancellationToken, IProgress<string> progress = null)
        {
            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                deadline.CancelAfter(timeout);
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (settings == null || string.IsNullOrWhiteSpace(settings.Model)) return Failure("Choose a model in Settings.");
                    var endpoint = Endpoint(settings);
                    if (settings.AiServer != "Ollama" && !endpoint.IsLoopback && string.IsNullOrWhiteSpace(settings.ApiKey))
                        return Failure("This provider needs an API key. Open Settings.");
                    string input = string.IsNullOrWhiteSpace(selectedText) ? prompt : selectedText + "\n\n" + prompt;
                    if ((input ?? "").Length > 262144) return Failure("The selection and prompt exceed the 256 Ki-character limit. Send a smaller selection.");
                    using (var request = CreateRequest(endpoint, input, settings))
                    using (var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token).ConfigureAwait(false))
                    {
                        if (!response.IsSuccessStatusCode) return Failure(HttpError(response.StatusCode));
                        using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                        using (deadline.Token.Register(() => stream.Dispose()))
                        using (var bounded = new ResponseStream(stream, 4 * 1024 * 1024))
                        using (var reader = new StreamReader(bounded, Encoding.UTF8, true, 4096))
                        {
                            string output = await ReadResponseAsync(reader, deadline.Token, progress).ConfigureAwait(false);
                            deadline.Token.ThrowIfCancellationRequested();
                            if (string.IsNullOrWhiteSpace(output)) return Failure("The provider returned no usable text. Nothing can be applied.");
                            return new LlmResponse { IsSuccess = true, Text = output };
                        }
                    }
                }
                catch (Exception) when (deadline.IsCancellationRequested)
                {
                    if (cancellationToken.IsCancellationRequested) throw new OperationCanceledException(cancellationToken);
                    return Failure("The request timed out. Nothing can be applied; retry or use a smaller prompt.");
                }
                catch (JsonException) { return Failure("The provider returned invalid JSON. Nothing can be applied."); }
                catch (InvalidOperationException) { return Failure("The provider returned an unexpected response shape. Nothing can be applied."); }
                catch (InvalidDataException) { return Failure("The provider response was incomplete or exceeded the response limit. Nothing can be applied."); }
                catch (ArgumentException) { return Failure("Check the provider, model and URL in Settings. Remote endpoints require HTTPS; credentials must not be in the URL."); }
                catch (Exception ex) when (ex is HttpRequestException || ex is IOException || ex is ObjectDisposedException)
                { return Failure("Could not read the provider response. Check the endpoint and connection; nothing can be applied."); }
            }
        }

        protected abstract HttpRequestMessage CreateRequest(Uri endpoint, string input, LlmSettings settings);
        protected abstract Task<string> ReadResponseAsync(StreamReader reader, CancellationToken token, IProgress<string> progress);
        protected static HttpRequestMessage JsonRequest(Uri uri, object payload)
        {
            return new HttpRequestMessage(HttpMethod.Post, uri)
                { Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json") };
        }
        protected static async Task<JObject> ReadJsonAsync(StreamReader reader, CancellationToken token)
        {
            var result = new StringBuilder();
            var buffer = new char[4096];
            int read;
            while ((read = await reader.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0)
            {
                token.ThrowIfCancellationRequested();
                if (result.Length + read > MaxResponseCharacters) throw new InvalidDataException();
                result.Append(buffer, 0, read);
            }
            return JObject.Parse(result.ToString());
        }
        protected static string Text(JToken value) => value?.Type == JTokenType.String ? (string)value : null;
        private static LlmResponse Failure(string message) => new LlmResponse { IsSuccess = false, ErrorMessage = message };
        private static string HttpError(HttpStatusCode code)
        {
            switch ((int)code)
            {
                case 401: case 403: return "Provider authentication failed. Check your API key and permissions in Settings.";
                case 404: return "The endpoint or model was not found. Check Settings.";
                case 429: return "The provider rate limit was reached. Wait before retrying.";
                default: return "Provider request failed (HTTP " + (int)code + "). Nothing can be applied.";
            }
        }
    }
}
