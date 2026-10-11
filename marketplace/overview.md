# Local LLM Plugin — AI Assistant for Visual Studio

**A fast, private, beautifully designed AI coding assistant that lives inside Visual Studio.**
Run models locally with **Ollama** or **LM Studio**, or use **OpenAI**, **Anthropic** and **Google Gemini** — with streaming answers, one-click quick actions and a single-step undoable apply, all in a macOS-inspired *Liquid Glass* interface.

![The AI Assistant window](https://raw.githubusercontent.com/furkiak/visualStudioLocalLLMPlugin/master/docs/images/assistant-dark.png)

> **Version 4.0** is a complete refresh: new interface, live streaming, conversations, quick actions, secure key storage, Visual Studio 2026 and Arm64 support — and many fixes. See the [release notes](https://github.com/furkiak/visualStudioLocalLLMPlugin/releases).

## Why you'll like it

- **Private by default** — use local models through Ollama, LM Studio, llama.cpp, vLLM, Jan or LocalAI and nothing leaves your machine.
- **Your choice of cloud** — OpenAI, Anthropic and Google Gemini are one setting away. Every provider keeps its own URL, model and API key.
- **Answers as they are written** — responses stream token by token; press **Esc** to stop at any time.
- **Built for code** — only the code is applied to the editor, indentation is matched, line endings are kept, and the whole change is a single **Ctrl+Z**.
- **Looks at home on any theme** — dark and light Liquid Glass themes follow your Visual Studio theme, with real Windows 11 acrylic blur.

## Features

### Ask, refine, apply

- **Streaming chat** with follow-up questions — the conversation keeps its context. *New chat* with **Ctrl+N**.
- **Quick actions** in one click: **Explain**, **Refactor**, **Fix bugs**, **Optimize**, **Document**, **Unit tests**, **Review**. Type extra instructions first to steer them.
- **Code context** — the selection, file name and language are attached automatically. Edit the code before sending, or turn on **Whole file** to send the entire document.
- **Markdown answers** with headings, lists and inline code, plus **code cards** with their own *Copy* and *Apply* buttons.
- **Thought process** — reasoning from thinking models (DeepSeek-R1, Qwen3, gpt-oss…) is shown separately instead of mixed into the answer.
- **Regenerate** an answer, or **Use as input** to keep transforming a result.

![Streaming with the intelligence glow](https://raw.githubusercontent.com/furkiak/visualStudioLocalLLMPlugin/master/docs/images/streaming-dark.png)

### Apply exactly what you want

| Action | Shortcut | What it does |
|---|---|---|
| Apply | `Shift+Enter` | Replaces the selection with the code from the latest answer (explanations and Markdown fences are stripped). |
| Insert below | `Ctrl+Shift+Enter` | Inserts the result below the selection — ideal for tests and docs. |
| Partial apply | highlight + `Shift+Enter` | Applies only the part of an answer you highlighted. |
| Per block | *Apply* on a code card | Applies one specific code block. |

Every edit uses the Visual Studio editor API as **one undo transaction**, re-indents the code to fit where it lands and uses the document's line endings.

### Text tools — no AI required

Remove duplicate lines · Modify & replicate · Erase text · Add to every line · Sort A→Z / Z→A · Reverse lines · Remove empty lines · Trim trailing whitespace · UPPERCASE · lowercase.
Results keep your line endings and can be applied, copied or chained into the next tool.

![Text tools and friendly errors](https://raw.githubusercontent.com/furkiak/visualStudioLocalLLMPlugin/master/docs/images/tools-and-errors-dark.png)

## Supported providers

| Provider | Runs | API key |
|---|---|---|
| **Ollama** | Locally | – |
| **LM Studio / any OpenAI-compatible server** (llama.cpp, vLLM, Jan, LocalAI…) | Locally | Optional |
| **OpenAI** | Cloud | Required |
| **Anthropic** | Cloud | Required |
| **Google Gemini** | Cloud | Required |

Press **↻** in Settings to list the models installed in Ollama or available to your API key, and **Test connection** to check everything in one click.

![Settings](https://raw.githubusercontent.com/furkiak/visualStudioLocalLLMPlugin/master/docs/images/settings-light.png)

## Getting started

1. Install the extension and restart Visual Studio.
2. **Local:** install [Ollama](https://ollama.com) and run `ollama pull qwen2.5-coder:7b`.
   **Cloud:** open *Settings* (`Ctrl+,`), choose a provider and paste your API key.
3. Select some code and press **Ctrl+1** — or right-click in the editor and choose **AI Assistant (Local LLM)**, or use **Tools ▸ AI Assistant (Local LLM)**.
4. Pick a quick action or type a question, then press **Shift+Enter** to apply the result.

## Keyboard shortcuts

| Shortcut | Action |
|---|---|
| `Ctrl+1` | Open the assistant |
| `Enter` / `Ctrl+Enter` | Send / new line |
| `Shift+Enter` | Apply (replace selection) |
| `Ctrl+Shift+Enter` | Insert below |
| `Esc` | Stop streaming · close |
| `Ctrl+N` / `Ctrl+,` | New chat / Settings |

## Privacy & security

- With local providers, your code never leaves your computer.
- API keys are encrypted with Windows DPAPI for your user account and stored per provider in `%AppData%\LLLMPluginModern\settings.json`.
- Requests go only to the endpoint you configure. Local addresses bypass the system proxy; cloud requests honor it.

## Compatibility

Visual Studio 2022 (17.0 and later) and Visual Studio 2026 — Community, Professional and Enterprise — on x64 and Arm64.
Settings from version 3.x are migrated automatically.

## Feedback

Questions, ideas and bug reports are welcome on [GitHub](https://github.com/furkiak/visualStudioLocalLLMPlugin/issues).
If the extension helps you, a ⭐ rating on the Marketplace is much appreciated!

---

### 🇹🇷 Türkçe

**Visual Studio için hızlı, gizliliğe saygılı ve şık bir yapay zekâ kodlama asistanı.** Modelleri **Ollama** veya **LM Studio** ile yerelde çalıştırın ya da **OpenAI**, **Anthropic** ve **Google Gemini** kullanın. Canlı akış hâlinde yanıtlar, sohbet, tek tıkla hızlı eylemler (Açıkla, Refactor, Hata düzelt, Optimize et, Belgele, Birim testi, İnceleme), editöre tek Ctrl+Z ile geri alınabilen uygulama, 10'dan fazla metin aracı ve Visual Studio temasını takip eden macOS tarzı *Liquid Glass* arayüz. API anahtarları DPAPI ile şifrelenir. Visual Studio 2022 ve 2026, x64 ve Arm64 desteklenir. Ayrıntılar: [GitHub](https://github.com/furkiak/visualStudioLocalLLMPlugin).
