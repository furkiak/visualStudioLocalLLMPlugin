using System;
using System.Text;

namespace LLLMPluginModern.Core.Text
{
    /// <summary>
    /// Separates inline &lt;think&gt;…&lt;/think&gt; reasoning (DeepSeek-R1, Qwen3 and similar local models)
    /// from the answer while text is streaming. Tags split across chunks are handled.
    /// </summary>
    public sealed class ThinkTagFilter
    {
        private const string OpenTag = "<think>";
        private const string CloseTag = "</think>";

        private readonly StringBuilder _pending = new StringBuilder();
        private bool _inThink;

        public void Process(string chunk, Action<string> onAnswer, Action<string> onReasoning)
        {
            if (string.IsNullOrEmpty(chunk))
                return;

            _pending.Append(chunk);
            string buffer = _pending.ToString();
            _pending.Clear();

            int pos = 0;
            while (pos < buffer.Length)
            {
                string tag = _inThink ? CloseTag : OpenTag;
                int index = buffer.IndexOf(tag, pos, StringComparison.Ordinal);
                if (index >= 0)
                {
                    Emit(buffer.Substring(pos, index - pos), onAnswer, onReasoning);
                    pos = index + tag.Length;
                    _inThink = !_inThink;
                    continue;
                }

                // Keep a possible partial tag at the end for the next chunk.
                int keep = PartialTagLength(buffer, pos, tag);
                Emit(buffer.Substring(pos, buffer.Length - pos - keep), onAnswer, onReasoning);
                if (keep > 0)
                    _pending.Append(buffer, buffer.Length - keep, keep);
                break;
            }
        }

        /// <summary>Emits anything held back at the end of the stream.</summary>
        public void Flush(Action<string> onAnswer, Action<string> onReasoning)
        {
            if (_pending.Length == 0)
                return;
            Emit(_pending.ToString(), onAnswer, onReasoning);
            _pending.Clear();
        }

        private void Emit(string text, Action<string> onAnswer, Action<string> onReasoning)
        {
            if (text.Length == 0)
                return;
            if (_inThink)
                onReasoning?.Invoke(text);
            else
                onAnswer?.Invoke(text);
        }

        private static int PartialTagLength(string buffer, int start, string tag)
        {
            int max = Math.Min(tag.Length - 1, buffer.Length - start);
            for (int len = max; len > 0; len--)
            {
                if (string.CompareOrdinal(buffer, buffer.Length - len, tag, 0, len) == 0)
                    return len;
            }
            return 0;
        }
    }
}
