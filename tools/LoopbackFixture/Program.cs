using System;
using System.IO;
using System.Threading;
using Newtonsoft.Json;
using LLLMPluginModern.Testing;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length != 2 || args[0] != "--profile-directory" || !Path.IsPathRooted(args[1]))
        { Console.Error.WriteLine("Usage: LoopbackFixture --profile-directory <new absolute directory>"); return 2; }
        string directory = Path.GetFullPath(args[1]);
        if (Directory.Exists(directory) || File.Exists(directory))
        { Console.Error.WriteLine("Choose a new profile directory; existing files are preserved."); return 2; }
        Directory.CreateDirectory(directory);
        object logGate = new object();
        using (var fixture = new LoopbackOllamaFixture(log: line => { lock (logGate) File.AppendAllText(Path.Combine(directory, "http-events.ndjson"), line + Environment.NewLine); }))
        {
            File.WriteAllText(Path.Combine(directory, "profile.json"), JsonConvert.SerializeObject(new { Version = 1, Mode = "LoopbackHttpFixture", Endpoint = fixture.Endpoint }, Formatting.Indented));
            Console.WriteLine("ISOLATED HTTP FIXTURE: synthetic response, no model; no prompt, response or credential content is logged.");
            Console.WriteLine("Profile: " + directory);
            Console.WriteLine("Endpoint: " + fixture.Endpoint);
            Console.WriteLine("Scenarios: fixture-stream / fixture-401 / fixture-malformed / fixture-timeout / fixture-cancel");
            Console.WriteLine("Set LLLM_VALIDATION_PROFILE only in the disposable Visual Studio process. Create stop.fixture to stop the server.");
            Console.Out.Flush();
            while (!File.Exists(Path.Combine(directory, "stop.fixture"))) Thread.Sleep(200);
        }
        return 0;
    }
}
