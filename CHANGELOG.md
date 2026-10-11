# Changelog

All notable changes to the Local LLM Plugin for Visual Studio.

## [4.0.0] — 2026-10-11

### Added
- Liquid Glass interface for every window: glass surfaces, capsule buttons, traffic-light window controls, aurora backdrop and an animated glow while the model writes. Dark and light themes follow the Visual Studio theme; Windows 11 acrylic backdrop.
- Streaming answers for Ollama, LM Studio / OpenAI-compatible servers, OpenAI, Anthropic and Google Gemini, with cancellation (Esc).
- Conversations with follow-up questions, Regenerate and Use as input.
- Quick actions: Explain, Refactor, Fix bugs, Optimize, Document, Unit tests, Review.
- Markdown rendering, code cards with Copy/Apply, and a separate thought process for reasoning models.
- Insert below (Ctrl+Shift+Enter), partial apply of highlighted text, column-selection apply.
- Text tools: sort A→Z / Z→A, reverse lines, remove empty lines, trim trailing whitespace, UPPERCASE, lowercase.
- Settings: per-provider profiles, model list, connection test, temperature, max tokens, idle timeout, system prompt, theme and translucency.
- Editor right-click menu entry; Visual Studio 2026 and Arm64 support.

### Changed
- Editor changes go through the Visual Studio editor API as a single undo transaction; only code is applied, indentation is matched and line endings are kept.
- API keys are encrypted with Windows DPAPI and stored per provider; the Gemini key is sent in a header.
- Removed the dependency-injection container and polyfill packages that could conflict inside Visual Studio.
- Default models updated; retired defaults are replaced when 3.x settings are migrated.

### Fixed
- Visual Studio could crash when a request failed; the busy state could get stuck.
- Shift+Enter could insert “AI Thinking…” or an error message into the code.
- Requests to local models timed out after 100 seconds.
- `localhost` endpoints were ignored for OpenAI and Anthropic.
- Switching providers carried the previous API key over.
- Text tools dropped the final line break; Remove duplicates removed blank lines.
- Enter sent the prompt while editing code; Ctrl+Enter did nothing.
- A corrupt settings file broke the extension; applying to non-text documents crashed.

## [3.0.0]
- Rebuilt with MVVM; added Gemini, OpenAI and Anthropic providers.

## [1.2.5]
- Support for Visual Studio 2015, 2017 and 2019.
