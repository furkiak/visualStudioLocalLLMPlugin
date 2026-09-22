using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LLLMPluginModern.Core.Models;
using LLLMPluginModern.Infrastructure.Services;
using Newtonsoft.Json.Linq;
using Xunit;

namespace LLLMPluginModern.Tests
{
    public sealed class ProviderTests
    {
        private sealed class Handler : HttpMessageHandler
        {
            public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Respond;
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => Respond(request, token);
        }
        private sealed class ImmediateProgress : IProgress<string>
        {
            private readonly Action<string> action;
            public ImmediateProgress(Action<string> action) { this.action = action; }
            public void Report(string value) => action(value);
        }
        private static HttpLlmApiService Service(string provider, HttpClient client, TimeSpan? timeout = null)
        {
            switch (provider)
            {
                case "OpenAI": return new OpenAiApiService(client, timeout);
                case "Anthropic": return new AnthropicApiService(client, timeout);
                case "Gemini": return new GeminiApiService(client, timeout);
                default: return new OllamaApiService(client, timeout);
            }
        }
        private static LlmSettings Settings(string provider) => new LlmSettings { AiServer = provider, Model = "fixture-model", Url = "https://example.test/generate", ApiKey = "fixture-secret" };

        [Theory]
        [InlineData("OpenAI", "{\"choices\":[{\"finish_reason\":\"stop\",\"message\":{\"content\":\"code\"}}]}", "Authorization")]
        [InlineData("Anthropic", "{\"stop_reason\":\"end_turn\",\"content\":[{\"type\":\"text\",\"text\":\"co\"},{\"type\":\"text\",\"text\":\"de\"}]}", "x-api-key")]
        [InlineData("Gemini", "{\"candidates\":[{\"finishReason\":\"STOP\",\"content\":{\"parts\":[{\"thought\":true,\"text\":\"private reasoning\"},{\"text\":\"code\"}]}}]}", "x-goog-api-key")]
        [InlineData("Ollama", "{\"response\":\"code\",\"done\":true}", "Authorization")]
        public async Task SendsOnlySelectionAndPromptAndUsesCredentialHeader(string provider, string payload, string authHeader)
        {
            var handler = new Handler { Respond = async (r, t) =>
            {
                Assert.Equal(HttpMethod.Post, r.Method);
                Assert.True(r.Headers.Contains(authHeader));
                Assert.DoesNotContain("fixture-secret", r.RequestUri.AbsoluteUri);
                string body = await r.Content.ReadAsStringAsync();
                Assert.Contains("selected", body); Assert.Contains("instruction", body);
                Assert.DoesNotContain("fixture-secret", body);
                Assert.Equal("fixture-model", JObject.Parse(body).Value<string>("model") ?? "fixture-model");
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(payload) };
            } };
            using (var client = new HttpClient(handler))
            {
                var response = await Service(provider, client).GenerateTextAsync("instruction", "selected", Settings(provider), CancellationToken.None);
                Assert.True(response.IsSuccess, response.ErrorMessage);
                Assert.Equal("code", response.Text);
            }
        }

        [Theory]
        [InlineData(401, "authentication")]
        [InlineData(403, "authentication")]
        [InlineData(404, "not found")]
        [InlineData(429, "rate limit")]
        [InlineData(500, "HTTP 500")]
        [InlineData(302, "HTTP 302")]
        public async Task HttpFailuresAreNotEchoedAsCode(int status, string expected)
        {
            using (var client = new HttpClient(new Handler { Respond = (r, t) => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status)
                { Content = new StringContent("private prompt and key") }) }))
            {
                var response = await new OllamaApiService(client).GenerateTextAsync("prompt", "", Settings("Ollama"), CancellationToken.None);
                Assert.False(response.IsSuccess); Assert.Null(response.Text);
                Assert.Contains(expected, response.ErrorMessage);
                Assert.DoesNotContain("private", response.ErrorMessage);
            }
        }

        [Theory]
        [InlineData("{bad json")]
        [InlineData("{\"response\":\"partial\",\"done\":false}\n")]
        [InlineData("{\"response\":\"\",\"done\":true}\n")]
        [InlineData("{\"response\":\"code\",\"done\":true,\"done_reason\":\"length\"}\n")]
        [InlineData("{\"error\":\"do-not-echo\"}\n")]
        [InlineData("{\"response\":\"<think>unfinished\",\"done\":true}\n")]
        public async Task MalformedEmptyOrIncompleteOllamaOutputCannotApply(string body)
        {
            using (var client = new HttpClient(new Handler { Respond = (r, t) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) }) }))
            {
                var result = await new OllamaApiService(client).GenerateTextAsync("p", "", Settings("Ollama"), CancellationToken.None);
                Assert.False(result.IsSuccess); Assert.Null(result.Text);
                Assert.DoesNotContain("do-not-echo", result.ErrorMessage);
            }
        }

        [Fact]
        public async Task SplitReasoningTagNeverAppearsInProgressOrCandidate()
        {
            string body = "{\"response\":\"safe<thi\",\"done\":false}\n" +
                "{\"response\":\"nk>private reasoning</think> output\",\"done\":true}\n";
            var updates = new System.Collections.Generic.List<string>();
            using (var client = new HttpClient(new Handler { Respond = (r, t) => Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) }) }))
            {
                var result = await new OllamaApiService(client).GenerateTextAsync("p", "", Settings("Ollama"),
                    CancellationToken.None, new ImmediateProgress(updates.Add));
                Assert.True(result.IsSuccess, result.ErrorMessage);
                Assert.Equal("safe output", result.Text);
                Assert.All(updates, text => { Assert.DoesNotContain("<thi", text); Assert.DoesNotContain("private", text); });
            }
        }

        [Theory]
        [InlineData("http://remote.example/api")]
        [InlineData("https://user:pass@example.test/api")]
        [InlineData("https://example.test/api?key=secret")]
        [InlineData("file:///c:/data")]
        public async Task InvalidDestinationIsRejectedBeforeSending(string url)
        {
            using (var client = new HttpClient(new Handler { Respond = (r, t) => throw new Exception("Must not send") }))
            {
                var settings = Settings("Ollama"); settings.Url = url;
                var result = await new OllamaApiService(client).GenerateTextAsync("p", "", settings, CancellationToken.None);
                Assert.False(result.IsSuccess);
                Assert.DoesNotContain(url, result.ErrorMessage);
            }
        }

        [Fact]
        public async Task TimeoutIsReportedAndExplicitCancellationPropagates()
        {
            using (var client = new HttpClient(new Handler { Respond = async (r, t) => { await Task.Delay(Timeout.Infinite, t); return null; } }))
            {
                var timed = await new OllamaApiService(client, TimeSpan.FromMilliseconds(100)).GenerateTextAsync("p", "", Settings("Ollama"), CancellationToken.None);
                Assert.False(timed.IsSuccess); Assert.Contains("timed out", timed.ErrorMessage);
                using (var cancel = new CancellationTokenSource(100))
                    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new OllamaApiService(client).GenerateTextAsync("p", "", Settings("Ollama"), cancel.Token));
            }
        }

        [Theory]
        [InlineData(false)] [InlineData(true)]
        public async Task RealChunkedHttpStreamsBeforeCompletionAndCanCancelDuringBody(bool cancelDuringBody)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var firstSeen = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var allowFinish = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            string stage = "accept";
            var server = Task.Run(async () =>
            {
                using (var socket = await listener.AcceptTcpClientAsync())
                using (var stream = socket.GetStream())
                {
                    var first = "{\"response\":\"first\",\"done\":false}\n";
                    var last = "{\"response\":\" last\",\"done\":true}\n";
                    stage = "request headers";
                    var requestReader = new StreamReader(stream, Encoding.ASCII, false, 1024, true);
                    int length = 0; bool expectContinue = false;
                    string header;
                    while (!string.IsNullOrEmpty(header = await requestReader.ReadLineAsync()))
                    {
                        if (header.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)) length = int.Parse(header.Substring(15).Trim());
                        if (header.StartsWith("Expect:", StringComparison.OrdinalIgnoreCase)) expectContinue = true;
                    }
                    if (expectContinue)
                    {
                        byte[] interim = Encoding.ASCII.GetBytes("HTTP/1.1 100 Continue\r\n\r\n");
                        await stream.WriteAsync(interim, 0, interim.Length);
                    }
                    stage = "request body " + length;
                    var requestBody = new char[length];
                    int consumed = 0;
                    while (consumed < length) consumed += await requestReader.ReadAsync(requestBody, consumed, length - consumed);
                    byte[] bytes = Encoding.UTF8.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: application/x-ndjson\r\nTransfer-Encoding: chunked\r\nConnection: close\r\n\r\n" + Encoding.UTF8.GetByteCount(first).ToString("X") + "\r\n" + first + "\r\n");
                    await stream.WriteAsync(bytes, 0, bytes.Length); await stream.FlushAsync();
                    stage = "first chunk flushed";
                    await allowFinish.Task;
                    bytes = Encoding.UTF8.GetBytes(Encoding.UTF8.GetByteCount(last).ToString("X") + "\r\n" + last + "\r\n0\r\n\r\n"); await stream.WriteAsync(bytes, 0, bytes.Length);
                }
            });
            using (var cancel = new CancellationTokenSource())
            try
            {
                var settings = Settings("Ollama"); settings.Url = "http://127.0.0.1:" + port + "/api/generate";
                var operation = new OllamaApiService().GenerateTextAsync("p", "", settings, cancel.Token,
                    new ImmediateProgress(s => { if (s == "first") firstSeen.TrySetResult(true); }));
                var observed = await Task.WhenAny(firstSeen.Task, operation, Task.Delay(5000));
                Assert.True(observed == firstSeen.Task, operation.IsCompleted ? (await operation).ErrorMessage : "No first HTTP chunk arrived in 5 seconds; server: " + server.Status + "; stage: " + stage);
                Assert.False(operation.IsCompleted);
                if (cancelDuringBody)
                {
                    cancel.Cancel();
                    Assert.Same(operation, await Task.WhenAny(operation, Task.Delay(5000)));
                    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);
                    return;
                }
                allowFinish.TrySetResult(true);
                var result = await operation;
                Assert.True(result.IsSuccess, result.ErrorMessage); Assert.Equal("first last", result.Text);
                await server;
            }
            finally
            {
                allowFinish.TrySetResult(true); listener.Stop();
                try { await server; } catch (IOException) { } catch (SocketException) { }
            }
        }
    }
}
