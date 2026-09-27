# Local LLM tools for Visual Studio

Review changes to selected code from Ollama or a configured cloud provider, then apply them as one editor operation. C# · WPF · Visual Studio 2022 · [AGPL-3.0](LICENSE.txt).

**3.1.2 is a local release candidate.** The fixes described below are in this branch; existing [release downloads](https://github.com/furkiak/visualStudioLocalLLMPlugin/releases) have not been updated. See [validation status](docs/VALIDATION.md) before using or publishing the candidate.

## What the workflow does

1. Select code in an editable file and open **Tools → Local LLM Plugin (Modern)**. To add a shortcut, use Visual Studio's keyboard settings; the previous Ctrl+1 binding conflicted with an IDE chord in the tested installation.
2. In Settings, choose a provider and an available model. Ollama defaults to `http://localhost:11434/api/generate`; install/pull the chosen model separately. Cloud model IDs are explicit inputs because availability depends on the provider/account.
3. Check the destination, model and UTF-8 byte count. Only the displayed selection and your instruction are sent. Remove secrets before sending.
4. Send, or use the local **Remove duplicates / Modify / Erase / Add** tools. The original selection stays read-only. Ollama chunks appear in Candidate while generating; cancellation aborts the request.
5. Review **Candidate** and **Changes**, check the review box and choose **Apply to selection**. Apply requires a successful finished candidate and an unchanged target document/selection. One editor Undo is intended to revert the replacement; the real IDE verification status is recorded separately.

A response remains plain text. Markdown fences and commentary are not silently stripped; inspect the whole candidate. The change view summarizes one replaced span, not a compiler-aware diff. It does not execute generated code or collect other solution files.

## Build the candidate

Requires Windows, Visual Studio 2022 with **.NET desktop development**, **Visual Studio extension development**, the .NET Framework 4.7.2 targeting pack, and a current .NET SDK for the tests. In a VS Developer PowerShell:

```powershell
dotnet test LLLMPluginModern.Tests/LLLMPluginModern.Tests.csproj -c Release
msbuild LLLMPluginModern/LLLMPluginModern.csproj /restore /t:Rebuild /p:Configuration=Release /p:DeployExtension=false
```

The package is `LLLMPluginModern/bin/Release/LLLMPluginModern.vsix`. Install it in a disposable experimental instance first; follow [the smoke procedure](docs/VALIDATION.md). The build does not install or publish the extension. CI uploads a build artifact and test reports; it does not automatically publish a release.

For repeatable native protocol checks without reading personal settings or API keys, use the [isolated loopback HTTP fixture](docs/HTTP_FIXTURE.md). Its visible Settings dropdown covers chunked streaming, 401, malformed NDJSON, timeout and cancel in the same IDE window. The five scenarios and subsequent review → Apply → Undo were also checked in the actual Professional 2022 IDE on 27 September 2026. It is explicitly synthetic; no model is invoked. The 62-case suite retains all prior 47 cases and checks the production provider/view-model path over actual TCP HTTP. A passing protocol fixture does not certify real Ollama or cloud-model output.

| Environment | Declared target | Verified locally |
| --- | --- | --- |
| VS 2022 Community, Windows x64 | 17.x | Runtime check pending |
| VS 2022 Professional, Windows x64 | 17.x through Community SKU inheritance | Release build; IDE check recorded in validation log |
| VS 2022 Enterprise, Windows x64 | 17.x through Community SKU inheritance | Runtime check pending |
| VS 2019, ARM64, VS Code, macOS/Linux | Not targeted | Not claimed |

## Privacy and limits

- API keys are encrypted with **Windows DPAPI CurrentUser** in `%APPDATA%/LLLMPluginModern/settings.json`. Old plaintext keys migrate on the first successful read. A PasswordBox only masks the screen. DPAPI is not protection against malware running as your Windows user or an administrator.
- Settings writes are atomic. Unreadable settings are preserved until you explicitly re-enter and save replacements. Changing providers clears the in-memory key to avoid sending one provider's credential to another.
- Remote endpoints require HTTPS; loopback may use HTTP. URL credentials/query strings and automatic redirects are rejected. Gemini uses an API-key header. Endpoint URLs and model names are not encrypted.
- A loopback service can forward requests. Cloud requests leave the computer and follow provider-specific retention/billing. No automatic secret detector is claimed.
- Requests are bounded to 256 Ki characters and two minutes; responses to 2 Mi characters / 4 MiB received, with a 64 Ki-character NDJSON-line limit. Partial, empty, malformed or truncated responses cannot be applied. Full-response cloud APIs do not show token streaming.
- No real-provider quality or latency result is claimed from HTTP fixtures. Current test and release limitations are in [VALIDATION.md](docs/VALIDATION.md).

## Keyboard

In the instruction box: Enter sends, Ctrl+Enter inserts a newline, Shift+Enter applies only when all review checks allow it. Escape cancels an active request; when idle it closes the window. Other controls retain their normal keyboard behavior.

## Türkçe

Seçili kodu yerel Ollama'ya veya seçtiğin sağlayıcıya gönder; özgün metni, aday kodu ve değişiklikleri inceleyerek uygula. Hata/iptal metni kod yerine yazılamaz. API anahtarı Windows kullanıcısına bağlı DPAPI ile korunur. Bu dal 3.1.2 yayın adayıdır; önceki indirmeler henüz bu düzeltmeleri içermez. Kişisel ayarlardan ayrılmış, sahte model olduğu açıkça yazan gerçek HTTP fixture ile beş senaryo aynı IDE penceresinde denenebilir. Gerçek IDE/sağlayıcı doğrulaması ile otomatik test sonuçları [ayrı kaydedilir](docs/VALIDATION.md).

Maintained by [Furkan Akça](https://furkiak.com). Space & Tab is a personal project label, not a commercial company. [Contributing](CONTRIBUTING.md) · [Security](SECURITY.md) · [Changelog](CHANGELOG.md).
