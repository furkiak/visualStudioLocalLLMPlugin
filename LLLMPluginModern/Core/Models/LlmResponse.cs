using System;

namespace LLLMPluginModern.Core.Models
{
    public sealed class LlmResponse
    {
        public bool IsSuccess { get; private set; }
        public bool IsCancelled { get; private set; }
        public string Text { get; private set; } = "";
        public string Reasoning { get; private set; } = "";
        public string ErrorMessage { get; private set; }

        /// <summary>Non-fatal information, e.g. "the answer was cut off at the token limit".</summary>
        public string Notice { get; set; }

        public int? InputTokens { get; set; }
        public int? OutputTokens { get; set; }
        public TimeSpan Elapsed { get; set; }

        public static LlmResponse Success(string text, string reasoning)
        {
            return new LlmResponse { IsSuccess = true, Text = text ?? "", Reasoning = reasoning ?? "" };
        }

        public static LlmResponse Failure(string message, string partialText = "", string partialReasoning = "")
        {
            return new LlmResponse { ErrorMessage = message, Text = partialText ?? "", Reasoning = partialReasoning ?? "" };
        }

        public static LlmResponse Cancelled(string partialText, string partialReasoning)
        {
            return new LlmResponse { IsCancelled = true, Text = partialText ?? "", Reasoning = partialReasoning ?? "" };
        }
    }
}
