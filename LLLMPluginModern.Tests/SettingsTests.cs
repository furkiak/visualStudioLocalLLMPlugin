using System;
using System.IO;
using LLLMPluginModern.Core.Models;
using LLLMPluginModern.Infrastructure.Services;
using Newtonsoft.Json.Linq;
using Xunit;

namespace LLLMPluginModern.Tests
{
    public sealed class SettingsTests : IDisposable
    {
        private readonly string directory = Path.Combine(Path.GetTempPath(), "LLLMPluginTests", Guid.NewGuid().ToString("N"));
        private string FilePath => Path.Combine(directory, "settings.json");
        private SettingsService Service => new SettingsService(FilePath);
        public SettingsTests() { Directory.CreateDirectory(directory); }
        public void Dispose() { Directory.Delete(directory, true); }

        [Fact]
        public void RealDpapiRoundtripDoesNotPersistPlaintext()
        {
            string key = "test-key-" + Guid.NewGuid();
            Service.SaveSettings(new LlmSettings { ApiKey = key, Model = "fixture" });
            Assert.DoesNotContain(key, File.ReadAllText(FilePath));
            Assert.Null(JObject.Parse(File.ReadAllText(FilePath))["ApiKey"]);
            Assert.Equal(key, Service.GetSettings().ApiKey);
            Assert.Equal("fixture", Service.GetSettings().Model);
            Assert.Single(Directory.GetFiles(directory));
        }

        [Fact]
        public void LegacyKeyMigratesOnceWithoutPlaintextBackup()
        {
            File.WriteAllText(FilePath, "{\"AiServer\":\"OpenAI\",\"Url\":\"https://example.test/v1\",\"Model\":\"custom\",\"ApiKey\":\"legacy-fixture-key\"}");
            var result = Service.GetSettings();
            Assert.Equal("legacy-fixture-key", result.ApiKey);
            Assert.Equal("OpenAI", result.AiServer);
            Assert.Equal("https://example.test/v1", result.Url);
            Assert.Equal("custom", result.Model);
            string migrated = File.ReadAllText(FilePath);
            Assert.DoesNotContain("legacy-fixture-key", migrated);
            Assert.Equal(2, JObject.Parse(migrated).Value<int>("Version"));
            Assert.Equal(result.ApiKey, Service.GetSettings().ApiKey);
            Assert.Equal(migrated, File.ReadAllText(FilePath));
            Assert.Single(Directory.GetFiles(directory));
        }

        [Theory]
        [InlineData("{\"ApiKey\": \"do-not-log-this\"")]
        [InlineData("{\"Version\":2,\"ProtectedApiKey\":\"invalid-secret\"}")]
        [InlineData("{\"Version\":999}")]
        public void InvalidSettingsArePreservedAndErrorsDoNotEchoInput(string content)
        {
            File.WriteAllText(FilePath, content);
            var error = Assert.Throws<InvalidOperationException>(() => Service.GetSettings());
            Assert.DoesNotContain("do-not-log-this", error.ToString());
            Assert.DoesNotContain("invalid-secret", error.ToString());
            Assert.Equal(content, File.ReadAllText(FilePath));
        }

        [Fact]
        public void TamperedDpapiBlobCannotBeLoaded()
        {
            Service.SaveSettings(new LlmSettings { ApiKey = "fixture" });
            var saved = JObject.Parse(File.ReadAllText(FilePath));
            var bytes = Convert.FromBase64String(saved.Value<string>("ProtectedApiKey"));
            bytes[bytes.Length / 2] ^= 0x20;
            saved["ProtectedApiKey"] = Convert.ToBase64String(bytes);
            File.WriteAllText(FilePath, saved.ToString());
            Assert.Throws<InvalidOperationException>(() => Service.GetSettings());
        }

        [Fact]
        public void FailedReplacePreservesPreviousSettings()
        {
            Service.SaveSettings(new LlmSettings { Model = "old", ApiKey = "old-key" });
            string before = File.ReadAllText(FilePath);
            using (var locked = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                Assert.ThrowsAny<IOException>(() => Service.SaveSettings(new LlmSettings { ApiKey = "new-key" }));
            Assert.Equal(before, File.ReadAllText(FilePath));
            Assert.Single(Directory.GetFiles(directory));
        }

        [Fact]
        public void ClearingKeyRemovesProtectedValue()
        {
            Service.SaveSettings(new LlmSettings { ApiKey = "old-key" });
            Service.SaveSettings(new LlmSettings());
            Assert.Equal("", Service.GetSettings().ApiKey);
            Assert.Equal("", JObject.Parse(File.ReadAllText(FilePath)).Value<string>("ProtectedApiKey"));
        }
    }
}
