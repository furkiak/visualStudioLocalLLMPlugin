using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using LLLMPluginModern.Core.Interfaces;
using LLLMPluginModern.Core.Models;
using LLLMPluginModern.Core.Text;
using LLLMPluginModern.Infrastructure.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LLLMPluginModern.Infrastructure.Services
{
    /// <summary>
    /// Shared streaming pipeline for all providers: sending, idle timeout, user cancellation,
    /// line based stream parsing and turning failures into actionable messages.
    /// </summary>
    public abstract class LlmApiServiceBase : ILlmApiService
    {
        private static readonly TimeSpan ListModelsTimeout = TimeSpan.FromSeconds(20);

        protected abstract string ProviderName { get; }

        /// <summary>Extra advice appended when the server cannot be reached.</summary>
        protected virtual string ConnectionHint => "Check the endpoint URL and your network connection.";

        /// <summary>Local models often emit inline &lt;think&gt; blocks that must be split from the answer.</summary>
        protected virtual bool UsesThinkTags => false;

        protected abstract HttpRequestMessage CreateChatRequest(LlmRequest request);

        protected abstract void HandleStreamLine(string line, StreamState state);

        /// <summary>Called after the stream ends; used to recover non-streaming JSON bodies.</summary>
        protected virtual void OnStreamEnd(StreamState state)
        {
        }

        protected abstract HttpRequestMessage CreateListModelsRequest(ProviderProfile profile, string pageToken);

        /// <summary>Parses one page of the model list. Returns the token of the next page, or null.</summary>
        protected abstract string ParseModels(JToken body, List<string> models);

        public async Task<LlmResponse> GenerateAsync(LlmRequest request, IProgress<StreamChunk> progress, CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            var state = new StreamState(request, progress, UsesThinkTags);

            using (var idle = new CancellationTokenSource())
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, idle.Token))
            {
                Uri endpoint = null;
                try
                {
                    using (var message = CreateChatRequest(request))
                    {
                        endpoint = message.RequestUri;
                        idle.CancelAfter(request.IdleTimeout);

                        using (var response = await SharedHttp.For(endpoint).SendAsync(message, HttpCompletionOption.ResponseHeadersRead, linked.Token).ConfigureAwait(false))
                        {
                            if (!response.IsSuccessStatusCode)
                            {
                                string body = await ReadBodySafeAsync(response).ConfigureAwait(false);
                                return Finish(LlmResponse.Failure(DescribeHttpError(response, body, request.Profile)), stopwatch);
                            }

                            // Disposing the response is the only reliable way to abort a blocked read on .NET Framework.
                            using (linked.Token.Register(response.Dispose))
                            using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                            using (var reader = new StreamReader(stream, Encoding.UTF8))
                            {
                                string line;
                                while (!state.Done && (line = await reader.ReadLineAsync().ConfigureAwait(false)) != null)
                                {
                                    linked.Token.ThrowIfCancellationRequested();
                                    idle.CancelAfter(request.IdleTimeout);
                                    if (line.Length == 0)
                                        continue;
                                    HandleStreamLine(line, state);
                                }
                            }
                            linked.Token.ThrowIfCancellationRequested();
                        }
                    }

                    state.Complete();
                    OnStreamEnd(state);

                    if (state.Error != null)
                        return Finish(LlmResponse.Failure(state.Error, state.Text, state.Reasoning), stopwatch);

                    if (!state.HasText && state.Reasoning.Length == 0)
                        return Finish(LlmResponse.Failure(state.Notice ?? $"{ProviderName} returned an empty response."), stopwatch);

                    var result = LlmResponse.Success(state.Text.Trim(), state.Reasoning.Trim());
                    result.Notice = state.Notice;
                    result.InputTokens = state.InputTokens;
                    result.OutputTokens = state.OutputTokens;
                    return Finish(result, stopwatch);
                }
                catch (Exception) when (cancellationToken.IsCancellationRequested)
                {
                    return Finish(LlmResponse.Cancelled(state.Text, state.Reasoning), stopwatch);
                }
                catch (Exception) when (idle.IsCancellationRequested)
                {
                    int seconds = (int)request.IdleTimeout.TotalSeconds;
                    return Finish(LlmResponse.Failure(
                        $"No data from {ProviderName} for {seconds} seconds, so the request was stopped. " +
                        "A large model may still be loading — try again, or raise the timeout in Settings.",
                        state.Text, state.Reasoning), stopwatch);
                }
                catch (LlmApiException ex)
                {
                    return Finish(LlmResponse.Failure(ex.Message, state.Text, state.Reasoning), stopwatch);
                }
                catch (HttpRequestException ex)
                {
                    return Finish(LlmResponse.Failure(DescribeConnectionError(ex, endpoint), state.Text, state.Reasoning), stopwatch);
                }
                catch (Exception ex)
                {
                    return Finish(LlmResponse.Failure($"{ProviderName}: {ex.GetBaseException().Message}", state.Text, state.Reasoning), stopwatch);
                }
            }
        }

        public async Task<IReadOnlyList<string>> ListModelsAsync(ProviderProfile profile, CancellationToken cancellationToken)
        {
            var models = new List<string>();
            string pageToken = null;
            Uri endpoint = null;

            using (var timeout = new CancellationTokenSource(ListModelsTimeout))
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token))
            {
                try
                {
                    for (int page = 0; page < 10; page++)
                    {
                        using (var message = CreateListModelsRequest(profile, pageToken))
                        {
                            endpoint = message.RequestUri;
                            using (var response = await SharedHttp.For(endpoint).SendAsync(message, linked.Token).ConfigureAwait(false))
                            {
                                string body = await ReadBodySafeAsync(response).ConfigureAwait(false);
                                if (!response.IsSuccessStatusCode)
                                    throw new LlmApiException(DescribeHttpError(response, body, profile));

                                pageToken = ParseModels(JToken.Parse(body), models);
                            }
                        }
                        if (string.IsNullOrEmpty(pageToken))
                            break;
                    }
                }
                catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                {
                    throw new LlmApiException($"{ProviderName} did not answer within {(int)ListModelsTimeout.TotalSeconds} seconds. {ConnectionHint}");
                }
                catch (HttpRequestException ex)
                {
                    throw new LlmApiException(DescribeConnectionError(ex, endpoint), ex);
                }
                catch (JsonException ex)
                {
                    throw new LlmApiException($"{ProviderName} returned an unexpected model list. Is the URL pointing at the right server?", ex);
                }
            }

            return models.Where(m => !string.IsNullOrWhiteSpace(m))
                         .Distinct(StringComparer.OrdinalIgnoreCase)
                         .OrderBy(m => m, StringComparer.OrdinalIgnoreCase)
                         .ToList();
        }

        private static LlmResponse Finish(LlmResponse response, Stopwatch stopwatch)
        {
            response.Elapsed = stopwatch.Elapsed;
            return response;
        }

        protected static StringContent JsonContent(object body)
        {
            var settings = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore };
            return new StringContent(JsonConvert.SerializeObject(body, settings), Encoding.UTF8, "application/json");
        }

        protected static string RequireModel(ProviderProfile profile, string providerName)
        {
            string model = (profile?.Model ?? "").Trim();
            if (model.Length == 0)
                throw new LlmApiException($"No model selected for {providerName}. Open Settings and choose a model.");
            return model;
        }

        protected static bool TryParseJson(string text, out JToken token)
        {
            token = null;
            if (string.IsNullOrWhiteSpace(text))
                return false;
            try
            {
                token = JToken.Parse(text);
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        /// <summary>Returns the payload of an SSE "data:" line, or null for other SSE lines.</summary>
        protected static string SseData(string line)
        {
            if (line.StartsWith("data:", StringComparison.Ordinal))
                return line.Substring(5).TrimStart();
            return null;
        }

        protected static bool IsSseControlLine(string line)
        {
            return line.StartsWith(":", StringComparison.Ordinal) ||
                   line.StartsWith("event:", StringComparison.Ordinal) ||
                   line.StartsWith("id:", StringComparison.Ordinal) ||
                   line.StartsWith("retry:", StringComparison.Ordinal);
        }

        private static async Task<string> ReadBodySafeAsync(HttpResponseMessage response)
        {
            try
            {
                return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            }
            catch (Exception)
            {
                return "";
            }
        }

        protected string DescribeHttpError(HttpResponseMessage response, string body, ProviderProfile profile)
        {
            int code = (int)response.StatusCode;
            string detail = ExtractErrorMessage(body);
            string hint;
            switch (code)
            {
                case 401:
                case 403:
                    hint = "Check the API key in Settings.";
                    break;
                case 404:
                    hint = $"Check the endpoint URL and that the model \"{profile?.Model}\" exists.";
                    break;
                case 429:
                    hint = "Rate limit or quota exceeded — wait a moment or check your plan.";
                    break;
                default:
                    hint = code >= 500 ? "The server had a problem — try again shortly." : null;
                    break;
            }

            var sb = new StringBuilder();
            sb.Append(ProviderName).Append(" returned ").Append(code);
            if (!string.IsNullOrEmpty(response.ReasonPhrase))
                sb.Append(' ').Append(response.ReasonPhrase);
            sb.Append('.');
            if (!string.IsNullOrEmpty(detail))
                sb.Append(' ').Append(detail.TrimEnd('.')).Append('.');
            if (hint != null)
                sb.Append(' ').Append(hint);
            return sb.ToString();
        }

        /// <summary>Finds the human readable message in the error formats used by the supported APIs.</summary>
        protected static string ExtractErrorMessage(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
                return null;

            if (TryParseJson(body, out var token))
            {
                if (token is JArray array && array.Count > 0)
                    token = array[0];

                var error = token["error"];
                if (error is JValue value && value.Type == JTokenType.String)
                    return (string)value;
                string message = (string)error?["message"] ?? (string)token["message"] ?? (string)token["detail"];
                if (!string.IsNullOrEmpty(message))
                    return message;
            }

            string text = Regex.Replace(body, "<[^>]+>", " ");
            text = Regex.Replace(text, @"\s+", " ").Trim();
            return text.Length > 300 ? text.Substring(0, 300) + "…" : text;
        }

        private string DescribeConnectionError(HttpRequestException ex, Uri endpoint)
        {
            string target = endpoint != null ? endpoint.GetLeftPart(UriPartial.Authority) : ProviderName;

            for (Exception inner = ex; inner != null; inner = inner.InnerException)
            {
                if (inner is WebException web)
                {
                    switch (web.Status)
                    {
                        case WebExceptionStatus.ConnectFailure:
                            return $"Could not connect to {target}. {ConnectionHint}";
                        case WebExceptionStatus.NameResolutionFailure:
                            return $"Could not resolve {target}. Check the URL and your internet connection.";
                        case WebExceptionStatus.TrustFailure:
                        case WebExceptionStatus.SecureChannelFailure:
                            return $"The secure (TLS) connection to {target} failed. Check the URL or your proxy/antivirus HTTPS inspection.";
                        case WebExceptionStatus.ProxyNameResolutionFailure:
                            return $"The proxy server could not be resolved while connecting to {target}.";
                    }
                }

                if (inner is SocketException socket && socket.SocketErrorCode == SocketError.ConnectionRefused)
                    return $"Could not connect to {target}. {ConnectionHint}";
            }

            return $"Connection to {target} failed: {ex.GetBaseException().Message}";
        }

        /// <summary>Accumulates a streamed answer and forwards deltas to the UI.</summary>
        protected sealed class StreamState
        {
            private readonly IProgress<StreamChunk> _progress;
            private readonly ThinkTagFilter _thinkFilter;
            private readonly StringBuilder _text = new StringBuilder();
            private readonly StringBuilder _reasoning = new StringBuilder();

            public StreamState(LlmRequest request, IProgress<StreamChunk> progress, bool splitThinkTags)
            {
                Request = request;
                _progress = progress;
                _thinkFilter = splitThinkTags ? new ThinkTagFilter() : null;
            }

            public LlmRequest Request { get; }

            /// <summary>Lines that were not part of the expected stream format (e.g. a plain JSON body).</summary>
            public StringBuilder Unparsed { get; } = new StringBuilder();

            public bool Done { get; set; }
            public string Error { get; set; }
            public string Notice { get; set; }
            public int? InputTokens { get; set; }
            public int? OutputTokens { get; set; }

            public string Text => _text.ToString();
            public string Reasoning => _reasoning.ToString();
            public bool HasText => _text.ToString().Trim().Length > 0;

            public void AppendText(string text)
            {
                if (string.IsNullOrEmpty(text))
                    return;
                if (_thinkFilter != null)
                    _thinkFilter.Process(text, AppendAnswer, AppendReasoning);
                else
                    AppendAnswer(text);
            }

            public void AppendReasoning(string text)
            {
                if (string.IsNullOrEmpty(text))
                    return;
                _reasoning.Append(text);
                _progress?.Report(new StreamChunk(StreamChunkKind.Reasoning, text));
            }

            public void Reset()
            {
                _text.Clear();
                _reasoning.Clear();
                _progress?.Report(new StreamChunk(StreamChunkKind.Reset, ""));
            }

            public void Complete()
            {
                _thinkFilter?.Flush(AppendAnswer, AppendReasoning);
            }

            private void AppendAnswer(string text)
            {
                _text.Append(text);
                _progress?.Report(new StreamChunk(StreamChunkKind.Content, text));
            }
        }
    }
}
