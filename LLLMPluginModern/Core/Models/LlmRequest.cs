using System;
using System.Collections.Generic;

namespace LLLMPluginModern.Core.Models
{
    public enum ChatRole
    {
        User,
        Assistant
    }

    public sealed class ChatMessage
    {
        public ChatMessage(ChatRole role, string content)
        {
            Role = role;
            Content = content ?? "";
        }

        public ChatRole Role { get; }
        public string Content { get; }
    }

    public sealed class LlmRequest
    {
        public string SystemPrompt { get; set; } = "";
        public IReadOnlyList<ChatMessage> Messages { get; set; } = Array.Empty<ChatMessage>();
        public ProviderProfile Profile { get; set; } = new ProviderProfile();

        /// <summary>Idle timeout: the request fails if no data arrives for this long.</summary>
        public TimeSpan IdleTimeout { get; set; } = TimeSpan.FromMinutes(5);
    }

    public enum StreamChunkKind
    {
        /// <summary>Answer text to append.</summary>
        Content,

        /// <summary>Model reasoning ("thinking") text to append.</summary>
        Reasoning,

        /// <summary>Discard everything received so far (e.g. the server switched to a fallback model).</summary>
        Reset
    }

    public struct StreamChunk
    {
        public StreamChunk(StreamChunkKind kind, string text)
        {
            Kind = kind;
            Text = text ?? "";
        }

        public StreamChunkKind Kind { get; }
        public string Text { get; }
    }
}
