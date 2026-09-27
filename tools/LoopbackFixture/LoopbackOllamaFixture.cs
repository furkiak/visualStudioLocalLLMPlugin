using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LLLMPluginModern.Testing
{
    public sealed class LoopbackOllamaFixture : IDisposable
    {
        public const string FirstText = "// Synthetic loopback fixture (no model) · Türkçe\n";
        public const string LastText = "// phase two\nint fixtureValue = 42;\n";
        private readonly TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource stop = new CancellationTokenSource();
        private readonly ConcurrentBag<Task> requests = new ConcurrentBag<Task>();
        private readonly TaskCompletionSource<bool> streamGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Action<string> log;
        private readonly bool holdStream;
        private readonly TimeSpan segmentDelay;
        private readonly Task acceptLoop;
        private int requestCount;
        public string Endpoint { get; }
        public int RequestCount => Volatile.Read(ref requestCount);
        public ConcurrentQueue<string> Events { get; } = new ConcurrentQueue<string>();

        public LoopbackOllamaFixture(bool holdStream = false, Action<string> log = null, TimeSpan? segmentDelay = null)
        {
            this.holdStream = holdStream;
            this.log = log;
            this.segmentDelay = segmentDelay ?? TimeSpan.FromSeconds(7);
            listener.Start();
            Endpoint = "http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port + "/api/generate";
            acceptLoop = Task.Run(AcceptAsync);
        }

        public void ReleaseStreams() => streamGate.TrySetResult(true);

        private async Task AcceptAsync()
        {
            try
            {
                while (!stop.IsCancellationRequested)
                {
                    var client = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
                    client.NoDelay = true;
                    requests.Add(Task.Run(() => ServeAsync(client)));
                }
            }
            catch (ObjectDisposedException) when (stop.IsCancellationRequested) { }
            catch (SocketException) when (stop.IsCancellationRequested) { }
        }

        private async Task ServeAsync(TcpClient client)
        {
            string scenario = "unread";
            using (client)
            using (stop.Token.Register(() => client.Close()))
            try
            {
                var stream = client.GetStream();
                var headerBytes = new MemoryStream();
                byte[] one = new byte[1];
                int tail = 0;
                while (tail != 0x0D0A0D0A)
                {
                    if (headerBytes.Length >= 16384 || await stream.ReadAsync(one, 0, 1, stop.Token).ConfigureAwait(false) == 0)
                        throw new InvalidDataException();
                    headerBytes.WriteByte(one[0]);
                    tail = (tail << 8) | one[0];
                }
                string headers = Encoding.ASCII.GetString(headerBytes.ToArray());
                string[] lines = headers.Split(new[] { "\r\n" }, StringSplitOptions.None);
                if (lines[0] != "POST /api/generate HTTP/1.1") { await Status(stream, 404).ConfigureAwait(false); return; }
                int length = -1;
                bool expectContinue = false, authorization = false;
                foreach (string line in lines)
                {
                    if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                        length = int.Parse(line.Substring(15).Trim(), CultureInfo.InvariantCulture);
                    if (line.StartsWith("Expect:", StringComparison.OrdinalIgnoreCase)) expectContinue = true;
                    if (line.StartsWith("Authorization:", StringComparison.OrdinalIgnoreCase)) authorization = true;
                }
                if (authorization || length < 0 || length > 1100000) { await Status(stream, 400).ConfigureAwait(false); return; }
                if (expectContinue) await Write(stream, Encoding.ASCII.GetBytes("HTTP/1.1 100 Continue\r\n\r\n")).ConfigureAwait(false);
                byte[] body = new byte[length];
                int received = 0;
                while (received < length)
                {
                    int read = await stream.ReadAsync(body, received, length - received, stop.Token).ConfigureAwait(false);
                    if (read == 0) throw new InvalidDataException();
                    received += read;
                }
                var input = JObject.Parse(Encoding.UTF8.GetString(body));
                Array.Clear(body, 0, body.Length);
                string model = input.Value<string>("model");
                if (model != "fixture-stream" && model != "fixture-401" && model != "fixture-malformed" && model != "fixture-timeout" && model != "fixture-cancel")
                { input.RemoveAll(); await Status(stream, 400).ConfigureAwait(false); return; }
                scenario = model;
                if (input.Value<bool?>("stream") != true || input["prompt"]?.Type != JTokenType.String) { await Status(stream, 400).ConfigureAwait(false); return; }
                int promptBytes = Encoding.UTF8.GetByteCount(input.Value<string>("prompt"));
                input.RemoveAll();
                Interlocked.Increment(ref requestCount);
                Record(scenario, "request", promptBytes);
                if (scenario == "fixture-401") { await Status(stream, 401).ConfigureAwait(false); Record(scenario, "status-401"); return; }
                await Write(stream, Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: application/x-ndjson; charset=utf-8\r\nTransfer-Encoding: chunked\r\nConnection: close\r\n\r\n")).ConfigureAwait(false);
                byte[] first = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new { response = FirstText, done = false }) + "\n");
                int split = Array.IndexOf(first, (byte)0xC3) + 1;
                if (split <= 0) throw new InvalidDataException();
                await Chunk(stream, first, 0, split).ConfigureAwait(false);
                await Task.Delay(80, stop.Token).ConfigureAwait(false);
                await Chunk(stream, first, split, first.Length - split).ConfigureAwait(false);
                Record(scenario, "first-chunk-flushed");
                if (scenario == "fixture-timeout" || scenario == "fixture-cancel")
                {
                    for (int i = 0; i < 30; i++)
                    {
                        await Task.Delay(1000, stop.Token).ConfigureAwait(false);
                        await Chunk(stream, Encoding.UTF8.GetBytes("\n")).ConfigureAwait(false);
                    }
                    return;
                }
                if (scenario == "fixture-stream" && holdStream)
                    await streamGate.Task.ConfigureAwait(false);
                else await Task.Delay(segmentDelay, stop.Token).ConfigureAwait(false);
                if (scenario == "fixture-malformed")
                    await Chunk(stream, Encoding.UTF8.GetBytes("{broken NDJSON\n")).ConfigureAwait(false);
                else
                    await Chunk(stream, Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new { response = LastText, done = true }) + "\n")).ConfigureAwait(false);
                await Write(stream, Encoding.ASCII.GetBytes("0\r\n\r\n")).ConfigureAwait(false);
                Record(scenario, scenario == "fixture-malformed" ? "malformed-sent" : "done-sent");
            }
            catch (Exception ex) when (ex is IOException || ex is SocketException || ex is ObjectDisposedException || ex is OperationCanceledException || ex is JsonException || ex is InvalidDataException || ex is FormatException)
            { Record(scenario, stop.IsCancellationRequested ? "fixture-stopped" : "connection-ended"); }
        }

        private void Record(string scenario, string stage, int? promptBytes = null)
        {
            string entry = JsonConvert.SerializeObject(new { utc = DateTime.UtcNow.ToString("O"), scenario, stage, promptBytes });
            Events.Enqueue(entry);
            log?.Invoke(entry);
        }
        private Task Status(Stream stream, int status) => Write(stream, Encoding.ASCII.GetBytes("HTTP/1.1 " + status + " Fixture\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"));
        private async Task Chunk(Stream stream, byte[] data, int offset = 0, int count = -1)
        {
            if (count < 0) count = data.Length;
            await Write(stream, Encoding.ASCII.GetBytes(count.ToString("X", CultureInfo.InvariantCulture) + "\r\n")).ConfigureAwait(false);
            await stream.WriteAsync(data, offset, count, stop.Token).ConfigureAwait(false);
            await Write(stream, Encoding.ASCII.GetBytes("\r\n")).ConfigureAwait(false);
        }
        private async Task Write(Stream stream, byte[] data)
        {
            await stream.WriteAsync(data, 0, data.Length, stop.Token).ConfigureAwait(false);
            await stream.FlushAsync(stop.Token).ConfigureAwait(false);
        }
        public void Dispose()
        {
            stop.Cancel();
            ReleaseStreams();
            listener.Stop();
            acceptLoop.GetAwaiter().GetResult();
            Task.WhenAll(requests.ToArray()).GetAwaiter().GetResult();
            stop.Dispose();
        }
    }
}
